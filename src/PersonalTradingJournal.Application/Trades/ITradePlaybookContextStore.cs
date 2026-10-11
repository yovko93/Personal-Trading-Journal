using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Trades;

/// <summary>Repository boundary only; no Desktop or Trade-linking command is introduced.</summary>
public interface ITradePlaybookContextStore
{
    Task<TradePlaybookContext?> GetAsync(Guid tradeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert once, atomically rechecking Trade audit/classification/opening time, active Setup,
    /// and exact persisted definition. False on stale/missing records or an existing capture.
    /// Never rewrites classification, executions or prior context. Context is deleted with its Trade.
    /// </summary>
    Task<bool> TryAddAsync(TradePlaybookContext context, DateTimeOffset expectedTradeUpdatedAtUtc,
        CancellationToken cancellationToken = default);
}
