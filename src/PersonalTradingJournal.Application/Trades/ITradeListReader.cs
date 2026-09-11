namespace PersonalTradingJournal.Application.Trades;

/// <summary>
/// Reads a bounded, authoritative projection of recent persisted trades.
/// </summary>
/// <remarks>
/// Results include open and closed trades regardless of the current activation state of
/// their referenced account or instrument. They are ordered by market-event chronology:
/// <see cref="TradeListItem.OpenedAtUtc"/> descending, then
/// <see cref="TradeListItem.Id"/> ascending as a deterministic tie-breaker.
/// </remarks>
public interface ITradeListReader
{
    Task<IReadOnlyList<TradeListItem>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default);
}
