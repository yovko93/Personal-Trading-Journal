using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Analytics;

public sealed class EstimatedNetPnLTests
{
    [Theory]
    [InlineData(-285, null, -285, NetPnLProvenance.Estimated)]
    [InlineData(20, null, 20, NetPnLProvenance.Estimated)]
    [InlineData(0, null, 0, NetPnLProvenance.Estimated)]
    [InlineData(0, 0, 0, NetPnLProvenance.Verified)]
    [InlineData(20, 0, 0, NetPnLProvenance.Verified)]
    [InlineData(20, -2, -2, NetPnLProvenance.Verified)]
    [InlineData(null, null, null, NetPnLProvenance.Unavailable)]
    public void ClosedTradeEffectiveNetKeepsValueAndProvenanceTogether(int? gross, int? net, int? expected, NetPnLProvenance provenance)
    {
        EffectiveNetPnL result = EffectiveNetPnL.Resolve(TradeStatus.Closed, gross, net);
        Assert.Equal(expected.HasValue ? (decimal?)expected.Value : null, result.Value);
        Assert.Equal(provenance, result.Provenance);
        Assert.Equal(provenance == NetPnLProvenance.Estimated, result.IsEstimated);
    }

    [Fact]
    public void OpenTradeCannotHaveAnEstimatedFinalOutcome()
    {
        Assert.Equal(new EffectiveNetPnL(null, NetPnLProvenance.Unavailable),
            EffectiveNetPnL.Resolve(TradeStatus.Open, 10m, null));
    }

    [Fact]
    public void MixedVerifiedEstimatedTotalsAndRatiosAreExplicitlyEstimatedButStrictNetRemainsIncomplete()
    {
        CurrencyTradeMetrics result = Calculate(Fact(10m, 2m), Fact(-285m, null), Fact(0m, 0m));
        Assert.Equal(-275m, result.Metrics.Gross.Total);
        Assert.Null(result.Metrics.Net.Total);
        Assert.Equal(8m, result.Metrics.Net.KnownSubtotal);
        Assert.Equal(new MetricCoverage(3, 2), result.Metrics.Net.Coverage);
        Assert.False(result.Metrics.Net.IsEstimated);
        Assert.Null(result.Metrics.Net.WinRatePercent);
        Assert.Equal(ProfitFactorStatus.IncompleteCoverage, result.Metrics.Net.ProfitFactor.Status);
        PnlMetrics effective = result.Metrics.EffectiveNet;
        Assert.Equal(PnlBasis.EffectiveNet, effective.Basis);
        Assert.Equal(-277m, effective.Total);
        Assert.True(effective.IsEstimated);
        Assert.Equal(1, effective.EstimatedTradeCount);
        Assert.Equal(2, effective.VerifiedTradeCount);
        Assert.Equal(new MetricCoverage(3, 3), effective.Coverage);
        Assert.Equal(100m / 3m, effective.WinRatePercent);
        Assert.Equal(8m / 285m, effective.ProfitFactor.Value);
        Assert.Equal(effective, Assert.Single(result.Days).Metrics.EffectiveNet);
        Assert.Equal(effective, Assert.Single(result.Weeks).CumulativeMetrics.EffectiveNet);
        Assert.Equal(effective, Assert.Single(result.Setups).Metrics.EffectiveNet);
    }

    [Fact]
    public void LaterVerifiedPeriodDoesNotEraseEarlierCumulativeEstimate()
    {
        CurrencyTradeMetrics result = Calculate(Fact(-285m, null), Fact(5m, 1m, day: 9));
        Assert.Equal(-285m, result.Days[0].Metrics.EffectiveNet.Total);
        Assert.True(result.Days[0].Metrics.EffectiveNet.IsEstimated);
        Assert.Equal(4m, result.Days[1].Metrics.EffectiveNet.Total);
        Assert.False(result.Days[1].Metrics.EffectiveNet.IsEstimated);
        Assert.Equal(-281m, result.Days[1].CumulativeMetrics.EffectiveNet.Total);
        Assert.True(result.Days[1].CumulativeMetrics.EffectiveNet.IsEstimated);
        Assert.True(result.Weeks[1].CumulativeMetrics.EffectiveNet.IsEstimated);
        Assert.False(result.Weeks[1].Metrics.EffectiveNet.IsEstimated);
        Assert.Null(result.Weeks[1].CumulativeMetrics.Net.Total);
    }

