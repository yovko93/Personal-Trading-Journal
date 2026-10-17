using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.TradingPlans;
using static PersonalTradingJournal.Domain.Tests.TradingPlans.TradingPlanFixture;

namespace PersonalTradingJournal.Domain.Tests.TradingPlans;

public sealed class TradingPlanSnapshotTests
{
    [Fact]
    public void ReadyRemainsReadableAfterReferenceRenameCurrencyChangeAndArchive()
    {
        var f = new TradingPlanFixture();
        var ready = f.Ready();
        f.Account.UpdateDetails("Renamed", TradingAccountType.Demo, null, null, "EUR", null, Time.AddMinutes(4));
        f.Instrument.UpdateDetails("RENAMED", "Different instrument label", AssetClass.Futures, null, "EUR", .5m, 10, Time.AddMinutes(4));
        f.Setup.UpdateDetails("Changed Setup", "Changed description", Time.AddMinutes(4));
        var playbook = f.Playbook.Revise(f.Setup, 1, new("Other context", "Other entries", "Other confirmations", "Other invalidation", "Other execution"), Time.AddMinutes(4));
        var rule = f.Rule.Revise(1, new("Revised principle", "Changed criteria"), Time.AddMinutes(4)).Archive(2, Time.AddMinutes(5));
        f.Account.Deactivate(Time.AddMinutes(5));
        f.Instrument.Deactivate(Time.AddMinutes(5));
        f.Setup.Deactivate(Time.AddMinutes(5));

        Assert.Equal("Synthetic USD", ready.References.Account.Name);
        Assert.Equal("USD", ready.References.Account.Currency);
        Assert.Equal("NQ", ready.References.Instruments[0].Symbol);
        Assert.Equal("USD", ready.References.Instruments[0].Currency);
        Assert.Equal(.25m, ready.References.Instruments[0].TickSize);
        Assert.Equal("Reclaim", ready.References.Setups[0].Name);
        Assert.Equal("Synthetic guidance", ready.References.Setups[0].Description);
        Assert.Equal("Range", ready.References.Setups[0].MarketContext);
        Assert.Equal(1, ready.References.Setups[0].Revision);
        Assert.Equal(2, playbook.Revision);
        Assert.Equal("Wait for confirmation", ready.References.Rules[0].Definition.Name);
        Assert.Equal(1, ready.References.Rules[0].Revision);
        Assert.False(rule.IsActive);
        Assert.Equal(Time, ready.References.Account.UpdatedAtUtc);
        Assert.Equal(Time, ready.References.Setups[0].RecordedAtUtc);
        Assert.Equal(Time.AddMinutes(3), ready.References.CapturedAtUtc);
        // Historical reading has no catalog argument; unavailable navigation cannot rewrite it.
        f.Plan.StartDraft(f.Plan.Version, ready, Time.AddMinutes(6));
        Assert.Equal(ready.Id, f.Plan.WorkingDraft!.DerivedFromRevision);
        Assert.Same(ready, f.Plan.LatestReadyRevision);
        UnchangedAfter<InvalidOperationException>(f.Plan, () => f.References(f.Plan.WorkingDraft.Content, Time.AddMinutes(7)));
    }

