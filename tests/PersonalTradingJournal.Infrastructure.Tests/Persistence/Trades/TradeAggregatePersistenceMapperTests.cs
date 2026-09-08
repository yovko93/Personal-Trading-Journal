using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

public sealed class TradeAggregatePersistenceMapperTests
{
    private static readonly Guid TradeId =
        Guid.Parse("92afe9d8-40ad-420d-9703-57331d9ef971");

    private static readonly Guid TradingAccountId =
        Guid.Parse("1f6f283a-2909-4fa8-990b-38e1468b9bb6");

    private static readonly Guid InstrumentId =
        Guid.Parse("a48fbb9a-b85f-425b-a7e7-85d4f41c1cd4");

    private static readonly DateTimeOffset FirstExecutionAtUtc =
        new(2026, 9, 10, 13, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 10, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToDomainRejectsNullTradeRecord()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TradePersistenceMapper.ToDomain(null!, []));
    }

    [Fact]
    public void ToDomainRejectsNullExecutionRecords()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TradePersistenceMapper.ToDomain(CreateTradeRecord(), null!));
    }

    [Fact]
    public void ToDomainOrdersExecutionsBySequence()
    {
        TradeExecutionRecord sequence1 = CreateExecutionRecord(
            Guid.Parse("05aa9b16-d113-43f8-8c1a-97fb6ce7c33e"),
            TradeId,
            1,
            FirstExecutionAtUtc,
            ExecutionSide.Buy,
            2m,
            100m);
        TradeExecutionRecord sequence2 = CreateExecutionRecord(
            Guid.Parse("6b16ac34-532c-426a-8107-9772485a7092"),
            TradeId,
            2,
            FirstExecutionAtUtc.AddMinutes(1),
            ExecutionSide.Sell,
            1m,
            110m);
        TradeExecutionRecord sequence3 = CreateExecutionRecord(
            Guid.Parse("00fe0163-4e7f-45a5-b124-e9c0d092c05d"),
            TradeId,
            3,
            FirstExecutionAtUtc.AddMinutes(2),
            ExecutionSide.Sell,
            1m,
            120m);

        Trade trade = TradePersistenceMapper.ToDomain(
            CreateTradeRecord(),
            [sequence3, sequence1, sequence2]);

        Assert.Equal([1, 2, 3], trade.Executions.Select(execution => execution.Sequence));
        Assert.Equal(TradeStatus.Closed, trade.Status);
    }

    [Fact]
    public void ToDomainRejectsCorruptedTradeScalarState()
    {
        AssertInvalidRecord(record => record.Id = Guid.Empty);
        AssertInvalidRecord(record => record.TradingAccountId = Guid.Empty);
        AssertInvalidRecord(record => record.InstrumentId = Guid.Empty);
        AssertInvalidRecord(record => record.PricingPointValue = 0m);
        AssertInvalidRecord(record => record.PricingPointValue = -1m);
        AssertInvalidRecord(record => record.PricingCurrency = "   ");
        AssertInvalidRecord(record => record.PricingCurrency = "CURRENCY9");
        AssertInvalidRecord(record => record.StrategyId = Guid.Empty);
        AssertInvalidRecord(record => record.TradingSetupId = Guid.Empty);
        AssertInvalidRecord(record =>
            record.CreatedAtUtc = record.CreatedAtUtc.ToOffset(TimeSpan.FromHours(2)));
        AssertInvalidRecord(record =>
            record.UpdatedAtUtc = record.UpdatedAtUtc.ToOffset(TimeSpan.FromHours(-3)));
        AssertInvalidRecord(record => record.UpdatedAtUtc = record.CreatedAtUtc.AddTicks(-1));
    }

    [Fact]
    public void ToDomainRejectsCorruptedAggregateExecutionHistory()
    {
        AssertInvalidHistory([]);

        AssertInvalidHistory(
        [
            CreateExecutionRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                FirstExecutionAtUtc,
                ExecutionSide.Buy,
                1m,
                100m),
        ]);

        Guid duplicateExecutionId = Guid.NewGuid();
        AssertInvalidHistory(
        [
            CreateExecutionRecord(
                duplicateExecutionId,
                TradeId,
                1,
                FirstExecutionAtUtc,
                ExecutionSide.Buy,
                2m,
                100m),
            CreateExecutionRecord(
                duplicateExecutionId,
                TradeId,
                2,
                FirstExecutionAtUtc.AddMinutes(1),
                ExecutionSide.Sell,
                1m,
                110m),
        ]);

        AssertInvalidHistory(
        [
            CreateExecutionRecord(
                Guid.NewGuid(),
                TradeId,
                1,
                FirstExecutionAtUtc,
                ExecutionSide.Buy,
                2m,
                100m),
            CreateExecutionRecord(
                Guid.NewGuid(),
                TradeId,
                1,
                FirstExecutionAtUtc.AddMinutes(1),
                ExecutionSide.Sell,
                1m,
                110m),
        ]);

        AssertInvalidHistory(
        [
            CreateExecutionRecord(
                Guid.NewGuid(),
                TradeId,
                1,
                FirstExecutionAtUtc,
                ExecutionSide.Buy,
                2m,
                100m),
            CreateExecutionRecord(
                Guid.NewGuid(),
                TradeId,
                3,
                FirstExecutionAtUtc.AddMinutes(1),
                ExecutionSide.Sell,
                1m,
                110m),
        ]);

        AssertInvalidHistory(
        [
            CreateExecutionRecord(
                Guid.NewGuid(),
                TradeId,
                2,
                FirstExecutionAtUtc,
                ExecutionSide.Buy,
                1m,
                100m),
        ]);

        AssertInvalidHistory(
        [
            CreateExecutionRecord(
                Guid.NewGuid(),
                TradeId,
                1,
                FirstExecutionAtUtc,
                ExecutionSide.Buy,
                2m,
                100m),
            CreateExecutionRecord(
                Guid.NewGuid(),
                TradeId,
                2,
                FirstExecutionAtUtc.AddTicks(-1),
                ExecutionSide.Sell,
                1m,
                110m),
        ]);

        AssertInvalidHistory(
        [
            CreateExecutionRecord(
                Guid.NewGuid(),
                TradeId,
                1,
                FirstExecutionAtUtc,
                ExecutionSide.Buy,
                1m,
                100m),
            CreateExecutionRecord(
                Guid.NewGuid(),
                TradeId,
                2,
                FirstExecutionAtUtc.AddMinutes(1),
                ExecutionSide.Sell,
                1m,
                110m),
            CreateExecutionRecord(
                Guid.NewGuid(),
                TradeId,
                3,
                FirstExecutionAtUtc.AddMinutes(2),
                ExecutionSide.Buy,
                1m,
                120m),
        ]);

        AssertInvalidHistory(
        [
            CreateExecutionRecord(
                Guid.NewGuid(),
                TradeId,
                1,
                FirstExecutionAtUtc,
                ExecutionSide.Buy,
                1m,
                100m),
            CreateExecutionRecord(
                Guid.NewGuid(),
                TradeId,
                2,
                FirstExecutionAtUtc.AddMinutes(1),
                ExecutionSide.Sell,
                2m,
                110m),
        ]);
    }

    [Fact]
    public void ToDomainPropagatesIndividualExecutionCorruption()
    {
        TradeExecutionRecord invalidSide = CreateOpeningExecutionRecord();
        invalidSide.Side = (ExecutionSide)999;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TradePersistenceMapper.ToDomain(CreateTradeRecord(), [invalidSide]));

        TradeExecutionRecord negativeCommission = CreateOpeningExecutionRecord();
        negativeCommission.Commission = -0.01m;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TradePersistenceMapper.ToDomain(CreateTradeRecord(), [negativeCommission]));
    }

    [Fact]
    public void ToDomainSupportsNegativePricesAndFractionalQuantities()
    {
        Trade trade = TradePersistenceMapper.ToDomain(
            CreateTradeRecord(),
            [
                CreateExecutionRecord(
                    Guid.NewGuid(),
                    TradeId,
                    1,
                    FirstExecutionAtUtc,
                    ExecutionSide.Buy,
                    0.5m,
                    -40m),
                CreateExecutionRecord(
                    Guid.NewGuid(),
                    TradeId,
                    2,
                    FirstExecutionAtUtc.AddMinutes(1),
                    ExecutionSide.Sell,
                    0.5m,
                    -30m),
            ]);

        Assert.Equal(TradeDirection.Long, trade.Direction);
        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(0m, trade.OpenQuantity);
        Assert.Equal(-40m, trade.AverageEntryPrice);
        Assert.Equal(-30m, trade.AverageExitPrice);
        Assert.Equal(100m, trade.GrossPnL);
        Assert.Equal(100m, trade.NetPnL);
    }

    private static void AssertInvalidRecord(Action<TradeRecord> corrupt)
    {
        TradeRecord record = CreateTradeRecord();
        corrupt(record);

        Assert.ThrowsAny<ArgumentException>(() =>
            TradePersistenceMapper.ToDomain(record, [CreateOpeningExecutionRecord()]));
    }

    private static void AssertInvalidHistory(
        IEnumerable<TradeExecutionRecord> executionRecords)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            TradePersistenceMapper.ToDomain(CreateTradeRecord(), executionRecords));
    }

    private static TradeRecord CreateTradeRecord()
    {
        return new TradeRecord
        {
            Id = TradeId,
            TradingAccountId = TradingAccountId,
            InstrumentId = InstrumentId,
            PricingPointValue = 20m,
            PricingCurrency = "USD",
            StrategyId = null,
            TradingSetupId = null,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        };
    }

    private static TradeExecutionRecord CreateOpeningExecutionRecord()
    {
        return CreateExecutionRecord(
            Guid.Parse("0f9c484f-a1a5-42e4-b554-5fcb62f6de39"),
            TradeId,
            1,
            FirstExecutionAtUtc,
            ExecutionSide.Buy,
            1m,
            100m);
    }

    private static TradeExecutionRecord CreateExecutionRecord(
        Guid id,
        Guid tradeId,
        int sequence,
        DateTimeOffset executedAtUtc,
        ExecutionSide side,
        decimal quantity,
        decimal price)
    {
        return new TradeExecutionRecord
        {
            Id = id,
            TradeId = tradeId,
            Sequence = sequence,
            ExecutedAtUtc = executedAtUtc,
            Side = side,
            Quantity = quantity,
            Price = price,
            Commission = 0m,
            Fees = 0m,
            ExternalExecutionId = null,
            ExternalOrderId = null,
            BrokerSymbol = null,
        };
    }
}
