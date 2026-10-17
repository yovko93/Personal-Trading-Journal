using PersonalTradingJournal.Domain.TradingPlans;
using static PersonalTradingJournal.Domain.Tests.TradingPlans.TradingPlanFixture;

namespace PersonalTradingJournal.Domain.Tests.TradingPlans;

public sealed class TradingPlanRevisionTests
{
    [Fact]
    public void ReadyCapturesCompleteConfirmedPreparationAndActualChronology()
    {
        var f = new TradingPlanFixture();
        var plan = f.Plan;
        Assert.Equal(TradingPlanPreparationState.DraftOnly, plan.PreparationState);
        Assert.Null(plan.LatestReadyRevision);
        Assert.Equal(TradingPlanChecklist.None, plan.WorkingDraft!.Content.Confirmed);
        plan.SetConfirmations(plan.Version, TradingPlanChecklist.All, Time.AddMinutes(2));
        var draft = plan.WorkingDraft;
        long draftVersion = plan.Version;
        var ready = plan.MarkReady(draftVersion, f.References(draft!.Content, Time.AddMinutes(3)), Time.AddMinutes(3));

        Assert.Equal(new TradingPlanRevisionId(plan.Id, 1), ready.Id);
        Assert.Equal(plan.Key, ready.Key);
        Assert.Equal(new DateOnly(2026, 10, 16), ready.Key.NewYorkDate);
        Assert.Equal(Time.AddMinutes(3), ready.ReadyAtUtc); // Not the backdated selected date.
        Assert.Equal(plan.CreatedAtUtc, ready.PlanCreatedAtUtc);
        Assert.Equal(Time.AddMinutes(1), ready.DraftStartedAtUtc);
        Assert.Equal(Time.AddMinutes(2), ready.DraftUpdatedAtUtc);
        Assert.Equal(draftVersion, ready.SourceDraftVersion);
        Assert.Equal(draftVersion + 1, plan.Version);
        Assert.Null(ready.DerivedFromRevision);
        Assert.Null(plan.WorkingDraft);
        Assert.Same(ready, plan.LatestReadyRevision);
        Assert.Equal(TradingPlanStatus.Ready, plan.Status);
        Assert.Equal(TradingPlanPreparationState.Ready, plan.PreparationState);
        Assert.Equal(TradingPlanChecklist.All, ready.Content.Confirmed);
        Assert.Equal(f.Content.Focus, ready.Content.Focus);
        Assert.Equal(f.Content.NewsAndRestrictions, ready.Content.NewsAndRestrictions);
        Assert.Equal(f.Content.StandAside, ready.Content.StandAside);
        Assert.Equal(f.Content.Scenarios.ToArray(), ready.Content.Scenarios.ToArray());
        Assert.Equal(f.Content.Instruments.ToArray(), ready.Content.Instruments.ToArray());
        Assert.Equal(20_000.2500000001m, ready.Content.Instruments[0].Levels.PreviousDayHigh);
        Assert.Null(ready.Content.RiskLimits.DailyLossLimit);
        Assert.Equal("USD", ready.References.Account.Currency);
        Assert.Equal("Retest", ready.References.Setups[0].Confirmations);
        Assert.Equal("Observe a retest", ready.References.Rules[0].Definition.Criterion);
        Assert.Equal(50m, ready.References.Rules[0].Definition.AutomaticCriterion!.MaximumLoss);
    }

    [Fact]
    public void InvalidReadyRetainsDraftAndProvidesFieldIssues()
    {
        var f = new TradingPlanFixture();
        var plan = f.Plan;
        var before = (plan.Version, plan.UpdatedAtUtc, plan.WorkingDraft);
        var error = Assert.Throws<TradingPlanNotReadyException>(() => plan.MarkReady(plan.Version,
            f.References(plan.WorkingDraft!.Content, Time.AddMinutes(3)), Time.AddMinutes(3)));
        Assert.Equal(5, error.Issues.Length);
        Assert.All(error.Issues, i => Assert.StartsWith("Checklist.", i.Field));
        Assert.Equal(before, (plan.Version, plan.UpdatedAtUtc, plan.WorkingDraft));
        Assert.Null(plan.LatestReadyRevision);
        plan.EditDraft(plan.Version, Change(f.Content, focus: " "), Time.AddMinutes(4));
        plan.SetConfirmations(plan.Version, TradingPlanChecklist.All, Time.AddMinutes(5));
        error = Assert.Throws<TradingPlanNotReadyException>(() => f.Ready(Time.AddMinutes(6)));
        Assert.Contains(error.Issues, i => i.Field == "Focus");
        Assert.Equal(" ", plan.WorkingDraft!.Content.Focus);
    }

