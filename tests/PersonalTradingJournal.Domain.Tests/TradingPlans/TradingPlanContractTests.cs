using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Domain.Tests.TradingPlans;

public sealed class TradingPlanContractTests
{
    private static readonly Guid Account = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Instrument = Guid.Parse("20000000-0000-0000-0000-000000000001");

    [Fact]
    public void IdentityIsExactAccountAndDateIndependentOfSessionAndInstruments()
    {
        var date = new DateOnly(2026, 10, 17);
        Assert.Equal(new TradingPlanKey(Account, date), new TradingPlanKey(Account, date));
        Assert.NotEqual(new TradingPlanKey(Account, date), new TradingPlanKey(Guid.NewGuid(), date));
        Assert.NotEqual(new TradingPlanKey(Account, date), new TradingPlanKey(Account, date.AddDays(1)));
        Assert.Throws<ArgumentException>(() => new TradingPlanKey(Guid.Empty, date));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReadyRevisionAddressRequiresPositiveRevision(int revision) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingPlanRevisionId(Account, revision));

    [Fact]
    public void ReadyRevisionAddressRetainsStableIdentity()
    {
        Assert.Equal(new TradingPlanRevisionId(Account, 1), new TradingPlanRevisionId(Account, 1));
        Assert.NotEqual(new TradingPlanRevisionId(Account, 1), new TradingPlanRevisionId(Account, 2));
        Assert.Throws<ArgumentException>(() => new TradingPlanRevisionId(Guid.Empty, 1));
    }

    [Fact]
    public void EmptyDraftIsValidContentButHasActionableReadinessErrors()
    {
        var draft = new TradingPlanContent();
        var issues = TradingPlanReadiness.Validate(draft);
        Assert.Equal(4, issues.Length);
        Assert.Contains(issues, e => e.Field == "Session");
        Assert.Contains(issues, e => e.Field == "Focus");
        Assert.Contains(issues, e => e.Field == "Instruments");
        Assert.Contains(issues, e => e.Field == "Scenarios");
        Assert.DoesNotContain(issues, e => e.Field.StartsWith("Checklist.", StringComparison.Ordinal));
        Assert.Equal(TradingPlanChecklist.None, draft.Confirmed);
    }

    [Fact]
    public void StandAsideNeedsNoInventedOpportunityThesisBiasLevelsOrRiskAmounts()
    {
        var content = StandAside();
        Assert.Empty(TradingPlanReadiness.Validate(content));
        Assert.Empty(content.Scenarios);
        Assert.Null(content.Instruments[0].Bias);
        Assert.Null(content.Instruments[0].Levels.PreviousDayHigh);
        Assert.Null(content.RiskLimits.RiskPerTrade);
        Assert.Empty(TradingPlanReadiness.Validate(Opportunity()));
    }

    [Fact]
    public void MissingStandAsideReasonDoesNotBecomeReadyFromCheckmarksAlone()
    {
        var content = new TradingPlanContent(TradingPlanSession.NewYork, "Preserve attention",
            TradingPlanIntent.StandAside, [new(Instrument)], confirmed: TradingPlanChecklist.All);
        Assert.Contains(TradingPlanReadiness.Validate(content), i => i.Field == "StandAside");
    }

    [Fact]
    public void ScenarioTextIsOptionalButDirectionRemainsExplicit()
    {
        var content = new TradingPlanContent(TradingPlanSession.London, "Wait",
            instruments: [new(Instrument, TradingPlanBias.Conditional, "Range")],
            scenarios: [new(Guid.NewGuid(), Instrument)], confirmed: TradingPlanChecklist.All);
        Assert.Equal(new[] { "Scenarios[0].Direction" }, TradingPlanReadiness.Validate(content).Select(i => i.Field));
    }

    [Fact]
    public void OppositeDirectionsAlternativeScenarioAndCatalogLinksAreOptional()
    {
        var source = Opportunity();
        var content = new TradingPlanContent(source.Session, source.Focus, instruments: source.Instruments,
            scenarios: source.Scenarios, confirmed: source.Confirmed);
        Assert.Single(content.Scenarios);
        Assert.Equal(TradeDirection.Long, content.Scenarios[0].Direction);
        Assert.Null(content.Scenarios[0].Setup);
        Assert.Empty(content.Rules);
        Assert.Empty(TradingPlanReadiness.Validate(content));
    }

