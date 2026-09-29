namespace PersonalTradingJournal.Application.Analytics;

public enum AverageRUnavailableReason { AuthoritativeInitialRiskNotRecorded }

public sealed record DailyTradeMetrics(DateOnly NewYorkDate, ClosedTradeMetrics Metrics);
public sealed record SetupTradeMetrics(Guid? TradingSetupId, ClosedTradeMetrics Metrics);

/// <summary>There is deliberately no combined-currency P&amp;L total.</summary>
public sealed record CurrencyTradeMetrics(
    string Currency,
    int ExcludedOpenTradeCount,
    ClosedTradeMetrics Metrics,
    IReadOnlyList<DailyTradeMetrics> Days,
    IReadOnlyList<SetupTradeMetrics> Setups);

/// <summary>
/// Read-only calculations only: no database aggregation, chart points, UI wiring or risk estimation.
/// An empty population returns no currency buckets, not a fabricated zero-valued USD bucket.
/// </summary>
public sealed record DashboardAnalyticsSnapshot(
    int SelectedTradeCount,
    int ExcludedOpenTradeCount,
    IReadOnlyList<CurrencyTradeMetrics> Currencies)
{
    public decimal? AverageR => null;
    public AverageRUnavailableReason AverageRReason =>
        AverageRUnavailableReason.AuthoritativeInitialRiskNotRecorded;
}
