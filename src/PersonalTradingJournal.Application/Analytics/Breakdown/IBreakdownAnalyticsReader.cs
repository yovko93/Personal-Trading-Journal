namespace PersonalTradingJournal.Application.Analytics.Breakdown;

public interface IBreakdownAnalyticsReader
{
    /// <summary>
    /// Reads the complete matching closed-Trade population and reference metadata from one
    /// database snapshot, then delegates to BreakdownMetricCalculator. No paging or caching.
    /// Unknown filter IDs produce an empty result; cancellation and read/calculation failures
    /// propagate without publishing partial results. Storage resources end within the call.
    /// </summary>
    Task<BreakdownAnalyticsSnapshot> GetAsync(BreakdownQuery query,
        CancellationToken cancellationToken = default);
}