    [Fact]
    public void StandAsideReadyRequiresNoInventedScenarioBiasOrLinkedCatalogs()
    {
        var f = new TradingPlanFixture(standAside: true);
        var revision = f.Ready();
        Assert.Equal(TradingPlanIntent.StandAside, revision.Content.Intent);
        Assert.Empty(revision.Content.Scenarios);
        Assert.Null(revision.Content.Instruments[0].Bias);
        Assert.Empty(revision.References.Setups);
        Assert.Empty(revision.References.Rules);
        Assert.NotEmpty(revision.Content.StandAside);
    }

    [Fact]
    public void HistoricalDraftAndSubsequentReadyNeverChangePriorContentOrTimes()
    {
        var f = new TradingPlanFixture();
        var plan = f.Plan;
        var first = f.Ready();
        plan.StartDraft(plan.Version, first, Time.AddMinutes(4));
        Assert.Equal(TradingPlanPreparationState.DraftAfterReady, plan.PreparationState);
        Assert.Equal(first.Id, plan.WorkingDraft!.DerivedFromRevision);
        Assert.Equal(TradingPlanChecklist.None, plan.WorkingDraft.Content.Confirmed);
        plan.EditDraft(plan.Version, Change(plan.WorkingDraft.Content, focus: "One opportunity"), Time.AddMinutes(5));
        Assert.Same(first, plan.LatestReadyRevision);
        Assert.Equal("Wait for confirmation", first.Content.Focus);
        Assert.Equal(TradingPlanChecklist.All, first.Content.Confirmed);
        var second = f.Ready(Time.AddMinutes(6));
        Assert.Equal(2, second.Id.Revision);
        Assert.Equal(first.Id, second.DerivedFromRevision);
        Assert.Equal("One opportunity", second.Content.Focus);
        plan.StartDraft(plan.Version, first, Time.AddMinutes(7));
        Assert.Same(second, plan.LatestReadyRevision);
        Assert.Equal(first.Content.Focus, plan.WorkingDraft!.Content.Focus);
        var third = f.Ready(Time.AddMinutes(8));
        Assert.Equal(3, third.Id.Revision);
        Assert.Equal(first.Id, third.DerivedFromRevision);
        Assert.Equal(Time.AddMinutes(3), first.ReadyAtUtc);
        Assert.Equal(Time.AddMinutes(6), second.ReadyAtUtc);
        Assert.Equal(Time.AddMinutes(7), third.DraftStartedAtUtc);
        Assert.Equal(plan.Id, third.Id.PlanId);
        Assert.NotEqual(second.Id, third.Id);
    }

