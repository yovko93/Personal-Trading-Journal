using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

public sealed record ReviewPreparationRow(ReviewPlanRevision Source)
{
    public string Heading => $"{Source.Key.NewYorkDate:dd MMM yyyy} · New York · {Source.Account.Name} · Ready {Source.Id.Revision}";
    public string Timing => $"Actually marked Ready {ReviewDisplay.Time(Source.ReadyAtUtc)}. Latest preparation is not necessarily preparation available before a Trade.";
    public string Focus => Source.Content.Focus;
    public string Detail
    {
        get
        {
            var c = Source.Content;
            string Instrument(Guid id) => Source.Instruments.FirstOrDefault(i => i.InstrumentId == id)?.Symbol ?? "Unavailable recorded Instrument";
            string Value(object? v) => v?.ToString() ?? "Not set";
            return $"Saved intention · {(c.Intent == TradingPlanIntent.StandAside ? "Stand Aside" : "Look for opportunities")} · session {c.Session}\n" +
                string.Join("\n\n", c.Instruments.Select(i => $"{Instrument(i.InstrumentId)} · bias {Value(i.Bias)}\n{i.MarketThesis}\n" +
                    $"Manual levels: previous day high {Value(i.Levels.PreviousDayHigh)}, low {Value(i.Levels.PreviousDayLow)}; Asia high {Value(i.Levels.AsiaHigh)}, low {Value(i.Levels.AsiaLow)}; London high {Value(i.Levels.LondonHigh)}, low {Value(i.Levels.LondonLow)}")) +
                "\n\n" + string.Join("\n\n", c.Scenarios.Select(s => $"{s.Name} · {Instrument(s.InstrumentId)} · {s.Role} · {Value(s.Direction)}\nIf: {s.Condition}\nThen: {s.Response}\nInvalid if: {s.Invalidation}\nPlaybook: " +
                    (s.Setup is null ? "Not linked" : Source.Setups.FirstOrDefault(x => x.SetupId == s.Setup.SetupId)?.Name ?? "Unavailable recorded Setup"))) +
                $"\n\nStand Aside conditions\n{c.StandAside}\n\nNews and restrictions\n{c.NewsAndRestrictions}\n\nPlanned limits · {Source.Account.Currency}\n" +
                $"Risk per Trade {Value(c.RiskLimits.RiskPerTrade)} · daily loss {Value(c.RiskLimits.DailyLossLimit)} · maximum Trades {Value(c.RiskLimits.MaximumTrades)} · stop after losses {Value(c.RiskLimits.StopAfterConsecutiveLosses)}\n" +
                $"Saved manual confirmations: {c.Confirmed}\n" + string.Join("\n", Source.Rules.Select(r => $"Linked Rule · {r.Name} · revision {r.Revision}\n{r.Criterion}")) +
                "\nIntentions and confirmations do not establish adherence. Actual initial risk and scenario confirmation are unknown; limits are not evaluated here.";
        }
    }
}

/// <summary>Uses supplied evidence only, including on saved reports. Never resolves current catalog labels.</summary>
public sealed class ReviewPreparation(ReviewPlanEvidence? evidence)
{
    public string Coverage => evidence is null ? "Preparation was not captured in this evidence version. No current Plan has been substituted." :
        evidence.Days.Count == 0 ? "No saved preparation or attributable Account/day coverage in this scope." :
        string.Join("\n", evidence.Days.Select(d => $"{d.Key.NewYorkDate:dd MMM yyyy} · New York · " +
            (evidence.Revisions.FirstOrDefault(r => r.Key == d.Key)?.Account.Name ?? d.AccountName ?? "Original Account (name unavailable)") + " · " + (d.State switch
            {
                ReviewPlanState.NoPlan => "No Trading Plan", ReviewPlanState.DraftOnly => "Draft only · no Ready evidence",
                ReviewPlanState.ReadyWithDraft => "Ready preparation with newer Draft changes · Draft text excluded",
                _ => "Ready preparation",
            })));
    public string Comparison => evidence is null ? "" : string.Join("\n", evidence.Trades.GroupBy(t => new { t.AccountId, t.Inclusion }).Select(g =>
        $"{evidence.Revisions.FirstOrDefault(r => r.Key.AccountId == g.Key.AccountId)?.Account.Name ?? evidence.Days.FirstOrDefault(d => d.Key.AccountId == g.Key.AccountId)?.AccountName ?? "Original Account (name unavailable)"} · " +
        (g.Key.Inclusion == Application.DailyReview.DailyReviewTradeInclusion.ClosedOnDate ? "closed Trades" : "separate activity context") +
        $": {g.Count(t => t.Timing == ReviewPlanTiming.AtOrBeforeEntry)} with Ready at/before entry; " +
        $"{g.Count(t => t.Timing == ReviewPlanTiming.ReadyAfterEntry)} with retrospective preparation only; " +
        $"{g.Count(t => t.Timing == ReviewPlanTiming.NoReadyRevision)} without Ready; {g.Count(t => t.Timing == ReviewPlanTiming.UnknownEntryTime)} unknown entry time.\n" +
        $"Recorded identity matches: {g.Count(t => t.InstrumentMatch == ReviewPlanMatch.MatchesRecordedIdentity)} Instrument, {g.Count(t => t.SetupMatch == ReviewPlanMatch.MatchesRecordedIdentity)} Setup. " +
        $"{g.Count(t => t.SetupMatch == ReviewPlanMatch.Unknown)} unknown Setup comparisons; {g.Count(t => t.StandAsideIntent == true)} Trades despite an eligible Stand Aside intention; {g.Count(t => t.CurrencyMatches == false)} currency mismatches. " +
        "These are recorded facts, not adherence or Rule violations. Actual initial risk is unknown."));
    public IReadOnlyList<ReviewPreparationRow> Revisions { get; } = evidence?.Revisions.Select(r => new ReviewPreparationRow(r)).ToArray() ?? [];
}
