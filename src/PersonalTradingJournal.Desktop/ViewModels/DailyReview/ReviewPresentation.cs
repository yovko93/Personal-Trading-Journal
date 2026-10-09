using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Formatting;

namespace PersonalTradingJournal.Desktop.ViewModels.DailyReview;

public sealed record ReviewAccount(Guid? Id, string Label, bool IsHistorical = false);
public sealed record ReviewText(string Heading, string Text, string? SourceDetails = null)
{
    public bool HasSourceDetails => SourceDetails is not null;
    public IReadOnlyList<ReviewCitation> Citations { get; init; } = [];
}

public sealed record ReviewCitation(string Label, ReviewTradeRow? Trade = null, ReviewJournalRow? Journal = null)
{
    public bool HasTrade => Trade is not null;
    public bool HasJournal => Journal is not null;
}

public static class ReviewDisplay
{
    public static string Amount(decimal? value, string currency) => value is { } amount
        ? $"{amount:N2} {currency}" : $"Unavailable ({currency})";
    public static string Time(DateTimeOffset value) => TradingTimestampFormatter.FormatNewYork(value,
        "dd MMM yyyy HH:mm:ss", CultureInfo.CurrentCulture) + " · New York";
    public static string Account(DailyReviewReference reference) =>
        $"{reference.Name ?? "Unavailable account"}{(reference.IsActive == false ? " (inactive)" : "")}";
    public static string Scope(CoachingAnalysisSummary item) => item.Scope.AccountId is not null
        ? $"Exact account: {item.AccountDisplayName ?? "Name not supplied"}"
        : "All accounts · aggregate analysis";
    public static string ActivityWarning(DailyReviewPopulation population) => string.Join(" ", new[]
    {
        population.ExcludedOpenActivity.Count > 0 ? $"{population.ExcludedOpenActivity.Count} open/partial Trades excluded from realized results." : "",
        population.ExcludedUnavailableLifecycle.Count > 0 ? $"{population.ExcludedUnavailableLifecycle.Count} Trades excluded because their closing status is unavailable." : "",
    }.Where(s => s.Length > 0));
}

public sealed record ReviewMetricRow(string Label, string Currency, DailyReviewPopulation Population,
    DailyReviewPnlStatistics Gross, DailyReviewPnlStatistics Net, Guid AccountId)
{
    public string Values => $"Closed Trades: {Population.Closed.Count} · Gross: {ReviewDisplay.Amount(Gross.Metrics.Total, Currency)} · Net: {ReviewDisplay.Amount(Net.Metrics.Total, Currency)}";
    public string Coverage
    {
        get
        {
            var warnings = new List<string>();
            if (Population.Closed.Count > 0 && Net.Known.Count < Population.Closed.Count)
            {
                int commissions = Population.Closed.TradeIds.Count(Population.UnknownCommissions.TradeIds.Contains);
                int fees = Population.Closed.TradeIds.Count(Population.UnknownFees.TradeIds.Contains);
                var missing = new List<string>();
                if (commissions > 0) missing.Add($"commissions missing for {commissions} {(commissions == 1 ? "Trade" : "Trades")}");
                if (fees > 0) missing.Add($"fees missing for {fees} {(fees == 1 ? "Trade" : "Trades")}");
                if (Net.Unavailable.TradeIds.Except(Population.ClosedUnknownCosts.TradeIds).Any())
                    missing.Add("other Net data incomplete");
                warnings.Add($"Net unavailable: {(missing.Count > 0 ? string.Join("; ", missing) : "required cost or P&L data incomplete")}. No partial total.");
            }
            if (Gross.Known.Count < Population.Closed.Count)
                warnings.Add($"Gross known for {Gross.Known.Count}/{Population.Closed.Count} closed Trades; unknown outcomes are not break-even.");
            var activity = ReviewDisplay.ActivityWarning(Population);
            if (activity.Length > 0) warnings.Add(activity);
            return string.Join(" ", warnings);
        }
    }
    public string Outcomes => Population.Closed.Count == 0 ? "" : Gross.Known.Count == 0
        ? "Gross outcomes unavailable." : $"Gross outcomes: {Gross.Wins.Count} wins / {Gross.Losses.Count} losses / {Gross.BreakEvens.Count} break-even" +
          (Gross.Known.Count < Population.Closed.Count ? $" (of {Gross.Known.Count} known outcomes)." : ".");
    public string Sources => JsonSerializer.Serialize(new { AccountId, Currency, Population, Gross, Net }, ReviewSnapshot.JsonOptions);
    public string SourceLabel => $"Source details · {Label} calculation";
}

