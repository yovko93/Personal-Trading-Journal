using System.Text.Json;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.DailyReview;

public sealed class DailyReviewStatisticsCalculatorTests
{
    private static readonly DailyReviewQuery Query = new(new(2026, 10, 8));
    private static readonly Guid Account = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Fact]
    public void SharedFormulasAndProvenanceUseClosedPopulationIncludingBreakEvens()
    {
        var trades = new[] { Trade(100), Trade(-40), Trade(0), Trade(20) };
        var result = Calculate(trades);
        var currency = Assert.Single(result.Currencies);
        var gross = currency.Gross;
        Assert.Equal(4, result.Population.Closed.Count);
        Assert.Equal(80m, gross.Metrics.Total);
        Assert.Equal(50m, gross.Metrics.WinRatePercent);
        Assert.Equal(60m, gross.Metrics.AverageWin.Value);
        Assert.Equal(40m, gross.Metrics.AverageLoss.Value);
        Assert.Equal(3m, gross.Metrics.ProfitFactor.Value);
        Assert.Equal(new[] { trades[0].TradeId, trades[3].TradeId }.Order(), gross.Wins.TradeIds);
        Assert.Equal(trades[1].TradeId, Assert.Single(gross.Losses.TradeIds));
        Assert.Equal(trades[2].TradeId, Assert.Single(gross.BreakEvens.TradeIds));
        Assert.Equal(result.Population.Closed.TradeIds, gross.Known.TradeIds);
        Assert.Empty(gross.Unavailable.TradeIds);
        Assert.Equal(gross.Metrics with { Basis = PnlBasis.Net }, currency.Net.Metrics);
    }

    [Fact]
    public void GrossWinCanBeVerifiedNetLossWithoutRecomputingEconomics()
    {
        var trade = Trade(10, costs: 15);
        var result = Assert.Single(Calculate(trade).Currencies);
        Assert.Equal(1, result.Gross.Wins.Count);
        Assert.Equal(1, result.Net.Losses.Count);
        Assert.Equal(-5m, result.Net.Metrics.Total);
        Assert.Equal(0m, result.Net.Metrics.WinRatePercent);
        Assert.Equal(5m, result.Net.Metrics.AverageLoss.Value);
    }

    [Fact]
    public void UnknownCostsNeverSupplyCompleteNetOrKnownSubsetRatios()
    {
        var known = Trade(100);
        var commission = Trade(-285, unknownCommission: true);
        var fees = Trade(20, unknownFees: true);
        var both = Trade(0, unknownCommission: true, unknownFees: true);
        var result = Calculate(known, commission, fees, both);
        var currency = Assert.Single(result.Currencies);
        Assert.Equal(-165m, currency.Gross.Metrics.Total);
        Assert.Equal(2, result.Population.UnknownCommissions.Count);
        Assert.Equal(2, result.Population.UnknownFees.Count);
        Assert.Equal(3, result.Population.ClosedUnknownCosts.Count); // union, not 2+2
        Assert.Equal(MetricCoverageStatus.Partial, currency.Net.Metrics.Coverage.Status);
        Assert.Equal(100m, currency.Net.Metrics.KnownSubtotal);
        Assert.Null(currency.Net.Metrics.Total);
        Assert.Null(currency.Net.Metrics.WinRatePercent);
        Assert.Null(currency.Net.Metrics.AverageWin.Value);
        Assert.Null(currency.Net.Metrics.ProfitFactor.Value);
        Assert.Equal(3, currency.Net.Unavailable.Count);
        Assert.False(currency.Net.Metrics.IsEstimated);
    }

    [Fact]
    public void MissingCostComponentsBlockEvenAnInconsistentNumericNet()
    {
        var trade = Trade(20);
        var missing = trade with { Executions = [trade.Executions[0] with { Commission = null }] };
        var result = Assert.Single(Calculate(missing).Currencies);
        Assert.Equal(20m, result.Gross.Metrics.Total);
        Assert.Null(result.Net.Metrics.Total);
        Assert.Equal(1, result.Population.UnknownCommissions.Count);
        Assert.Equal(1, result.Population.ClosedUnknownCosts.Count);
    }

    [Theory]
    [InlineData(DailyReviewTradeQuality.UnknownGrossPnL)]
    [InlineData(DailyReviewTradeQuality.UnsupportedProjectionVersion)]
    [InlineData(DailyReviewTradeQuality.MissingProjection)]
    public void UnavailableGrossPreservesClosedDenominator(DailyReviewTradeQuality quality)
    {
        var a = Trade(3);
        var b = Trade(8) with { Quality = quality };
        var result = Assert.Single(Calculate(a, b).Currencies);
        Assert.Equal(2, result.Population.Closed.Count);
        Assert.Null(result.Gross.Metrics.Total);
        Assert.Equal(3m, result.Gross.Metrics.KnownSubtotal);
        Assert.Equal(b.TradeId, Assert.Single(result.Gross.Unavailable.TradeIds));
        Assert.Equal(b.TradeId, Assert.Single(Assert.Single(result.Population.SourceQuality).Trades.TradeIds));
        Assert.Null(result.Net.Metrics.Total);
    }

    [Fact]
    public void NullEconomicsAndMissingExecutionsAreNotZeros()
    {
        var trade = Trade(0);
        var unknown = trade with { Facts = trade.Facts! with { GrossPnL = null, NetPnL = null }, Executions = [] };
        var result = Assert.Single(Calculate(unknown).Currencies);
        Assert.Equal(MetricCoverageStatus.Unavailable, result.Gross.Metrics.Coverage.Status);
        Assert.Null(result.Gross.Metrics.KnownSubtotal);
        Assert.Equal(0, result.Gross.BreakEvens.Count);
        Assert.Equal(1, result.Population.UnknownCommissions.Count);
        Assert.Equal(1, result.Population.UnknownFees.Count);
        Assert.Null(result.Net.Metrics.Total);
    }

    [Fact]
    public void EmptyAndContextOnlyDaysHaveNoRealizedResults()
    {
        var empty = Calculate();
        Assert.Equal(0, empty.Population.Closed.Count);
        Assert.Empty(empty.Currencies);
        var trade = Trade(10);
        var open = trade with { Inclusion = DailyReviewTradeInclusion.OpenActivityOnDate,
            Facts = trade.Facts! with { Status = TradeStatus.Open, ClosedAtUtc = null, GrossPnL = null, NetPnL = null, OpenQuantity = 1 } };
        var unavailable = Trade(3) with { Inclusion = DailyReviewTradeInclusion.UnavailableLifecycleActivityOnDate,
            Facts = null, Quality = DailyReviewTradeQuality.MissingProjection };
        var result = Calculate(open, unavailable);
        Assert.Equal(0, result.Population.Closed.Count);
        Assert.Equal(open.TradeId, Assert.Single(result.Population.ExcludedOpenActivity.TradeIds));
        Assert.Equal(unavailable.TradeId, Assert.Single(result.Population.ExcludedUnavailableLifecycle.TradeIds));
        var currency = Assert.Single(result.Currencies);
        Assert.Equal(MetricCoverageStatus.Empty, currency.Gross.Metrics.Coverage.Status);
        Assert.Equal(ProfitFactorStatus.NoTrades, currency.Net.Metrics.ProfitFactor.Status);
        Assert.Null(currency.Gross.Metrics.Total);
    }

    [Theory]
    [InlineData(0, ProfitFactorStatus.AllBreakEven, AveragePnlStatus.NoWins, AveragePnlStatus.NoLosses)]
    [InlineData(5, ProfitFactorStatus.NoLosses, AveragePnlStatus.Defined, AveragePnlStatus.NoLosses)]
    [InlineData(-5, ProfitFactorStatus.Defined, AveragePnlStatus.NoWins, AveragePnlStatus.Defined)]
    public void ZeroAndOneSidedOutcomesHaveExplicitUndefinedStates(int amount, ProfitFactorStatus factor,
        AveragePnlStatus win, AveragePnlStatus loss)
    {
        var metric = Assert.Single(Calculate(Trade(amount)).Currencies).Net.Metrics;
        Assert.Equal(amount, metric.Total);
        Assert.Equal(factor, metric.ProfitFactor.Status);
        Assert.Equal(win, metric.AverageWin.Status);
        Assert.Equal(loss, metric.AverageLoss.Status);
        Assert.Equal(amount > 0 ? 100m : 0m, metric.WinRatePercent);
        if (amount == 0) Assert.Equal(1, metric.KnownBreakEvens);
    }

    [Fact]
    public void CurrencyAndAccountPartitionsRetainIdsAndDeterministicOrdering()
    {
        var trades = new[] { Trade(100, account: Other), Trade(10, currency: "EUR"), Trade(-40),
            Trade(8, account: Other, currency: "EUR") };
        var a = Calculate(trades);
        var b = Calculate(trades.Reverse().ToArray());
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.Equal(new[] { "EUR", "USD" }, a.Currencies.Select(c => c.Currency));
        var usd = a.Currencies[1];
        Assert.Equal(60m, usd.Gross.Metrics.Total);
        Assert.Equal(new[] { Account, Other }, usd.Accounts.Select(g => g.Account.Id));
        Assert.Equal(-40m, usd.Accounts[0].Gross.Metrics.Total);
        Assert.Equal(100m, usd.Accounts[1].Gross.Metrics.Total);
        Assert.Equal(trades[0].TradeId, Assert.Single(usd.Accounts[1].Gross.Known.TradeIds));
        Assert.Equal(trades.Select(t => t.TradeId).Order(), a.Population.Closed.TradeIds);
    }

    [Fact]
    public void PrecisionAndLargeNegativeAmountsAreNotRounded()
    {
        var result = Assert.Single(Calculate(Trade(0.1234567890123456789012345678m),
            Trade(-1000000000000000000m)).Currencies);
        Assert.Equal(0.1234567890123456789012345678m, result.Gross.Metrics.AverageWin.Value);
        Assert.Equal(1000000000000000000m, result.Gross.Metrics.AverageLoss.Value);
        Assert.Equal(0.1234567890123456789012345678m - 1000000000000000000m, result.Gross.Metrics.Total);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OverflowFailsExplicitlyWithoutPartialResult(bool negative)
    {
        Assert.Throws<OverflowException>(() => Calculate(Trade(negative ? decimal.MinValue : decimal.MaxValue),
            Trade(negative ? -1m : 1m)));
    }

    [Fact]
    public void DuplicateWrongAccountOrWrongClosureDateIsRejectedAndCancellationHonored()
    {
        var trade = Trade(1);
        Assert.Throws<ArgumentException>(() => Calculate(trade, trade));
        Assert.Throws<ArgumentException>(() => DailyReviewStatisticsCalculator.Calculate(
            new(new(Query.Date, Other), [trade], [])));
        Assert.Throws<ArgumentException>(() => Calculate(trade with { Facts = trade.Facts! with { ClosedAtUtc = Query.BeforeUtc } }));
        Assert.Throws<ArgumentException>(() => Calculate(trade with { Facts = trade.Facts! with { ClosedAtUtc = Query.FromUtc.AddTicks(-1) } }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => DailyReviewStatisticsCalculator.Calculate(
            new(Query, [trade], []), cancellation.Token));
    }

    private static DailyReviewStatistics Calculate(params DailyReviewTradeEvidence[] trades) =>
        DailyReviewStatisticsCalculator.Calculate(new(Query, trades, []));

    private static DailyReviewTradeEvidence Trade(decimal gross, decimal costs = 0, Guid? account = null,
        string currency = "USD", bool unknownCommission = false, bool unknownFees = false)
    {
        var close = Query.FromUtc.AddHours(12);
        var unknown = unknownCommission || unknownFees;
        var facts = new DailyReviewTradeFacts(1, TradeStatus.Closed, TradeDirection.Long, Query.FromUtc.AddDays(-1),
            close, 0, 100, 100, unknown ? null : costs, gross, unknown ? null : gross - costs);
        var execution = new TradeExecutionDetailItem(Guid.NewGuid(), 1, close, ExecutionSide.Sell, 1, 100,
            unknownCommission ? null : costs, unknownFees ? null : 0, unknown ? null : costs, null, null, null);
        return new(Guid.NewGuid(), new(account ?? Account, "Account", true), new(Guid.NewGuid(), "Instrument", true),
            null, currency, 1, close, close, DailyReviewTradeInclusion.ClosedOnDate, facts, [execution], [],
            (unknownCommission ? DailyReviewTradeQuality.UnknownCommission : 0) |
            (unknownFees ? DailyReviewTradeQuality.UnknownFees : 0));
    }
}
