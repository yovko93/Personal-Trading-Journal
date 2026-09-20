namespace PersonalTradingJournal.Application.Imports.Tradovate;

/// <summary>
/// One normalized matched-fill source row from a Tradovate export.
/// </summary>
/// <remarks>
/// A row is evidence about a matched quantity between buy and sell fills. It is not
/// an independent execution or a Trade, and external fill identifiers may repeat.
/// </remarks>
public sealed record TradovateMatchedFillRow(
    int SourceRecordIndex,
    int? SourceLineNumber,
    string BrokerSymbol,
    string PriceFormat,
    string PriceFormatType,
    decimal TickSize,
    string BuyFillId,
    string SellFillId,
    decimal MatchedQuantity,
    decimal BuyPrice,
    decimal SellPrice,
    decimal SourceReportedPnL,
    DateTime BoughtLocalTimestamp,
    DateTime SoldLocalTimestamp,
    string SourceDurationText);
