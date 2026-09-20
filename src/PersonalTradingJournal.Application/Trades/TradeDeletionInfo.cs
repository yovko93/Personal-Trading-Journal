namespace PersonalTradingJournal.Application.Trades;

public sealed record TradeDeletionInfo(
    Guid TradeId,
    IReadOnlyList<string> ScreenshotStorageKeys);
