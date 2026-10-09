using System.Text.Json;

namespace PersonalTradingJournal.Application.DailyReview.Coaching;

public enum CoachingAnalysisScopeKind { AllAccounts, ExactAccount }

/// <summary>AllAccounts means an aggregate analysis, not a null-scoped Journal.</summary>
public sealed record CoachingAnalysisScope
{
    public CoachingAnalysisScope(Guid? accountId = null)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("An exact Account ID must be nonempty.", nameof(accountId));
        AccountId = accountId;
    }
    public Guid? AccountId { get; }
    public CoachingAnalysisScopeKind Kind => AccountId.HasValue
        ? CoachingAnalysisScopeKind.ExactAccount : CoachingAnalysisScopeKind.AllAccounts;
}

/// <summary>Allowlisted successful-generation metadata only. No raw envelope, errors, credentials or diagnostics.</summary>
public sealed record SavedCoachingProviderMetadata(string Provider, string Model, string ClientRequestId,
    string? RequestId, string? ResponseId, CoachingTokenUsage? Usage);

public sealed record CoachingAnalysisSummary(Guid Id, DateOnly ReviewDate, CoachingAnalysisScope Scope,
    string? AccountDisplayName, DateTimeOffset GeneratedAtUtc, string Provider, string Model);

/// <summary>Versioned JSON is the historical display source. Never rebuild it from current records/calculations.
/// Strings and metadata are immutable; loading does not depend on today's packet builder or validator version.</summary>
public sealed record SavedCoachingAnalysis(CoachingAnalysisSummary Summary, string EvidenceContractVersion,
    string ResponseContractVersion, string PacketId, string EvidenceJson, string ResponseJson,
    SavedCoachingProviderMetadata Metadata)
{
    public override string ToString() => $"SavedCoachingAnalysis: {Summary.Id}";
}

/// <summary>Only a successful, revalidated pair can cross the persistence write boundary.</summary>
public sealed class CoachingAnalysisSnapshot
{
    private CoachingAnalysisSnapshot(SavedCoachingAnalysis analysis) => Analysis = analysis;
    public SavedCoachingAnalysis Analysis { get; }

