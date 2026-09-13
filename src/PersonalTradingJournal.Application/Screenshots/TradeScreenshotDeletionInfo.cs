namespace PersonalTradingJournal.Application.Screenshots;

public sealed record TradeScreenshotDeletionInfo(
    Guid Id,
    Guid TradeId,
    string StorageKey);
