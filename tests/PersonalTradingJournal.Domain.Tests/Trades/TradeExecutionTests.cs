using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Domain.Tests.Trades;

public sealed class TradeExecutionTests
{
    private static readonly Guid TradeId =
        new("0e97d38d-a4d7-43da-8684-8516e569cb17");

    private static readonly Guid ExistingExecutionId =
        new("a7170057-909f-4778-b560-069791cad1e3");

    private static readonly DateTimeOffset ExecutedAtUtc =
        new(2026, 4, 1, 14, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ExecutionSide.Buy)]
    [InlineData(ExecutionSide.Sell)]
    public void CreatesValidExecution(ExecutionSide side)
    {
        TradeExecution execution = CreateExecution(side: side);

        Assert.NotEqual(Guid.Empty, execution.Id);
        Assert.Equal(TradeId, execution.TradeId);
        Assert.Equal(1, execution.Sequence);
        Assert.Equal(ExecutedAtUtc, execution.ExecutedAtUtc);
        Assert.Equal(side, execution.Side);
        Assert.Equal(2.5m, execution.Quantity);
        Assert.Equal(21_345.125m, execution.Price);
        Assert.Equal(1.25m, execution.Commission);
        Assert.Equal(0.75m, execution.Fees);
        Assert.Equal("Exec-AbC-123", execution.ExternalExecutionId);
        Assert.Equal("Order-XyZ-456", execution.ExternalOrderId);
        Assert.Equal("NQu6", execution.BrokerSymbol);
        Assert.Equal(2.00m, execution.TotalCosts);
    }

    [Fact]
    public void RejectsEmptyTradeIdentifier()
    {
        Assert.Throws<ArgumentException>(() => CreateExecution(tradeId: Guid.Empty));
    }

    [Fact]
    public void RehydrationPreservesExistingIdentifierAndValues()
    {
        TradeExecution execution = TradeExecution.Rehydrate(
            ExistingExecutionId,
            TradeId,
            3,
            ExecutedAtUtc,
            ExecutionSide.Sell,
            1.75m,
            -37.123456m,
            2.10m,
            0.90m,
            "Execution-AbC",
            "Order-XyZ",
            "CLm6");

        Assert.Equal(ExistingExecutionId, execution.Id);
        Assert.Equal(TradeId, execution.TradeId);
        Assert.Equal(3, execution.Sequence);
        Assert.Equal(ExecutedAtUtc, execution.ExecutedAtUtc);
        Assert.Equal(ExecutionSide.Sell, execution.Side);
        Assert.Equal(1.75m, execution.Quantity);
        Assert.Equal(-37.123456m, execution.Price);
        Assert.Equal(2.10m, execution.Commission);
        Assert.Equal(0.90m, execution.Fees);
        Assert.Equal("Execution-AbC", execution.ExternalExecutionId);
        Assert.Equal("Order-XyZ", execution.ExternalOrderId);
        Assert.Equal("CLm6", execution.BrokerSymbol);
        Assert.Equal(3.00m, execution.TotalCosts);
    }

    [Fact]
    public void RehydrationRejectsEmptyExecutionIdentifier()
    {
        Assert.Throws<ArgumentException>(() => RehydrateExecution(id: Guid.Empty));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveSequence(int sequence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateExecution(sequence: sequence));
    }

    [Fact]
    public void PreservesPositiveSequence()
    {
        TradeExecution execution = CreateExecution(sequence: 4);

        Assert.Equal(4, execution.Sequence);
    }

    [Fact]
    public void AcceptsUtcExecutionTimestamp()
    {
        TradeExecution execution = CreateExecution(executedAtUtc: ExecutedAtUtc);

        Assert.Equal(ExecutedAtUtc, execution.ExecutedAtUtc);
    }

    [Fact]
    public void RejectsNonZeroExecutionTimestampOffset()
    {
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            4,
            1,
            16,
            30,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            () => CreateExecution(executedAtUtc: nonUtcTimestamp));
    }

    [Theory]
    [InlineData((ExecutionSide)0)]
    [InlineData((ExecutionSide)999)]
    public void RejectsUndefinedSide(ExecutionSide side)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateExecution(side: side));
    }

    [Fact]
    public void AcceptsPositiveIntegerQuantity()
    {
        TradeExecution execution = CreateExecution(quantity: 2m);

        Assert.Equal(2m, execution.Quantity);
    }

    [Fact]
    public void AcceptsPositiveFractionalQuantity()
    {
        TradeExecution execution = CreateExecution(quantity: 0.125m);

        Assert.Equal(0.125m, execution.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveQuantity(int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateExecution(quantity: quantity));
    }

    [Fact]
    public void PreservesQuantityPrecision()
    {
        const decimal quantity = 0.123456789m;

        TradeExecution execution = CreateExecution(quantity: quantity);

        Assert.Equal(quantity, execution.Quantity);
    }

    [Fact]
    public void AcceptsPositivePrice()
    {
        TradeExecution execution = CreateExecution(price: 1.25m);

        Assert.Equal(1.25m, execution.Price);
    }

    [Fact]
    public void AcceptsZeroPrice()
    {
        TradeExecution execution = CreateExecution(price: 0m);

        Assert.Equal(0m, execution.Price);
    }

    [Fact]
    public void AcceptsNegativePrice()
    {
        TradeExecution execution = CreateExecution(price: -37.63m);

        Assert.Equal(-37.63m, execution.Price);
    }

    [Fact]
    public void PreservesPricePrecision()
    {
        const decimal price = 21_345.123456789m;

        TradeExecution execution = CreateExecution(price: price);

        Assert.Equal(price, execution.Price);
    }

    [Fact]
    public void AcceptsZeroCommission()
    {
        TradeExecution execution = CreateExecution(commission: 0m);

        Assert.Equal(0m, execution.Commission);
    }

    [Fact]
    public void AcceptsZeroFees()
    {
        TradeExecution execution = CreateExecution(fees: 0m);

        Assert.Equal(0m, execution.Fees);
    }

    [Fact]
    public void PreservesPositiveCostsAndDerivesTotal()
    {
        TradeExecution execution = CreateExecution(commission: 1.2345m, fees: 0.6789m);

        Assert.Equal(1.2345m, execution.Commission);
        Assert.Equal(0.6789m, execution.Fees);
        Assert.Equal(1.9134m, execution.TotalCosts);
    }

    [Fact]
    public void RejectsNegativeCommission()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateExecution(commission: -0.01m));
    }

    [Fact]
    public void RejectsNegativeFees()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateExecution(fees: -0.01m));
    }

    [Fact]
    public void NormalizesExternalExecutionIdentifier()
    {
        TradeExecution execution = CreateExecution(externalExecutionId: "  Exec-AbC-123  ");

        Assert.Equal("Exec-AbC-123", execution.ExternalExecutionId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertsMissingExternalExecutionIdentifierToNull(
        string? externalExecutionId)
    {
        TradeExecution execution = CreateExecution(
            externalExecutionId: externalExecutionId);

        Assert.Null(execution.ExternalExecutionId);
    }

    [Fact]
    public void RejectsExternalExecutionIdentifierLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateExecution(externalExecutionId: new string('E', 129)));
    }

    [Fact]
    public void NormalizesExternalOrderIdentifier()
    {
        TradeExecution execution = CreateExecution(externalOrderId: "  Order-XyZ-456  ");

        Assert.Equal("Order-XyZ-456", execution.ExternalOrderId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertsMissingExternalOrderIdentifierToNull(string? externalOrderId)
    {
        TradeExecution execution = CreateExecution(externalOrderId: externalOrderId);

        Assert.Null(execution.ExternalOrderId);
    }

    [Fact]
    public void RejectsExternalOrderIdentifierLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateExecution(externalOrderId: new string('O', 129)));
    }

    [Fact]
    public void NormalizesBrokerSymbol()
    {
        TradeExecution execution = CreateExecution(brokerSymbol: "  NQu6  ");

        Assert.Equal("NQu6", execution.BrokerSymbol);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertsMissingBrokerSymbolToNull(string? brokerSymbol)
    {
        TradeExecution execution = CreateExecution(brokerSymbol: brokerSymbol);

        Assert.Null(execution.BrokerSymbol);
    }

    [Fact]
    public void RejectsBrokerSymbolLongerThanMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateExecution(brokerSymbol: new string('S', 65)));
    }

    [Fact]
    public void RehydrationEnforcesNormalValidation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RehydrateExecution(sequence: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RehydrateExecution(commission: -1m));
    }

    private static TradeExecution CreateExecution(
        Guid? tradeId = null,
        int sequence = 1,
        DateTimeOffset? executedAtUtc = null,
        ExecutionSide side = ExecutionSide.Buy,
        decimal quantity = 2.5m,
        decimal price = 21_345.125m,
        decimal commission = 1.25m,
        decimal fees = 0.75m,
        string? externalExecutionId = "Exec-AbC-123",
        string? externalOrderId = "Order-XyZ-456",
        string? brokerSymbol = "NQu6")
    {
        return new TradeExecution(
            tradeId ?? TradeId,
            sequence,
            executedAtUtc ?? ExecutedAtUtc,
            side,
            quantity,
            price,
            commission,
            fees,
            externalExecutionId,
            externalOrderId,
            brokerSymbol);
    }

    private static TradeExecution RehydrateExecution(
        Guid? id = null,
        Guid? tradeId = null,
        int sequence = 1,
        DateTimeOffset? executedAtUtc = null,
        ExecutionSide side = ExecutionSide.Buy,
        decimal quantity = 2.5m,
        decimal price = 21_345.125m,
        decimal commission = 1.25m,
        decimal fees = 0.75m,
        string? externalExecutionId = "Exec-AbC-123",
        string? externalOrderId = "Order-XyZ-456",
        string? brokerSymbol = "NQu6")
    {
        return TradeExecution.Rehydrate(
            id ?? ExistingExecutionId,
            tradeId ?? TradeId,
            sequence,
            executedAtUtc ?? ExecutedAtUtc,
            side,
            quantity,
            price,
            commission,
            fees,
            externalExecutionId,
            externalOrderId,
            brokerSymbol);
    }
}
