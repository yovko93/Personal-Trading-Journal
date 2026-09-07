using PersonalTradingJournal.Domain.Common;

namespace PersonalTradingJournal.Domain.Trades;

/// <summary>
/// Represents an immutable market execution associated with a trade.
/// </summary>
public sealed class TradeExecution : Entity
{
    private const int MaximumExternalIdentifierLength = 128;
    private const int MaximumBrokerSymbolLength = 64;

    public TradeExecution(
        Guid tradeId,
        int sequence,
        DateTimeOffset executedAtUtc,
        ExecutionSide side,
        decimal quantity,
        decimal price,
        decimal commission,
        decimal fees,
        string? externalExecutionId,
        string? externalOrderId,
        string? brokerSymbol)
        : base()
    {
        TradeId = ValidateTradeId(tradeId);
        Sequence = ValidateSequence(sequence);
        ExecutedAtUtc = ValidateExecutedAtUtc(executedAtUtc);
        Side = ValidateSide(side);
        Quantity = ValidateQuantity(quantity);
        Price = price;
        Commission = ValidateNonNegativeCost(commission, nameof(commission));
        Fees = ValidateNonNegativeCost(fees, nameof(fees));
        ExternalExecutionId = NormalizeOptional(
            externalExecutionId,
            MaximumExternalIdentifierLength,
            nameof(externalExecutionId));
        ExternalOrderId = NormalizeOptional(
            externalOrderId,
            MaximumExternalIdentifierLength,
            nameof(externalOrderId));
        BrokerSymbol = NormalizeOptional(
            brokerSymbol,
            MaximumBrokerSymbolLength,
            nameof(brokerSymbol));
    }

    private TradeExecution(
        Guid id,
        Guid tradeId,
        int sequence,
        DateTimeOffset executedAtUtc,
        ExecutionSide side,
        decimal quantity,
        decimal price,
        decimal commission,
        decimal fees,
        string? externalExecutionId,
        string? externalOrderId,
        string? brokerSymbol)
        : base(id)
    {
        TradeId = ValidateTradeId(tradeId);
        Sequence = ValidateSequence(sequence);
        ExecutedAtUtc = ValidateExecutedAtUtc(executedAtUtc);
        Side = ValidateSide(side);
        Quantity = ValidateQuantity(quantity);
        Price = price;
        Commission = ValidateNonNegativeCost(commission, nameof(commission));
        Fees = ValidateNonNegativeCost(fees, nameof(fees));
        ExternalExecutionId = NormalizeOptional(
            externalExecutionId,
            MaximumExternalIdentifierLength,
            nameof(externalExecutionId));
        ExternalOrderId = NormalizeOptional(
            externalOrderId,
            MaximumExternalIdentifierLength,
            nameof(externalOrderId));
        BrokerSymbol = NormalizeOptional(
            brokerSymbol,
            MaximumBrokerSymbolLength,
            nameof(brokerSymbol));
    }

    public Guid TradeId { get; }

    public int Sequence { get; }

    public DateTimeOffset ExecutedAtUtc { get; }

    public ExecutionSide Side { get; }

    public decimal Quantity { get; }

    public decimal Price { get; }

    public decimal Commission { get; }

    public decimal Fees { get; }

    public string? ExternalExecutionId { get; }

    public string? ExternalOrderId { get; }

    public string? BrokerSymbol { get; }

    public decimal TotalCosts => Commission + Fees;

    public static TradeExecution Rehydrate(
        Guid id,
        Guid tradeId,
        int sequence,
        DateTimeOffset executedAtUtc,
        ExecutionSide side,
        decimal quantity,
        decimal price,
        decimal commission,
        decimal fees,
        string? externalExecutionId,
        string? externalOrderId,
        string? brokerSymbol)
    {
        return new TradeExecution(
            id,
            tradeId,
            sequence,
            executedAtUtc,
            side,
            quantity,
            price,
            commission,
            fees,
            externalExecutionId,
            externalOrderId,
            brokerSymbol);
    }

    private static Guid ValidateTradeId(Guid tradeId)
    {
        if (tradeId == Guid.Empty)
        {
            throw new ArgumentException("A trade identifier cannot be empty.", nameof(tradeId));
        }

        return tradeId;
    }

    private static int ValidateSequence(int sequence)
    {
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                sequence,
                "The execution sequence must be greater than zero.");
        }

        return sequence;
    }

    private static DateTimeOffset ValidateExecutedAtUtc(DateTimeOffset executedAtUtc)
    {
        if (executedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The execution timestamp must use a UTC offset of zero.",
                nameof(executedAtUtc));
        }

        return executedAtUtc;
    }

    private static ExecutionSide ValidateSide(ExecutionSide side)
    {
        if (!Enum.IsDefined(side))
        {
            throw new ArgumentOutOfRangeException(
                nameof(side),
                side,
                "The execution side is not defined.");
        }

        return side;
    }

    private static decimal ValidateQuantity(decimal quantity)
    {
        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                "The execution quantity must be greater than zero.");
        }

        return quantity;
    }

    private static decimal ValidateNonNegativeCost(decimal value, string parameterName)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "An execution cost cannot be negative.");
        }

        return value;
    }

    private static string? NormalizeOptional(
        string? value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"The value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }
}
