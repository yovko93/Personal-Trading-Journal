namespace PersonalTradingJournal.Application.Trades;

/// <summary>
/// Atomically removes a Trade and all database-owned dependent rows.
/// </summary>
public interface ITradeDeletionStore
{
    Task<TradeDeletionInfo?> DeleteAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default);
}
