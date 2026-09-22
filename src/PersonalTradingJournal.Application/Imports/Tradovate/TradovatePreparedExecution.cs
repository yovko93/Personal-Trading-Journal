using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed record TradovatePreparedExecution(
    string BrokerSymbol,
    ExecutionSide Side,
    string ExternalFillId,
    decimal Quantity,
    decimal Price,
    DateTime SourceLocalTimestamp,
    string SourceTimeZoneId,
    DateTimeOffset ExecutedAtUtc,
    DateTime TradingLocalTimestamp,
    TimeSpan TradingUtcOffset,
    string TradingTimeZoneId,
    IReadOnlyList<int> SourceRecordIndices,
    IReadOnlyList<int> SourceLineNumbers);
