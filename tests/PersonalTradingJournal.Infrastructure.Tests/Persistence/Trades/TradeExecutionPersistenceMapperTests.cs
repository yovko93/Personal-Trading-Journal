using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

public sealed class TradeExecutionPersistenceMapperTests
{
    private static readonly Guid ExecutionId =
        Guid.Parse("674ceeca-eb34-4c41-a276-fce1b2d6cd9c");

    private static readonly Guid TradeId =
        Guid.Parse("6631796d-5e7b-486d-87e1-5ee0c8cf5fd3");

    private static readonly DateTimeOffset ExecutedAtUtc =
        new(2026, 8, 10, 13, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ToRecordMapsAllPersistedExecutionState()
    {
        TradeExecution execution = CreateExecution();

        TradeExecutionRecord record =
            TradeExecutionPersistenceMapper.ToRecord(execution);

        Assert.Equal(ExecutionId, record.Id);
        Assert.Equal(TradeId, record.TradeId);
        Assert.Equal(2, record.Sequence);
        Assert.Equal(ExecutedAtUtc, record.ExecutedAtUtc);
        Assert.Equal(ExecutionSide.Sell, record.Side);
        Assert.Equal(0.5m, record.Quantity);
        Assert.Equal(-37.63m, record.Price);
        Assert.Equal(1.25m, record.Commission);
        Assert.Equal(0.75m, record.Fees);
        Assert.Equal("EXEC-42", record.ExternalExecutionId);
        Assert.Equal("ORDER-17", record.ExternalOrderId);
        Assert.Equal("CL", record.BrokerSymbol);
    }

    [Fact]
    public void ToRecordPreservesNullMetadataAndZeroValues()
    {
        TradeExecution execution = TradeExecution.Rehydrate(
            ExecutionId,
            TradeId,
            1,
            ExecutedAtUtc,
            ExecutionSide.Buy,
            0.25m,
            0m,
            0m,
            0m,
            null,
            null,
            null);

        TradeExecutionRecord record =
            TradeExecutionPersistenceMapper.ToRecord(execution);

        Assert.Equal(0.25m, record.Quantity);
        Assert.Equal(0m, record.Price);
        Assert.Equal(0m, record.Commission);
        Assert.Equal(0m, record.Fees);
        Assert.Null(record.ExternalExecutionId);
        Assert.Null(record.ExternalOrderId);
        Assert.Null(record.BrokerSymbol);
    }

    [Fact]
    public void ToRecordRejectsNullExecution()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TradeExecutionPersistenceMapper.ToRecord(null!));
    }

    [Fact]
    public void ToDomainRehydratesAllPersistedExecutionState()
    {
        TradeExecutionRecord record = CreateValidRecord();

        TradeExecution execution = TradeExecutionPersistenceMapper.ToDomain(record);

        Assert.Equal(record.Id, execution.Id);
        Assert.Equal(record.TradeId, execution.TradeId);
        Assert.Equal(record.Sequence, execution.Sequence);
        Assert.Equal(record.ExecutedAtUtc, execution.ExecutedAtUtc);
        Assert.Equal(record.Side, execution.Side);
        Assert.Equal(record.Quantity, execution.Quantity);
        Assert.Equal(record.Price, execution.Price);
        Assert.Equal(record.Commission, execution.Commission);
        Assert.Equal(record.Fees, execution.Fees);
        Assert.Equal(record.ExternalExecutionId, execution.ExternalExecutionId);
        Assert.Equal(record.ExternalOrderId, execution.ExternalOrderId);
        Assert.Equal(record.BrokerSymbol, execution.BrokerSymbol);
        Assert.Equal(2m, execution.TotalCosts);
    }

    [Fact]
    public void ToDomainPreservesValidEdgeCases()
    {
        TradeExecutionRecord record = CreateValidRecord();
        record.Quantity = 0.25m;
        record.Price = 0m;
        record.Commission = 0m;
        record.Fees = 0m;
        record.ExternalExecutionId = null;
        record.ExternalOrderId = null;
        record.BrokerSymbol = null;

        TradeExecution execution = TradeExecutionPersistenceMapper.ToDomain(record);

        Assert.Equal(0.25m, execution.Quantity);
        Assert.Equal(0m, execution.Price);
        Assert.Equal(0m, execution.TotalCosts);
        Assert.Null(execution.ExternalExecutionId);
        Assert.Null(execution.ExternalOrderId);
        Assert.Null(execution.BrokerSymbol);
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TradeExecutionPersistenceMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomainRejectsPersistedDataThatViolatesDomainInvariants()
    {
        AssertInvalid(record => record.Id = Guid.Empty);
        AssertInvalid(record => record.TradeId = Guid.Empty);
        AssertInvalid(record => record.Sequence = 0);
        AssertInvalid(record =>
            record.ExecutedAtUtc = record.ExecutedAtUtc.ToOffset(TimeSpan.FromHours(2)));
        AssertInvalid(record => record.Side = (ExecutionSide)999);
        AssertInvalid(record => record.Quantity = 0m);
        AssertInvalid(record => record.Quantity = -0.25m);
        AssertInvalid(record => record.Commission = -0.01m);
        AssertInvalid(record => record.Fees = -0.01m);
        AssertInvalid(record => record.ExternalExecutionId = new string('E', 129));
        AssertInvalid(record => record.ExternalOrderId = new string('O', 129));
        AssertInvalid(record => record.BrokerSymbol = new string('B', 65));
    }

    private static void AssertInvalid(Action<TradeExecutionRecord> corrupt)
    {
        TradeExecutionRecord record = CreateValidRecord();
        corrupt(record);

        Assert.ThrowsAny<ArgumentException>(() =>
            TradeExecutionPersistenceMapper.ToDomain(record));
    }

    private static TradeExecution CreateExecution()
    {
        return TradeExecution.Rehydrate(
            ExecutionId,
            TradeId,
            2,
            ExecutedAtUtc,
            ExecutionSide.Sell,
            0.5m,
            -37.63m,
            1.25m,
            0.75m,
            "EXEC-42",
            "ORDER-17",
            "CL");
    }

    private static TradeExecutionRecord CreateValidRecord()
    {
        return new TradeExecutionRecord
        {
            Id = ExecutionId,
            TradeId = TradeId,
            Sequence = 2,
            ExecutedAtUtc = ExecutedAtUtc,
            Side = ExecutionSide.Sell,
            Quantity = 0.5m,
            Price = -37.63m,
            Commission = 1.25m,
            Fees = 0.75m,
            ExternalExecutionId = "EXEC-42",
            ExternalOrderId = "ORDER-17",
            BrokerSymbol = "CL",
        };
    }
}
