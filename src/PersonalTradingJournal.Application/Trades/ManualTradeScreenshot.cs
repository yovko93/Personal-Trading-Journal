using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Trades;

public sealed record ManualTradeScreenshot(TradeScreenshotType Type, string? Timeframe, string FileName, byte[] Content);

/// <summary>Commits a new Trade and its screenshot metadata in one database transaction.</summary>
public interface IManualTradeScreenshotStore
{
    Task AddAsync(Trade trade, IReadOnlyList<TradeScreenshot> screenshots, CancellationToken cancellationToken = default);
}
