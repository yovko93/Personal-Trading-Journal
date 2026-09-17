namespace PersonalTradingJournal.Application.Trades;

/// <summary>
/// Reads an authoritative, server-paged projection of persisted trades.
/// </summary>
/// <remarks>
/// Results include open and closed trades regardless of the current activation state of
/// their referenced account or instrument. Ordering is selected explicitly by the caller,
/// with Trade ID used as the deterministic final tie-breaker.
/// </remarks>
public interface ITradeListReader
{
    Task<TradeListPage> GetPageAsync(
        TradeListQuery query,
        CancellationToken cancellationToken = default);
}