    [Fact]
    public void ChangingInputListsAndDerivedNestedValuesDoesNotChangeReadyEvidence()
    {
        var f = new TradingPlanFixture();
        var contexts = f.Content.Instruments.ToList();
        var scenarios = f.Content.Scenarios.ToList();
        var rules = f.Content.Rules.ToList();
        var draftContent = Change(f.Content, instruments: contexts, scenarios: scenarios, rules: rules);
        var plan = new TradingPlan(f.Plan.Key, draftContent, Time.AddMinutes(1));
        plan.SetConfirmations(plan.Version, TradingPlanChecklist.All, Time.AddMinutes(2));
        var instruments = new List<Instrument> { f.Instrument };
        var setups = new List<TradingPlanPlaybookSource> { new(f.Setup, f.Playbook) };
        var ruleSources = new List<TradingRuleRevision> { f.Rule };
        var references = TradingPlanReferenceSnapshot.Capture(plan.Key, draftContent, f.Account,
            instruments, setups, ruleSources, Time.AddMinutes(3));
        contexts.Clear(); scenarios.Clear(); rules.Clear(); instruments.Clear(); setups.Clear(); ruleSources.Clear();
        var ready = plan.MarkReady(plan.Version, references, Time.AddMinutes(3));
        var changedLevels = draftContent.Instruments[0].Levels with { PreviousDayHigh = 9 };
        Assert.NotEqual(changedLevels, ready.Content.Instruments[0].Levels);
        Assert.Single(ready.Content.Scenarios);
        Assert.Single(ready.References.Instruments);
        Assert.Single(ready.References.Setups);
        Assert.Single(ready.References.Rules);
        Assert.Equal(20_000.2500000001m, ready.Content.Instruments[0].Levels.PreviousDayHigh);
        plan.StartDraft(plan.Version, ready, Time.AddMinutes(4));
        plan.EditDraft(plan.Version, Change(draftContent, instruments: [new(f.Instrument.Id,
            TradingPlanBias.Long, "Different thesis", changedLevels)]), Time.AddMinutes(5));
        Assert.Equal(9, plan.WorkingDraft!.Content.Instruments[0].Levels.PreviousDayHigh);
        Assert.Equal("Range at prior high", ready.Content.Instruments[0].MarketThesis);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("instrument")]
    [InlineData("setup")]
    [InlineData("setupChanged")]
    [InlineData("playbookRevision")]
    [InlineData("ruleArchived")]
    [InlineData("ruleChanged")]
    [InlineData("legacyRule")]
    public void CaptureRejectsInactiveChangedOrLegacySelectionsWithoutChangingDraft(string failure)
    {
        var f = new TradingPlanFixture();
        var rule = f.Rule;
        var playbook = f.Playbook;
        switch (failure)
        {
            case "account": f.Account.Deactivate(Time.AddMinutes(2)); break;
            case "instrument": f.Instrument.Deactivate(Time.AddMinutes(2)); break;
            case "setup": f.Setup.Deactivate(Time.AddMinutes(2)); break;
            case "setupChanged": f.Setup.UpdateDetails("Changed", null, Time.AddMinutes(2)); break;
            case "playbookRevision": playbook = f.Playbook.Revise(f.Setup, 1, f.Playbook.Guidance, Time.AddMinutes(2)); break;
            case "ruleArchived": rule = f.Rule.Archive(1, Time.AddMinutes(2)); break;
            case "ruleChanged": rule = f.Rule.Revise(1, new("Changed"), Time.AddMinutes(2)); break;
            case "legacyRule": rule = TradingRuleRevision.Rehydrate(f.Rule.RuleId, 1, new("Legacy", "Condition", "Evidence",
                TradingRuleApplicability.Trade, TradingRuleAccountScope.ForAccount(f.Account.Id), TradingRuleEvaluationMode.ManualOnly), true, Time); break;
        }
        UnchangedAfter<InvalidOperationException>(f.Plan, () => TradingPlanReferenceSnapshot.Capture(f.Plan.Key, f.Content,
            f.Account, [f.Instrument], [new(f.Setup, playbook)], [rule], Time.AddMinutes(3)));
    }

    [Theory]
    [InlineData("missingInstrument")]
    [InlineData("duplicateInstrument")]
    [InlineData("extraInstrument")]
    [InlineData("missingSetup")]
    [InlineData("duplicateSetup")]
    [InlineData("missingRule")]
    [InlineData("extraRule")]
    [InlineData("duplicateRule")]
    public void EverySelectedReferenceMustBeSuppliedExactlyOnce(string failure)
    {
        var f = new TradingPlanFixture();
        Instrument[] instruments = failure switch
        {
            "missingInstrument" => [], "duplicateInstrument" => [f.Instrument, f.Instrument],
            "extraInstrument" => [f.Instrument, new("ES", "Synthetic S&P", AssetClass.Futures, null, "USD", .25m, 12.5m, Time)],
            _ => [f.Instrument]
        };
        TradingPlanPlaybookSource[] setups = failure switch
        {
            "missingSetup" => [], "duplicateSetup" => [new(f.Setup, f.Playbook), new(f.Setup, f.Playbook)],
            _ => [new(f.Setup, f.Playbook)]
        };
        TradingRuleRevision[] rules = failure switch
        {
            "missingRule" => [], "duplicateRule" => [f.Rule, f.Rule],
            "extraRule" => [f.Rule, TradingRuleRevision.Create(Guid.NewGuid(), new("Unrelated"), Time)],
            _ => [f.Rule]
        };
        UnchangedAfter<InvalidOperationException>(f.Plan, () => TradingPlanReferenceSnapshot.Capture(f.Plan.Key,
            f.Content, f.Account, instruments, setups, rules, Time.AddMinutes(3)));
    }

    [Fact]
    public void SameNameReplacementIsNotTheOriginalAccountOrInstrument()
    {
        var f = new TradingPlanFixture();
        var other = new TradingPlanFixture();
        UnchangedAfter<InvalidOperationException>(f.Plan, () => TradingPlanReferenceSnapshot.Capture(f.Plan.Key,
            f.Content, other.Account, [f.Instrument], [new(f.Setup, f.Playbook)], [f.Rule], Time.AddMinutes(3)));
        UnchangedAfter<InvalidOperationException>(f.Plan, () => TradingPlanReferenceSnapshot.Capture(f.Plan.Key,
            f.Content, f.Account, [other.Instrument], [new(f.Setup, f.Playbook)], [f.Rule], Time.AddMinutes(3)));
    }

