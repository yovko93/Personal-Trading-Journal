using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Imports.Tradovate;

/// <summary>
/// A unique broker fill reconstructed from one or more matched-fill source rows.
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
    }

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