    [Fact]
    public void StaleRepeatedAndForeignTransitionsLeaveAggregateUnchanged()
    {
        var f = new TradingPlanFixture();
        var plan = f.Plan;
        long stale = plan.Version;
        var first = f.Ready();
        var references = f.References(first.Content, Time.AddMinutes(4));
        UnchangedAfter<InvalidOperationException>(plan, () => plan.MarkReady(stale, references, Time.AddMinutes(4)));
        UnchangedAfter<InvalidOperationException>(plan, () => plan.MarkReady(plan.Version, references, Time.AddMinutes(4)));
        UnchangedAfter<InvalidOperationException>(plan, () => plan.EditDraft(plan.Version, f.Content, Time.AddMinutes(4)));
        UnchangedAfter<InvalidOperationException>(plan, () => plan.SetConfirmations(plan.Version, TradingPlanChecklist.All, Time.AddMinutes(4)));
        UnchangedAfter<InvalidOperationException>(plan, () => plan.StartDraft(stale, first, Time.AddMinutes(4)));
        var foreign = new TradingPlanFixture().Ready();
        UnchangedAfter<InvalidOperationException>(plan, () => plan.StartDraft(plan.Version, foreign, Time.AddMinutes(4)));
        plan.StartDraft(plan.Version, first, Time.AddMinutes(4));
        UnchangedAfter<InvalidOperationException>(plan, () => plan.StartDraft(plan.Version, first, Time.AddMinutes(5)));
        UnchangedAfter<InvalidOperationException>(plan, () => plan.EditDraft(stale, f.Content, Time.AddMinutes(5)));
        UnchangedAfter<InvalidOperationException>(plan, () => plan.SetConfirmations(stale, TradingPlanChecklist.All, Time.AddMinutes(5)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BackwardsAndNonUtcTimesCannotPartiallyChangeState(bool nonUtc)
    {
        var f = new TradingPlanFixture();
        var plan = f.Plan;
        var invalid = nonUtc ? Time.AddHours(1).ToOffset(TimeSpan.FromHours(2)) : Time;
        UnchangedAfter<ArgumentException>(plan, () => plan.EditDraft(plan.Version, Change(f.Content, focus: "Change"), invalid));
        UnchangedAfter<ArgumentException>(plan, () => plan.SetConfirmations(plan.Version, TradingPlanChecklist.All, invalid));
        UnchangedAfter<ArgumentException>(plan, () => plan.MarkReady(plan.Version, f.References(f.Content, Time.AddMinutes(3)), invalid));
        var ready = f.Ready();
        UnchangedAfter<ArgumentException>(plan, () => plan.StartDraft(plan.Version, ready, invalid));
        Assert.Throws<ArgumentException>(() => new TradingPlan(plan.Key, f.Content, Time.ToOffset(TimeSpan.FromHours(2))));
    }

    [Fact]
    public void NoOpInputsNeitherIncrementVersionNorChangeConfirmations()
    {
        var f = new TradingPlanFixture();
        var plan = f.Plan;
        var before = (plan.Version, plan.UpdatedAtUtc, plan.WorkingDraft);
        Assert.False(plan.EditDraft(plan.Version, Change(f.Content), Time.AddMinutes(2)));
        Assert.Equal(before, (plan.Version, plan.UpdatedAtUtc, plan.WorkingDraft));
        Assert.Equal(TradingPlanChecklist.None, plan.WorkingDraft!.Content.Confirmed); // Incoming All is not confirmation.
        plan.SetConfirmations(plan.Version, TradingPlanChecklist.All, Time.AddMinutes(2));
        before = (plan.Version, plan.UpdatedAtUtc, plan.WorkingDraft);
        Assert.False(plan.EditDraft(plan.Version, Change(f.Content, confirmed: TradingPlanChecklist.None), Time.AddMinutes(3)));
        Assert.False(plan.SetConfirmations(plan.Version, TradingPlanChecklist.All, Time.AddMinutes(3)));
        Assert.Equal(before, (plan.Version, plan.UpdatedAtUtc, plan.WorkingDraft));
        UnchangedAfter<ArgumentOutOfRangeException>(plan, () => plan.SetConfirmations(plan.Version, (TradingPlanChecklist)32, Time.AddMinutes(3)));
    }

    [Theory]
    [InlineData("focus", TradingPlanChecklist.MindsetAndFocus)]
    [InlineData("session", TradingPlanChecklist.All)]
    [InlineData("context", TradingPlanChecklist.ContextAndLevels | TradingPlanChecklist.ScenariosOrStandAside)]
    [InlineData("instrument", TradingPlanChecklist.All)]
    [InlineData("scenario", TradingPlanChecklist.ScenariosOrStandAside)]
    [InlineData("standAside", TradingPlanChecklist.ScenariosOrStandAside)]
    [InlineData("risk", TradingPlanChecklist.RiskLimits)]
    [InlineData("rules", TradingPlanChecklist.RiskLimits)]
    [InlineData("news", TradingPlanChecklist.News | TradingPlanChecklist.ScenariosOrStandAside)]
    public void RealEditsInvalidateExistingChecksDespiteIncomingAllFlags(string field, TradingPlanChecklist reset)
    {
        var f = new TradingPlanFixture();
        var plan = f.Plan;
        plan.SetConfirmations(plan.Version, TradingPlanChecklist.All, Time.AddMinutes(2));
        var content = f.Content;
        var next = field switch
        {
            "focus" => Change(content, focus: "Slow down"),
            "session" => Change(content, session: TradingPlanSession.London),
            "context" => Change(content, instruments: [new(f.Instrument.Id, TradingPlanBias.Long, "Changed thesis")]),
            "instrument" => Change(content, instruments: [..content.Instruments, new(Guid.NewGuid())]),
            "scenario" => Change(content, scenarios: []),
            "standAside" => Change(content, standAside: "Skip on fatigue"),
            "risk" => Change(content, risk: new(25m)),
            "rules" => Change(content, rules: []),
            _ => Change(content, news: "New restriction")
        };
        Assert.True(plan.EditDraft(plan.Version, next, Time.AddMinutes(3)));
        Assert.Equal(TradingPlanChecklist.All & ~reset, plan.WorkingDraft!.Content.Confirmed);
        Assert.Equal(TradingPlanChecklist.All, next.Confirmed);
        Assert.Null(plan.LatestReadyRevision);
    }

    [Fact]
    public void MultipleEditsCombineResetsAndExplicitReconfirmationIsRequired()
    {
        var f = new TradingPlanFixture();
        f.Plan.SetConfirmations(f.Plan.Version, TradingPlanChecklist.All, Time.AddMinutes(2));
        f.Plan.EditDraft(f.Plan.Version, Change(f.Content, focus: "New focus", risk: new(25m), news: "New notes"), Time.AddMinutes(3));
        Assert.Equal(TradingPlanChecklist.ContextAndLevels, f.Plan.WorkingDraft!.Content.Confirmed);
        UnchangedAfter<TradingPlanNotReadyException>(f.Plan, () => f.Plan.MarkReady(f.Plan.Version,
            f.References(f.Plan.WorkingDraft.Content, Time.AddMinutes(4)), Time.AddMinutes(4)));
        Assert.Equal(TradingPlanChecklist.All, f.Ready(Time.AddMinutes(5)).Content.Confirmed);
    }
}