    [Fact]
    public void UnknownGrossRemainsUnavailableAndCannotCreateACompleteEffectiveTotal()
    {
        CurrencyTradeMetrics result = Calculate(Fact(null, null), Fact(5m, null), Fact(1m, 0m));
        PnlMetrics effective = result.Metrics.EffectiveNet;
        Assert.Null(effective.Total);
        Assert.Equal(6m, effective.KnownSubtotal);
        Assert.True(effective.IsEstimated);
        Assert.Equal(new MetricCoverage(3, 2), effective.Coverage);
        Assert.Equal(1, effective.VerifiedTradeCount);
        Assert.Equal(1, effective.EstimatedTradeCount);
        Assert.Null(effective.WinRatePercent);
        Assert.Equal(ProfitFactorStatus.IncompleteCoverage, effective.ProfitFactor.Status);
        Assert.Null(Calculate(Fact(null, null)).Metrics.EffectiveNet.KnownSubtotal);
    }

    [Fact]
    public void EstimatesDoNotCrossCurrencyBoundaries()
    {
        var result = DashboardMetricCalculator.Calculate([Fact(-285m, null), Fact(5m, 0m) with { Currency = "EUR" }]);
        Assert.True(Assert.Single(result.Currencies, c => c.Currency == "USD").Metrics.EffectiveNet.IsEstimated);
        PnlMetrics eur = Assert.Single(result.Currencies, c => c.Currency == "EUR").Metrics.EffectiveNet;
        Assert.False(eur.IsEstimated);
        Assert.Equal(5m, eur.Total);
    }

    [Fact]
    public void PartialKnownCostComponentsStayUntouchedAndAreNotPartiallyDeductedFromEstimate()
    {
        DateTimeOffset time = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        Guid id = Guid.NewGuid();
        Trade trade = Trade.Start(Guid.NewGuid(), Guid.NewGuid(), new(1m, "USD"),
            new(id, 1, time, ExecutionSide.Buy, 1m, 100m, 2m, null, null, null, null), time);
        trade.AddExecution(new(id, 2, time, ExecutionSide.Sell, 1m, 110m, 0m, 0m, null, null, null), time);
        ClosedTradeMetrics result = Calculate(TradeAnalyticsFact.FromTrade(trade)).Metrics;
        Assert.Equal(10m, result.EffectiveNet.Total);
        Assert.True(result.EffectiveNet.IsEstimated);
        Assert.Null(trade.NetPnL);
        Assert.Null(trade.TotalCosts);
        Assert.Null(trade.Executions[0].Fees);
        Assert.Equal(2m, trade.Executions[0].Commission);
    }

    [Fact]
    public void KnownTopstepEconomicsAndKnownZeroCostsAreNotEstimates()
    {
        foreach (var (gross, costs) in new[] { (5m, 2.44m), (0m, 0m) })
        {
            ClosedTradeMetrics result = Calculate(Fact(gross, costs)).Metrics;
            Assert.Equal(gross - costs, result.Net.Total);
            Assert.Equal(result.Net.Total, result.EffectiveNet.Total);
            Assert.False(result.EffectiveNet.IsEstimated);
            Assert.Equal(1, result.EffectiveNet.VerifiedTradeCount);
        }
        Assert.Empty(DashboardMetricCalculator.Calculate([]).Currencies);
    }

    private static CurrencyTradeMetrics Calculate(params TradeAnalyticsFact[] facts) =>
        Assert.Single(DashboardMetricCalculator.Calculate(facts).Currencies);
    private static TradeAnalyticsFact Fact(decimal? gross, decimal? costs, int day = 1) =>
        new(Guid.NewGuid(), TradeStatus.Closed, new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new(2026, 1, day, 12, 0, 0, TimeSpan.Zero), "USD", null, gross, costs, gross - costs);
}
