namespace PersonalTradingJournal.Application.Screenshots;

public sealed record DeleteTradeScreenshotCommand(
    Guid TradeId,
    Guid ScreenshotId);
