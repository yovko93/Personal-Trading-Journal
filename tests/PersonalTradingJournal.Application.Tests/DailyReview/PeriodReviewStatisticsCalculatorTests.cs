using System.Text.Json;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.DailyReview;

public sealed class PeriodReviewStatisticsCalculatorTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);
    private static readonly PeriodReviewQuery Query = new(ReviewPeriodKind.Week, Day);
    private static readonly Guid Account = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Fact]
    public void CountsAveragesAndExtremaHaveExactBasisDenominatorsAndTiedProvenance()
    {
        var rows = new[] { Trade(100), Trade(-40), Trade(0, date: Day.AddDays(1)), Trade(100, date: Day.AddDays(1)) };
        var result = Calculate(rows); var group = Assert.Single(result.Currencies).Statistics;
        Assert.Equal(4, result.Population.Closed.Count); Assert.Equal(2, result.ActiveDays.Count);
        Assert.Equal(new[] { Day, Day.AddDays(1) }, group.ActiveDays.Select(d => d.Date));
        Assert.Equal(rows.Select(t => t.TradeId).Order(), group.ActiveDays.SelectMany(d => d.ClosedTrades.TradeIds).Order());
        var gross = group.Gross;
        Assert.Equal(160, gross.Outcomes.Metrics.Total); Assert.Equal(50, gross.Outcomes.Metrics.WinRatePercent);
        Assert.Equal(5, gross.Outcomes.Metrics.ProfitFactor.Value);
        Assert.Equal(2, gross.Outcomes.Wins.Count); Assert.Single(gross.Outcomes.Losses.TradeIds); Assert.Single(gross.Outcomes.BreakEvens.TradeIds);
        Assert.Equal(40, gross.AveragePerClosedTrade.Value); Assert.Equal(80, gross.AveragePerActiveDay.Value);
        Assert.Equal(100, gross.Outcomes.Metrics.AverageWin.Value); Assert.Equal(40, gross.Outcomes.Metrics.AverageLoss.Value);
        Assert.Equal(100, gross.BestTrade!.Value); Assert.Equal(new[] { rows[0].TradeId, rows[3].TradeId }.Order(), gross.BestTrade.Trades.TradeIds);
        Assert.Equal(-40, gross.WorstTrade!.Value); Assert.Equal(rows[1].TradeId, Assert.Single(gross.WorstTrade.Trades.TradeIds));
        Assert.Equal(PnlBasis.Net, group.Net.Outcomes.Metrics.Basis);
        Assert.Equal(160, group.Net.Outcomes.Metrics.Total); Assert.Empty(group.Costs.MissingCostTrades.TradeIds);
    }

    [Fact]
    public void PartialNetNeverSuppliesPeriodMeansExtremaOrRatiosEvenWithNumericProjection()
    {
        var known = Trade(100, costs: 5);
        var commission = Trade(-40, missingCommission: true);
        var fee = Trade(10, missingFees: true, date: Day.AddDays(1));
        var both = Trade(0, missingCommission: true, missingFees: true);
        // Actual execution costs govern eligibility, even if a projection erroneously contains a number.
        commission = commission with { Quality = DailyReviewTradeQuality.None,
            Facts = commission.Facts! with { TotalCosts = 0, NetPnL = -40 } };
        var group = Calculate(known, commission, fee, both).Currencies[0].Statistics;
        Assert.Equal(2, group.ActiveDays.Count);
        Assert.Equal(2, group.Costs.MissingCommissionTrades.Count); Assert.Equal(2, group.Costs.MissingFeeTrades.Count);
        Assert.Equal(3, group.Costs.MissingCostTrades.Count);
        Assert.Equal(70, group.Gross.Outcomes.Metrics.Total);
        Assert.Equal(95, group.Net.Outcomes.Metrics.KnownSubtotal);
        Assert.Equal(MetricCoverageStatus.Partial, group.Net.Outcomes.Metrics.Coverage.Status);
        Assert.Equal(1, group.Net.Outcomes.Known.Count); Assert.Equal(3, group.Net.Outcomes.Unavailable.Count);
        Assert.Null(group.Net.Outcomes.Metrics.Total); Assert.Null(group.Net.Outcomes.Metrics.WinRatePercent);
        Assert.Null(group.Net.Outcomes.Metrics.ProfitFactor.Value); Assert.Null(group.Net.Outcomes.Metrics.AverageWin.Value);
        Assert.Null(group.Net.Outcomes.Metrics.AverageLoss.Value);
        Assert.Equal(AveragePnlStatus.IncompleteCoverage, group.Net.AveragePerClosedTrade.Status);
        Assert.Null(group.Net.AveragePerClosedTrade.Value); Assert.Null(group.Net.AveragePerActiveDay.Value);
        Assert.Null(group.Net.BestTrade); Assert.Null(group.Net.WorstTrade); Assert.False(group.Net.Outcomes.Metrics.IsEstimated);
    }

    [Fact]
    public void CurrencyAggregatesAndExactAccountGroupsNeverMergeCurrenciesOrSumDistinctDayCounts()
    {
        var rows = new[] { Trade(100), Trade(-40, account: Other), Trade(10, currency: "EUR"), Trade(8, currency: "EUR", account: Other) };
        var result = Calculate(rows);
        Assert.Equal(new[] { "EUR", "USD" }, result.Currencies.Select(c => c.Currency));
        Assert.Single(result.ActiveDays); Assert.Equal(4, result.Population.Closed.Count);
        var usd = result.Currencies[1]; Assert.Equal(60, usd.Statistics.Gross.Outcomes.Metrics.Total);
        Assert.Equal(new[] { Account, Other }, usd.Accounts.Select(a => a.Account.Id));
        Assert.Equal(100, usd.Accounts[0].Statistics.Gross.Outcomes.Metrics.Total);
        Assert.Equal(-40, usd.Accounts[1].Statistics.Gross.Outcomes.Metrics.Total);
        Assert.Equal(18, result.Currencies[0].Statistics.Net.Outcomes.Metrics.Total);
        Assert.Single(usd.Statistics.ActiveDays); // NOT one day per Account.
        var reversed = Calculate(rows.Reverse().ToArray());
        Assert.Equal(JsonSerializer.Serialize(result.Currencies), JsonSerializer.Serialize(reversed.Currencies));
        Assert.Equal(JsonSerializer.Serialize(result.Population), JsonSerializer.Serialize(reversed.Population));
        var exact = PeriodReviewStatisticsCalculator.Calculate(Evidence(rows.Where(t => t.Account.Id == Account).ToArray()) with
            { Query = new(ReviewPeriodKind.Week, Day, Account) });
        Assert.All(exact.Currencies, c => Assert.Equal(Account, Assert.Single(c.Accounts).Account.Id));
        Assert.Equal(2, exact.Population.Closed.Count);
    }

    [Fact]
    public void MissingCostsInOneAccountDoNotInvalidateAnotherAccountButBlockCurrencyTotal()
    {
        var group = Calculate(Trade(10), Trade(20, account: Other, missingFees: true)).Currencies[0];
        Assert.Equal(10, group.Accounts[0].Statistics.Net.Outcomes.Metrics.Total);
        Assert.Equal(MetricCoverageStatus.Unavailable, group.Accounts[1].Statistics.Net.Outcomes.Metrics.Coverage.Status);
        Assert.Null(group.Statistics.Net.Outcomes.Metrics.Total); Assert.Null(group.Statistics.Net.BestTrade);
        Assert.Null(group.Statistics.Net.AveragePerActiveDay.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(5)] [InlineData(-5)]
    public void ZeroAndOneSidedResultsHaveValidExtremaAndExplicitUndefinedRatios(int amount)
    {
        var group = Calculate(Trade(amount)).Currencies[0].Statistics;
        Assert.Equal(amount, group.Gross.BestTrade!.Value); Assert.Equal(amount, group.Gross.WorstTrade!.Value);
        Assert.Equal(amount, group.Net.AveragePerClosedTrade.Value); Assert.Equal(amount, group.Net.AveragePerActiveDay.Value);
        Assert.Equal(amount == 0 ? 1 : 0, group.Net.Outcomes.BreakEvens.Count);
        Assert.Equal(amount == 0 ? ProfitFactorStatus.AllBreakEven : amount > 0 ? ProfitFactorStatus.NoLosses : ProfitFactorStatus.Defined,
            group.Net.Outcomes.Metrics.ProfitFactor.Status);
    }

    [Fact]
    public void EmptyAndOpenOnlyPeriodsHaveNoInventedRealizedOutcomeOrActiveClosureDate()
    {
        var empty = Calculate(); Assert.Empty(empty.Currencies); Assert.Empty(empty.ActiveDays);
        var open = Trade(3, missingFees: true); open = open with { Inclusion = DailyReviewTradeInclusion.OpenActivityOnDate,
            Facts = open.Facts! with { Status = TradeStatus.Open, OpenQuantity = 1, ClosedAtUtc = null, GrossPnL = null, NetPnL = null } };
        var unknown = Trade(2) with { Inclusion = DailyReviewTradeInclusion.UnavailableLifecycleActivityOnDate,
            Facts = null, Quality = DailyReviewTradeQuality.MissingProjection };
        var result = PeriodReviewStatisticsCalculator.Calculate(Evidence([]) with { ActivityContext = [open, unknown] });
        Assert.Empty(result.ActiveDays); Assert.Equal(0, result.Population.Closed.Count);
        Assert.Equal(open.TradeId, Assert.Single(result.Population.ExcludedOpenActivity.TradeIds));
        Assert.Equal(unknown.TradeId, Assert.Single(result.Population.ExcludedUnavailableLifecycle.TradeIds));
        var group = result.Currencies[0].Statistics;
        Assert.Empty(group.Costs.MissingCostTrades.TradeIds); Assert.Equal(1, group.Population.UnknownFees.Count);
        Assert.Equal(MetricCoverageStatus.Empty, group.Net.Outcomes.Metrics.Coverage.Status);
        Assert.Equal(AveragePnlStatus.NoTrades, group.Net.AveragePerActiveDay.Status);
        Assert.Null(group.Gross.Outcomes.Metrics.Total); Assert.Null(group.Gross.BestTrade);
    }

    [Theory]
    [InlineData(DailyReviewTradeQuality.UnknownGrossPnL)]
    [InlineData(DailyReviewTradeQuality.UnsupportedProjectionVersion)]
    [InlineData(DailyReviewTradeQuality.MissingProjection)]
    public void UnavailableGrossKeepsDenominatorAndSourceQualityButSuppressesMeansAndExtrema(DailyReviewTradeQuality flag)
    {
        var unknown = Trade(20) with { Quality = flag, Account = new(Account, null, null) };
        var result = Calculate(unknown); var group = result.Currencies[0].Statistics;
        Assert.Single(result.ActiveDays); Assert.Equal(1, result.Population.Closed.Count);
        Assert.Null(group.Gross.AveragePerClosedTrade.Value); Assert.Null(group.Net.WorstTrade);
        Assert.Null(group.Gross.Outcomes.Metrics.Total); Assert.Null(result.Currencies[0].Accounts[0].Account.Name);
        Assert.Equal(flag, Assert.Single(result.Population.SourceQuality).Flag);
        Assert.Equal(unknown.TradeId, Assert.Single(group.Gross.Outcomes.Unavailable.TradeIds));
    }

    [Fact]
    public void EvidenceIsCarriedUnchangedAndJournalOrRuleTextDoesNotBecomeAMetric()
    {
        var query = new DailyReviewQuery(Day);
        var journal = new DailyReviewJournalEvidence(Guid.NewGuid(), Day, null, null, DailyJournalAccountState.AllAccounts,
            "A user opinion, not a result", new("", "", ""), true, 1, query.FromUtc, query.FromUtc);
        var rule = TradingRuleRevision.Create(Guid.NewGuid(), new("A general principle"), query.FromUtc);
        var trade = Trade(4) with { Playbook = new(ReviewPlaybookState.LegacyClassification, []) };
        var evidence = Evidence([trade]) with { Journals = [journal], ApplicableRules = [rule] };
        string original = JsonSerializer.Serialize(evidence);
        var result = PeriodReviewStatisticsCalculator.Calculate(evidence);
        Assert.Same(evidence, result.Evidence); Assert.Same(trade.Playbook, result.Evidence.ClosedTrades[0].Playbook);
        Assert.Same(journal, result.Evidence.Journals[0]); Assert.Same(rule, result.Evidence.ApplicableRules[0]);
        Assert.Empty(result.Evidence.Assessments); Assert.Equal(original, JsonSerializer.Serialize(evidence));
        var journalOnly = PeriodReviewStatisticsCalculator.Calculate(evidence with { ClosedTrades = [] });
        Assert.Empty(journalOnly.Currencies); Assert.Empty(journalOnly.ActiveDays);
    }

    [Theory]
    [InlineData(ReviewPeriodKind.Week, 2026, 3, 8)]
    [InlineData(ReviewPeriodKind.Week, 2026, 11, 1)]
    [InlineData(ReviewPeriodKind.Week, 2027, 1, 1)]
    [InlineData(ReviewPeriodKind.Month, 2026, 12, 31)]
    public void BoundariesAndNyActiveDaysReuseClosureAttribution(ReviewPeriodKind kind, int year, int month, int day)
    {
        var q = new PeriodReviewQuery(kind, new(year, month, day));
        var first = Trade(1); first = first with { Facts = first.Facts! with { OpenedAtUtc = q.FromUtc.AddDays(-1), ClosedAtUtc = q.FromUtc } };
        var last = Trade(2); last = last with { Facts = last.Facts! with { ClosedAtUtc = q.BeforeUtc.AddTicks(-1) } };
        var evidence = Evidence([first, last]) with { Query = q };
        var result = PeriodReviewStatisticsCalculator.Calculate(evidence);
        Assert.Equal(new[] { q.FirstDate, q.LastDate }, result.ActiveDays.Select(d => d.Date));
        Assert.Equal(3, result.Currencies[0].Statistics.Gross.Outcomes.Metrics.Total);
        Assert.Throws<ArgumentException>(() => PeriodReviewStatisticsCalculator.Calculate(evidence with
            { ClosedTrades = [first with { Facts = first.Facts! with { ClosedAtUtc = q.FromUtc.AddTicks(-1) } }] }));
        Assert.Throws<ArgumentException>(() => PeriodReviewStatisticsCalculator.Calculate(evidence with
            { ClosedTrades = [last with { Facts = last.Facts! with { ClosedAtUtc = q.BeforeUtc } }] }));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SingleActiveDayMatchesEveryExistingDailyMetric(bool incomplete)
    {
        var rows = new[] { Trade(10, costs: 15), Trade(-20), Trade(0, missingFees: incomplete) };
        var daily = DailyReviewStatisticsCalculator.Calculate(new(new(Day), rows, []));
        var period = Calculate(rows);
        Assert.Single(period.ActiveDays);
        Assert.Equal(JsonSerializer.Serialize(daily.Population), JsonSerializer.Serialize(period.Population));
        Assert.Equal(JsonSerializer.Serialize(daily.Currencies[0].Gross), JsonSerializer.Serialize(period.Currencies[0].Statistics.Gross.Outcomes));
        Assert.Equal(JsonSerializer.Serialize(daily.Currencies[0].Net), JsonSerializer.Serialize(period.Currencies[0].Statistics.Net.Outcomes));
        Assert.Equal(2, period.Currencies[0].Statistics.Net.Outcomes.Losses.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OverflowReturnsNoPartialStatistics(bool negative)
    {
        var rows = new[] { Trade(negative ? decimal.MinValue : decimal.MaxValue), Trade(negative ? -1m : 1m) };
        Assert.Throws<OverflowException>(() => Calculate(rows));
    }

    [Fact]
    public void DecimalPrecisionIsNotRoundedForPresentation()
    {
        const decimal amount = 0.1234567890123456789012345678m;
        var group = Calculate(Trade(amount), Trade(-1m)).Currencies[0].Statistics;
        Assert.Equal(amount, group.Net.BestTrade!.Value);
        Assert.Equal((amount - 1m) / 2m, group.Net.AveragePerClosedTrade.Value);
        Assert.Equal(amount, group.Net.Outcomes.Metrics.AverageWin.Value);
    }

    [Fact]
    public void DuplicateWrongScopeInvalidBucketsOversizeAndCancellationAreRejected()
    {
        var trade = Trade(1); Assert.Throws<ArgumentException>(() => Calculate(trade, trade));
        Assert.Throws<ArgumentException>(() => PeriodReviewStatisticsCalculator.Calculate(Evidence([trade]) with { Query = new(ReviewPeriodKind.Week, Day, Other) }));
        Assert.Throws<ArgumentException>(() => PeriodReviewStatisticsCalculator.Calculate(Evidence([]) with { ActivityContext = [trade] }));
        Assert.Throws<ArgumentException>(() => Calculate(trade with { Inclusion = DailyReviewTradeInclusion.OpenActivityOnDate }));
        var inactive = trade with { Inclusion = DailyReviewTradeInclusion.OpenActivityOnDate, Facts = null,
            Executions = [trade.Executions[0] with { ExecutedAtUtc = Query.BeforeUtc }] };
        Assert.Throws<ArgumentException>(() => PeriodReviewStatisticsCalculator.Calculate(Evidence([]) with { ActivityContext = [inactive] }));
        Assert.Throws<ArgumentException>(() => Calculate(Enumerable.Repeat(trade, 10_001).ToArray()));
        Assert.Throws<OperationCanceledException>(() => PeriodReviewStatisticsCalculator.Calculate(Evidence([trade]), new(true)));
    }

    private static PeriodReviewEvidence Evidence(DailyReviewTradeEvidence[] rows) => new(Query, rows, [], [], [], [], [], []);
    private static PeriodReviewStatistics Calculate(params DailyReviewTradeEvidence[] rows) => PeriodReviewStatisticsCalculator.Calculate(Evidence(rows));
    private static DailyReviewTradeEvidence Trade(decimal gross, decimal costs = 0, Guid? account = null, string currency = "USD",
        bool missingCommission = false, bool missingFees = false, DateOnly? date = null)
    {
        var close = new DailyReviewQuery(date ?? Day).FromUtc.AddHours(12);
        bool missing = missingCommission || missingFees;
        var facts = new DailyReviewTradeFacts(1, TradeStatus.Closed, TradeDirection.Long, close.AddHours(-1), close, 0, 100, 100,
            missing ? null : costs, gross, missing ? null : gross - costs);
        var execution = new TradeExecutionDetailItem(Guid.NewGuid(), 1, close, ExecutionSide.Sell, 1, 100,
            missingCommission ? null : costs, missingFees ? null : 0, missing ? null : costs, null, null, null);
        return new(Guid.NewGuid(), new(account ?? Account, "Synthetic", true), new(Guid.NewGuid(), "NQ", true), null, currency, 1,
            close, close, DailyReviewTradeInclusion.ClosedOnDate, facts, [execution], [],
            (missingCommission ? DailyReviewTradeQuality.UnknownCommission : 0) | (missingFees ? DailyReviewTradeQuality.UnknownFees : 0));
    }
}
