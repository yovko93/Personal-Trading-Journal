using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;

namespace PersonalTradingJournal.Desktop.ViewModels.PeriodReviews;

public sealed record PeriodHistoryRow(PeriodReviewSummary Source)
{
    public string Heading => $"{ReviewDisplay.Time(Source.GeneratedAtUtc)} · {ReviewDisplay.Provider(Source.Provider, Source.Model)}";
    public string Scope => Source.Scope.AccountId.HasValue ? $"Exact Account: {Source.AccountDisplayName ?? "Historical Account"}" : "All accounts · aggregate review";
}
public sealed record PeriodCitation(string Label, string Detail, Guid? TradeId = null, DailyReviewJournalEvidence? Journal = null)
{
    public bool HasTrade => TradeId.HasValue;
    public bool HasJournal => Journal is not null;
}
public sealed record PeriodFinding(string Basis, string Text, IReadOnlyList<PeriodCitation> Citations)
{
    // Editorial presentation only: preserve every character of the provider's validated text.
    private int Break => Text.IndexOf(". ", StringComparison.Ordinal) is > 0 and < 120 ? Text.IndexOf(". ", StringComparison.Ordinal) + 1 : 0;
    public string Title => Break > 0 ? Text[..Break] : Basis;
    public string Explanation => Break > 0 ? Text[Break..].TrimStart() : Text;
}
public sealed record PeriodCard(string Heading, string Note, IReadOnlyList<PeriodFinding> Findings);