    public static CoachingAnalysisSnapshot Create(CoachingEvidencePacket packet, CoachingGenerationResult generation,
        DateTimeOffset generatedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(generation);
        cancellationToken.ThrowIfCancellationRequested();
        if (generatedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Generation timestamp must be UTC.", nameof(generatedAtUtc));
        if (generation.Status != CoachingGenerationStatus.Success || generation.Review is null || generation.Metadata is null)
            throw new ArgumentException("Only successful reviews with provider identity can be saved.", nameof(generation));
        // Serialize the validated structured object, never the provider's raw HTTP response.
        string responseJson = JsonSerializer.Serialize(generation.Review, CoachingContract.JsonOptions);
        if (!CoachingResponseValidator.Validate(responseJson, packet, cancellationToken).IsValid)
            throw new ArgumentException("Response does not validate against the exact supplied packet.", nameof(generation));
        var metadata = generation.Metadata;
        static bool Identifier(string? value, int maximum) => value is { Length: > 0 } && value.Length <= maximum &&
            value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
        // Some providers use one namespaced model ID (e.g. openai/gpt-oss-120b).
        // Only the model field accepts a slash; neither component may be empty.
        static bool ModelIdentifier(string? value) => Identifier(value, 128) ||
            value is { Length: <= 128 } && value.Split('/') is [var owner, var model] &&
            Identifier(owner, 128) && Identifier(model, 128);
        if (!Identifier(metadata.Provider, 64) || !ModelIdentifier(metadata.Model) ||
            !Identifier(metadata.ClientRequestId, 128) ||
            metadata.RequestId is not null && !Identifier(metadata.RequestId, 128) ||
            metadata.ResponseId is not null && !Identifier(metadata.ResponseId, 128))
            throw new ArgumentException("Provider metadata contains invalid identifiers.", nameof(generation));
        var usage = metadata.Usage;
        if (usage is not null && (usage.InputTokens < 0 || usage.CachedInputTokens < 0 || usage.OutputTokens < 0 ||
            usage.TotalTokens < 0 || usage.CachedInputTokens > usage.InputTokens ||
            usage.InputTokens is { } input && usage.OutputTokens is { } output && usage.TotalTokens is { } total &&
            (decimal)input + output != total))
            throw new ArgumentException("Provider usage is inconsistent.", nameof(generation));
        var scope = new CoachingAnalysisScope(packet.Content.Query.TradingAccountId);
        // Names are taken from the supplied snapshot, never looked up after generation.
        string? name = scope.Kind == CoachingAnalysisScopeKind.AllAccounts ? "All accounts" :
            packet.Content.RecordedTradeFacts.FirstOrDefault(t => t.Account.Id == scope.AccountId)?.Account.Name ??
            packet.Content.UntrustedJournalObservations.FirstOrDefault(j => j.TradingAccountId == scope.AccountId)?.AccountName;
        var summary = new CoachingAnalysisSummary(Guid.NewGuid(), packet.Content.Query.Date, scope, name,
            generatedAtUtc, metadata.Provider, metadata.Model);
        cancellationToken.ThrowIfCancellationRequested();
        return new(new(summary, packet.ContractVersion, generation.Review.ContractVersion, packet.PacketId,
            packet.Json, responseJson, new(metadata.Provider, metadata.Model, metadata.ClientRequestId,
                metadata.RequestId, metadata.ResponseId, usage)));
    }

    public override string ToString() => $"CoachingAnalysisSnapshot: {Analysis.Summary.Id}";
}

public sealed record CoachingAnalysisHistoryQuery
{
    public CoachingAnalysisHistoryQuery(DateOnly reviewDate, CoachingAnalysisScope scope, int page = 1, int pageSize = 20)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(page), "Use a positive page and page size 1–100.");
        ReviewDate = reviewDate; Scope = scope; Page = page; PageSize = pageSize;
    }
    public DateOnly ReviewDate { get; }
    public CoachingAnalysisScope Scope { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int Offset => (Page - 1) * PageSize;
}

public sealed record CoachingAnalysisHistoryPage(IReadOnlyList<CoachingAnalysisSummary> Items,
    int TotalCount, int Page, int PageSize)
{
    public bool HasPrevious => Page > 1;
    public bool HasNext => (long)Page * PageSize < TotalCount;
}

public interface ICoachingAnalysisRepository
{
    /// <summary>Unavailable exact-account scopes with saved analyses on this date. Names come from
    /// the newest saved analysis (generation descending, ID ascending), never a same-name Account.</summary>
    Task<HistoricalCoachingAccountPage> BrowseHistoricalAccountsAsync(HistoricalCoachingAccountQuery query,
        CancellationToken cancellationToken = default);
    Task<SavedCoachingAnalysis> SaveAsync(CoachingAnalysisSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<SavedCoachingAnalysis?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CoachingAnalysisHistoryPage> BrowseAsync(CoachingAnalysisHistoryQuery query, CancellationToken cancellationToken = default);
    /// <summary>Permanent deletion of this analysis only. Future UI must obtain confirmation first.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record HistoricalCoachingAccount(Guid AccountId, string? SavedAccountName);

public sealed record HistoricalCoachingAccountQuery
{
    public HistoricalCoachingAccountQuery(DateOnly reviewDate, int page = 1, int pageSize = 25)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(page), "Use a positive page and page size 1–100.");
        ReviewDate = reviewDate; Page = page; PageSize = pageSize;
    }
    public DateOnly ReviewDate { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int Offset => (Page - 1) * PageSize;
}

public sealed record HistoricalCoachingAccountPage(IReadOnlyList<HistoricalCoachingAccount> Items,
    int TotalCount, int Page, int PageSize)
{
    public bool HasPrevious => Page > 1;
    public bool HasNext => (long)Page * PageSize < TotalCount;
}
