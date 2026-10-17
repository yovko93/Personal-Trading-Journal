using System.Text.Json;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Analytics.Breakdown;

public sealed class BreakdownMetricCalculatorTests
{
    [Fact]
    public void MixedNetAndFallbackResolveOnceForAllMetricsIncludingKnownZeroAndOppositeSigns()
    {
        var rows = new[] { Trade(1, 10, -2), Trade(2, 30, 20), Trade(3, 6), Trade(4, 0), Trade(5, 10, 0), Trade(6, -4) };
        var result = Calculate(rows);
        var metrics = result.Summary;
        Assert.Equal(BreakdownCalculationMode.NetPreferred, result.Query.Mode);
        Assert.Equal(new BreakdownFinancialCoverage(BreakdownCalculationMode.NetPreferred, 6, 3, 3, 0, 0), metrics.FinancialSources);
        Assert.Equal(BreakdownFinancialSource.NetGross, metrics.FinancialSources.Source);
        Assert.Equal(20m, metrics.Selected.Total);
        Assert.Equal((2, 2, 2), (metrics.Selected.Wins, metrics.Selected.Losses, metrics.Selected.BreakEvens));
        Assert.Equal(100m * 2 / 6, metrics.Selected.WinRatePercent);
        Assert.Equal(26m / 6, metrics.Selected.ProfitFactor.Value);
        Assert.Equal(20m / 6, metrics.Selected.AverageTrade.Value);
        Assert.Equal(13m, metrics.Selected.AverageWin.Value);
        Assert.Equal(3m, metrics.Selected.AverageLoss.Value);
        Assert.Equal(52m, metrics.Gross.Total);
        Assert.Equal(new MetricCoverage(6, 3), metrics.Data.ActualNet);
        Assert.Equal(new MetricCoverage(6, 6), metrics.Data.Gross);
        Assert.Equal(3, metrics.Data.MissingCostTradeCount);
        Assert.Equal(JsonSerializer.Serialize(metrics), JsonSerializer.Serialize(Assert.Single(result.Groups).Metrics));
        Assert.Null(rows[2].Facts.NetPnL); // Fallback never writes Gross into actual Net or costs.
        Assert.Null(rows[2].Facts.TotalCosts);
        AssertInvariants(metrics);
    }

    [Fact]
    public void AllActualNetValuesMatchSharedStrictNetWhenTheirPopulationAndPolicyMatch()
    {
        var rows = new[] { Trade(1, 12, 10), Trade(2, -3, -5), Trade(3, 5, 0) };
        var result = Calculate(rows).Summary;
        var shared = Assert.Single(DashboardMetricCalculator.Calculate(rows.Select(r => r.Facts)).Currencies).Metrics.Net;
        Assert.Equal(BreakdownFinancialSource.Net, result.FinancialSources.Source);
        Assert.Equal(3, result.FinancialSources.NetUsedCount);
        Assert.Equal(0, result.FinancialSources.GrossFallbackCount);
        Assert.Equal(shared.Total, result.Selected.Total);
        Assert.Equal(shared.WinRatePercent, result.Selected.WinRatePercent);
        Assert.Equal(shared.ProfitFactor, result.Selected.ProfitFactor);
        Assert.Equal(shared.AverageWin, result.Selected.AverageWin);
        Assert.Equal(shared.AverageLoss, result.Selected.AverageLoss);
        AssertInvariants(result);
    }

