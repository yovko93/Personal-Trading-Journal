using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Domain.Tests.Trades;

public sealed class TradeCorrectionTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CorrectDetailsReplacesImmutableFactsAndRecalculatesEconomics()
    {
        Trade trade = CreateTrade(hasExit: true);
        Guid newAccountId = Guid.NewGuid();
        Guid newInstrumentId = Guid.NewGuid();
        Guid setupId = Guid.NewGuid();
        DateTimeOffset updatedAt = CreatedAt.AddHours(3);
        TradeExecution entry = Corrected(
            trade.Executions[0],
            1,
            ExecutionSide.Sell,
            2m,
            100m,
            1m,
            2m);
        TradeExecution exit = Corrected(
            trade.Executions[1],
            2,
            ExecutionSide.Buy,
            2m,
            90m,
            3m,
            4m);

        bool changed = trade.CorrectDetails(
            newAccountId,
            newInstrumentId,
            new TradePricingSnapshot(50m, "eur"),
            setupId,
            [entry, exit],
            updatedAt);

        Assert.True(changed);
        Assert.Equal(newAccountId, trade.TradingAccountId);
        Assert.Equal(newInstrumentId, trade.InstrumentId);
        Assert.Equal(setupId, trade.TradingSetupId);
        Assert.Equal(50m, trade.Pricing.PointValue);
        Assert.Equal("EUR", trade.Pricing.Currency);
        Assert.Equal(TradeDirection.Short, trade.Direction);
        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(0m, trade.OpenQuantity);
        Assert.Equal(100m, trade.AverageEntryPrice);
        Assert.Equal(90m, trade.AverageExitPrice);
        Assert.Equal(10m, trade.TotalCosts);
        Assert.Equal(1000m, trade.GrossPnL);
        Assert.Equal(990m, trade.NetPnL);
        Assert.Equal(CreatedAt, trade.CreatedAtUtc);
        Assert.Equal(updatedAt, trade.UpdatedAtUtc);
        Assert.Equal("external-execution", trade.Executions[0].ExternalExecutionId);
        Assert.Equal("external-order", trade.Executions[0].ExternalOrderId);
        Assert.Equal("ESU6", trade.Executions[0].BrokerSymbol);
    }

    [Fact]
    public void CorrectDetailsCanKeepTradeOpenAndPreservesExecutionIdentity()
    {
        Trade trade = CreateTrade(hasExit: true);
        Guid entryId = trade.Executions[0].Id;

        bool changed = trade.CorrectDetails(
            trade.TradingAccountId,
            trade.InstrumentId,
            trade.Pricing,
            null,
            [Corrected(trade.Executions[0], 1, ExecutionSide.Buy, 3m, 101m, 1m, 0m)],
            CreatedAt.AddHours(4));

        Assert.True(changed);
        Assert.Equal(TradeStatus.Open, trade.Status);
        Assert.Equal(3m, trade.OpenQuantity);
        Assert.Null(trade.ClosedAtUtc);
        Assert.Equal(entryId, trade.Executions[0].Id);
    }

    [Fact]
    public void CorrectDetailsNoOpPreservesUpdatedTimestamp()
    {
        Trade trade = CreateTrade(hasExit: true);
        DateTimeOffset originalUpdatedAt = trade.UpdatedAtUtc;
        TradeExecution[] equivalent = trade.Executions
            .Select(execution => Corrected(
                execution,
                execution.Sequence,
                execution.Side,
                execution.Quantity,
                execution.Price,
                execution.Commission,
                execution.Fees))
            .ToArray();

        bool changed = trade.CorrectDetails(
            trade.TradingAccountId,
            trade.InstrumentId,
            new TradePricingSnapshot(
                trade.Pricing.PointValue,
                trade.Pricing.Currency),
            trade.TradingSetupId,
            equivalent,
            CreatedAt.AddDays(1));

        Assert.False(changed);
        Assert.Equal(originalUpdatedAt, trade.UpdatedAtUtc);
    }

    [Fact]
    public void InvalidReplacementIsAtomic()
    {
        Trade trade = CreateTrade(hasExit: false);
        Guid originalAccountId = trade.TradingAccountId;
        Guid originalInstrumentId = trade.InstrumentId;
        DateTimeOffset originalUpdatedAt = trade.UpdatedAtUtc;
        TradeExecution originalExecution = trade.Executions[0];
        TradeExecution overClose = TradeExecution.Rehydrate(
            Guid.NewGuid(),
            trade.Id,
            2,
            CreatedAt.AddHours(1),
            ExecutionSide.Sell,
            2m,
            101m,
            0m,
            0m,
            null,
            null,
            null);

        Assert.Throws<ArgumentException>(() => trade.CorrectDetails(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new TradePricingSnapshot(100m, "EUR"),
            Guid.NewGuid(),
            [originalExecution, overClose],
            CreatedAt.AddHours(2)));

        Assert.Equal(originalAccountId, trade.TradingAccountId);
        Assert.Equal(originalInstrumentId, trade.InstrumentId);
        Assert.Null(trade.TradingSetupId);
        Assert.Equal(originalUpdatedAt, trade.UpdatedAtUtc);
        Assert.Single(trade.Executions);
        Assert.Same(originalExecution, trade.Executions[0]);
    }

    [Fact]
    public void InvalidAuditTimestampDoesNotPartiallyMutateTrade()
    {
        Trade trade = CreateTrade(hasExit: false);
        Guid originalAccountId = trade.TradingAccountId;

        Assert.Throws<ArgumentOutOfRangeException>(() => trade.CorrectDetails(
            Guid.NewGuid(),
            trade.InstrumentId,
            trade.Pricing,
            null,
            trade.Executions,
            CreatedAt.AddMinutes(-1)));

        Assert.Equal(originalAccountId, trade.TradingAccountId);
        Assert.Equal(CreatedAt, trade.UpdatedAtUtc);
    }

    [Fact]
    public void CorrectDetailsCanSwitchShortTradeToLongAndClearSetup()
    {
        Trade trade = CreateTrade(hasExit: true);
        Guid setupId = Guid.NewGuid();
        TradeExecution shortEntry = Corrected(
            trade.Executions[0], 1, ExecutionSide.Sell, 1m, 105m, 2m, 1m);
        TradeExecution shortExit = Corrected(
            trade.Executions[1], 2, ExecutionSide.Buy, 1m, 100m, 2m, 1m);
        Assert.True(trade.CorrectDetails(
            trade.TradingAccountId,
            trade.InstrumentId,
            trade.Pricing,
            setupId,
            [shortEntry, shortExit],
            CreatedAt.AddHours(2)));

        TradeExecution longEntry = Corrected(
            trade.Executions[0], 1, ExecutionSide.Buy, 1m, 100m, 3m, 1m);
        TradeExecution longExit = Corrected(
            trade.Executions[1], 2, ExecutionSide.Sell, 1m, 110m, 4m, 1m);

        Assert.True(trade.CorrectDetails(
            trade.TradingAccountId,
            trade.InstrumentId,
            trade.Pricing,
            null,
            [longEntry, longExit],
            CreatedAt.AddHours(3)));
        Assert.Equal(TradeDirection.Long, trade.Direction);
        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Null(trade.TradingSetupId);
        Assert.Equal(9m, trade.TotalCosts);
        Assert.Equal(100m, trade.GrossPnL);
        Assert.Equal(91m, trade.NetPnL);
    }

    [Fact]
    public void InvalidExecutionSequenceDoesNotPartiallyMutateTrade()
    {
        Trade trade = CreateTrade(hasExit: true);
        Guid originalAccountId = trade.TradingAccountId;
        IReadOnlyList<TradeExecution> originalExecutions = trade.Executions.ToList();
        TradeExecution invalidFirst = Corrected(
            trade.Executions[0], 2, ExecutionSide.Buy, 1m, 100m, 1m, 0m);

        Assert.Throws<ArgumentException>(() => trade.CorrectDetails(
            Guid.NewGuid(),
            trade.InstrumentId,
            trade.Pricing,
            null,
            [invalidFirst, trade.Executions[1]],
            CreatedAt.AddHours(3)));

        Assert.Equal(originalAccountId, trade.TradingAccountId);
        Assert.Equal(originalExecutions, trade.Executions);
        Assert.Equal(CreatedAt, trade.UpdatedAtUtc);
    }

    private static Trade CreateTrade(bool hasExit)
    {
        Guid tradeId = Guid.NewGuid();
        var entry = TradeExecution.Rehydrate(
            Guid.NewGuid(),
            tradeId,
            1,
            CreatedAt,
            ExecutionSide.Buy,
            1m,
            100m,
            1m,
            0m,
            "external-execution",
            "external-order",
            "ESU6");
        var executions = new List<TradeExecution> { entry };
        if (hasExit)
        {
            executions.Add(TradeExecution.Rehydrate(
                Guid.NewGuid(),
                tradeId,
                2,
                CreatedAt.AddHours(1),
                ExecutionSide.Sell,
                1m,
                105m,
                1m,
                0m,
                null,
                null,
                null));
        }

        return Trade.Rehydrate(
            tradeId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new TradePricingSnapshot(10m, "USD"),
            null,
            executions,
            CreatedAt,
            CreatedAt);
    }

    private static TradeExecution Corrected(
        TradeExecution current,
        int sequence,
        ExecutionSide side,
        decimal quantity,
        decimal price,
        decimal commission,
        decimal fees) =>
        TradeExecution.Rehydrate(
            current.Id,
            current.TradeId,
            sequence,
            current.ExecutedAtUtc,
            side,
            quantity,
            price,
            commission,
            fees,
            current.ExternalExecutionId,
            current.ExternalOrderId,
            current.BrokerSymbol);
}
