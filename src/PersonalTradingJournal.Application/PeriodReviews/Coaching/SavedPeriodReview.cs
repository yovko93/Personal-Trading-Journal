using System.Text.Json;
using PersonalTradingJournal.Application.DailyReview.Coaching;

namespace PersonalTradingJournal.Application.PeriodReviews.Coaching;

public sealed record PeriodReviewSummary(Guid Id, ReviewPeriodKind Kind, DateOnly FirstDate, DateOnly LastDate,
    DateTimeOffset FromUtc, DateTimeOffset BeforeUtc, CoachingAnalysisScope Scope, string? AccountDisplayName,
    DateTimeOffset GeneratedAtUtc, string Provider, string Model);

/// <summary>Immutable historical JSON; never rebuild from current sources or revalidate using a newer contract.</summary>
public sealed record SavedPeriodReview(PeriodReviewSummary Summary, string EvidenceContractVersion,
    string ResponseContractVersion, string PacketId, string EvidenceJson, string ResponseJson, SavedCoachingProviderMetadata Metadata)
{
    public override string ToString() => $"SavedPeriodReview: {Summary.Id}";
}

/// <summary>The only repository write input. Owns the validated generation's exact packet, not a second database read.</summary>
public sealed class PeriodReviewSnapshot
{
    private PeriodReviewSnapshot(SavedPeriodReview review) => Review = review;
    public SavedPeriodReview Review { get; }
    public static PeriodReviewSnapshot FromManual(ManualReviewDraft draft, CancellationToken token = default)
    {
        var validation = ManualReviewContract.Validate(draft, token);
        return validation.Valid && validation.Period is { } saved ? new(saved) : throw new ArgumentException("Manual review is not valid.");
    }
    public static PeriodReviewSnapshot Create(PeriodCoachingResult generation, DateTimeOffset generatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generation); cancellationToken.ThrowIfCancellationRequested();
        if (generatedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Generation timestamp must be UTC.");
        if (generation.Status != PeriodCoachingStatus.Success || generation.Packet is not { } packet ||
            generation.Review is null || generation.Metadata is not { } metadata)
            throw new ArgumentException("Only a complete successful generation can be saved.");
        string response = JsonSerializer.Serialize(generation.Review, PeriodCoachingContract.JsonOptions);
        if (!PeriodCoachingResponseValidator.Validate(response, packet, cancellationToken).IsValid)
            throw new ArgumentException("Response does not match the supplied immutable packet.");
        var savedMetadata = SavedCoachingMetadata.Create(metadata);
        var q = packet.Query; var scope = new CoachingAnalysisScope(q.Scope.AccountId);
        string? label = scope.Kind == CoachingAnalysisScopeKind.AllAccounts ? "All accounts" : AccountLabel(packet);
        cancellationToken.ThrowIfCancellationRequested();
        return new(new(new(Guid.NewGuid(), q.Kind, q.FirstDate, q.LastDate, q.FromUtc, q.BeforeUtc, scope, label,
            generatedAtUtc, metadata.Provider, metadata.Model), packet.ContractVersion, generation.Review.ContractVersion,
            packet.PacketId, packet.Json, response, savedMetadata));
    }
    private static string? AccountLabel(PeriodCoachingPacket packet)
    {
        // Current snapshot first, then previous. Never look up a replacement Account by name.
        foreach (string period in new[] { "current", "previous" })
            foreach (var kind in new[] { PeriodCoachingSourceKind.Trade, PeriodCoachingSourceKind.Journal, PeriodCoachingSourceKind.Assessment })
                foreach (var source in packet.Sources.Where(s => s.Period == period && s.Kind == kind))
                {
                    var content = kind == PeriodCoachingSourceKind.Trade ? source.Content.GetProperty("account") : source.Content;
                    string property = kind == PeriodCoachingSourceKind.Trade ? "name" : kind == PeriodCoachingSourceKind.Journal ? "accountName" : "accountNameAtAssessment";
                    if (content.TryGetProperty(property, out var name) && name.ValueKind == JsonValueKind.String) return name.GetString();
                }
        return null; // Unavailable label stays unknown; original identity is still retained.
    }
    public override string ToString() => $"PeriodReviewSnapshot: {Review.Summary.Id}";
}

public sealed record PeriodReviewHistoryQuery
{
    public PeriodReviewHistoryQuery(PeriodReviewQuery period, int page = 1, int pageSize = 20)
    {
        ArgumentNullException.ThrowIfNull(period);
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(page), "Use a positive page and page size 1–100.");
        Period = period; Page = page; PageSize = pageSize;
    }
    public PeriodReviewQuery Period { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int Offset => (Page - 1) * PageSize;
}
public sealed record PeriodReviewHistoryPage(IReadOnlyList<PeriodReviewSummary> Items, int TotalCount, int Page, int PageSize)
{
    public bool HasPrevious => Page > 1;
    public bool HasNext => (long)Page * PageSize < TotalCount;
}
public sealed record SavedPeriodScope(CoachingAnalysisScope Scope, string? AccountDisplayName);
public sealed record SavedPeriodScopePage(IReadOnlyList<SavedPeriodScope> Items, int TotalCount, int Page, int PageSize);
public enum PeriodReviewDeleteStatus { Deleted, NotFound, NotConfirmed }

public interface IPeriodReviewRepository
{
    Task<SavedPeriodReview> SaveAsync(PeriodReviewSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<SavedPeriodReview?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>All accounts matches aggregate reviews only, not exact-account reviews.</summary>
    Task<PeriodReviewHistoryPage> BrowseAsync(PeriodReviewHistoryQuery query, CancellationToken cancellationToken = default);
    /// <summary>Bounded scope discovery, including deleted Accounts. Query must have All accounts scope;
    /// labels come from the newest saved review, not the current Account catalog.</summary>
    Task<SavedPeriodScopePage> BrowseScopesAsync(PeriodReviewHistoryQuery query, CancellationToken cancellationToken = default);
    /// <summary>Future UI must confirm this exact immutable ID. False is a write-free decline.</summary>
    Task<PeriodReviewDeleteStatus> DeleteAsync(Guid id, bool confirmed, CancellationToken cancellationToken = default);
}