    [Fact]
    public void AllUnknownNetUsesGrossIncludingPositiveNegativeAndZeroWithoutClaimingNet()
    {
        var rows = new[] { Trade(1, 10), Trade(2, -5), Trade(3, 0) };
        var result = Calculate(rows).Summary;
        Assert.Equal(BreakdownFinancialSource.GrossFallback, result.FinancialSources.Source);
        Assert.Equal(3, result.FinancialSources.GrossFallbackCount);
        Assert.Equal(0, result.FinancialSources.NetUsedCount);
        Assert.Equal(5m, result.Selected.Total);
        Assert.Equal(result.Gross, result.Selected);
        Assert.Equal((1, 1, 1), (result.Selected.Wins, result.Selected.Losses, result.Selected.BreakEvens));
        Assert.Equal(100m / 3, result.Selected.WinRatePercent);
        Assert.Equal(2m, result.Selected.ProfitFactor.Value);
        Assert.Equal(MetricCoverageStatus.Unavailable, result.Data.ActualNet.Status);
        AssertInvariants(result);
    }

    [Fact]
    public void ExplicitGrossUsesNoFallbackEvenWithKnownNetAndKeepsIndependentSourceCoverage()
    {
        var rows = new[] { Trade(1, 10, -2), Trade(2, -5), Trade(3, 0), Trade(4, 8, 0) };
        var result = Calculate(rows, mode: BreakdownCalculationMode.Gross).Summary;
        Assert.Equal(BreakdownFinancialSource.Gross, result.FinancialSources.Source);
        Assert.Equal(new BreakdownFinancialCoverage(BreakdownCalculationMode.Gross, 4, 0, 0, 4, 0), result.FinancialSources);
        Assert.Equal(result.Gross, result.Selected);
        Assert.Equal(13m, result.Selected.Total);
        Assert.Equal((2, 1, 1), (result.Selected.Wins, result.Selected.Losses, result.Selected.BreakEvens));
        Assert.Equal(new MetricCoverage(4, 2), result.Data.ActualNet);
        AssertInvariants(result);
    }

    [Theory]
    [InlineData(BreakdownCalculationMode.Gross)]
    [InlineData(BreakdownCalculationMode.NetPreferred)]
    public void UnavailableTradeStaysInDenominatorAndSuppressesIncompleteTotalsRatiosAndAverages(BreakdownCalculationMode mode)
    {
        var result = Calculate([Trade(1, 10), Trade(2, 1, -2), Trade(3, null, costs: 0)], mode: mode).Summary;
        Assert.Equal(3, result.EligibleTradeCount);
        Assert.Equal(new MetricCoverage(3, 2), result.Selected.Coverage);
        Assert.Equal(1, result.FinancialSources.UnavailableCount);
        Assert.Null(result.Selected.Total);
        Assert.Equal(mode == BreakdownCalculationMode.Gross ? 11m : 8m, result.Selected.KnownSubtotal);
        Assert.Null(result.Selected.WinRatePercent);
        Assert.Equal(ProfitFactorStatus.IncompleteCoverage, result.Selected.ProfitFactor.Status);
        Assert.Null(result.Selected.ProfitFactor.Value);
        foreach (var average in new[] { result.Selected.AverageTrade, result.Selected.AverageWin, result.Selected.AverageLoss })
            Assert.Equal(new AveragePnlMetric(AveragePnlStatus.IncompleteCoverage, null), average);
        Assert.Equal(1, result.Data.MissingCostTradeCount); // Missing economics with known costs is a different condition.
        AssertInvariants(result);
    }

    [Theory]
    [InlineData(BreakdownCalculationMode.Gross)]
    [InlineData(BreakdownCalculationMode.NetPreferred)]
    public void EmptyAndEntirelyUnavailablePopulationsRemainDifferentFromZero(BreakdownCalculationMode mode)
    {
        var empty = Calculate([], mode: mode);
        var unavailable = Calculate([Trade(1, null)], mode: mode).Summary;
        Assert.Empty(empty.Groups);
        Assert.Empty(empty.Summary.TradeIds);
        Assert.Equal(MetricCoverageStatus.Empty, empty.Summary.Selected.Coverage.Status);
        Assert.Equal(ProfitFactorStatus.NoTrades, empty.Summary.Selected.ProfitFactor.Status);
        Assert.Equal(AveragePnlStatus.NoTrades, empty.Summary.Selected.AverageTrade.Status);
        Assert.Equal(MetricCoverageStatus.Unavailable, unavailable.Selected.Coverage.Status);
        Assert.Null(unavailable.Selected.KnownSubtotal);
        Assert.Null(unavailable.Selected.Total);
        Assert.Equal(0, unavailable.Selected.Wins + unavailable.Selected.Losses + unavailable.Selected.BreakEvens);
        Assert.Equal(mode == BreakdownCalculationMode.Gross ? BreakdownFinancialSource.Gross : BreakdownFinancialSource.NoEvaluableValues,
            unavailable.FinancialSources.Source);
        AssertInvariants(empty.Summary);
        AssertInvariants(unavailable);
    }