public sealed record ReviewTradeRow(DailyReviewTradeEvidence Source, bool CanNavigate = false, bool GrossKnown = false, bool NetKnown = false)
{
    public Guid Id => Source.TradeId;
    public string Heading => $"{Source.Instrument.Name ?? "Unavailable instrument"} · {ReviewDisplay.Account(Source.Account)}";
    public string Values => $"{Source.Facts?.Direction.ToString() ?? "Direction unavailable"} · " +
        $"Gross {ReviewDisplay.Amount(GrossKnown ? Source.Facts?.GrossPnL : null, Source.PricingCurrency)} · Net {ReviewDisplay.Amount(NetKnown ? Source.Facts?.NetPnL : null, Source.PricingCurrency)}";
    public string Context => Source.Inclusion switch
    {
        DailyReviewTradeInclusion.ClosedOnDate => $"Closed { (Source.Facts?.ClosedAtUtc is { } time ? ReviewDisplay.Time(time) : "time unavailable")}",
        DailyReviewTradeInclusion.OpenActivityOnDate => "Open/partial activity · excluded from realized results",
        _ => "Closing status unavailable · excluded from realized results",
    };
    public string Identity => $"Trade {Id} · last updated {ReviewDisplay.Time(Source.UpdatedAtUtc)} (timestamp, not a revision token)";
    public string Details => JsonSerializer.Serialize(Source, ReviewSnapshot.JsonOptions);
}

public sealed record ReviewJournalRow(DailyReviewJournalEvidence Source, bool CanNavigate = false)
{
    public string AccountLabel => (Source.TradingAccountId is null ? "All accounts" : Source.AccountName ?? "Unavailable account") +
        (Source.AccountState == DailyJournalAccountState.Inactive ? " (inactive)" : Source.AccountState == DailyJournalAccountState.Unavailable ? " (unavailable)" : "");
    public string Heading => $"{Source.TradingDate:dd MMM yyyy} · New York · {AccountLabel} · {(Source.IsDraft ? "Draft" : "Completed")}";
    public string Identity => $"Journal {Source.JournalId} · Account {Source.TradingAccountId?.ToString() ?? "null (All accounts)"} · {Source.AccountState} · revision {Source.Revision}";
    public string Details => JsonSerializer.Serialize(Source, ReviewSnapshot.JsonOptions);
    public IReadOnlyList<ReviewText> Fields => [new("Journal text", Field(Source.Text)),
        new("What went well?", Field(Source.Answers.WentWell)), new("What needs improvement?", Field(Source.Answers.NeedsImprovement)),
        new("Next trading day", Field(Source.Answers.NextTradingDay))];
    private static string Field(string text) => string.IsNullOrEmpty(text) ? "(Empty field)" : text;
}

/// <summary>Presentation only. Historical statistics are supplied from the snapshot, never recalculated.</summary>
public sealed class ReviewEvidencePresentation(DailyReviewEvidence evidence, DailyReviewStatistics statistics, bool canNavigate = false)
{
    public string Counts => (statistics.Population.Closed.Count == 0 ? "No closed trades" : $"{statistics.Population.Closed.Count} closed Trades") +
        $" · {evidence.Journals.Count} Journal entries (user-written observations)";
    public string CoverageSummary => string.Join(" ", new[]
    {
        statistics.Currencies.Any(c => c.Net.Unavailable.Count > 0) ? "Some Net results are unavailable. See the affected Account summaries below." : "",
        ReviewDisplay.ActivityWarning(statistics.Population),
        statistics.Currencies.Count > 1 ? "Multiple currencies shown separately; no combined monetary total." : "",
    }.Where(s => s.Length > 0));
    public string Summary => $"{evidence.Query.Date:dd MMM yyyy} · New York · {Counts}";
    public string SourceDetails => JsonSerializer.Serialize(new { Evidence = evidence, Statistics = statistics }, ReviewSnapshot.JsonOptions);
    public string EmptyText => evidence.Trades.Count == 0 && evidence.Journals.Count == 0
        ? "No Trade or Journal evidence for this day and scope. No realized result or reflection is inferred."
        : evidence.Trades.Count == 0 ? "No Trade evidence. Journal observations do not imply a trading result." : "";
    public string JournalEmptyText => evidence.Journals.Count == 0 ? "No Journal entries supplied (different from an empty field)." : "User-written observations, not verified trading facts.";
    public IReadOnlyList<ReviewMetricRow> Metrics { get; } = statistics.Currencies.SelectMany(c =>
        c.Accounts.Select(a => new ReviewMetricRow($"{ReviewDisplay.Account(a.Account)} · {c.Currency}", c.Currency, a.Population, a.Gross, a.Net, a.Account.Id))).ToArray();
    public IReadOnlyList<ReviewTradeRow> Trades { get; } = evidence.Trades.Select(t => new ReviewTradeRow(t, canNavigate,
        statistics.Currencies.Any(c => c.Gross.Known.TradeIds.Contains(t.TradeId)),
        statistics.Currencies.Any(c => c.Net.Known.TradeIds.Contains(t.TradeId)))).ToArray();
    public IReadOnlyList<ReviewJournalRow> Journals { get; } = evidence.Journals.Select(j => new ReviewJournalRow(j, canNavigate)).ToArray();
}

