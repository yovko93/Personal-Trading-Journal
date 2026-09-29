using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Analytics;

public sealed class DashboardOutcomeMetricsTests
{
    [Fact]
    public void AveragesReuseOutcomeSumsAndExcludeBreakEvensOnEveryBasisAndScope()
    {
        CurrencyTradeMetrics result = Calculate(Fact(100m), Fact(-40m), Fact(0m), Fact(20m));
        ClosedTradeMetrics[] scopes = [result.Metrics, Assert.Single(result.Days).Metrics,
            result.Days[0].CumulativeMetrics, Assert.Single(result.Weeks).Metrics,
            result.Weeks[0].CumulativeMetrics, Assert.Single(result.Setups).Metrics];

        foreach (ClosedTradeMetrics scope in scopes)
        foreach (PnlMetrics basis in Bases(scope))
        {
            Assert.Equal(2, basis.KnownWins);
            Assert.Equal(1, basis.KnownLosses);
            Assert.Equal(1, basis.KnownBreakEvens);
            Assert.Equal(50m, basis.WinRatePercent);
            Assert.Equal(new AveragePnlMetric(AveragePnlStatus.Defined, 60m), basis.AverageWin);
            Assert.Equal(new AveragePnlMetric(AveragePnlStatus.Defined, 40m), basis.AverageLoss);
            Assert.Equal(new ProfitFactorMetric(ProfitFactorStatus.Defined, 3m), basis.ProfitFactor);
            Assert.False(basis.IsEstimated);
        }
    }

    [Fact]
    public void GrossWinAndVerifiedNetLossStaySeparate()
    {
        ClosedTradeMetrics result = Calculate(Fact(5m, 8m)).Metrics;
        Assert.Equal(5m, result.Gross.AverageWin.Value);
        Assert.Equal(AveragePnlStatus.NoLosses, result.Gross.AverageLoss.Status);
        Assert.Equal(100m, result.Gross.WinRatePercent);
        foreach (PnlMetrics net in new[] { result.Net, result.EffectiveNet })
        {
            Assert.Equal(AveragePnlStatus.NoWins, net.AverageWin.Status);
            Assert.Equal(3m, net.AverageLoss.Value);
            Assert.Equal(0m, net.WinRatePercent);
            Assert.Equal(0m, net.ProfitFactor.Value);
            Assert.False(net.IsEstimated);
        }
    }

    [Fact]
    public void MixedEffectiveOutcomesKeepEstimatesAndSuppressStrictNetAverages()
    {
        ClosedTradeMetrics result = Calculate(Fact(100m, 10m), Fact(-285m, null), Fact(0m)).Metrics;
        Assert.Equal(100m, result.Gross.AverageWin.Value);
        Assert.Equal(285m, result.Gross.AverageLoss.Value);
        AssertIncomplete(result.Net);
        Assert.Equal(90m, result.Net.KnownSubtotal);
        PnlMetrics effective = result.EffectiveNet;
        Assert.Equal(PnlBasis.EffectiveNet, effective.Basis);
        Assert.True(effective.IsEstimated);
        Assert.Equal(1, effective.EstimatedTradeCount);
        Assert.Equal(2, effective.VerifiedTradeCount);
        Assert.Equal(new MetricCoverage(3, 3), effective.Coverage);
        Assert.Equal(90m, effective.AverageWin.Value);
        Assert.Equal(285m, effective.AverageLoss.Value);
        Assert.Equal(100m / 3m, effective.WinRatePercent);
        Assert.Equal(90m / 285m, effective.ProfitFactor.Value);
    }

