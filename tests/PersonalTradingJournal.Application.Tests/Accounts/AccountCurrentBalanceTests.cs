using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Accounts;

public sealed class AccountCurrentBalanceTests
{
    [Theory]
    [InlineData(20, 2, 3, 1015, 1)]
    [InlineData(-20, 2, 3, 975, -1)]
    [InlineData(5, 2, 3, 1000, 0)]
    [InlineData(0, 0, 0, 1000, 0)]
    [InlineData(20, null, 3, 1017, 1)]
    [InlineData(-20, 2, null, 978, -1)]
    [InlineData(0, null, null, 1000, 0)]
    public void IndividualKnownCostsAreDeductedButUnknownsRemainExplicit(int gross, int? commission, int? fee, int expected, int comparison)
    {
        var result = Calculate(1000, Fact(gross, new(commission, fee)));
        Assert.Equal(expected, result.Value);
        Assert.Equal(comparison, result.Comparison);
        Assert.Equal(commission is null ? 1 : 0, result.UnknownCommissionCount);
        Assert.Equal(fee is null ? 1 : 0, result.UnknownFeeCount);
        Assert.Equal(commission is null || fee is null, result.IsEstimated);
    }

    [Fact]
    public void EveryEntryAndExitComponentIsIncludedWithoutRounding()
    {
        var trade = Fact(10.123456789m, new(null, .1m)) with { Executions = [new(null, .1m), new(.2m, null)] };
        var result = Calculate(1000, trade);
        Assert.Equal(1009.823456789m, result.Value);
        Assert.Equal(1, result.TradesWithUnknownCosts);
        Assert.Equal(1, result.UnknownCommissionCount); Assert.Equal(1, result.UnknownFeeCount);
    }

    [Fact]
    public void NoTradesAndOnlyOpenOrOtherCurrencyTradesKeepStartingBalance()
    {
        Assert.Equal(1000, Calculate(1000).Value);
        var result = Calculate(1000, Fact(999, new(1, 1)) with { Currency = "EUR" },
            Fact(null, new(null, null)) with { Status = TradeStatus.Open });
        Assert.Equal(1000, result.Value); Assert.False(result.IsEstimated);
        Assert.Equal(0, result.ClosedTradeCount); Assert.Equal(1, result.OtherCurrencyTradeCount); Assert.Equal(1, result.OpenTradeCount);
    }

    [Fact]
    public void OverflowDoesNotWrapOrReturnAPartialBalance()
    {
        Assert.Equal(AccountBalanceUnavailable.Overflow, Calculate(decimal.MaxValue, Fact(1, new(0, 0))).Unavailable);
        var negative = Calculate(decimal.MinValue, Fact(0, new(1, 0)));
        Assert.Null(negative.Value); Assert.Equal(AccountBalanceUnavailable.Overflow, negative.Unavailable);
        Assert.Equal(-10000000000000000000m, Calculate(0, Fact(-10000000000000000000m, new(0, 0))).Value);
    }

    [Fact]
    public void AbsentStartingBalanceOrMalformedEconomicsAreNotInvented()
    {
        Assert.Equal(AccountBalanceUnavailable.StartingBalanceMissing, Calculate(null).Unavailable);
        Assert.Equal(AccountBalanceUnavailable.IncompleteTrade, Calculate(1000, Fact(null, new(0, 0))).Unavailable);
        Assert.Equal(AccountBalanceUnavailable.IncompleteTrade, Calculate(1000, Fact(10, new(0, 0)) with { Executions = [] }).Unavailable);
        Assert.Equal(AccountBalanceUnavailable.IncompleteTrade, Calculate(1000, Fact(10, new(0, 0)) with { Status = null }).Unavailable);
    }

    [Fact]
    public void CancellationAndOrderingAreDeterministic()
    {
        var a = Fact(1.23456789m, new(null, 0)); var b = Fact(-.001m, new(0, 0));
        Assert.Equal(Calculate(1000, a, b), Calculate(1000, b, a));
        Assert.ThrowsAny<OperationCanceledException>(() => AccountCurrentBalanceCalculator.Calculate(0, "USD", [], new(true)));
    }

    private static AccountBalanceTrade Fact(decimal? gross, AccountBalanceCost cost) => new(Guid.NewGuid(), "USD", TradeStatus.Closed, gross, [cost]);
    private static AccountCurrentBalance Calculate(decimal? start, params AccountBalanceTrade[] trades) => AccountCurrentBalanceCalculator.Calculate(start, "USD", trades);
}