    [Fact]
    public void StandAsideCannotAlsoAdvertiseTradingScenarios()
    {
        var opportunity = Opportunity();
        var content = new TradingPlanContent(opportunity.Session, opportunity.Focus, TradingPlanIntent.StandAside,
            opportunity.Instruments, opportunity.Scenarios, standAside: "No entries", confirmed: TradingPlanChecklist.All);
        Assert.Contains(TradingPlanReadiness.Validate(content), i => i.Field == "Scenarios");
    }

    [Fact]
    public void CollectionsAreDefensivelyCopiedAndInstrumentOwnershipIsEnforced()
    {
        var contexts = new List<TradingPlanInstrumentContext> { new(Instrument) };
        var content = new TradingPlanContent(instruments: contexts);
        contexts.Clear();
        Assert.Single(content.Instruments);
        Assert.Throws<ArgumentException>(() => new TradingPlanContent(instruments: [new(Instrument), new(Instrument)]));
        Assert.Throws<ArgumentException>(() => new TradingPlanContent(instruments: [new(Instrument)],
            scenarios: [new(Guid.NewGuid(), Guid.NewGuid())]));
        var scenario = new TradingPlanScenario(Guid.NewGuid(), Instrument);
        Assert.Throws<ArgumentException>(() => new TradingPlanContent(instruments: [new(Instrument)], scenarios: [scenario, scenario]));
        var rule = new TradingPlanRuleLink(Guid.NewGuid(), 1);
        Assert.Throws<ArgumentException>(() => new TradingPlanContent(rules: [rule, rule]));
    }

    [Fact]
    public void LimitsAreEnforcedWithoutEnumeratingUnboundedInput()
    {
        int visited = 0;
        IEnumerable<TradingPlanInstrumentContext> Infinite()
        {
            while (true) { visited++; yield return new(Guid.NewGuid()); }
        }
        Assert.Throws<ArgumentException>(() => new TradingPlanContent(instruments: Infinite()));
        Assert.Equal(TradingPlanContent.MaximumInstruments + 1, visited);
        Assert.Throws<ArgumentException>(() => new TradingPlanContent(focus: new string('a', 501)));
        Assert.Throws<ArgumentException>(() => new TradingPlanContent(newsAndRestrictions: new string('a', 4001)));
        Assert.Throws<ArgumentException>(() => new TradingPlanContent(instruments: [null!]));
    }

    [Fact]
    public void TextAndDecimalLevelsAreExactAndMissingIsNotZero()
    {
        const string text = "  NQ context\r\nWait for confirmation.  ";
        var levels = new TradingPlanLevels(PreviousDayHigh: 12345.67890123456789m, AsiaLow: 0, LondonLow: -5);
        var content = new TradingPlanContent(focus: "  Focus  ", instruments: [new(Instrument, marketThesis: text, levels: levels)]);
        Assert.Equal(text, content.Instruments[0].MarketThesis);
        Assert.Equal(12345.67890123456789m, content.Instruments[0].Levels.PreviousDayHigh);
        Assert.Null(content.Instruments[0].Levels.PreviousDayLow);
        Assert.Equal(0m, content.Instruments[0].Levels.AsiaLow);
        Assert.Equal(-5m, content.Instruments[0].Levels.LondonLow);
        Assert.Equal("  Focus  ", content.Focus);
    }

    [Theory]
    [InlineData(TradingPlanEditArea.Session, TradingPlanChecklist.All)]
    [InlineData(TradingPlanEditArea.Instruments, TradingPlanChecklist.All)]
    [InlineData(TradingPlanEditArea.Focus, TradingPlanChecklist.MindsetAndFocus)]
    [InlineData(TradingPlanEditArea.ContextAndLevels, TradingPlanChecklist.ContextAndLevels | TradingPlanChecklist.ScenariosOrStandAside)]
    [InlineData(TradingPlanEditArea.ScenariosAndSetups, TradingPlanChecklist.ScenariosOrStandAside)]
    [InlineData(TradingPlanEditArea.StandAside, TradingPlanChecklist.ScenariosOrStandAside)]
    [InlineData(TradingPlanEditArea.RiskLimitsAndRules, TradingPlanChecklist.RiskLimits)]
    [InlineData(TradingPlanEditArea.News, TradingPlanChecklist.News | TradingPlanChecklist.ScenariosOrStandAside)]
    public void EditingResetsOnlyAffectedExplicitConfirmations(TradingPlanEditArea area, TradingPlanChecklist invalidated)
    {
        Assert.Equal(TradingPlanChecklist.All & ~invalidated,
            TradingPlanChecklistPolicy.AfterEdit(TradingPlanChecklist.All, area));
        Assert.Equal(TradingPlanChecklist.None, TradingPlanChecklistPolicy.AfterEdit(TradingPlanChecklist.None, area));
    }

