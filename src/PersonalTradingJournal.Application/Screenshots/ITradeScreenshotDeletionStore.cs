namespace PersonalTradingJournal.Application.Screenshots;

/// <summary>
/// Deletes screenshot metadata scoped to its owning Trade.
/// </summary>
public interface ITradeScreenshotDeletionStore
{
    /// <returns>
    /// Opaque deletion information after the database delete commits, or
    /// <see langword="null"/> when no matching screenshot exists.
    /// </returns>
    Task<TradeScreenshotDeletionInfo?> DeleteAsync(
        Guid tradeId,
        Guid screenshotId,
        CancellationToken cancellationToken = default);
}
