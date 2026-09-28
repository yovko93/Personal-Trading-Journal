using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed class TradovateTradeCandidate
{
    public TradovateTradeCandidate(
        string brokerSymbol,
        TradeDirection? provisionalDirection,
        IEnumerable<TradovateReconstructedExecution> orderedExecutions,
        DateTime openingLocalTimestamp,
        DateTime? closingLocalTimestamp,
        decimal? openingQuantity,
        decimal? closingQuantity,
        decimal signedPositionAtEnd,
        TradovateReconstructionStatus status,
        IEnumerable<int> sourceRecordIndices,
        IEnumerable<string> diagnosticCodes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brokerSymbol);
        ArgumentNullException.ThrowIfNull(orderedExecutions);
        ArgumentNullException.ThrowIfNull(sourceRecordIndices);
        ArgumentNullException.ThrowIfNull(diagnosticCodes);

        TradovateReconstructedExecution[] executionSnapshot = orderedExecutions.ToArray();
        if (executionSnapshot.Length == 0)
        {
            throw new ArgumentException(
                "A candidate must contain at least one reconstructed execution.",
                nameof(orderedExecutions));
        }

        BrokerSymbol = brokerSymbol;
        ProvisionalDirection = provisionalDirection;
        OrderedExecutions = Array.AsReadOnly(executionSnapshot);
        OpeningLocalTimestamp = openingLocalTimestamp;
        ClosingLocalTimestamp = closingLocalTimestamp;
        OpeningQuantity = openingQuantity;
        ClosingQuantity = closingQuantity;
        SignedPositionAtEnd = signedPositionAtEnd;
        Status = status;
        SourceRecordIndices = Array.AsReadOnly(sourceRecordIndices.Distinct().Order().ToArray());
        DiagnosticCodes = Array.AsReadOnly(
            diagnosticCodes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    public string BrokerSymbol { get; }

    public TradeDirection? ProvisionalDirection { get; }

    public IReadOnlyList<TradovateReconstructedExecution> OrderedExecutions { get; }

    public DateTime OpeningLocalTimestamp { get; }

    public DateTime? ClosingLocalTimestamp { get; }

    public decimal? OpeningQuantity { get; }

    public decimal? ClosingQuantity { get; }

    public decimal SignedPositionAtEnd { get; }

    public TradovateReconstructionStatus Status { get; }

    public IReadOnlyList<int> SourceRecordIndices { get; }

    public IReadOnlyList<string> DiagnosticCodes { get; }
}
