using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Imports.Tradovate;

/// <summary>
/// A broker fill, or a quantity allocation linked to that unchanged source fill.
/// </summary>
public sealed class TradovateReconstructedExecution
{
    public TradovateReconstructedExecution(
        string brokerSymbol,
        ExecutionSide side,
        string externalFillId,
        decimal quantity,
        decimal price,
        DateTime sourceLocalTimestamp,
        decimal tickSize,
        IEnumerable<int> sourceRecordIndices,
        IEnumerable<int> sourceLineNumbers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brokerSymbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalFillId);
        ArgumentNullException.ThrowIfNull(sourceRecordIndices);
        ArgumentNullException.ThrowIfNull(sourceLineNumbers);

        if (!Enum.IsDefined(side))
        {
            throw new ArgumentOutOfRangeException(nameof(side));
        }

        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (tickSize <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(tickSize));
        }

        if (sourceLocalTimestamp.Kind != DateTimeKind.Unspecified)
        {
            throw new ArgumentException(
                "The source timestamp must remain timezone-unspecified.",
                nameof(sourceLocalTimestamp));
        }

        BrokerSymbol = brokerSymbol;
        Side = side;
        ExternalFillId = externalFillId;
        Quantity = quantity;
        Price = price;
        SourceLocalTimestamp = sourceLocalTimestamp;
        TickSize = tickSize;
        SourceRecordIndices = Array.AsReadOnly(sourceRecordIndices.Distinct().Order().ToArray());
        SourceLineNumbers = Array.AsReadOnly(sourceLineNumbers.Distinct().Order().ToArray());
        SourceFill = this;
    }

    private TradovateReconstructedExecution(
        TradovateReconstructedExecution sourceFill,
        decimal quantity,
        int allocationIndex,
        IEnumerable<int> sourceRecordIndices,
        IEnumerable<int> sourceLineNumbers)
        : this(sourceFill.BrokerSymbol, sourceFill.Side, sourceFill.ExternalFillId,
            quantity, sourceFill.Price, sourceFill.SourceLocalTimestamp, sourceFill.TickSize,
            sourceRecordIndices, sourceLineNumbers)
    {
        if (quantity >= sourceFill.Quantity || allocationIndex is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        SourceFill = sourceFill.SourceFill;
        AllocationIndex = allocationIndex;
    }

    /// <summary>The original immutable fill; its quantity is never split or replaced.</summary>
    public TradovateReconstructedExecution SourceFill { get; }

    /// <summary>Zero for a whole fill or closing allocation; one for a reversal opening allocation.</summary>
    public int AllocationIndex { get; }

    public TradovateReconstructedExecution Allocate(
        decimal quantity,
        int allocationIndex,
        IEnumerable<int> sourceRecordIndices,
        IEnumerable<int> sourceLineNumbers) =>
        new(SourceFill, quantity, allocationIndex, sourceRecordIndices, sourceLineNumbers);

    public string BrokerSymbol { get; }

    public ExecutionSide Side { get; }

    public string ExternalFillId { get; }

    public decimal Quantity { get; }

    public decimal Price { get; }

    public DateTime SourceLocalTimestamp { get; }

    public decimal TickSize { get; }

    public IReadOnlyList<int> SourceRecordIndices { get; }

    public IReadOnlyList<int> SourceLineNumbers { get; }
}
