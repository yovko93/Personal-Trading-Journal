using System.Text.Json;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.DailyReview;

public sealed class PeriodReviewComparisonTests
{
    private static readonly Guid Account = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Instrument = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly PeriodReviewQuery Current = new(ReviewPeriodKind.Week, new(2026, 10, 8));
    private static readonly PeriodReviewQuery Previous = PeriodComparisonEvidence.PreviousQuery(Current);

    [Theory]
    [InlineData(ReviewPeriodKind.Week, 2026, 3, 8, 167)]
    [InlineData(ReviewPeriodKind.Week, 2026, 11, 1, 169)]
    [InlineData(ReviewPeriodKind.Week, 2027, 1, 1, 168)]
    [InlineData(ReviewPeriodKind.Month, 2027, 1, 1, 744)]
    [InlineData(ReviewPeriodKind.Month, 2024, 3, 1, 743)]
    public void PreviousPeriodIsAdjacentWholeNyPeriodAcrossDstLeapAndYear(ReviewPeriodKind kind, int year, int month, int day, int hours)
    {
        var current = new PeriodReviewQuery(kind, new(year, month, day), Account);
        var previous = PeriodComparisonEvidence.PreviousQuery(current);
        Assert.Equal(current.FromUtc, previous.BeforeUtc); Assert.Equal(current.FirstDate.AddDays(-1), previous.LastDate);
        Assert.Equal(hours, (current.BeforeUtc - current.FromUtc).TotalHours);
        Assert.Equal(kind, previous.Kind); Assert.Equal(Account, previous.Scope.AccountId);
        var r = Calculate(new(Evidence(previous), Evidence(current), PeriodSourceConsistency.SingleReadTransaction));
        Assert.Equal(0, r.ClosedTrades.Absolute); Assert.Null(r.ClosedTrades.Percent); Assert.Empty(r.Groups);
    }

    [Fact]
    public void ExactAmountsCountsCoverageAndSourceIdsReusePeriodMetrics()
    {
        var old = Trade(Previous, 40); var first = Trade(Current, 70); var second = Trade(Current, -10);
        var pair = Pair([old], [first, second]); var before = JsonSerializer.Serialize(pair);
        var r = Calculate(pair); var usd = r.Groups.Single(g => g.AccountId == Account);
        Assert.Equal(20, usd.Gross.Absolute); Assert.Equal(50, usd.Gross.Percent);
        Assert.Equal(20, usd.Net.Absolute); Assert.Equal(1, r.ClosedTrades.Absolute); Assert.Equal(100, r.ClosedTrades.Percent);
        Assert.Equal(0, r.ActiveDays.Absolute); Assert.Equal(0, usd.MissingCostTrades.Absolute);
        Assert.Equal(new[] { first.TradeId, second.TradeId }.Order(), usd.Current!.Population.Closed.TradeIds);
        Assert.Same(pair.Current, r.Current.Evidence); Assert.Same(pair.Previous, r.PreviousPatterns.Evidence);
        Assert.Equal(before, JsonSerializer.Serialize(pair));
    }

    [Theory]
    [InlineData(0, PeriodPercentStatus.ZeroBaseline)]
    [InlineData(-10, PeriodPercentStatus.NegativeBaseline)]
    public void NonpositiveBaselineDoesNotProduceMisleadingPercentage(int old, PeriodPercentStatus status)
    {
        var g = Calculate(Pair([Trade(Previous, old)], [Trade(Current, 10)])).Groups[0];
        Assert.Equal(10 - old, g.Gross.Absolute); Assert.Null(g.Gross.Percent); Assert.Equal(status, g.Gross.PercentStatus);
    }

    [Fact]
    public void MixedCurrenciesAndChangedAccountCoverageWithholdPercentAndPartialNet()
    {
        var other = Guid.NewGuid();
        var pair = Pair([Trade(Previous, 10)], [Trade(Current, 20), Trade(Current, 30, other, missing: true), Trade(Current, 4, currency: "EUR")]);
        var r = Calculate(pair); var usd = r.Groups.Single(g => g.Currency == "USD" && g.AccountId is null);
        Assert.Equal(40, usd.Gross.Absolute); Assert.Null(usd.Gross.Percent);
        Assert.Equal(PeriodPercentStatus.AccountOrCurrencyPopulationChanged, usd.Gross.PercentStatus);
        Assert.Null(usd.Net.Absolute); Assert.Equal(PeriodChangeStatus.IncompleteCoverage, usd.Net.Status);
        Assert.Equal(1, usd.MissingCommissionTrades.Absolute); Assert.Equal(1, usd.MissingCostTrades.Absolute);
        Assert.Null(r.ClosedTrades.Percent);
        Assert.Equal(10, r.Groups.Single(g => g.Currency == "USD" && g.AccountId == Account).Net.Absolute);
        Assert.Equal(PeriodChangeStatus.MissingPopulation, r.Groups.Single(g => g.Currency == "EUR" && g.AccountId is null).Gross.Status);
        Assert.Equal(new[] { "EUR", "USD" }, r.Groups.Where(g => g.AccountId is null).Select(g => g.Currency));
    }

