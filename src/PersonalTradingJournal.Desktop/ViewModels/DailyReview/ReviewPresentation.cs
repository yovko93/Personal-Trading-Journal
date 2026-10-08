using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Desktop.Formatting;

namespace PersonalTradingJournal.Desktop.ViewModels.DailyReview;

public sealed record ReviewAccount(Guid? Id, string Label);
public sealed record ReviewText(string Heading, string Text);

public static class ReviewDisplay
{
    public static string Amount(decimal? value, string currency) => value is { } amount
        ? $"{amount:N2} {currency}" : $"Unavailable ({currency})";
    public static string Time(DateTimeOffset value) => TradingTimestampFormatter.FormatNewYork(value,
        "g", CultureInfo.CurrentCulture) + " · New York";
    public static string Account(DailyReviewReference reference) =>
        $"{reference.Name ?? "Unavailable account"}{(reference.IsActive == false ? " (inactive)" : "")} · {reference.Id}";
    public static string Scope(CoachingAnalysisSummary item) => item.Scope.AccountId is { } id
        ? $"Exact account: {item.AccountDisplayName ?? "Name not supplied"} · {id}"
        : "All accounts · aggregate analysis";
}

public sealed record ReviewMetricRow(string Label, string Currency, DailyReviewPopulation Population,
    DailyReviewPnlStatistics Gross, DailyReviewPnlStatistics Net)
{
    public string Values => $"Closed Trades: {Population.Closed.Count} · Gross: {ReviewDisplay.Amount(Gross.Metrics.Total, Currency)} · Net: {ReviewDisplay.Amount(Net.Metrics.Total, Currency)}";
    public string Coverage => $"Gross coverage {Gross.Known.Count}/{Population.Closed.Count}; Net coverage {Net.Known.Count}/{Population.Closed.Count}. " +
        $"Unknown commissions: {Population.UnknownCommissions.Count}; unknown fees: {Population.UnknownFees.Count}. " +
        $"Excluded open/partial: {Population.ExcludedOpenActivity.Count}; unavailable lifecycle: {Population.ExcludedUnavailableLifecycle.Count}.";
    public string Outcomes => $"Known Gross outcomes: {Gross.Wins.Count} wins / {Gross.Losses.Count} losses / {Gross.BreakEvens.Count} break-even. " +
        "Unknown outcomes are not counted as break-even. No cross-currency total.";
    public string Sources => "Closed contributing Trade IDs: " + string.Join(", ", Population.Closed.TradeIds);
}

public sealed record ReviewTradeRow(DailyReviewTradeEvidence Source, bool CanNavigate = false)
{
    public Guid Id => Source.TradeId;
    public string Heading => $"{Source.Instrument.Name ?? "Unavailable instrument"} · {ReviewDisplay.Account(Source.Account)}";
    public string Values => $"{Source.Facts?.Status.ToString() ?? "Lifecycle unavailable"} · {Source.Facts?.Direction.ToString() ?? "Direction unavailable"} · " +
        $"Gross {ReviewDisplay.Amount(Source.Facts?.GrossPnL, Source.PricingCurrency)} · Net {ReviewDisplay.Amount(Source.Facts?.NetPnL, Source.PricingCurrency)}";
    public string Context => $"{Source.Inclusion} · Closure: {(Source.Facts?.ClosedAtUtc is { } time ? ReviewDisplay.Time(time) : "Unavailable")} · Quality: {Source.Quality}";
    public string Identity => $"Trade {Id} · last updated {ReviewDisplay.Time(Source.UpdatedAtUtc)} (timestamp, not a revision token)";
    public string Details => JsonSerializer.Serialize(Source, ReviewSnapshot.JsonOptions);
}

public sealed record ReviewJournalRow(DailyReviewJournalEvidence Source, bool CanNavigate = false)
{
    public string Heading => $"{Source.TradingDate:dd MMM yyyy} · {(Source.TradingAccountId is null ? "All accounts journal (distinct scope)" : Source.AccountName ?? "Unavailable account")} · {Source.AccountState} · {(Source.IsDraft ? "Draft" : "Completed")}";
    public string Identity => $"Journal {Source.JournalId} · Account {Source.TradingAccountId?.ToString() ?? "null (All accounts)"} · revision {Source.Revision}";
    public IReadOnlyList<ReviewText> Fields => [new("Journal text", Field(Source.Text)),
        new("What went well?", Field(Source.Answers.WentWell)), new("What needs improvement?", Field(Source.Answers.NeedsImprovement)),
        new("Next trading day", Field(Source.Answers.NextTradingDay))];
    private static string Field(string text) => string.IsNullOrEmpty(text) ? "(Empty field)" : text;
}