    [Theory]
    [InlineData(10, ProfitFactorStatus.NoLosses, null, 100, AveragePnlStatus.Defined, AveragePnlStatus.NoLosses)]
    [InlineData(-10, ProfitFactorStatus.Defined, 0, 0, AveragePnlStatus.NoWins, AveragePnlStatus.Defined)]
    [InlineData(0, ProfitFactorStatus.AllBreakEven, null, 0, AveragePnlStatus.NoWins, AveragePnlStatus.NoLosses)]
    public void OneSidedAndBreakEvenPopulationsPreserveUndefinedRatioStatuses(int amount, ProfitFactorStatus status,
        int? factor, int winRate, AveragePnlStatus win, AveragePnlStatus loss)
    {
        var result = Calculate([Trade(1, amount), Trade(2, amount)]).Summary;
        Assert.Equal(status, result.Selected.ProfitFactor.Status);
        Assert.Equal(factor.HasValue ? (decimal?)factor.Value : null, result.Selected.ProfitFactor.Value);
        Assert.Equal((decimal)winRate, result.Selected.WinRatePercent);
        Assert.Equal(win, result.Selected.AverageWin.Status);
        Assert.Equal(loss, result.Selected.AverageLoss.Status);
        AssertInvariants(result);
    }

    [Fact]
    public void SummaryRatiosAreCalculatedFromTradesInsteadOfAveragingUnequalGroups()
    {
        var rows = new[] { Trade(1, 10, setup: SetupA), Trade(2, 10, setup: SetupA), Trade(3, 10, setup: SetupA),
            Trade(4, -10, setup: SetupA), Trade(5, -100, setup: SetupB) };
        var result = Calculate(rows, BreakdownGrouping.TradingSetup);
        Assert.Equal(60m, result.Summary.Selected.WinRatePercent);
        Assert.Equal(30m / 110m, result.Summary.Selected.ProfitFactor.Value);
        Assert.Equal(-16m, result.Summary.Selected.AverageTrade.Value);
        Assert.NotEqual(result.Groups.Average(g => g.Metrics.Selected.WinRatePercent), result.Summary.Selected.WinRatePercent);
        Assert.NotEqual(result.Groups.Average(g => g.Metrics.Selected.ProfitFactor.Value), result.Summary.Selected.ProfitFactor.Value);
        Assert.NotEqual(result.Groups.Average(g => g.Metrics.Selected.AverageTrade.Value), result.Summary.Selected.AverageTrade.Value);
        AssertReconciles(result);
    }

    [Fact]
    public void UnclassifiedRetainedUnavailableAndInactiveSetupsWithDuplicateNamesStaySeparate()
    {
        var rows = new[] { Trade(1, 10), Trade(2, 20, setup: SetupA) with { SetupName = "Same", SetupIsActive = false },
            Trade(3, 30, setup: SetupB) with { SetupName = "Same" }, Trade(4, 40, setup: Id(500)) with { SetupName = null } };
        var result = Calculate(rows, BreakdownGrouping.TradingSetup);
        Assert.Equal(4, result.Groups.Count);
        Assert.Equal(1, result.Summary.Data.UnclassifiedTradeCount);
        Assert.Equal(3, result.Summary.Data.SetupClassification.KnownTradeCount);
        Assert.Equal(1, result.Summary.Data.UnavailableSetupMetadataCount);
        Assert.Equal("Unclassified", result.Groups.Single(g => g.Key.IsUnclassified).Label);
        Assert.Equal("Same · inactive", result.Groups.Single(g => g.Key.ReferenceId == SetupA).Label);
        Assert.Equal("Same", result.Groups.Single(g => g.Key.ReferenceId == SetupB).Label);
        Assert.Equal($"Missing Setup ({Id(500):D})", result.Groups.Single(g => g.Key.ReferenceId == Id(500)).Label);
        AssertReconciles(result);
    }