    [Fact]
    public void EmptyPeriodHasNoInventedMoneyAndOpenContextDoesNotBecomeRealized()
    {
        var open = Trade(Current, 10) with { Inclusion = DailyReviewTradeInclusion.OpenActivityOnDate };
        open = open with { Facts = open.Facts! with { Status = TradeStatus.Open, ClosedAtUtc = null, OpenQuantity = 1 } };
        var pair = Pair([Trade(Previous, 10)], []) with { Current = Evidence(Current) with { ActivityContext = [open] } };
        var r = Calculate(pair); Assert.Equal(-1, r.ClosedTrades.Absolute);
        Assert.Equal(1, r.Groups[0].OpenActivity.Absolute); Assert.Null(r.Groups[0].Gross.Absolute); Assert.Null(r.Groups[0].Net.Absolute);
        Assert.Empty(r.Current.ActiveDays); Assert.Equal(open.TradeId, Assert.Single(r.Current.Population.ExcludedOpenActivity.TradeIds));
    }

    [Fact]
    public void IndependentReadsExplicitlyWithholdAllDeltasAndWrongPeriodsReject()
    {
        var pair = Pair([Trade(Previous, 10)], [Trade(Current, 20)]) with { Consistency = PeriodSourceConsistency.IndependentReads };
        var r = Calculate(pair); Assert.Null(r.ClosedTrades.Absolute);
        Assert.All(r.Groups, g => { Assert.Null(g.Gross.Absolute); Assert.Equal(PeriodChangeStatus.IndependentSnapshots, g.Net.Status); });
        Assert.Throws<ArgumentException>(() => Calculate(pair with { Previous = pair.Current }));
        Assert.Throws<ArgumentException>(() => Calculate(pair with { Previous = pair.Previous with { Query = new(ReviewPeriodKind.Week, Previous.FirstDate, Account) } }));
    }

    [Fact]
    public void MultipleMistakesOverlapOnlyBreakdownsNotTotalsAndUnavailableIdentitySurvives()
    {
        var trade = Trade(Current, 30);
        var missing = new DailyReviewReference(Guid.NewGuid(), null, null);
        trade = trade with { AssignedMistakes = [Assignment(missing), Assignment(new(Guid.NewGuid(), "Recorded observation", false))] };
        var r = Calculate(Pair([], [trade])); Assert.Equal(1, r.Current.Population.Closed.Count);
        Assert.Equal(30, r.Current.Currencies[0].Statistics.Gross.Outcomes.Metrics.Total);
        var buckets = r.Breakdowns.Where(g => g.Key.Kind == PeriodBreakdownKind.RecordedMistake).ToArray();
        Assert.Equal(2, buckets.Length); Assert.All(buckets, b => Assert.Equal(trade.TradeId, Assert.Single(b.CurrentTrades.TradeIds)));
        Assert.Null(r.CurrentPatterns.Mistakes.Single(m => m.Mistake.Id == missing.Id).Mistake.Name);
        Assert.Equal(PeriodBreakdownContext.UnknownSession, Assert.Single(r.Breakdowns, g => g.Key.Kind == PeriodBreakdownKind.Session).Key.Context);
    }

    [Fact]
    public void FrozenSetupRevisionsNeverMergeWithLegacyOrRemovedContext()
    {
        var setup = new DailyReviewReference(Guid.NewGuid(), null, null);
        var old = Trade(Previous, 10) with { Setup = setup, Playbook = Captured(setup.Id, 1) };
        var next = Trade(Current, 20) with { Setup = setup, Playbook = Captured(setup.Id, 2) };
        var legacy = Trade(Current, 1) with { Setup = setup };
        var removed = Trade(Current, 2) with { Playbook = old.Playbook! with
            { CurrentState = ReviewPlaybookState.NoAssociation, Associations = [old.Playbook!.Associations[0] with { State = ReviewPlaybookAssociationState.Removed }] } };
        var r = Calculate(Pair([old], [next, legacy, removed]));
        var groups = r.Breakdowns.Where(b => b.Key.Kind == PeriodBreakdownKind.Setup).ToArray();
        Assert.Equal(4, groups.Length);
        Assert.Equal(old.TradeId, Assert.Single(groups.Single(g => g.Key.Revision == 1).PreviousTrades.TradeIds));
        Assert.Equal(next.TradeId, Assert.Single(groups.Single(g => g.Key.Revision == 2).CurrentTrades.TradeIds));
        Assert.Equal(legacy.TradeId, Assert.Single(groups.Single(g => g.Key.Context == PeriodBreakdownContext.LegacyClassification).CurrentTrades.TradeIds));
        Assert.Equal(removed.TradeId, Assert.Single(groups.Single(g => g.Key.Context == PeriodBreakdownContext.NoAssociation).CurrentTrades.TradeIds));
    }

