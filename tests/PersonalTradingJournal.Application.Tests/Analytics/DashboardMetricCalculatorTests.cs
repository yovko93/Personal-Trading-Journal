using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Analytics;

public sealed class DashboardMetricCalculatorTests
{
    private static readonly DateTimeOffset Opened = new(2026, 1, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Closed = Opened.AddHours(1);

    [Fact]
    public void UnknownCostsNeverBecomeZeroOrGrossFallback()
    {
        CurrencyTradeMetrics result = Single(Fact(5m, null), Fact(10m, 2m));

        Assert.Equal(15m, result.Metrics.Gross.Total);
        Assert.Equal(MetricCoverageStatus.Complete, result.Metrics.Gross.Coverage.Status);
        Assert.Equal(1, result.Metrics.UnknownCostTradeCount);
        PnlMetrics net = result.Metrics.Net;
        Assert.Equal(PnlBasis.Net, net.Basis);
        Assert.Equal(MetricCoverageStatus.Partial, net.Coverage.Status);
        Assert.Equal(2, net.Coverage.ClosedTradeCount);
        Assert.Equal(1, net.Coverage.KnownTradeCount);
        Assert.Equal(1, net.Coverage.UnknownTradeCount);
        Assert.Equal(8m, net.KnownSubtotal);
        Assert.Null(net.Total);
        Assert.Null(net.WinRatePercent);
        Assert.Equal(new ProfitFactorMetric(ProfitFactorStatus.IncompleteCoverage, null), net.ProfitFactor);
    }

    [Fact]
    public void AllUnknownIsDifferentFromKnownZeroAndEmpty()
    {
        PnlMetrics unknown = Single(Fact(0m, null)).Metrics.Net;
        PnlMetrics zero = Single(Fact(0m, 0m)).Metrics.Net;
        Assert.Equal(MetricCoverageStatus.Unavailable, unknown.Coverage.Status);
        Assert.Null(unknown.Total);
        Assert.Null(unknown.KnownSubtotal);
        Assert.Null(unknown.KnownProfitSum);
        Assert.Null(unknown.KnownLossMagnitude);
        Assert.Equal(0, unknown.KnownBreakEvens);
        Assert.Equal(MetricCoverageStatus.Complete, zero.Coverage.Status);
        Assert.Equal(0m, zero.Total);
        Assert.Equal(0m, zero.KnownSubtotal);
        Assert.Equal(1, zero.KnownBreakEvens);
        Assert.Equal(0m, zero.WinRatePercent);
        Assert.Equal(new ProfitFactorMetric(ProfitFactorStatus.AllBreakEven, null), zero.ProfitFactor);

        DashboardAnalyticsSnapshot empty = DashboardMetricCalculator.Calculate([]);
        Assert.Equal(0, empty.SelectedTradeCount);
        Assert.Equal(0, empty.ExcludedOpenTradeCount);
        Assert.Empty(empty.Currencies);
    }

    [Fact]
    public void WinRateIncludesBreakEvensAndProfitFactorUsesProfitOverAbsoluteLoss()
    {
        PnlMetrics result = Single(Fact(10m), Fact(-5m), Fact(0m)).Metrics.Net;
        Assert.Equal(5m, result.Total);
        Assert.Equal(1, result.KnownWins);
        Assert.Equal(1, result.KnownLosses);
        Assert.Equal(1, result.KnownBreakEvens);
        Assert.Equal(100m / 3m, result.WinRatePercent);
        Assert.Equal(10m, result.KnownProfitSum);
        Assert.Equal(5m, result.KnownLossMagnitude);
        Assert.Equal(new ProfitFactorMetric(ProfitFactorStatus.Defined, 2m), result.ProfitFactor);
    }

    [Theory]
    [InlineData(10, ProfitFactorStatus.NoLosses, null, 100)]
    [InlineData(-10, ProfitFactorStatus.Defined, 0, 0)]
    [InlineData(0, ProfitFactorStatus.AllBreakEven, null, 0)]
    public void ZeroDenominatorsAreExplicit(int pnl, ProfitFactorStatus status, int? factor, int winRate)
    {
        PnlMetrics result = Single(Fact(pnl)).Metrics.Net;
        Assert.Equal(status, result.ProfitFactor.Status);
        Assert.Equal(factor.HasValue ? (decimal?)factor.Value : null, result.ProfitFactor.Value);
        Assert.Equal((decimal)winRate, result.WinRatePercent);
    }

    [Fact]
    public void GrossAndNetOutcomesCanHaveOppositeSignsWithoutBeingMixed()
    {
        ClosedTradeMetrics result = Single(Fact(5m, 10m)).Metrics;
        Assert.Equal(PnlBasis.Gross, result.Gross.Basis);
        Assert.Equal(5m, result.Gross.Total);
        Assert.Equal(100m, result.Gross.WinRatePercent);
        Assert.Equal(-5m, result.Net.Total);
        Assert.Equal(0m, result.Net.WinRatePercent);
        Assert.Equal(0m, result.Net.ProfitFactor.Value);
    }

    [Fact]
    public void KnownSubsetThatSumsToZeroIsStillNotACompleteNetTotal()
    {
        PnlMetrics result = Single(Fact(10m), Fact(-10m), Fact(7m, null)).Metrics.Net;
        Assert.Equal(0m, result.KnownSubtotal);
        Assert.Null(result.Total);
        Assert.Null(result.WinRatePercent);
        Assert.Equal(1, result.Coverage.UnknownTradeCount);
    }

    [Fact]
    public void MissingGrossIsUnavailableRatherThanAnInventedZero()
    {
        ClosedTradeMetrics result = Single(Fact(5m) with { GrossPnL = null, NetPnL = null }).Metrics;
        Assert.Equal(MetricCoverageStatus.Unavailable, result.Gross.Coverage.Status);
        Assert.Null(result.Gross.Total);
        Assert.Equal(MetricCoverageStatus.Unavailable, result.Net.Coverage.Status);
        Assert.Equal(0, result.UnknownCostTradeCount);
    }

    [Fact]
    public void CurrenciesHaveIndependentTotalsCoverageDaysAndSetups()
    {
        DashboardAnalyticsSnapshot result = DashboardMetricCalculator.Calculate([
            Fact(10m), Fact(5m, null), Fact(100m) with { Currency = "EUR" }]);
        Assert.Equal(2, result.Currencies.Count);
        CurrencyTradeMetrics eur = result.Currencies[0];
        CurrencyTradeMetrics usd = result.Currencies[1];
        Assert.Equal("EUR", eur.Currency);
        Assert.Equal(100m, eur.Metrics.Net.Total);
        Assert.Equal("USD", usd.Currency);
        Assert.Equal(15m, usd.Metrics.Gross.Total);
        Assert.Null(usd.Metrics.Net.Total);
        Assert.Equal(100m, Assert.Single(eur.Days).Metrics.Net.Total);
        Assert.Null(Assert.Single(usd.Days).Metrics.Net.Total);
    }

    [Fact]
    public void OpenTradeIncludingPartialExitContributesToNoClosedMetric()
    {
        Trade trade = StartTrade(2m, 100m, 0m, 0m);
        trade.AddExecution(Execution(trade.Id, 2, ExecutionSide.Sell, 1m, 105m, 0m, 0m), Closed);
        Assert.Equal(1m, trade.OpenQuantity);
        Assert.Equal(105m, trade.AverageExitPrice);
        TradeAnalyticsFact fact = TradeAnalyticsFact.FromTrade(trade);
        Assert.Null(fact.GrossPnL);
        Assert.Null(fact.NetPnL);

        CurrencyTradeMetrics result = Single(fact);
        Assert.Equal(1, result.ExcludedOpenTradeCount);
        Assert.Equal(0, result.Metrics.ClosedTradeCount);
        Assert.Equal(MetricCoverageStatus.Empty, result.Metrics.Net.Coverage.Status);
        Assert.Null(result.Metrics.Net.Total);
        Assert.Equal(ProfitFactorStatus.NoTrades, result.Metrics.Net.ProfitFactor.Status);
        Assert.Empty(result.Days);
        Assert.Empty(result.Setups);
        Assert.Equal(10m, Single(fact, Fact(10m)).Metrics.Net.Total);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(0, null)]
    [InlineData(null, null)]
    [InlineData(0, 0)]
    public void DomainCostCompletenessIncludingExplicitZeroIsPreserved(int? commission, int? fees)
    {
        Trade trade = StartTrade(2m, 20123.125m, commission, fees);
        trade.AddExecution(Execution(trade.Id, 2, ExecutionSide.Sell, 2m, 20124.375m, 0m, 0m), Closed);
        ClosedTradeMetrics result = Single(TradeAnalyticsFact.FromTrade(trade)).Metrics;
        Assert.Equal(5m, result.Gross.Total);
        bool costsKnown = commission.HasValue && fees.HasValue;
        Assert.Equal(costsKnown ? 5m : (decimal?)null, result.Net.Total);
        Assert.Equal(costsKnown ? 0 : 1, result.UnknownCostTradeCount);
    }

    [Fact]
    public void UsesHistoricalDomainEconomicsWithoutRoundingWeightedPrices()
    {
        Trade trade = StartTrade(1m, 100.123456789m, 0.01m, 0.02m);
        trade.AddExecution(Execution(trade.Id, 2, ExecutionSide.Buy, 2m, 100.333333333m, 0m, 0m), Closed);
        trade.AddExecution(Execution(trade.Id, 3, ExecutionSide.Sell, 3m, 101.111111111m, 0m, 0m), Closed.AddMinutes(1));
        TradeAnalyticsFact fact = TradeAnalyticsFact.FromTrade(trade);
        ClosedTradeMetrics result = Single(fact).Metrics;
        Assert.Equal("USD", fact.Currency);
        Assert.Equal(trade.GrossPnL, result.Gross.Total);
        Assert.Equal(trade.NetPnL, result.Net.Total);
        Assert.NotEqual(decimal.Round(trade.GrossPnL!.Value, 2), result.Gross.Total);
        Assert.Equal(trade.ClosedAtUtc, fact.ClosedAtUtc);
    }

    [Fact]
    public void SetupGroupingUsesCurrentIdentityAndIncludesUnclassifiedTrades()
    {
        Guid setup = Guid.NewGuid();
        CurrencyTradeMetrics result = Single(Fact(10m) with { TradingSetupId = setup },
            Fact(5m, null) with { TradingSetupId = setup }, Fact(-3m));
        Assert.Equal(2, result.Setups.Count);
        SetupTradeMetrics classified = Assert.Single(result.Setups, s => s.TradingSetupId == setup);
        Assert.Equal(2, classified.Metrics.ClosedTradeCount);
        Assert.Equal(15m, classified.Metrics.Gross.Total);
        Assert.Null(classified.Metrics.Net.Total);
        Assert.Equal(-3m, Assert.Single(result.Setups, s => s.TradingSetupId is null).Metrics.Net.Total);
    }

    [Fact]
    public void DailyBucketsUseClosureNotOpeningAndCarryTheirOwnCoverage()
    {
        TradeAnalyticsFact first = Fact(10m) with { ClosedAtUtc = new(2026, 1, 15, 4, 59, 59, TimeSpan.Zero) };
        TradeAnalyticsFact second = Fact(5m, null) with { ClosedAtUtc = new(2026, 1, 15, 5, 0, 0, TimeSpan.Zero) };
        CurrencyTradeMetrics result = Single(second, first);
        Assert.Equal(new DateOnly(2026, 1, 14), result.Days[0].NewYorkDate);
        Assert.Equal(10m, result.Days[0].Metrics.Net.Total);
        Assert.Equal(new DateOnly(2026, 1, 15), result.Days[1].NewYorkDate);
        Assert.Null(result.Days[1].Metrics.Net.Total);
        Assert.Null(result.Metrics.Net.Total);
    }

    [Theory]
    [InlineData("2026-01-15T04:59:59Z", "2026-01-14")]
    [InlineData("2026-01-15T05:00:00Z", "2026-01-15")]
    [InlineData("2026-07-15T03:59:59Z", "2026-07-14")]
    [InlineData("2026-07-15T04:00:00Z", "2026-07-15")]
    [InlineData("2026-03-08T06:59:59Z", "2026-03-08")]
    [InlineData("2026-03-08T07:00:00Z", "2026-03-08")]
    [InlineData("2026-11-01T05:30:00Z", "2026-11-01")]
    [InlineData("2026-11-01T06:30:00Z", "2026-11-01")]
    public void NewYorkDateHandlesMidnightAndBothDstTransitions(string instant, string expectedDate)
    {
        DateTimeOffset utc = DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(DateOnly.Parse(expectedDate, System.Globalization.CultureInfo.InvariantCulture),
            DashboardMetricCalculator.GetNewYorkCloseDate(utc));
        Assert.Equal(TimeSpan.Zero, utc.Offset);
    }

    [Fact]
    public void AverageRHasAnExplicitUnavailableReasonEvenForKnownPnl()
    {
        foreach (var facts in new[] { Array.Empty<TradeAnalyticsFact>(), new[] { Fact(10m) } })
        {
            DashboardAnalyticsSnapshot result = DashboardMetricCalculator.Calculate(facts);
            Assert.Null(result.AverageR);
            Assert.Equal(AverageRUnavailableReason.AuthoritativeInitialRiskNotRecorded, result.AverageRReason);
        }
    }

    [Fact]
    public void DuplicateFactsCannotDoubleCountTrades()
    {
        TradeAnalyticsFact fact = Fact(10m);
        Assert.Throws<ArgumentException>(() => DashboardMetricCalculator.Calculate([fact, fact]));
    }

    [Fact]
    public void InvalidProjectionCannotClaimKnownNetWithUnknownCosts()
    {
        Assert.Throws<ArgumentException>(() => Single(Fact(10m) with { TotalCosts = null }));
        Assert.Throws<ArgumentException>(() => Single(Fact(10m) with { ClosedAtUtc = null }));
        Assert.Throws<ArgumentException>(() => Single(Fact(10m) with { Currency = " usd " }));
        Assert.Throws<ArgumentException>(() => Single(Fact(10m) with { ClosedAtUtc = Closed.ToOffset(TimeSpan.FromHours(1)) }));
        Assert.Throws<ArgumentException>(() => DashboardMetricCalculator.GetNewYorkCloseDate(Closed.ToOffset(TimeSpan.FromHours(1))));
    }

    [Fact]
    public void OverflowIsNotSilentlyRoundedOrWrapped()
    {
        Assert.Throws<OverflowException>(() => Single(Fact(decimal.MaxValue), Fact(1m)));
    }

    [Fact]
    public void CancellationPropagatesBeforeOrDuringEnumeration()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => DashboardMetricCalculator.Calculate([], cancelled.Token));
        using var during = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => DashboardMetricCalculator.Calculate(Facts(), during.Token));
        IEnumerable<TradeAnalyticsFact> Facts()
        {
            yield return Fact(1m);
            during.Cancel();
            yield return Fact(2m);
        }
    }

    private static CurrencyTradeMetrics Single(params TradeAnalyticsFact[] facts) =>
        Assert.Single(DashboardMetricCalculator.Calculate(facts).Currencies);

    private static TradeAnalyticsFact Fact(decimal gross, decimal? costs = 0m) =>
        new(Guid.NewGuid(), TradeStatus.Closed, Opened, Closed, "USD", null, gross, costs, gross - costs);

    private static Trade StartTrade(decimal quantity, decimal price, decimal? commission, decimal? fees)
    {
        Guid id = Guid.NewGuid();
        return Trade.Start(Guid.NewGuid(), Guid.NewGuid(), new(2m, "USD"),
            new TradeExecution(id, 1, Opened, ExecutionSide.Buy, quantity, price, commission, fees, null, null, null), Opened);
    }

    private static TradeExecution Execution(Guid id, int sequence, ExecutionSide side, decimal quantity,
        decimal price, decimal? commission, decimal? fees) =>
        new(id, sequence, Closed.AddMinutes(sequence - 2), side, quantity, price, commission, fees, null, null, null);
}
