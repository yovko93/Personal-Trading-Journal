using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Domain.Tests.TradingPlans;

internal sealed class TradingPlanFixture
{
    internal static readonly DateTimeOffset Time = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    internal TradingAccount Account { get; } = new("Synthetic USD", TradingAccountType.Demo, null, null, "USD", null, Time);
    internal Instrument Instrument { get; } = new("NQ", "Synthetic Nasdaq", AssetClass.Futures, null, "USD", .25m, 5m, Time);
    internal TradingSetup Setup { get; } = new("Reclaim", "Synthetic guidance", Time);
    internal TradingRuleRevision Rule { get; } = TradingRuleRevision.Create(Guid.NewGuid(),
        new("Wait for confirmation", "Observe a retest", automaticCriterion: new(RuleProfitBasis.StrictNet, 50, "USD")), Time);
    internal TradingSetupPlaybookRevision Playbook { get; }
    internal TradingPlanContent Content { get; }
    internal TradingPlan Plan { get; }

    internal TradingPlanFixture(bool standAside = false)
    {
        Playbook = TradingSetupPlaybookRevision.Create(Setup, new("Range", "Reclaim", "Retest", "Lost level", "Wait",
            [new PlaybookExample(Guid.NewGuid(), "Unrelated chart annotation")]), Time);
        Content = new(TradingPlanSession.NewYork, "Wait for confirmation",
            standAside ? TradingPlanIntent.StandAside : TradingPlanIntent.Opportunities,
            [new(Instrument.Id, standAside ? null : TradingPlanBias.Conditional,
                standAside ? "" : "Range at prior high", new(PreviousDayHigh: 20_000.2500000001m))],
            standAside ? [] : [new(Guid.NewGuid(), Instrument.Id, "Reclaim", direction: TradeDirection.Long,
                condition: "Reclaim high", response: "Wait for retest", invalidation: "Acceptance below",
                setup: new(Setup.Id, Playbook.Revision, Setup.UpdatedAtUtc))],
            "Manual news review", "No entries without confirmation", new(50m, null, 3, 2),
            standAside ? [] : [new(Rule.RuleId, Rule.Revision)], TradingPlanChecklist.All);
        Plan = new(new(Account.Id, new DateOnly(2026, 10, 16)), Content, Time.AddMinutes(1));
    }

    internal TradingPlanReferenceSnapshot References(TradingPlanContent content, DateTimeOffset at) =>
        TradingPlanReferenceSnapshot.Capture(Plan.Key, content, Account, [Instrument],
            content.Scenarios.Any(s => s.Setup is not null) ? [new(Setup, Playbook)] : [],
            content.Rules.IsEmpty ? [] : [Rule], at);

    internal TradingPlanReadyRevision Ready(DateTimeOffset? at = null)
    {
        var readyAt = at ?? Time.AddMinutes(3);
        Plan.SetConfirmations(Plan.Version, TradingPlanChecklist.All, readyAt);
        return Plan.MarkReady(Plan.Version, References(Plan.WorkingDraft!.Content, readyAt), readyAt);
    }

    internal static TradingPlanContent Change(TradingPlanContent source, string? focus = null,
        TradingPlanSession? session = null, IEnumerable<TradingPlanInstrumentContext>? instruments = null,
        IEnumerable<TradingPlanScenario>? scenarios = null, string? news = null, string? standAside = null,
        TradingPlanRiskLimits? risk = null, IEnumerable<TradingPlanRuleLink>? rules = null,
        TradingPlanChecklist confirmed = TradingPlanChecklist.All) =>
        new(session ?? source.Session, focus ?? source.Focus, source.Intent, instruments ?? source.Instruments,
            scenarios ?? source.Scenarios, news ?? source.NewsAndRestrictions, standAside ?? source.StandAside,
            risk ?? source.RiskLimits, rules ?? source.Rules, confirmed);

    internal static void UnchangedAfter<T>(TradingPlan plan, Action action) where T : Exception
    {
        var before = (plan.Version, plan.UpdatedAtUtc, plan.WorkingDraft, plan.LatestReadyRevision, plan.PreparationState);
        Assert.Throws<T>(action);
        Assert.Equal(before, (plan.Version, plan.UpdatedAtUtc, plan.WorkingDraft, plan.LatestReadyRevision, plan.PreparationState));
    }
}