/// <summary>Presentation only. Historical statistics are supplied from the snapshot, never recalculated.</summary>
public sealed class ReviewEvidencePresentation(DailyReviewEvidence evidence, DailyReviewStatistics statistics, bool canNavigate = false)
{
    public string Summary => $"{evidence.Query.Date:dd MMM yyyy} New York · Closed Trades: {statistics.Population.Closed.Count} · " +
        $"Excluded open/partial activity: {statistics.Population.ExcludedOpenActivity.Count} · Unavailable lifecycle: {statistics.Population.ExcludedUnavailableLifecycle.Count}";
    public string EmptyText => evidence.Trades.Count == 0 && evidence.Journals.Count == 0
        ? "No Trade or Journal evidence for this day and scope. No realized result or reflection is inferred."
        : evidence.Trades.Count == 0 ? "No Trade evidence. Journal observations do not imply a trading result." : "";
    public string JournalEmptyText => evidence.Journals.Count == 0 ? "No Journal entries supplied (different from an empty field)." : "User-written observations, not verified trading facts.";
    public IReadOnlyList<ReviewMetricRow> Metrics { get; } = statistics.Currencies.SelectMany(c =>
        new[] { new ReviewMetricRow($"{c.Currency} · all included accounts", c.Currency, c.Population, c.Gross, c.Net) }
            .Concat(c.Accounts.Select(a => new ReviewMetricRow(ReviewDisplay.Account(a.Account), c.Currency, a.Population, a.Gross, a.Net)))).ToArray();
    public IReadOnlyList<ReviewTradeRow> Trades { get; } = evidence.Trades.Select(t => new ReviewTradeRow(t, canNavigate)).ToArray();
    public IReadOnlyList<ReviewJournalRow> Journals { get; } = evidence.Journals.Select(j => new ReviewJournalRow(j, canNavigate)).ToArray();
}

public sealed record ReviewAnalysisRow(CoachingAnalysisSummary Source)
{
    public string Heading => $"{ReviewDisplay.Time(Source.GeneratedAtUtc)} · {Source.Provider} / {Source.Model}";
    public string Scope => ReviewDisplay.Scope(Source);
    public string Identity => $"Analysis {Source.Id} · {Source.ReviewDate:yyyy-MM-dd}";
}

public sealed class ReviewSnapshot
{
    internal static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }, MaxDepth = 48,
    };
    public required string Heading { get; init; }
    public required string Metadata { get; init; }
    public required ReviewEvidencePresentation Evidence { get; init; }
    public required IReadOnlyList<ReviewText> Statements { get; init; }
    public required string CompleteEvidence { get; init; }

    public static ReviewSnapshot Read(SavedCoachingAnalysis saved)
    {
        if (saved.EvidenceContractVersion != CoachingContract.Version || saved.ResponseContractVersion != CoachingContract.Version)
            throw new InvalidDataException("This saved contract version is not supported by this Desktop version.");
        using var document = JsonDocument.Parse(saved.EvidenceJson);
        if (document.RootElement.GetProperty("packetId").GetString() != saved.PacketId ||
            document.RootElement.GetProperty("contractVersion").GetString() != saved.EvidenceContractVersion)
            throw new InvalidDataException("Saved evidence identity is inconsistent.");
        var content = document.RootElement.GetProperty("content").Deserialize<CoachingEvidenceContent>(JsonOptions)
            ?? throw new InvalidDataException("Saved evidence is unavailable.");
        var response = JsonSerializer.Deserialize<CoachingResponse>(saved.ResponseJson, JsonOptions)
            ?? throw new InvalidDataException("Saved response is unavailable.");
        if (response.PacketId != saved.PacketId || response.ContractVersion != saved.ResponseContractVersion ||
            content.Query.Date != saved.Summary.ReviewDate || content.Query.TradingAccountId != saved.Summary.Scope.AccountId)
            throw new InvalidDataException("Saved review identity is inconsistent.");
        string Citations(IEnumerable<string> ids) => string.Join("\n", ids.Select(id =>
        {
            var source = content.Sources.SingleOrDefault(s => s.Id == id)
                ?? throw new InvalidDataException("A saved citation is unavailable.");
            return $"{source.Id} — {source.Kind}; Account {source.AccountId?.ToString() ?? "aggregate/null scope"}; " +
                $"currency {source.Currency ?? "not monetary"}; Trade {source.TradeId}; Journal {source.JournalId}; revision {source.Revision}. See saved evidence below.";
        }));
        ReviewText Statement(string label, CoachingStatement s) => new(label, s.Text + "\n\nSources (saved):\n" + Citations(s.SourceIds));
        var statements = new List<ReviewText> { Statement("AI day summary · not calculated fact", response.DaySummary) };
        statements.AddRange(response.ExecutionObservations.Select(o => Statement("Execution · " + o.Basis, new(o.Text, o.SourceIds))));
        statements.AddRange(response.BehaviorObservations.Select(o => Statement("Behavior · " + o.Basis, new(o.Text, o.SourceIds))));
        statements.AddRange(response.ImprovementSuggestions.Select(o => Statement("AI suggestion", o)));
        statements.AddRange(response.Uncertainties.Select(o => Statement("Uncertainty", o)));
        var usage = saved.Metadata.Usage;
        static string Token(long? value) => value?.ToString(CultureInfo.CurrentCulture) ?? "unknown";
        return new()
        {
            Heading = $"Saved analysis · {saved.Summary.Id} · {ReviewDisplay.Scope(saved.Summary)}",
            Metadata = $"{ReviewDisplay.Time(saved.Summary.GeneratedAtUtc)} · {saved.Summary.Provider} / {saved.Summary.Model}\n" +
                $"Tokens: input {Token(usage?.InputTokens)}, cached {Token(usage?.CachedInputTokens)}, output {Token(usage?.OutputTokens)}, total {Token(usage?.TotalTokens)}. Cost unknown.\n" +
                $"Evidence {saved.EvidenceContractVersion}; response {saved.ResponseContractVersion}; packet {saved.PacketId}",
            Evidence = new(new(content.Query, content.RecordedTradeFacts, content.UntrustedJournalObservations), content.CalculatedFacts),
            Statements = statements,
            CompleteEvidence = JsonSerializer.Serialize(document.RootElement, JsonOptions),
        };
    }
}
