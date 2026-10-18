using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;

namespace PersonalTradingJournal.Application.DailyReview.Coaching;

public enum ManualReviewKind { Daily, Weekly, Monthly }
public sealed class ManualReviewException(string message) : Exception(message);
public sealed record ManualReviewRequest(ManualReviewKind Kind, DateOnly Date, Guid? AccountId, string AccountLabel);

/// <summary>One durable slot per workspace, independent of the currently browsed scope.</summary>
public sealed record ManualReviewDraft(Guid Id, long Revision, ManualReviewRequest Request, DateTimeOffset PreparedAtUtc,
    string EvidenceJson, string Prompt, string ResponseText, bool Saved = false)
{
    public const int MaximumPromptBytes = 300_000;
    public const int MaximumResponseBytes = 65_536;
    public override string ToString() => $"ManualReviewDraft: {KindLabel}, revision {Revision}";
    [JsonIgnore] public string KindLabel => Request.Kind.ToString();
    [JsonIgnore] public string ScopeLabel => $"{Request.Kind} · {Request.Date:dd MMM yyyy} · New York · {Request.AccountLabel}";
}

public sealed record ManualReviewValidation(bool Valid, string Message, SavedCoachingAnalysis? Daily = null, SavedPeriodReview? Period = null);

/// <summary>No provider or credential dependency. All text is sensitive; never log these arguments/results.</summary>
public interface IManualReviewService
{
    Task<ManualReviewDraft?> LoadAsync(ManualReviewKind kind, CancellationToken token = default);
    Task<ManualReviewDraft> PrepareAsync(ManualReviewRequest request, CancellationToken token = default);
    Task<ManualReviewDraft> KeepResponseAsync(ManualReviewDraft expected, string response, CancellationToken token = default);
    ManualReviewValidation Validate(ManualReviewDraft draft, CancellationToken token = default);
    Task<ManualReviewDraft> SaveAsync(ManualReviewDraft expected, CancellationToken token = default);
    Task DiscardAsync(ManualReviewDraft expected, CancellationToken token = default);
}

public static class ManualReviewContract
{
    public const string Provenance = "Pasted external AI response";
    public const string ProviderId = "external-paste";
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = 64,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static ManualReviewValidation Validate(ManualReviewDraft draft, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            if (draft.Id == Guid.Empty || draft.PreparedAtUtc.Offset != TimeSpan.Zero ||
                Encoding.UTF8.GetByteCount(draft.ResponseText) > ManualReviewDraft.MaximumResponseBytes)
                return Invalid("Response exceeds 65,536 UTF-8 bytes or the pending identity is invalid.");
            string json = Unfence(draft.ResponseText);
            using var doc = JsonDocument.Parse(draft.EvidenceJson, new() { MaxDepth = 64 });
            var root = doc.RootElement;
            string id = root.GetProperty("packetId").GetString()!;
            var metadata = new CoachingRequestMetadata(ProviderId, "unverified", draft.Id.ToString("N"));
            if (draft.Request.Kind == ManualReviewKind.Daily)
            {
                string version = root.GetProperty("contractVersion").GetString()!;
                if (version is not (CoachingContract.Version or CoachingContract.PlaybookVersion)) return Invalid("Unsupported prepared Daily contract. Keep this draft; do not retarget it.");
                var content = root.GetProperty("content").Deserialize<CoachingEvidenceContent>(ReadOptions)!;
                if (content.Query.Date != draft.Request.Date || content.Query.TradingAccountId != draft.Request.AccountId) return Invalid("Prepared date or Account scope does not match.");
                var packet = new CoachingEvidencePacket(content, id, draft.EvidenceJson, Encoding.UTF8.GetByteCount(draft.EvidenceJson), version);
                var result = CoachingResponseValidator.Validate(json, packet, token);
                if (!result.IsValid) return Invalid(string.Join("\n", result.Errors.Take(8)));
                var snapshot = CoachingAnalysisSnapshot.Create(packet, new(CoachingGenerationStatus.Success, result.Response, metadata, Provenance), draft.PreparedAtUtc, token);
                var saved = snapshot.Analysis;
                return new(true, Provenance + ". Citations validated; service and truth of claims are not verified. Preview only until Save Review.",
                    saved with { Summary = saved.Summary with { Id = draft.Id, AccountDisplayName = draft.Request.AccountLabel } });
            }
            string periodVersion = root.GetProperty("contractVersion").GetString()!;
            if (periodVersion is not (PeriodCoachingContract.Version or PeriodCoachingContract.CompactVersion)) return Invalid("Unsupported prepared period contract. Keep this draft; do not retarget it.");
            var query = new PeriodReviewQuery(draft.Request.Kind == ManualReviewKind.Weekly ? ReviewPeriodKind.Week : ReviewPeriodKind.Month, draft.Request.Date, draft.Request.AccountId);
            var projection = root.GetProperty("providerProjection");
            var selected = projection.GetProperty("currentPeriod");
            if (selected.GetProperty("firstDate").GetDateOnly() != query.FirstDate ||
                selected.GetProperty("scope").GetProperty("accountId").Deserialize<Guid?>() != query.Scope.AccountId ||
                selected.GetProperty("kind").GetString() != query.Kind.ToString()) return Invalid("Prepared period or Account scope does not match.");
            var packet2 = new PeriodCoachingPacket(query, id, draft.EvidenceJson, Encoding.UTF8.GetByteCount(draft.EvidenceJson),
                root.GetProperty("content").GetProperty("sources").Deserialize<PeriodCoachingSource[]>(ReadOptions)!, periodVersion)
            {
                ProviderJson = projection.GetRawText(),
                SuppliedSources = projection.GetProperty("sources").Deserialize<PeriodCoachingSource[]>(ReadOptions)!,
            };
            var validation = PeriodCoachingResponseValidator.Validate(json, packet2, token);
            if (!validation.IsValid) return Invalid($"Response validation: {validation.ErrorCode}. Paste one complete JSON object for this prompt, with all five sections, matching packetId/version and citations only from supplied sources. Do not substitute another period's response.");
            var period = PeriodReviewSnapshot.Create(new(PeriodCoachingStatus.Success, validation.Response, packet2, metadata, Provenance), draft.PreparedAtUtc, token).Review;
            return new(true, Provenance + ". Citations validated; service and truth of claims are not verified. Preview only until Save Review.",
                Period: period with { Summary = period.Summary with { Id = draft.Id, AccountDisplayName = draft.Request.AccountLabel } });
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException)
        { return Invalid("The response or prepared packet is invalid. Paste a single JSON object (optionally inside one ```json fence); retain the exact identity, version, sections and supplied citations."); }
    }

    private static string Unfence(string value)
    {
        string text = value.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal)) return text;
        int newline = text.IndexOf('\n');
        if (newline < 0 || text[..newline].TrimEnd() is not ("```json" or "```") || !text.EndsWith("```", StringComparison.Ordinal))
            throw new JsonException();
        return text[(newline + 1)..^3].Trim();
    }
    private static ManualReviewValidation Invalid(string message) => new(false, message);
    private static DateOnly GetDateOnly(this JsonElement element) => DateOnly.ParseExact(element.GetString()!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