    [Fact]
    public void RuleComparisonsKeepExactRevisionsOutcomesAndDenominatorsWithoutImprovementScore()
    {
        var first = TradingRuleRevision.Create(Guid.NewGuid(), new("Manual principle"), Previous.FromUtc);
        var latest = first.Revise(1, new("New text"), Current.FromUtc);
        var p = Trade(Previous, 10); var c = Trade(Current, 20);
        var pair = Pair([p], [c]);
        pair = pair with
        {
            Previous = pair.Previous with { ApplicableRules = [latest], Assessments = [Assessment(first, p, TradingRuleAssessmentOutcome.ConfirmedViolation)] },
            Current = pair.Current with { ApplicableRules = [latest], Assessments = [Assessment(latest, c, TradingRuleAssessmentOutcome.CannotEvaluate)] },
        };
        var r = Calculate(pair);
        var historical = r.Rules.Single(g => g.Previous?.Rule.Revision == 1);
        Assert.Null(historical.Current); Assert.Equal(PeriodRuleComparisonStatus.MissingCohort, historical.Status); Assert.Null(historical.CompliantChange);
        var trades = r.Rules.Single(g => g.Current?.Rule.Revision == 2 && g.Current.TargetKind == TradingRuleTargetKind.Trade);
        Assert.Equal(1, trades.CannotEvaluateChange); Assert.Equal(-1, trades.NotAssessedChange); Assert.Equal(0, trades.CompliantChange);
        Assert.Equal(1, trades.Current!.LatestManual);
        var absent = Calculate(pair with { Previous = pair.Previous with { ApplicableRules = [] } });
        Assert.All(absent.Rules.Where(g => g.Previous is null || g.Current is null), g => Assert.Null(g.AssessedChange));
    }

    [Fact]
    public void DifferentMonthDayDenominatorsAreShownNotScored()
    {
        var current = new PeriodReviewQuery(ReviewPeriodKind.Month, new(2026, 3, 3));
        var previous = PeriodComparisonEvidence.PreviousQuery(current);
        var rule = TradingRuleRevision.Create(Guid.NewGuid(), new("Manual"), previous.FromUtc);
        var r = Calculate(new(Evidence(previous) with { ApplicableRules = [rule] }, Evidence(current) with { ApplicableRules = [rule] }, PeriodSourceConsistency.SingleReadTransaction));
        var days = Assert.Single(r.Rules); Assert.Equal(28, days.Previous!.TargetCount); Assert.Equal(31, days.Current!.TargetCount);
        Assert.Equal(PeriodRuleComparisonStatus.DifferentTargetCount, days.Status); Assert.Null(days.NotAssessedChange);
    }

    [Fact]
    public void ChangedCurrencyWithinSameAccountCannotSupplyPercentageOrFxDelta()
    {
        var r = Calculate(Pair([Trade(Previous, 10)], [Trade(Current, 30, currency: "EUR")]));
        Assert.Equal(0, r.ClosedTrades.Absolute); Assert.Null(r.ClosedTrades.Percent);
        Assert.Equal(PeriodPercentStatus.AccountOrCurrencyPopulationChanged, r.ClosedTrades.PercentStatus);
        Assert.All(r.Groups, g => { Assert.Null(g.Gross.Absolute); Assert.Null(g.Net.Absolute); });
    }

    [Fact]
    public void UnavailableInstrumentAndGrossRemainExplicitInBreakdownCoverage()
    {
        var trade = Trade(Current, 0);
        trade = trade with { Instrument = new(Instrument, null, null), Facts = trade.Facts! with { GrossPnL = null, NetPnL = null },
            Quality = DailyReviewTradeQuality.UnknownGrossPnL | DailyReviewTradeQuality.UnavailableInstrument };
        var r = Calculate(Pair([Trade(Previous, 1)], [trade]));
        var instrument = Assert.Single(r.Breakdowns, b => b.Key.Kind == PeriodBreakdownKind.Instrument);
        Assert.Equal(Instrument, instrument.Key.SourceId); Assert.Single(instrument.CurrentTrades.TradeIds);
        Assert.Null(instrument.Groups[0].Gross.Absolute); Assert.Null(instrument.Groups[0].Net.Absolute);
        Assert.Equal(1, r.Current.Population.Closed.Count); Assert.Null(r.Current.Evidence.ClosedTrades[0].Instrument.Name);
    }

