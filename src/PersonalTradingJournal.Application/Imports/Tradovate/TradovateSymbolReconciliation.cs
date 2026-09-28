namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed record TradovateSymbolReconciliation(
    string BrokerSymbol,
    int SourceRecordCount,
    decimal MatchedQuantity,
    decimal ReconstructedBuyQuantity,
    decimal ReconstructedSellQuantity,
    bool IsQuantityReconciled);