    [Theory]
    [InlineData(BreakdownGrouping.Instrument, BreakdownCalculationMode.NetPreferred)]
    [InlineData(BreakdownGrouping.Account, BreakdownCalculationMode.NetPreferred)]
    [InlineData(BreakdownGrouping.Direction, BreakdownCalculationMode.NetPreferred)]
    [InlineData(BreakdownGrouping.TradingSetup, BreakdownCalculationMode.NetPreferred)]
    [InlineData(BreakdownGrouping.EntryWeekday, BreakdownCalculationMode.NetPreferred)]
    [InlineData(BreakdownGrouping.EntryHour, BreakdownCalculationMode.NetPreferred)]
    [InlineData(BreakdownGrouping.HoldingDuration, BreakdownCalculationMode.NetPreferred)]
    [InlineData(BreakdownGrouping.Instrument, BreakdownCalculationMode.Gross)]
    [InlineData(BreakdownGrouping.Account, BreakdownCalculationMode.Gross)]
    [InlineData(BreakdownGrouping.Direction, BreakdownCalculationMode.Gross)]
    [InlineData(BreakdownGrouping.TradingSetup, BreakdownCalculationMode.Gross)]
    [InlineData(BreakdownGrouping.EntryWeekday, BreakdownCalculationMode.Gross)]
    [InlineData(BreakdownGrouping.EntryHour, BreakdownCalculationMode.Gross)]
    [InlineData(BreakdownGrouping.HoldingDuration, BreakdownCalculationMode.Gross)]
    public void EveryDimensionHasExactDisjointMembershipReconciliationAndInputIndependentOrder(BreakdownGrouping grouping, BreakdownCalculationMode mode)
    {
        var rows = new[] {
            Trade(1, 12, 10, setup: SetupA, open: Utc(6, 3, 30), close: Utc(6, 4, 15)),
            Trade(2, 20, setup: SetupB, open: Utc(6, 13), close: Utc(6, 13, 2)),
            Trade(3, -3, -5, account: AccountB, instrument: InstrumentB, direction: TradeDirection.Short, open: Utc(7, 14), close: Utc(7, 22)) };
        var result = Calculate(rows, grouping, mode);
        BreakdownGroupKey[] expected = grouping switch
        {
            BreakdownGrouping.Instrument => [BreakdownGroupKey.ForInstrument(InstrumentA), BreakdownGroupKey.ForInstrument(InstrumentA), BreakdownGroupKey.ForInstrument(InstrumentB)],
            BreakdownGrouping.Account => [BreakdownGroupKey.ForAccount(AccountA), BreakdownGroupKey.ForAccount(AccountA), BreakdownGroupKey.ForAccount(AccountB)],
            BreakdownGrouping.Direction => [BreakdownGroupKey.ForDirection(TradeDirection.Long), BreakdownGroupKey.ForDirection(TradeDirection.Long), BreakdownGroupKey.ForDirection(TradeDirection.Short)],
            BreakdownGrouping.TradingSetup => [BreakdownGroupKey.ForSetup(SetupA), BreakdownGroupKey.ForSetup(SetupB), BreakdownGroupKey.ForSetup(null)],
            BreakdownGrouping.EntryWeekday => [BreakdownGroupKey.ForEntryWeekday(DayOfWeek.Monday), BreakdownGroupKey.ForEntryWeekday(DayOfWeek.Tuesday), BreakdownGroupKey.ForEntryWeekday(DayOfWeek.Wednesday)],
            BreakdownGrouping.EntryHour => [BreakdownGroupKey.ForEntryHour(23), BreakdownGroupKey.ForEntryHour(9), BreakdownGroupKey.ForEntryHour(10)],
            BreakdownGrouping.HoldingDuration => [BreakdownGroupKey.ForHoldingDuration(HoldingDurationBucket.FifteenToSixtyMinutes), BreakdownGroupKey.ForHoldingDuration(HoldingDurationBucket.OneToFiveMinutes), BreakdownGroupKey.ForHoldingDuration(HoldingDurationBucket.FourHoursOrMore)],
            _ => throw new ArgumentOutOfRangeException(nameof(grouping)),
        };
        for (int index = 0; index < rows.Length; index++)
            Assert.Contains(rows[index].Facts.TradeId, result.Groups.Single(g => g.Key == expected[index]).Metrics.TradeIds);
        Assert.Equal(result.Groups.Select(g => g.Key).Order(), result.Groups.Select(g => g.Key));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Calculate(rows.Reverse(), grouping, mode)));
        AssertReconciles(result);
    }

    [Fact]
    public void FiltersUseExactAccountInstrumentHistoricalCurrencyAndHalfOpenClosureBounds()
    {
        var query = new BreakdownQuery(" usd ", AccountA, InstrumentA, new(2026, 10, 6), new(2026, 10, 6), grouping: BreakdownGrouping.EntryWeekday);
        DateTimeOffset first = query.TradeFilter.ClosedFromUtc!.Value, before = query.TradeFilter.ClosedBeforeUtc!.Value;
        var rows = new[] { Trade(1, 10, open: first.AddMinutes(-30), close: first), Trade(2, 20, open: before.AddMinutes(-2), close: before.AddTicks(-1)),
            Trade(3, 100, close: first.AddTicks(-1), open: first.AddHours(-1)), Trade(4, 100, close: before, open: before.AddHours(-1)),
            Trade(5, 100, account: AccountB), Trade(6, 100, instrument: InstrumentB), Trade(7, 100) with { Facts = Trade(7, 100).Facts with { Currency = "EUR" } },
            Trade(8, null) with { Facts = Trade(8, null).Facts with { Status = TradeStatus.Open, ClosedAtUtc = null } } };
        var result = BreakdownMetricCalculator.Calculate(query, rows);
        Assert.Equal(new[] { Id(1), Id(2) }, result.Summary.TradeIds);
        Assert.Equal(30m, result.Summary.Selected.Total);
        Assert.Contains(result.Groups, g => g.Key == BreakdownGroupKey.ForEntryWeekday(DayOfWeek.Monday));
        AssertReconciles(result);
    }

    [Theory]
    [InlineData(3, 8, 6)]
    [InlineData(11, 1, 5)]
    public void DstEntryGroupsAndElapsedDurationAgreeWithoutDoubleCounting(int month, int day, int hour)
    {
        var start = new DateTimeOffset(2026, month, day, hour, 30, 0, TimeSpan.Zero);
        var rows = new[] { Trade(1, 10, open: start, close: start.AddHours(1)), Trade(2, -5, open: start.AddHours(1), close: start.AddHours(2)) };
        var query = new BreakdownQuery("USD", closedFromNewYork: new(2026, month, day), closedThroughNewYork: new(2026, month, day), grouping: BreakdownGrouping.EntryHour);
        var result = BreakdownMetricCalculator.Calculate(query, rows);
        Assert.Equal(month == 11 ? 1 : 2, result.Groups.Count);
        Assert.Equal(2, result.Summary.EligibleTradeCount);
        var duration = Calculate(rows, BreakdownGrouping.HoldingDuration);
        Assert.Equal(BreakdownGroupKey.ForHoldingDuration(HoldingDurationBucket.OneToFourHours), Assert.Single(duration.Groups).Key);
        AssertReconciles(result);
    }

    [Fact]
    public void DuplicateInputsAreRejectedEvenOutsideFilterRatherThanSilentlyDeduplicated()
    {
        var row = Trade(1, 10);
        Assert.Throws<ArgumentException>(() => Calculate([row, row]));
        Assert.Throws<ArgumentException>(() => BreakdownMetricCalculator.Calculate(new("EUR"), [row, row]));
        Assert.Throws<ArgumentException>(() => Calculate([row, row with { InstrumentId = InstrumentB }]));
    }

    [Fact]
    public void CancellationBeforeOrDuringEnumerationProducesNoPartialSnapshot()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        bool enumerated = false;
        Assert.Throws<OperationCanceledException>(() => BreakdownMetricCalculator.Calculate(new("USD"),
            NeverEnumerate(), cancellation.Token));
        Assert.False(enumerated);
        using var during = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => BreakdownMetricCalculator.Calculate(new("USD"), CancelDuring(), during.Token));
        IEnumerable<BreakdownTradeFact> CancelDuring()
        {
            yield return Trade(1, 10);
            during.Cancel();
            yield return Trade(2, 10);
        }
        IEnumerable<BreakdownTradeFact> NeverEnumerate()
        {
            enumerated = true;
            yield return Trade(1, 10);
        }
    }

    [Fact]
    public void PrecisionIsPreservedAndOverflowCannotPublishACappedOrPartialResult()
    {
        var result = Calculate([Trade(1, .0001m), Trade(2, -.0002m), Trade(3, .0003m)]).Summary;
        Assert.Equal(.0002m, result.Selected.Total);
        Assert.Equal(100m * 2 / 3, result.Selected.WinRatePercent);
        Assert.Equal(.0002m / 3, result.Selected.AverageTrade.Value);
        Assert.Equal(2m, result.Selected.ProfitFactor.Value);
        Assert.Throws<OverflowException>(() => Calculate([Trade(1, decimal.MaxValue), Trade(2, 1)]));
        Assert.Equal(decimal.MinValue, Calculate([Trade(1, decimal.MinValue)]).Summary.Selected.Total);
        Assert.Throws<OverflowException>(() => Calculate([Trade(1, decimal.MinValue), Trade(2, -1)]));
    }

    [Fact]
    public void CandidateEnumerationIsSinglePassAndPublishedCollectionsAreReadOnly()
    {
        int enumerations = 0;
        var result = Calculate(Once());
        Assert.Equal(1, enumerations);
        Assert.Throws<NotSupportedException>(() => ((IList<BreakdownGroupMetrics>)result.Groups).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Guid>)result.Summary.TradeIds).Clear());
        IEnumerable<BreakdownTradeFact> Once()
        {
            if (++enumerations != 1) throw new InvalidOperationException("Input was reread.");
            yield return Trade(1, 10);
        }
    }

    internal static readonly Guid AccountA = Id(101), AccountB = Id(102), InstrumentA = Id(201), InstrumentB = Id(202), SetupA = Id(301), SetupB = Id(302);
    internal static Guid Id(int number) => Guid.Parse($"00000000-0000-0000-0000-{number:D12}");
    internal static DateTimeOffset Utc(int day, int hour, int minute = 0) => new(2026, 10, day, hour, minute, 0, TimeSpan.Zero);
    internal static BreakdownTradeFact Trade(int id, decimal? gross, decimal? net = null, decimal? costs = null,
        Guid? account = null, Guid? instrument = null, Guid? setup = null, TradeDirection direction = TradeDirection.Long,
        DateTimeOffset? open = null, DateTimeOffset? close = null) => new(
            new(Id(id), TradeStatus.Closed, open ?? Utc(6, 13), close ?? Utc(6, 14), "USD", setup,
                gross, net.HasValue ? costs ?? gross!.Value - net.Value : costs, net),
            account ?? AccountA, instrument ?? InstrumentA, direction)
        { AccountName = "Same account name", InstrumentName = "Same instrument name", SetupName = setup.HasValue ? "Same setup name" : null };

    private static BreakdownAnalyticsSnapshot Calculate(IEnumerable<BreakdownTradeFact> trades,
        BreakdownGrouping grouping = BreakdownGrouping.Instrument, BreakdownCalculationMode mode = BreakdownCalculationMode.NetPreferred) =>
        BreakdownMetricCalculator.Calculate(new("USD", mode: mode, grouping: grouping), trades);

    private static void AssertInvariants(BreakdownMetrics metrics)
    {
        var source = metrics.FinancialSources;
        Assert.Equal(metrics.EligibleTradeCount, source.NetUsedCount + source.GrossFallbackCount + source.GrossUsedCount + source.UnavailableCount);
        Assert.Equal(metrics.Selected.EvaluableTradeCount, metrics.Selected.Wins + metrics.Selected.Losses + metrics.Selected.BreakEvens);
        Assert.Equal(source.EvaluableTradeCount, metrics.Selected.EvaluableTradeCount);
        Assert.Equal(metrics.EligibleTradeCount, metrics.TradeIds.Count);
    }

    private static void AssertReconciles(BreakdownAnalyticsSnapshot result)
    {
        AssertInvariants(result.Summary);
        Assert.All(result.Groups, group => AssertInvariants(group.Metrics));
        Assert.Equal(result.Summary.TradeIds, result.Groups.SelectMany(g => g.Metrics.TradeIds).Order());
        Assert.Equal(result.Summary.EligibleTradeCount, result.Groups.Sum(g => g.Metrics.EligibleTradeCount));
        Assert.Equal(result.Summary.FinancialSources.NetUsedCount, result.Groups.Sum(g => g.Metrics.FinancialSources.NetUsedCount));
        Assert.Equal(result.Summary.FinancialSources.GrossFallbackCount, result.Groups.Sum(g => g.Metrics.FinancialSources.GrossFallbackCount));
        Assert.Equal(result.Summary.FinancialSources.GrossUsedCount, result.Groups.Sum(g => g.Metrics.FinancialSources.GrossUsedCount));
        Assert.Equal(result.Summary.FinancialSources.UnavailableCount, result.Groups.Sum(g => g.Metrics.FinancialSources.UnavailableCount));
        Assert.Equal(result.Summary.Selected.EvaluableTradeCount, result.Groups.Sum(g => g.Metrics.Selected.EvaluableTradeCount));
        Assert.Equal(result.Summary.Data.ActualNet.KnownTradeCount, result.Groups.Sum(g => g.Metrics.Data.ActualNet.KnownTradeCount));
        Assert.Equal(result.Summary.Data.Gross.KnownTradeCount, result.Groups.Sum(g => g.Metrics.Data.Gross.KnownTradeCount));
        Assert.Equal(result.Summary.Data.SetupClassification.KnownTradeCount, result.Groups.Sum(g => g.Metrics.Data.SetupClassification.KnownTradeCount));
        Assert.Equal(result.Summary.Data.UnavailableSetupMetadataCount, result.Groups.Sum(g => g.Metrics.Data.UnavailableSetupMetadataCount));
        Assert.Equal(result.Summary.Data.UnclassifiedTradeCount, result.Groups.Sum(g => g.Metrics.Data.UnclassifiedTradeCount));
        Assert.Equal(result.Summary.Data.MissingCostTradeCount, result.Groups.Sum(g => g.Metrics.Data.MissingCostTradeCount));
        if (result.Summary.Selected.Total.HasValue) Assert.Equal(result.Summary.Selected.Total, result.Groups.Sum(g => g.Metrics.Selected.Total));
        if (result.Summary.Gross.Total.HasValue) Assert.Equal(result.Summary.Gross.Total, result.Groups.Sum(g => g.Metrics.Gross.Total));
    }
}