    [Fact]
    public void OrderingLimitsCancellationAndOverflowAreExplicitWithoutPartialOutput()
    {
        var pair = Pair([Trade(Previous, 10)], [Trade(Current, 20), Trade(Current, -1)]);
        var r = Calculate(pair); var reversed = Calculate(pair with { Current = pair.Current with { ClosedTrades = pair.Current.ClosedTrades.Reverse().ToArray() } });
        Assert.Equal(JsonSerializer.Serialize(r.Groups), JsonSerializer.Serialize(reversed.Groups));
        Assert.Equal(JsonSerializer.Serialize(r.Breakdowns), JsonSerializer.Serialize(reversed.Breakdowns));
        foreach (var limits in new[] { new PeriodComparisonLimits(maxBreakdownGroups: 1), new PeriodComparisonLimits(maxMemberships: 1) })
        {
            var result = PeriodReviewComparisonCalculator.Calculate(pair, limits: limits);
            Assert.Equal(PeriodEvidenceStatus.LimitExceeded, result.Status); Assert.Null(result.Comparison);
        }
        Assert.Throws<OperationCanceledException>(() => PeriodReviewComparisonCalculator.Calculate(pair, new(true)));
        Assert.Throws<OverflowException>(() => Calculate(Pair([Trade(Previous, -1)], [Trade(Current, decimal.MaxValue)])));
    }

    private static PeriodReviewComparison Calculate(PeriodComparisonEvidence pair)
    {
        var result = PeriodReviewComparisonCalculator.Calculate(pair); Assert.Equal(PeriodEvidenceStatus.Ready, result.Status);
        return Assert.IsType<PeriodReviewComparison>(result.Comparison);
    }
    private static PeriodReviewEvidence Evidence(PeriodReviewQuery q, params DailyReviewTradeEvidence[] trades) => new(q, trades, [], [], [], [], [], []);
    private static PeriodComparisonEvidence Pair(DailyReviewTradeEvidence[] p, DailyReviewTradeEvidence[] c) =>
        new(Evidence(Previous, p), Evidence(Current, c), PeriodSourceConsistency.SingleReadTransaction);
    private static DailyReviewTradeEvidence Trade(PeriodReviewQuery q, decimal gross, Guid? account = null, string currency = "USD", bool missing = false)
    {
        var close = q.FromUtc.AddHours(12);
        return new(Guid.NewGuid(), new(account ?? Account, "Synthetic", true), new(Instrument, "NQ", true), null, currency, 1, close, close,
            DailyReviewTradeInclusion.ClosedOnDate, new(1, TradeStatus.Closed, TradeDirection.Long, close.AddHours(-1), close, 0, 100, 100, missing ? null : 0, gross, missing ? null : gross),
            [new TradeExecutionDetailItem(Guid.NewGuid(), 1, close, ExecutionSide.Sell, 1, 100, missing ? null : 0, 0, missing ? null : 0, null, null, null)],
            [], missing ? DailyReviewTradeQuality.UnknownCommission : DailyReviewTradeQuality.None);
    }
    private static DailyReviewAssignedMistake Assignment(DailyReviewReference r) => new(Guid.NewGuid(), r, Current.FromUtc, Current.FromUtc);
    private static DailyReviewPlaybookEvidence Captured(Guid setup, int revision) => new(ReviewPlaybookState.Captured,
        [new(1, setup, Current.FromUtc, Previous.FromUtc, ReviewPlaybookAssociationState.Current,
            new(setup, "Frozen setup", null, revision, Previous.FromUtc, "Context", "Entry", "Confirm", "Invalid", "Execution", []))]);
    private static TradingRuleAssessmentDetails Assessment(TradingRuleRevision rule, DailyReviewTradeEvidence trade, TradingRuleAssessmentOutcome outcome) =>
        new(TradingRuleAssessment.Record(Guid.NewGuid(), rule, TradingRuleAssessmentTarget.ForTrade(trade.TradeId, trade.Account.Id), outcome,
            TradingRuleAssessmentSource.Manual, outcome == TradingRuleAssessmentOutcome.ConfirmedViolation ? TradingRuleAssessmentBasis.UserEnteredViolation : TradingRuleAssessmentBasis.RecordedEvidence,
            TradingRuleEvidenceSufficiency.Missing, "Synthetic", [], null, Current.BeforeUtc), 1, "Synthetic", TradingRuleSourceState.Available, []);
}