    [Theory]
    [InlineData(10, AveragePnlStatus.Defined, AveragePnlStatus.NoLosses, ProfitFactorStatus.NoLosses, 100)]
    [InlineData(-10, AveragePnlStatus.NoWins, AveragePnlStatus.Defined, ProfitFactorStatus.Defined, 0)]
    [InlineData(0, AveragePnlStatus.NoWins, AveragePnlStatus.NoLosses, ProfitFactorStatus.AllBreakEven, 0)]
    public void MissingOutcomeAndZeroDenominatorHaveExplicitReasons(int pnl, AveragePnlStatus win,
        AveragePnlStatus loss, ProfitFactorStatus factor, int winRate)
    {
        foreach (PnlMetrics basis in Bases(Calculate(Fact(pnl)).Metrics))
        {
            Assert.Equal(new AveragePnlMetric(win, pnl > 0 ? pnl : null), basis.AverageWin);
            Assert.Equal(new AveragePnlMetric(loss, pnl < 0 ? -pnl : null), basis.AverageLoss);
            Assert.Equal(new ProfitFactorMetric(factor, pnl < 0 ? 0m : null), basis.ProfitFactor);
            Assert.Equal((decimal)winRate, basis.WinRatePercent);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(-10)]
    public void AllUnknownCostsAreNotVerifiedZeroOrUndefinedDenominators(int gross)
    {
        ClosedTradeMetrics result = Calculate(Fact(gross, null)).Metrics;
        AssertIncomplete(result.Net);
        Assert.Equal(MetricCoverageStatus.Unavailable, result.Net.Coverage.Status);
        Assert.True(result.EffectiveNet.IsEstimated);
        Assert.Equal(1, result.EffectiveNet.EstimatedTradeCount);
        Assert.Equal(result.Gross.AverageWin, result.EffectiveNet.AverageWin);
        Assert.Equal(result.Gross.AverageLoss, result.EffectiveNet.AverageLoss);
    }

    [Fact]
    public void UnknownGrossBlocksEveryBasisEvenWhenKnownSubsetHasWinsAndLosses()
    {
        ClosedTradeMetrics result = Calculate(Fact(10m), Fact(-4m), Fact(null, null)).Metrics;
        foreach (PnlMetrics basis in Bases(result))
        {
            AssertIncomplete(basis);
            Assert.Equal(new MetricCoverage(3, 2), basis.Coverage);
            Assert.Equal(1, basis.KnownWins);
            Assert.Equal(1, basis.KnownLosses);
        }
    }

    [Fact]
    public void EmptyAndOpenOnlyPopulationsHaveNoInventedAverages()
    {
        Assert.Empty(DashboardMetricCalculator.Calculate([]).Currencies);
        TradeAnalyticsFact open = Fact(null, null) with { Status = TradeStatus.Open, ClosedAtUtc = null };
        foreach (PnlMetrics basis in Bases(Calculate(open).Metrics))
        {
            Assert.Equal(new AveragePnlMetric(AveragePnlStatus.NoTrades, null), basis.AverageWin);
            Assert.Equal(new AveragePnlMetric(AveragePnlStatus.NoTrades, null), basis.AverageLoss);
            Assert.Null(basis.WinRatePercent);
            Assert.Equal(ProfitFactorStatus.NoTrades, basis.ProfitFactor.Status);
        }
        Assert.Equal(10m, Calculate(open, Fact(10m)).Metrics.Net.AverageWin.Value);
    }

    [Fact]
    public void AveragesRemainPerCurrencyAndKeepDecimalDivisionPrecision()
    {
        DashboardAnalyticsSnapshot snapshot = DashboardMetricCalculator.Calculate([
            Fact(.1m), Fact(.1m), Fact(.2m), Fact(-.1m), Fact(-.1m), Fact(-.2m),
            Fact(900m) with { Currency = "EUR" }]);
        PnlMetrics usd = Assert.Single(snapshot.Currencies, c => c.Currency == "USD").Metrics.Net;
        Assert.Equal(.4m / 3m, usd.AverageWin.Value);
        Assert.Equal(.4m / 3m, usd.AverageLoss.Value);
        Assert.Equal(1m, usd.ProfitFactor.Value);
        Assert.Equal(900m, Assert.Single(snapshot.Currencies, c => c.Currency == "EUR").Metrics.Net.AverageWin.Value);
    }

    [Fact]
    public void EstimateProvenanceStaysOnCumulativeAveragesNotLaterVerifiedOnlyPeriods()
    {
        TradeAnalyticsFact estimated = Fact(-285m, null);
        TradeAnalyticsFact verified = Fact(-15m) with { ClosedAtUtc = estimated.ClosedAtUtc!.Value.AddDays(7) };
        CurrencyTradeMetrics result = Calculate(estimated, verified);
        Assert.False(result.Days[1].Metrics.EffectiveNet.IsEstimated);
        Assert.Equal(15m, result.Days[1].Metrics.EffectiveNet.AverageLoss.Value);
        foreach (ClosedTradeMetrics cumulative in new[] { result.Days[1].CumulativeMetrics, result.Weeks[1].CumulativeMetrics })
        {
            Assert.True(cumulative.EffectiveNet.IsEstimated);
            Assert.Equal(150m, cumulative.EffectiveNet.AverageLoss.Value);
            AssertIncomplete(cumulative.Net);
        }
    }

    [Fact]
    public void OverflowRejectsSnapshotEvenIfAnAverageAloneWouldFit()
    {
        Assert.Throws<OverflowException>(() => Calculate(Fact(decimal.MaxValue), Fact(decimal.MaxValue)));
        Assert.Throws<OverflowException>(() => Calculate(Fact(decimal.MinValue), Fact(-1m)));
        // Individually representable sums can still overflow the Profit Factor division.
        Assert.Throws<OverflowException>(() => Calculate(Fact(decimal.MaxValue), Fact(-.1m)));
    }

    private static void AssertIncomplete(PnlMetrics result)
    {
        Assert.Equal(new AveragePnlMetric(AveragePnlStatus.IncompleteCoverage, null), result.AverageWin);
        Assert.Equal(new AveragePnlMetric(AveragePnlStatus.IncompleteCoverage, null), result.AverageLoss);
        Assert.Null(result.WinRatePercent);
        Assert.Equal(new ProfitFactorMetric(ProfitFactorStatus.IncompleteCoverage, null), result.ProfitFactor);
    }

    private static PnlMetrics[] Bases(ClosedTradeMetrics metrics) => [metrics.Gross, metrics.Net, metrics.EffectiveNet];
    private static CurrencyTradeMetrics Calculate(params TradeAnalyticsFact[] facts) =>
        Assert.Single(DashboardMetricCalculator.Calculate(facts).Currencies);
    private static TradeAnalyticsFact Fact(decimal? gross, decimal? costs = 0m) =>
        new(Guid.NewGuid(), TradeStatus.Closed, new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new(2026, 1, 5, 12, 0, 0, TimeSpan.Zero), "USD", null, gross, costs, gross - costs);
}
