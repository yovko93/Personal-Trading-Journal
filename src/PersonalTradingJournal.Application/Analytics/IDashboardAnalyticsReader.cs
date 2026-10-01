namespace PersonalTradingJournal.Application.Analytics;

public interface IDashboardAnalyticsReader
{
    /// <summary>
    /// Reads all matching fully closed Trades and applies the M12.1 calculator without paging or caching.
    /// Open Trades are excluded by the query, so SelectedTradeCount is the matched closed count and
    /// ExcludedOpenTradeCount is zero (not a count of open Trades elsewhere in the database).
    /// Unknown but nonempty filter IDs produce an empty result. Cancellation and database failures propagate.
    /// </summary>
    Task<DashboardAnalyticsSnapshot> GetAsync(
        DashboardAnalyticsQuery query,
        CancellationToken cancellationToken = default);
}
