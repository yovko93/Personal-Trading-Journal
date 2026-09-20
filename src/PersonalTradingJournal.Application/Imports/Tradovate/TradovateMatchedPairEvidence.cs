namespace PersonalTradingJournal.Application.Imports.Tradovate;

/// <summary>
/// Preserves one source row's matched buy/sell relationship without treating it as a Trade.
/// </summary>
public sealed record TradovateMatchedPairEvidence(
    int SourceRecordIndex,
    int? SourceLineNumber,
    string BrokerSymbol,
    string BuyFillId,
    string SellFillId,
    decimal MatchedQuantity,
    decimal SourceReportedPnL);