    [Fact]
    public void UnknownFlagsAndEnumValuesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingPlanContent(session: (TradingPlanSession)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingPlanContent(intent: (TradingPlanIntent)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingPlanContent(confirmed: (TradingPlanChecklist)32));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingPlanInstrumentContext(Instrument, (TradingPlanBias)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingPlanScenario(Guid.NewGuid(), Instrument, direction: (TradeDirection)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => TradingPlanChecklistPolicy.AfterEdit(TradingPlanChecklist.All, (TradingPlanEditArea)999));
        Assert.Equal(5, TradingPlanChecklistPolicy.CompletedCount(TradingPlanChecklist.All));
    }

    [Fact]
    public void RiskLimitsRetainNullZeroAndExactAmountsButRejectNegativeRisk()
    {
        Assert.Null(new TradingPlanRiskLimits().DailyLossLimit);
        Assert.Equal(0, new TradingPlanRiskLimits(maximumTrades: 0).MaximumTrades);
        Assert.Equal(0m, new TradingPlanRiskLimits(riskPerTrade: 0).RiskPerTrade);
        Assert.Equal(0.123456789m, new TradingPlanRiskLimits(dailyLossLimit: 0.123456789m).DailyLossLimit);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingPlanRiskLimits(riskPerTrade: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingPlanRiskLimits(dailyLossLimit: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingPlanRiskLimits(maximumTrades: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingPlanRiskLimits(stopAfterConsecutiveLosses: 0));
    }

    [Fact]
    public void CopyForwardRetainsStructureButClearsDatedDataAndEveryConfirmation()
    {
        var source = Opportunity();
        var copy = source.CopyForward();
        Assert.Equal(source.Focus, copy.Focus);
        Assert.Equal(source.Session, copy.Session);
        Assert.Equal(source.Scenarios.ToArray(), copy.Scenarios.ToArray());
        Assert.Equal(source.Rules.ToArray(), copy.Rules.ToArray());
        Assert.Equal(source.RiskLimits, copy.RiskLimits);
        Assert.Equal(source.Instruments[0].InstrumentId, copy.Instruments[0].InstrumentId);
        Assert.Null(copy.Instruments[0].Bias);
        Assert.Equal("", copy.Instruments[0].MarketThesis);
        Assert.Equal(new TradingPlanLevels(), copy.Instruments[0].Levels);
        Assert.Equal("", copy.NewsAndRestrictions);
        Assert.Equal("", copy.StandAside);
        Assert.Equal(TradingPlanChecklist.None, copy.Confirmed);
        Assert.Equal(TradingPlanChecklist.All, source.Confirmed);
        Assert.Empty(TradingPlanReadiness.Validate(source));
        Assert.NotEmpty(TradingPlanReadiness.Validate(copy));
        Assert.NotEmpty(TradingPlanReadiness.Validate(StandAside().CopyForward()));
    }

    private static TradingPlanContent StandAside() => new(TradingPlanSession.NewYork, "Preserve attention",
        TradingPlanIntent.StandAside, [new(Instrument)], standAside: "Missing confirmation; no entries today.",
        confirmed: TradingPlanChecklist.All);

    private static TradingPlanContent Opportunity() => new(TradingPlanSession.NewYork, "Wait for confirmation",
        instruments: [new(Instrument, TradingPlanBias.Conditional, "Range near prior high", new(PreviousDayHigh: 20000.25m))],
        scenarios: [new(Guid.NewGuid(), Instrument, "Reclaim", direction: TradeDirection.Long,
            condition: "Price reclaims the level", response: "Wait for a retest", invalidation: "Acceptance below the level")],
        newsAndRestrictions: "Manual news review at 09:00", standAside: "Skip if news conflicts",
        riskLimits: new(50, 150, 3, 2), rules: [new(Guid.NewGuid(), 2)], confirmed: TradingPlanChecklist.All);
}
