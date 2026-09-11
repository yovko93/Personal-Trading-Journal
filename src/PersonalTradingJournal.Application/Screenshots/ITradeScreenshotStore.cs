using PersonalTradingJournal.Domain.Screenshots;

namespace PersonalTradingJournal.Application.Screenshots;

/// <summary>
/// Persists authoritative Trade screenshot metadata.
/// </summary>
/// <remarks>
/// This boundary does not copy, resolve, open, validate, or delete physical image files.
/// Binary image storage is a separate concern.
/// </remarks>
public interface ITradeScreenshotStore
{
    Task AddAsync(
        TradeScreenshot screenshot,
        CancellationToken cancellationToken = default);
}