public sealed record ReviewAnalysisRow(CoachingAnalysisSummary Source)
{
    public string Heading => $"{ReviewDisplay.Time(Source.GeneratedAtUtc)} · {Source.Provider} / {Source.Model}";
    public string Scope => ReviewDisplay.Scope(Source);
    public string Identity => $"Analysis {Source.Id} · {Source.ReviewDate:dd MMM yyyy} · New York";
    public string Details => JsonSerializer.Serialize(Source, ReviewSnapshot.JsonOptions);
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
    public string SourceDetails { get; init; } = "";

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
        ReviewCitation Citation(string id)
        {
            var source = content.Sources.SingleOrDefault(s => s.Id == id)
                ?? throw new InvalidDataException("A saved citation is unavailable.");
            if (source.Kind is CoachingSourceKind.Trade or CoachingSourceKind.Execution)
            {
                var trade = content.RecordedTradeFacts.SingleOrDefault(t => t.TradeId == source.TradeId)
                    ?? throw new InvalidDataException("A saved Trade citation is unavailable.");
                var row = new ReviewTradeRow(trade, CanNavigate: true);
                string context = source.Kind == CoachingSourceKind.Execution
                    ? "Execution · " + ReviewDisplay.Time(trade.Executions.Single(e => e.Id == source.ExecutionId).ExecutedAtUtc)
                    : row.Context;
                return new($"Saved source: {row.Heading} · {trade.PricingCurrency} · {context}", Trade: row);
            }
            if (source.Kind == CoachingSourceKind.Journal)
            {
                var journal = content.UntrustedJournalObservations.SingleOrDefault(j => j.JournalId == source.JournalId)
                    ?? throw new InvalidDataException("A saved Journal citation is unavailable.");
                var row = new ReviewJournalRow(journal, CanNavigate: true);
                return new($"Saved Journal: {row.Heading} · revision {source.Revision}", Journal: row);
            }
            string scope = source.Kind switch
            {
                CoachingSourceKind.DayStatistics => "day summary",
                CoachingSourceKind.CurrencyStatistics => $"{source.Currency} summary",
                CoachingSourceKind.AccountStatistics => $"{content.CalculatedFacts.Currencies.SelectMany(c => c.Accounts)
                    .First(a => a.Account.Id == source.AccountId).Account.Name ?? "Unavailable account"} · {source.Currency}",
                _ => throw new InvalidDataException("A saved citation kind is unavailable."),
            };
            return new($"Saved calculation: {scope} · {content.Query.Date:dd MMM yyyy} · New York");
        }
        ReviewText Statement(string label, CoachingStatement s) => new(label, s.Text, Citations(s.SourceIds))
        { Citations = s.SourceIds.Select(Citation).ToArray() };
        static string Basis(CoachingObservationBasis basis) => basis switch
        {
            CoachingObservationBasis.CalculatedFact => "calculated facts",
            CoachingObservationBasis.RecordedTradeFact => "recorded Trade facts",
            _ => "user-written Journal observations",
        };
        var statements = new List<ReviewText> { Statement("AI day summary · not calculated fact", response.DaySummary) };
        statements.AddRange(response.ExecutionObservations.Select(o => Statement("Execution · " + Basis(o.Basis), new(o.Text, o.SourceIds))));
        statements.AddRange(response.BehaviorObservations.Select(o => Statement("Behavior · " + Basis(o.Basis), new(o.Text, o.SourceIds))));
        statements.AddRange(response.ImprovementSuggestions.Select(o => Statement("AI suggestion", o)));
        statements.AddRange(response.Uncertainties.Select(o => Statement("Uncertainty", o)));
        var usage = saved.Metadata.Usage;
        static string Token(long? value) => value?.ToString(CultureInfo.CurrentCulture) ?? "unknown";
        return new()
        {
            Heading = $"Saved analysis · {ReviewDisplay.Scope(saved.Summary)}",
            Metadata = $"{ReviewDisplay.Time(saved.Summary.GeneratedAtUtc)} · {saved.Summary.Provider} / {saved.Summary.Model}\n" +
                $"Tokens: input {Token(usage?.InputTokens)}, cached {Token(usage?.CachedInputTokens)}, output {Token(usage?.OutputTokens)}, total {Token(usage?.TotalTokens)}. Cost unknown.",
            SourceDetails = JsonSerializer.Serialize(new { saved.Summary, saved.Metadata, saved.PacketId, saved.EvidenceContractVersion, saved.ResponseContractVersion }, JsonOptions),
            Evidence = new(new(content.Query, content.RecordedTradeFacts, content.UntrustedJournalObservations), content.CalculatedFacts),
            Statements = statements,
            CompleteEvidence = JsonSerializer.Serialize(document.RootElement, JsonOptions),
        };
    }
}