    [Fact]
    public void ReadyRejectsOldCaptureTimeAndMismatchedReferenceBundleAtomically()
    {
        var f = new TradingPlanFixture();
        f.Plan.SetConfirmations(f.Plan.Version, TradingPlanChecklist.All, Time.AddMinutes(2));
        var references = f.References(f.Content, Time.AddMinutes(3));
        UnchangedAfter<InvalidOperationException>(f.Plan, () => f.Plan.MarkReady(f.Plan.Version, references, Time.AddMinutes(4)));
        var other = new TradingPlanFixture();
        UnchangedAfter<InvalidOperationException>(f.Plan, () => f.Plan.MarkReady(f.Plan.Version,
            other.References(other.Content, Time.AddMinutes(3)), Time.AddMinutes(3)));
        f.Plan.EditDraft(f.Plan.Version, Change(f.Content, rules: []), Time.AddMinutes(3));
        f.Plan.SetConfirmations(f.Plan.Version, TradingPlanChecklist.All, Time.AddMinutes(3));
        UnchangedAfter<InvalidOperationException>(f.Plan, () => f.Plan.MarkReady(f.Plan.Version, references, Time.AddMinutes(3)));
    }

    [Fact]
    public void CaptureRejectsNonUtcOrBeforeSourceTimeAndBoundsEnumeration()
    {
        var f = new TradingPlanFixture();
        Assert.Throws<ArgumentException>(() => f.References(f.Content, Time.AddMinutes(-1)));
        Assert.Throws<ArgumentException>(() => f.References(f.Content, Time.ToOffset(TimeSpan.FromHours(2))));
        int visited = 0;
        IEnumerable<Instrument> Infinite()
        {
            while (true) { visited++; yield return f.Instrument; }
        }
        Assert.Throws<ArgumentException>(() => TradingPlanReferenceSnapshot.Capture(f.Plan.Key, f.Content,
            f.Account, Infinite(), [new(f.Setup, f.Playbook)], [f.Rule], Time.AddMinutes(3)));
        Assert.Equal(TradingPlanContent.MaximumInstruments + 1, visited);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapturedIdentitiesAloneCannotAuthorizeChangedRevisionTokens(bool changeSetup)
    {
        var f = new TradingPlanFixture();
        var captured = f.References(f.Content, Time.AddMinutes(3));
        var scenario = f.Content.Scenarios[0];
        var changed = changeSetup ? Change(f.Content, scenarios: [new(scenario.Id, scenario.InstrumentId,
            scenario.Name, scenario.Role, scenario.Direction, scenario.Condition, scenario.Response, scenario.Invalidation,
            new(f.Setup.Id, 2, f.Setup.UpdatedAtUtc))]) : Change(f.Content, rules: [new(f.Rule.RuleId, 2)]);
        f.Plan.EditDraft(f.Plan.Version, changed, Time.AddMinutes(2));
        f.Plan.SetConfirmations(f.Plan.Version, TradingPlanChecklist.All, Time.AddMinutes(3));
        UnchangedAfter<InvalidOperationException>(f.Plan, () => f.Plan.MarkReady(f.Plan.Version, captured, Time.AddMinutes(3)));
    }

    [Fact]
    public void OneSetupMayServeTwoScenariosButConflictingRevisionTokensCannotBeFrozen()
    {
        var f = new TradingPlanFixture();
        var s = f.Content.Scenarios[0];
        var alternative = new TradingPlanScenario(Guid.NewGuid(), f.Instrument.Id, "Alternative", TradingPlanScenarioRole.Alternative,
            s.Direction, s.Condition, s.Response, s.Invalidation, s.Setup);
        var content = Change(f.Content, scenarios: [s, alternative]);
        Assert.Single(f.References(content, Time.AddMinutes(3)).Setups);
        var stale = new TradingPlanScenario(alternative.Id, f.Instrument.Id, alternative.Name, alternative.Role,
            alternative.Direction, alternative.Condition, alternative.Response, alternative.Invalidation,
            new(f.Setup.Id, 2, f.Setup.UpdatedAtUtc));
        Assert.Throws<InvalidOperationException>(() => f.References(Change(content, scenarios: [s, stale]), Time.AddMinutes(3)));
    }

    [Fact]
    public void CaptureOrdersReferencesByPreparationRatherThanQueryOrder()
    {
        var f = new TradingPlanFixture(standAside: true);
        var second = new Instrument("ES", "Synthetic ES", AssetClass.Futures, null, "USD", .25m, 12.5m, Time);
        var content = Change(f.Content, instruments: [new(f.Instrument.Id), new(second.Id)]);
        var references = TradingPlanReferenceSnapshot.Capture(f.Plan.Key, content, f.Account, [second, f.Instrument], [], [], Time.AddMinutes(3));
        Assert.Equal(new[] { f.Instrument.Id, second.Id }, references.Instruments.Select(i => i.InstrumentId));
    }
}