public sealed class PeriodCurrentPresentation
{
    public PeriodCurrentPresentation(PeriodReviewComparison comparison)
    {
        var current = comparison.Current; var e = current.Evidence;
        Summary = (current.Population.Closed.Count == 0 ? "No closed trades" : $"{current.Population.Closed.Count} closed Trades") +
            $" · {current.ActiveDays.Count} active trading days · {e.Journals.Count} Journal entries";
        Coverage = ReviewDisplay.ActivityWarning(current.Population) + (current.Currencies.Count > 1 ? " Currencies are separate; no FX conversion or cross-currency total." : "");
        Metrics = MetricsFor(current.Currencies);
        if (Metrics.Any(m => m.Coverage.Length > 0 && m.Net.Unavailable.Count > 0))
            Coverage += " Net is unavailable for some Account/currency groups because required costs or P&L data are incomplete. See calculated coverage; no partial total is shown.";
        Journals = e.Journals.Select(j => new ReviewJournalRow(j)).ToArray();
        string RuleScope(PeriodRuleSummary r) => r.Scope.AccountId is not { } id ? "All accounts" : "Exact Account: " +
            (e.ClosedTrades.Concat(e.ActivityContext).FirstOrDefault(t => t.Account.Id == id)?.Account.Name
                ?? e.Journals.FirstOrDefault(j => j.TradingAccountId == id)?.AccountName
                ?? r.Targets.Select(t => t.Latest?.AccountNameAtAssessment).FirstOrDefault(n => n is not null) ?? "Unavailable original Account");
        Rules = comparison.CurrentPatterns.Rules.Select(r => new ReviewText(
            $"{r.Rule.Definition.Name} · revision {r.Rule.Revision} · {(r.TargetKind == Domain.Rules.TradingRuleTargetKind.Trade ? "Trade targets" : "New York-day targets")} · {RuleScope(r)}",
            $"Assessed as compliant: {r.Compliant} · Confirmed violation: {r.ConfirmedViolation} · Cannot evaluate: {r.CannotEvaluate} · Not assessed: {r.NotAssessed}\n" +
            $"{r.TargetCount} exact revision/target pairs; {r.LatestManual} manual, {r.LatestAutomatic} calculated. " +
            (r.Denominator == PeriodRuleDenominator.RecordedTargetsOnly ? "Historical/recorded targets only; no current compliance denominator. " : "Applicable opportunities, including manual-only rules. ") +
            (r.VersionState == PeriodRuleVersionState.CurrentGlobal ? "Current global revision." : "Historical, superseded or unavailable revision; not a current global-rule result.") +
            (r.UnavailableTargets + r.ChangedTargets + r.TargetsWithChangedOrUnavailableEvidence == 0 ? "" :
                $" Unavailable targets: {r.UnavailableTargets}; changed targets: {r.ChangedTargets}; targets with changed/unavailable evidence: {r.TargetsWithChangedOrUnavailableEvidence}."))).ToArray();
        RuleNotice = (Rules.Count == 0 ? "No applicable Rule coverage supplied. No compliance is inferred." : "Not assessed is absence of assessment; Cannot evaluate is not a pass. Trade and day targets are counted separately.") +
            (e.UnattributedTradeAssessments.Count == 0 ? "" : $" {e.UnattributedTradeAssessments.Count} retained assessments cannot be assigned to this period and are excluded from coverage counts.");
        Comparison = comparison.Groups.Where(g => g.AccountId.HasValue).Select(g => new ReviewText(
            $"{current.Currencies.SelectMany(c => c.Accounts).FirstOrDefault(a => a.Account.Id == g.AccountId)?.Account.Name ?? comparison.Previous.Currencies.SelectMany(c => c.Accounts).FirstOrDefault(a => a.Account.Id == g.AccountId)?.Account.Name ?? "Unavailable Account"} · {g.Currency}",
            $"Versus the previous equivalent period · closed Trades change {Change(g.ClosedTrades)} · active days {Change(g.ActiveDays)}\nGross change {Change(g.Gross, g.Currency)} · Net change {Change(g.Net, g.Currency)}\n" +
            $"Missing-cost Trade count change {Change(g.MissingCostTrades)}. Unavailable changes are not zero or evidence of improvement.")).ToArray();
        Mistakes = comparison.CurrentPatterns.Mistakes.Select(m => new ReviewText(m.Mistake.Name ?? "Unavailable recorded mistake",
            $"Recorded on {m.Frequency} distinct Trades ({m.ClosedFrequency} closed, {m.ActivityFrequency} activity). An observation, not a proven cause of losses.")).ToArray();
    }
    private static string Change(PeriodValueChange c, string currency = "") => c.Absolute is { } v ? $"{v:+0.##;-0.##;0} {currency}" : "Unavailable (coverage or population differs)";
    internal static IReadOnlyList<ReviewMetricRow> MetricsFor(IReadOnlyList<PeriodCurrencyStatistics> currencies) => currencies.SelectMany(c => c.Accounts.Select(a =>
        new ReviewMetricRow($"{ReviewDisplay.Account(a.Account)} · {c.Currency}", c.Currency, a.Statistics.Population, a.Statistics.Gross.Outcomes, a.Statistics.Net.Outcomes, a.Account.Id))).ToArray();
    public string Summary { get; }
    public string Coverage { get; }
    public IReadOnlyList<ReviewMetricRow> Metrics { get; }
    public IReadOnlyList<ReviewJournalRow> Journals { get; }
    public IReadOnlyList<ReviewText> Rules { get; }
    public IReadOnlyList<ReviewText> Comparison { get; }
    public IReadOnlyList<ReviewText> Mistakes { get; }
    public string JournalNotice => Journals.Count == 0 ? "No Journal entries supplied; this is different from an empty field." : "User-written observations, not calculated facts or proof of an outcome.";
    public string RuleNotice { get; }
}

