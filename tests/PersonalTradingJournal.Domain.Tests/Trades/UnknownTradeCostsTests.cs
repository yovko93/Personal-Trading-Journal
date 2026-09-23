using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Domain.Tests.Trades;

public sealed class UnknownTradeCostsTests
{
    private static readonly DateTimeOffset OpenedAt =
        new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<decimal?, decimal?> UnknownCostCases => new()
    {
        { null, 1m },
        { 1m, null },
        { null, null },
    };

    [Theory]
    [MemberData(nameof(UnknownCostCases))]
    public void ExecutionTotalCostsIsUnknownWhenEitherComponentIsUnknown(
        decimal? commission,
        decimal? fees)
    {
        TradeExecution execution = Execution(
            Guid.NewGuid(), 1, ExecutionSide.Buy, commission, fees);

        Assert.Null(execution.TotalCosts);
    }

    [Fact]
    public void TradeCostsAndNetPnlRemainUnknownWhenAnyExecutionCostIsUnknown()
    {
        Guid tradeId = Guid.NewGuid();
        Trade trade = Trade.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new TradePricingSnapshot(2m, "USD"),
            Execution(tradeId, 1, ExecutionSide.Buy, null, null),
            OpenedAt);
        trade.AddExecution(
            Execution(tradeId, 2, ExecutionSide.Sell, 0m, 0m),
            OpenedAt.AddMinutes(1));

        Assert.Equal(2m, trade.GrossPnL);
        Assert.Null(trade.TotalCosts);
        Assert.Null(trade.NetPnL);
    }

    [Fact]
    public void KnownZeroCostsRemainKnown()
    {
        Guid tradeId = Guid.NewGuid();
        Trade trade = Trade.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new TradePricingSnapshot(2m, "USD"),
            Execution(tradeId, 1, ExecutionSide.Buy, 0m, 0m),
            OpenedAt);
        trade.AddExecution(
            Execution(tradeId, 2, ExecutionSide.Sell, 0m, 0m),
            OpenedAt.AddMinutes(1));

        Assert.Equal(0m, trade.TotalCosts);
        Assert.Equal(2m, trade.NetPnL);
    }

    private static TradeExecution Execution(
        Guid tradeId,
        int sequence,
        ExecutionSide side,
        decimal? commission,
        decimal? fees) =>
        new(
            tradeId,
            sequence,
            OpenedAt.AddMinutes(sequence - 1),
            side,
            1m,
            side == ExecutionSide.Buy ? 100m : 101m,
            commission,
            fees,
            $"fill-{sequence}",
            null,
            "MNQU6");
}
