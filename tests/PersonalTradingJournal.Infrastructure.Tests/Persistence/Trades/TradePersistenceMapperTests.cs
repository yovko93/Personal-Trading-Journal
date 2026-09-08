using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

public sealed class TradePersistenceMapperTests
{
    private static readonly Guid TradeId =
        Guid.Parse("7a1316ae-e941-4802-a216-208bd4519596");

    private static readonly Guid TradingAccountId =
        Guid.Parse("51cf5ed2-685f-458c-a861-9be4e236d9d7");

    private static readonly Guid InstrumentId =
        Guid.Parse("6a753c7c-900e-49f9-9a1d-1cae5f485a65");

    private static readonly Guid StrategyId =
        Guid.Parse("2ba9cc87-1ff1-4838-9ef5-980526cd45aa");

    private static readonly Guid TradingSetupId =
        Guid.Parse("1c273469-af07-41d2-8587-a87c92f09dda");

    private static readonly DateTimeOffset ExecutedAtUtc =
        new(2026, 7, 10, 13, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 7, 10, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToRecordMapsOpenUnclassifiedTradeScalarState()
    {
        Trade trade = StartTrade();

        TradeRecord record = TradePersistenceMapper.ToRecord(trade);

        Assert.Equal(TradeId, record.Id);
        Assert.Equal(TradingAccountId, record.TradingAccountId);
        Assert.Equal(InstrumentId, record.InstrumentId);
        Assert.Equal(20m, record.PricingPointValue);
        Assert.Equal("USD", record.PricingCurrency);
        Assert.Null(record.StrategyId);
        Assert.Null(record.TradingSetupId);
        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, record.UpdatedAtUtc);
    }

    [Fact]
    public void ToRecordMapsClosedClassifiedTradeScalarState()
    {
        Trade trade = CreateClosedClassifiedTrade();
        DateTimeOffset classifiedAtUtc = CreatedAtUtc.AddMinutes(3);

        TradeRecord record = TradePersistenceMapper.ToRecord(trade);

        Assert.Equal(TradeId, record.Id);
        Assert.Equal(TradingAccountId, record.TradingAccountId);
        Assert.Equal(InstrumentId, record.InstrumentId);
        Assert.Equal(20m, record.PricingPointValue);
        Assert.Equal("USD", record.PricingCurrency);
        Assert.Equal(StrategyId, record.StrategyId);
        Assert.Equal(TradingSetupId, record.TradingSetupId);
        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
        Assert.Equal(classifiedAtUtc, record.UpdatedAtUtc);
    }

    [Fact]
    public void ToRecordRejectsNullTrade()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TradePersistenceMapper.ToRecord(null!));
    }

    [Fact]
    public void TradeRecordDoesNotExposeExecutionDerivedState()
    {
        string[] derivedPropertyNames =
        [
            "Direction",
            "Status",
            "OpenQuantity",
            "OpenedAtUtc",
            "ClosedAtUtc",
            "TotalCosts",
            "AverageEntryPrice",
            "AverageExitPrice",
            "GrossPnL",
            "NetPnL",
        ];

        foreach (string propertyName in derivedPropertyNames)
        {
            Assert.Null(typeof(TradeRecord).GetProperty(propertyName));
        }
    }

    [Fact]
    public void MapperDoesNotExposeReverseTradeMapping()
    {
        Assert.DoesNotContain(
            typeof(TradePersistenceMapper).GetMethods(),
            method => method.Name == "ToDomain");
    }

    private static Trade CreateClosedClassifiedTrade()
    {
        Trade trade = StartTrade();
        trade.AddExecution(
            CreateExecution(2, ExecutionSide.Sell, 1m, 20_010m),
            CreatedAtUtc.AddMinutes(1));
        trade.AddExecution(
            CreateExecution(3, ExecutionSide.Sell, 1m, 20_020m),
            CreatedAtUtc.AddMinutes(2));
        trade.SetClassification(
            StrategyId,
            TradingSetupId,
            CreatedAtUtc.AddMinutes(3));

        return trade;
    }

    private static Trade StartTrade()
    {
        return Trade.Start(
            TradingAccountId,
            InstrumentId,
            new TradePricingSnapshot(20m, "USD"),
            CreateExecution(1, ExecutionSide.Buy, 2m, 20_000m),
            CreatedAtUtc);
    }

    private static TradeExecution CreateExecution(
        int sequence,
        ExecutionSide side,
        decimal quantity,
        decimal price)
    {
        return new TradeExecution(
            TradeId,
            sequence,
            ExecutedAtUtc.AddMinutes(sequence - 1),
            side,
            quantity,
            price,
            1m,
            0.25m,
            null,
            null,
            "NQ");
    }
}