/// <summary>Version-specific historical presentation. Never consults or recalculates current data.</summary>
public sealed class PeriodReport
{
    internal static JsonSerializerOptions JsonOptions { get; } = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }, MaxDepth = 64 };
    public required string Heading { get; init; }
    public required string Metadata { get; init; }
    public required IReadOnlyList<PeriodCard> Cards { get; init; }
    public required IReadOnlyList<ReviewMetricRow> Metrics { get; init; }
    public string Notice => "Saved AI interpretation and saved evidence · not current records. Citations identify supplied evidence, not proof that a claim is true.";
    public static bool Matches(PeriodReviewSummary s, PeriodReviewQuery q) => s.Kind == q.Kind && s.FirstDate == q.FirstDate && s.LastDate == q.LastDate && s.Scope.AccountId == q.Scope.AccountId;
    public static PeriodReport Read(SavedPeriodReview saved)
    {
        if (saved.EvidenceContractVersion is not ("period-coaching.v1" or "period-coaching.v2") || saved.ResponseContractVersion != saved.EvidenceContractVersion ||
            Encoding.UTF8.GetByteCount(saved.EvidenceJson) > (saved.EvidenceContractVersion == "period-coaching.v1" ? PeriodCoachingContract.MaximumPacketBytes : PeriodCoachingContract.MaximumSnapshotBytes) || Encoding.UTF8.GetByteCount(saved.ResponseJson) > PeriodCoachingContract.MaximumResponseBytes)
            throw new InvalidDataException("Unsupported saved period contract.");
        using var doc = JsonDocument.Parse(saved.EvidenceJson, new() { MaxDepth = 64 });
        var root = doc.RootElement; var content = root.GetProperty("content"); var period = content.GetProperty("currentPeriod");
        var response = JsonSerializer.Deserialize<PeriodCoachingResponse>(saved.ResponseJson, JsonOptions) ?? throw new InvalidDataException();
        if (S(root, "packetId") != saved.PacketId || S(root, "contractVersion") != saved.EvidenceContractVersion || response.PacketId != saved.PacketId || response.ContractVersion != saved.ResponseContractVersion ||
            S(period, "kind") != saved.Summary.Kind.ToString() || DateOnly.Parse(S(period, "firstDate")) != saved.Summary.FirstDate || DateOnly.Parse(S(period, "lastDate")) != saved.Summary.LastDate ||
            Id(period.GetProperty("scope"), "accountId") != saved.Summary.Scope.AccountId) throw new InvalidDataException("Saved identity mismatch.");
        var sources = content.GetProperty("sources").Deserialize<PeriodCoachingSource[]>(JsonOptions) ?? throw new InvalidDataException();
        if (saved.EvidenceContractVersion == "period-coaching.v2" &&
            (S(root.GetProperty("providerProjection"), "packetId") != saved.PacketId ||
             S(root.GetProperty("providerProjection"), "contractVersion") != saved.EvidenceContractVersion))
            throw new InvalidDataException("Saved projection identity mismatch.");
        // v2 citations can reference only the provider projection, never local-only examples.
        var supplied = saved.EvidenceContractVersion == "period-coaching.v2"
            ? root.GetProperty("providerProjection").GetProperty("sources").Deserialize<PeriodCoachingSource[]>(JsonOptions)!
            : sources;
        var map = supplied.ToDictionary(s => s.Id, StringComparer.Ordinal);
        PeriodCitation Cite(string id)
        {
            if (!map.TryGetValue(id, out var source)) throw new InvalidDataException("Unknown saved citation.");
            var c = source.Content; string side = source.Period == "previous" ? "Previous period" : source.Period == "both" ? "Both periods" : "Selected period";
            if (source.Kind is PeriodCoachingSourceKind.Trade or PeriodCoachingSourceKind.Execution)
            {
                Guid tradeId = Id(c, "tradeId") ?? throw new InvalidDataException();
                var tradeSource = sources.First(s => s.Kind == PeriodCoachingSourceKind.Trade && Id(s.Content, "tradeId") == tradeId);
                var trade = tradeSource.Content.Deserialize<DailyReviewTradeEvidence>(JsonOptions)!;
                var row = new ReviewTradeRow(trade);
                return new($"{side} · Trade · {row.Heading}", $"{row.Context}\nCurrency: {trade.PricingCurrency}. This is the saved source, not the current Trade.\n" +
                    $"Recorded Gross: {ReviewDisplay.Amount(trade.Facts?.GrossPnL, trade.PricingCurrency)}. Net is shown only in fully covered calculated summaries.", tradeId);
            }
            if (source.Kind == PeriodCoachingSourceKind.Journal)
            {
                var j = c.Deserialize<DailyReviewJournalEvidence>(JsonOptions)!; var row = new ReviewJournalRow(j);
                return new($"{side} · Journal · {row.Heading} · revision {j.Revision}", string.Join("\n\n", row.Fields.Select(f => f.Heading + "\n" + f.Text)), Journal: j);
            }
            if (source.Kind == PeriodCoachingSourceKind.JournalRevision)
            {
                var revision = c.GetProperty("revision").GetProperty("revision");
                string account = Id(c, "tradingAccountId") is null ? "All accounts" : S(c, "accountName");
                return new($"{side} · Journal revision {S(revision, "revision")} · {DateOnly.Parse(S(c, "tradingDate")):dd MMM yyyy} · New York · {account}",
                    JournalText(revision));
            }
            if (source.Kind == PeriodCoachingSourceKind.PlaybookAssociation)
            {
                var a = c.GetProperty("association").Deserialize<ReviewPlaybookAssociation>(JsonOptions)!; var g = a.Guidance;
                string text = $"Captured {ReviewDisplay.Time(a.CapturedAtUtc)} · {a.State.ToString().ToLowerInvariant()} association. Association is not verified adherence. No chart image was sent to AI.\n";
                if (g is not null) text += $"\nMarket context\n{g.MarketContext}\n\nEntry conditions\n{g.EntryConditions}\n\nConfirmations\n{g.Confirmations}\n\nInvalidation\n{g.Invalidation}\n\nExecution guidance\n{g.ExecutionGuidance}" + string.Concat(g.Examples.Select(e => $"\n\nExample ({e.Kind})\n{e.Annotation}"));
                return new($"{side} · Captured Playbook · {g?.SetupName ?? "No captured guidance"} · revision {g?.Revision.ToString() ?? "unavailable"}", text, Id(c, "tradeId"));
            }
            if (source.Kind is PeriodCoachingSourceKind.Assessment or PeriodCoachingSourceKind.UnattributedAssessment)
            {
                var a = c.GetProperty("assessment"); var rule = a.GetProperty("ruleRevision"); var target = a.GetProperty("target");
                string outcome = S(a, "outcome") switch { "NoViolation" => "Assessed as compliant", "ConfirmedViolation" => "Confirmed violation", _ => "Cannot evaluate" };
                string origin = S(a, "source") == "Manual" ? "Manual user assessment" : "Calculated assessment";
                string account = Id(target.GetProperty("accountScope"), "accountId") is null ? "All accounts" : S(c, "accountNameAtAssessment");
                return new($"{side} · Rule assessment · {S(rule.GetProperty("definition"), "name")} · {outcome}",
                    $"{origin} · rule revision {S(rule, "revision")} · {account}\n" +
                    $"{(Id(target, "tradeId").HasValue ? "Exact Trade target" : S(target, "newYorkDate") + " · New York-day target")}\n" +
                    $"Recorded {ReviewDisplay.Time(a.GetProperty("recordedAtUtc").GetDateTimeOffset())}\nRule: {S(rule.GetProperty("definition"), "name")}\n{S(rule.GetProperty("definition"), "criterion")}\n\n{S(a, "explanation")}\nSaved target availability: {S(c, "targetState")}. Later corrections do not rewrite this snapshot.", Id(target, "tradeId"));
            }
            if (source.Kind == PeriodCoachingSourceKind.RuleRevision) return new($"{side} · Rule · {S(c.GetProperty("definition"), "name")} · revision {S(c, "revision")}",
                S(c.GetProperty("definition"), "criterion") + "\nA Rule definition alone is not an assessment or proof of adherence.");
            if (source.Kind == PeriodCoachingSourceKind.CalculatedStatistics && c.TryGetProperty("accounts", out var accounts))
                return new($"{side} · Saved calculated statistics", $"{S(c.GetProperty("population"), "closed")} closed Trades · {S(c, "activeDays")} New York trading days.\n" +
                    string.Join("\n", accounts.EnumerateArray().Select(a =>
                    {
                        var values = a.GetProperty("values"); var gross = values.GetProperty("gross").GetProperty("metrics").GetProperty("total");
                        var net = values.GetProperty("net").GetProperty("metrics").GetProperty("total");
                        return $"{S(a.GetProperty("account"), "name")} · {S(a, "currency")} · Gross {(gross.ValueKind == JsonValueKind.Null ? "Unavailable" : gross.ToString())} · Net {(net.ValueKind == JsonValueKind.Null ? "Unavailable" : net.ToString())} · Missing-cost Trades: {S(values, "missingCosts")}";
                    })) + "\nThese supplied aggregates cover the complete population; individual examples are bounded. Missing costs are not zero.");
            return new($"{side} · Saved {(source.Kind == PeriodCoachingSourceKind.Comparison ? "period comparison" : source.Kind == PeriodCoachingSourceKind.CalculatedPatterns ? "Rule coverage and recorded mistake patterns" : "calculated statistics")}",
                "Calculated from the complete saved evidence with Account/currency boundaries. Unknown costs are not zero; unassessed Rules are not compliant. This source remains the snapshot used at generation.");
        }
        IReadOnlyList<PeriodCitation> Citations(IReadOnlyList<string> ids) => ids.Select(Cite).ToArray();
        PeriodCard Card(string heading, PeriodCoachingSection s) => new(heading, s.InsufficientEvidence ? "Evidence insufficient" : "AI interpretation · verify against evidence",
            s.Findings.Select(f => new PeriodFinding(f.Basis switch { PeriodFindingBasis.CalculatedFact => "References calculated facts", PeriodFindingBasis.UserObservation => "References user observations", PeriodFindingBasis.RecordedEvidence => "References recorded evidence", _ => "AI interpretation" }, f.Text, Citations(f.SourceIds))).ToArray());
        if (response.ActionPlan.Steps.Count > 3) throw new InvalidDataException();
        var plan = new List<PeriodFinding> { new("AI action plan", response.ActionPlan.Explanation, Citations(response.ActionPlan.SourceIds)) };
        plan.AddRange(response.ActionPlan.Steps.Select((s, i) => new PeriodFinding($"Step {i + 1}", $"{s.Action}\nMeasure: {s.Measure}\nWhen: {s.Timeframe}", Citations(s.SourceIds))));
        var stats = sources.Single(s => s.Id == "current:statistics").Content;
        var currencies = stats.GetProperty("currencies").Deserialize<PeriodCurrencyStatistics[]>(JsonOptions)!;
        var usage = saved.Metadata.Usage;
        return new()
        {
            Heading = $"{saved.Summary.FirstDate:dd MMM yyyy} – {saved.Summary.LastDate:dd MMM yyyy} · New York · {new PeriodHistoryRow(saved.Summary).Scope}",
            Metadata = $"{(saved.Metadata.Provider == ManualReviewContract.ProviderId ? "Prepared" : "Generated")} {ReviewDisplay.Time(saved.Summary.GeneratedAtUtc)} · {ReviewDisplay.Provider(saved.Metadata.Provider, saved.Metadata.Model)}\nTokens: input {usage?.InputTokens?.ToString() ?? "unknown"}, output {usage?.OutputTokens?.ToString() ?? "unknown"}, total {usage?.TotalTokens?.ToString() ?? "unknown"}. Monetary cost unknown.",
            Metrics = PeriodCurrentPresentation.MetricsFor(currencies),
            Cards = [Card("Strengths", response.Strengths), Card("Areas for Improvement", response.AreasForImprovement), Card("Mistakes Observed", response.MistakesObserved), Card("Progress", response.ProgressVersusPreviousPeriod), new("Action Plan", response.ActionPlan.InsufficientEvidence ? "Evidence insufficient" : "At most three measurable next steps", plan)],
        };
    }
    private static string JournalText(JsonElement r)
    {
        var revision = r.Deserialize<PersonalTradingJournal.Application.Journals.DailyJournalRevision>(JsonOptions)
            ?? throw new InvalidDataException();
        return $"Journal text\n{revision.Text}\n\nWhat went well?\n{revision.Review?.WentWell ?? "Not recorded"}\n\nWhat needs improvement?\n{revision.Review?.NeedsImprovement ?? "Not recorded"}\n\nNext trading day\n{revision.Review?.NextTradingDay ?? "Not recorded"}";
    }
    private static string S(JsonElement e, string p) => e.TryGetProperty(p, out var v) ? v.ValueKind == JsonValueKind.Null ? "Unavailable" : v.ToString() : "Unavailable";
    private static Guid? Id(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String && v.TryGetGuid(out var id) ? id : null;
}
