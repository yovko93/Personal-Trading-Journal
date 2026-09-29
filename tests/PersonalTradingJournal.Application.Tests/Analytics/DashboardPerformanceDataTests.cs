using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Analytics;

public sealed class DashboardPerformanceDataTests
{
    [Fact]
    public void ChartPointsReusePeriodSnapshotsPreservingZerosGapsAndCoverage()
    {
        TradeAnalyticsFact[] facts = [Fact(10m, 2m, day: 1), Fact(-285m, null, day: 3), Fact(0m, day: 4)];
        CurrencyTradeMetrics result = Single(facts);
        Assert.Equal(new[] { new DateOnly(2026, 1, 1), new(2026, 1, 3), new(2026, 1, 4) },
            result.DailyPnl.Select(p => p.NewYorkDate));
        Assert.Equal(result.DailyPnl.Select(p => p.NewYorkDate), result.CumulativeRealizedPnl.Select(p => p.NewYorkDate));
        for (int i = 0; i < result.Days.Count; i++)
        {
            Assert.Same(result.Days[i].Metrics, result.DailyPnl[i].Metrics);
            Assert.Same(result.Days[i].CumulativeMetrics, result.CumulativeRealizedPnl[i].Metrics);
        }
        Assert.Equal(8m, result.DailyPnl[0].Metrics.Net.Total);
        Assert.Null(result.DailyPnl[1].Metrics.Net.Total);
        Assert.Equal(0m, result.DailyPnl[2].Metrics.Net.Total);
        Assert.False(result.DailyPnl[2].Metrics.EffectiveNet.IsEstimated);
        Assert.Null(result.CumulativeRealizedPnl[2].Metrics.Net.Total);
        Assert.Equal(new MetricCoverage(3, 2), result.CumulativeRealizedPnl[2].Metrics.Net.Coverage);
        Assert.Equal(8m, result.CumulativeRealizedPnl[2].Metrics.Net.KnownSubtotal);
        Assert.Equal(-277m, result.CumulativeRealizedPnl[2].Metrics.EffectiveNet.Total);
        Assert.True(result.CumulativeRealizedPnl[2].Metrics.EffectiveNet.IsEstimated);
        Assert.Equal(1, result.CumulativeRealizedPnl[2].Metrics.EffectiveNet.EstimatedTradeCount);
        Assert.Equal(result.DailyPnl.ToArray(), Single(facts.Reverse().ToArray()).DailyPnl.ToArray());
        Assert.Throws<NotSupportedException>(() => ((IList<PnlChartPoint>)result.DailyPnl).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<PnlChartPoint>)result.CumulativeRealizedPnl).Clear());
    }

    [Fact]
    public void SetupsKeepIdentityActivityAndSharedOutcomeRulesWithAnExplicitUnclassifiedBucket()
    {
        Guid a = Guid.Parse("00000001-0000-0000-0000-000000000000");
        Guid b = Guid.Parse("00000002-0000-0000-0000-000000000000");
        var result = Assert.Single(DashboardMetricCalculator.Calculate([
            Fact(100m, setup: a), Fact(-40m, setup: a), Fact(0m, setup: a), Fact(20m, setup: a),
            Fact(-285m, null, setup: b), Fact(10m, 2m, setup: b), Fact(0m)],
            [new(b, "Same name", false), new(a, "Same name", true)]).Currencies);
        Assert.True(result.HasClassifiedTrades);
        Assert.True(result.HasClosedTrades);
        Assert.Equal(new Guid?[] { null, a, b }, result.Setups.Select(s => s.TradingSetupId));
        Assert.Equal(SetupReferenceStatus.Unclassified, result.Setups[0].ReferenceStatus);
        Assert.Null(result.Setups[0].Name);
        Assert.Null(result.Setups[0].IsActive);
        SetupTradeMetrics active = result.Setups[1], inactive = result.Setups[2];
        Assert.Equal(SetupReferenceStatus.Available, inactive.ReferenceStatus);
        Assert.False(inactive.IsActive);
        Assert.True(active.IsActive);
        Assert.Equal(50m, active.Metrics.Net.WinRatePercent);
        Assert.Equal(60m, active.Metrics.Net.AverageWin.Value);
        Assert.Equal(40m, active.Metrics.Net.AverageLoss.Value);
        Assert.Equal(3m, active.Metrics.Net.ProfitFactor.Value);
        Assert.Equal(AveragePnlStatus.IncompleteCoverage, inactive.Metrics.Net.AverageLoss.Status);
        Assert.Null(inactive.Metrics.Net.WinRatePercent);
        Assert.Equal(ProfitFactorStatus.IncompleteCoverage, inactive.Metrics.Net.ProfitFactor.Status);
        Assert.True(inactive.Metrics.EffectiveNet.IsEstimated);
        Assert.Equal(285m, inactive.Metrics.EffectiveNet.AverageLoss.Value);
        Assert.Equal(50m, inactive.Metrics.EffectiveNet.WinRatePercent);
        Assert.Equal(7, result.Setups.Sum(s => s.Metrics.ClosedTradeCount));
    }

    [Fact]
    public void MissingAndUnloadedReferencesNeverBecomeUnclassifiedOrDropTradeEconomics()
    {
        Guid id = Guid.NewGuid();
        TradeAnalyticsFact fact = Fact(5m, setup: id);
        SetupTradeMetrics unloaded = Assert.Single(Single(fact).Setups);
        var missing = Assert.Single(DashboardMetricCalculator.Calculate([fact], []).Currencies);
        SetupTradeMetrics group = Assert.Single(missing.Setups);
        Assert.Equal(SetupReferenceStatus.NotLoaded, unloaded.ReferenceStatus);
        Assert.Equal(SetupReferenceStatus.Missing, group.ReferenceStatus);
        Assert.Equal(id, group.TradingSetupId);
        Assert.Null(group.Name);
        Assert.Null(group.IsActive);
        Assert.True(missing.HasClassifiedTrades);
        Assert.Equal(unloaded.Metrics, group.Metrics);
        Assert.Equal(5m, group.Metrics.Net.Total);
    }

    [Fact]
    public void EmptyUnclassifiedAndUnavailableNetAreDistinctStates()
    {
        Assert.Empty(DashboardMetricCalculator.Calculate([], []).Currencies);
        var open = Fact(null, null) with { Status = TradeStatus.Open, ClosedAtUtc = null };
        CurrencyTradeMetrics empty = Single(open);
        Assert.False(empty.HasClosedTrades);
        Assert.False(empty.HasClassifiedTrades);
        Assert.Empty(empty.DailyPnl);
        Assert.Empty(empty.CumulativeRealizedPnl);
        Assert.Empty(empty.Setups);

        CurrencyTradeMetrics unclassified = Single(Fact(null, null));
        Assert.True(unclassified.HasClosedTrades);
        Assert.False(unclassified.HasClassifiedTrades);
        Assert.Equal(SetupReferenceStatus.Unclassified, Assert.Single(unclassified.Setups).ReferenceStatus);
        Assert.Equal(MetricCoverageStatus.Unavailable, Assert.Single(unclassified.DailyPnl).Metrics.Net.Coverage.Status);
        Assert.Null(unclassified.DailyPnl[0].Metrics.EffectiveNet.Total);
        Assert.Equal(1, unclassified.DailyPnl[0].Metrics.ClosedTradeCount);
    }

    [Theory]
    [InlineData("2026-03-08T04:59:59Z", "2026-03-07")]
    [InlineData("2026-03-08T05:00:00Z", "2026-03-08")]
    [InlineData("2026-11-01T05:30:00Z", "2026-11-01")]
    [InlineData("2026-11-01T06:30:00Z", "2026-11-01")]
    public void ChartDatesRemainNewYorkClosureDatesAcrossDst(string utc, string expected)
    {
        var instant = DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture);
        var point = Assert.Single(Single(Fact(1m) with { ClosedAtUtc = instant }).DailyPnl);
        Assert.Equal(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), point.NewYorkDate);
    }

    [Fact]
    public void CurrencyPartitionsAndSetupChangesDoNotChangeChartEconomics()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        TradeAnalyticsFact fact = Fact(10m, setup: a);
        DashboardAnalyticsSnapshot before = DashboardMetricCalculator.Calculate(
            [fact, Fact(100m) with { Currency = "EUR" }], [new(a, "Old name", true)]);
        DashboardAnalyticsSnapshot after = DashboardMetricCalculator.Calculate(
            [fact with { TradingSetupId = b }, Fact(100m) with { Currency = "EUR" }], [new(b, "New name", false)]);
        Assert.Equal(new[] { "EUR", "USD" }, before.Currencies.Select(c => c.Currency));
        Assert.Equal(100m, before.Currencies[0].DailyPnl[0].Metrics.Net.Total);
        Assert.Equal(before.Currencies[1].DailyPnl.ToArray(), after.Currencies[1].DailyPnl.ToArray());
        Assert.Equal(a, Assert.Single(before.Currencies[1].Setups).TradingSetupId);
        Assert.Equal(b, Assert.Single(after.Currencies[1].Setups).TradingSetupId);
        Assert.Equal("New name", after.Currencies[1].Setups[0].Name);
    }

    [Fact]
    public void InvalidReferenceSnapshotsAndCancellationAreExplicit()
    {
        Guid id = Guid.NewGuid();
        var reference = new TradingSetupAnalyticsReference(id, "Setup", true);
        Assert.Throws<ArgumentException>(() => DashboardMetricCalculator.Calculate([], [reference, reference]));
        Assert.Throws<ArgumentException>(() => DashboardMetricCalculator.Calculate([], [reference with { Name = " " }]));
        Assert.Throws<ArgumentException>(() => DashboardMetricCalculator.Calculate([], [reference with { TradingSetupId = Guid.Empty }]));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => DashboardMetricCalculator.Calculate([], [reference], cancellation.Token));
    }

    private static CurrencyTradeMetrics Single(params TradeAnalyticsFact[] facts) =>
        Assert.Single(DashboardMetricCalculator.Calculate(facts).Currencies);
    private static TradeAnalyticsFact Fact(decimal? gross, decimal? costs = 0m, int day = 1, Guid? setup = null) =>
        new(Guid.NewGuid(), TradeStatus.Closed, new(2025, 12, 1, 0, 0, 0, TimeSpan.Zero),
            new(2026, 1, day, 12, 0, 0, TimeSpan.Zero), "USD", setup, gross, costs, gross - costs);
}
