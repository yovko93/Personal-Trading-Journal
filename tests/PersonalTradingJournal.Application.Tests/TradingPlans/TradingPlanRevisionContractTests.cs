using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Application.Tests.TradingPlans;

public sealed class TradingPlanRevisionContractTests
{
    [Fact]
    public void AggregateUsesExistingCommandVersionAndExposesDraftReadyAndPendingDraft()
    {
        var (plan, account, instrument, clock) = Create();
        var loaded = TradingPlanLoadedState.FromPlan(plan);
        Assert.Equal(new TradingPlanExpectedState(plan.Id, 1), loaded.Token);
        Assert.Equal(0, loaded.LatestReadyRevision);
        var save = new TradingPlanWriteCommand(TradingPlanWriteOperation.SaveDraft, plan.Key, plan.WorkingDraft!.Content, loaded.Token);
        Assert.Null(save.CheckTransition(loaded));
        var ready = Ready(plan, account, instrument, clock);
        Assert.Equal(TradingPlanWriteStatus.Conflict, save.CheckTransition(TradingPlanLoadedState.FromPlan(plan)));
        loaded = TradingPlanLoadedState.FromPlan(plan);
        Assert.Equal(TradingPlanStatus.Ready, loaded.Status);
        Assert.Equal(1, loaded.LatestReadyRevision);
        var start = new TradingPlanWriteCommand(TradingPlanWriteOperation.StartDraft, plan.Key, ready.Content, loaded.Token, ready.Id);
        Assert.Null(start.CheckTransition(loaded));
        plan.StartDraft(loaded.Token.Version, ready, clock.GetUtcNow());
        loaded = TradingPlanLoadedState.FromPlan(plan);
        Assert.Equal(TradingPlanStatus.Draft, loaded.Status);
        Assert.Equal(1, loaded.LatestReadyRevision);
        Assert.Equal(TradingPlanPreparationState.DraftAfterReady, plan.PreparationState);
        Assert.Equal(TradingPlanWriteStatus.Conflict, start.CheckTransition(loaded));
    }

    [Fact]
    public void ActualReadyTimeNotSelectedDateSupportsLaterCutoffConsumers()
    {
        var (plan, account, instrument, clock) = Create();
        var ready = Ready(plan, account, instrument, clock);
        Assert.Equal(new DateOnly(2026, 10, 16), ready.Key.NewYorkDate);
        Assert.Equal(clock.GetUtcNow(), ready.ReadyAtUtc);
        Assert.False(TradingPlanDatePolicy.WasRecordedBeforeEntry(ready.ReadyAtUtc, clock.GetUtcNow().AddMinutes(-1)));
        Assert.False(TradingPlanDatePolicy.WasRecordedBeforeEntry(ready.ReadyAtUtc, clock.GetUtcNow()));
        Assert.True(TradingPlanDatePolicy.WasRecordedBeforeEntry(ready.ReadyAtUtc, clock.GetUtcNow().AddMinutes(1)));
        Assert.False(TradingPlanDatePolicy.WasRecordedBeforeEntry(ready.ReadyAtUtc, null));
    }

    private static (TradingPlan, TradingAccount, Instrument, TimeProvider) Create()
    {
        var clock = new FixedClock();
        var account = new TradingAccount("Synthetic", TradingAccountType.Demo, null, null, "USD", null, clock.GetUtcNow());
        var instrument = new Instrument("NQ", "Synthetic", AssetClass.Futures, null, "USD", .25m, 5, clock.GetUtcNow());
        var content = new TradingPlanContent(TradingPlanSession.NewYork, "Protect attention", TradingPlanIntent.StandAside,
            [new(instrument.Id)], standAside: "No confirmed opportunity");
        return (new(new(account.Id, new DateOnly(2026, 10, 16)), content, clock.GetUtcNow()), account, instrument, clock);
    }

    private static TradingPlanReadyRevision Ready(TradingPlan plan, TradingAccount account, Instrument instrument, TimeProvider clock)
    {
        var at = clock.GetUtcNow();
        plan.SetConfirmations(plan.Version, TradingPlanChecklist.All, at);
        return plan.MarkReady(plan.Version, TradingPlanReferenceSnapshot.Capture(plan.Key,
            plan.WorkingDraft!.Content, account, [instrument], [], [], at), at);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 17, 13, 0, 0, TimeSpan.Zero);
    }
}
