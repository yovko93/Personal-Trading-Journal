namespace PersonalTradingJournal.Application.Trades;

public sealed record CloseManualTradeCommand(
    Guid TradeId,
    DateTimeOffset ExecutedAtUtc,
    decimal Price,
    decimal Commission,
    decimal Fees);
