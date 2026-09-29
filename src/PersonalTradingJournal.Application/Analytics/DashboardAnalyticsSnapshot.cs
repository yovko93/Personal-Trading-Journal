namespace PersonalTradingJournal.Application.Analytics;

public enum AverageRUnavailableReason { AuthoritativeInitialRiskNotRecorded }

/// <summary>One occupied New York date, with period and selection-to-date coverage.</summary>
public sealed record DailyTradeMetrics(
    DateOnly NewYorkDate, ClosedTradeMetrics Metrics, ClosedTradeMetrics CumulativeMetrics);

/// <summary>
/// One occupied Monday–Sunday New York week. Only selected Trades contribute, even when
/// date filters cover just part of this calendar week. Empty weeks are omitted.
/// </summary>
public sealed record WeeklyTradeMetrics(
    DateOnly WeekStartingMonday, ClosedTradeMetrics Metrics, ClosedTradeMetrics CumulativeMetrics);
public sealed record SetupTradeMetrics(Guid? TradingSetupId, ClosedTradeMetrics Metrics)
{
    public string? Name { get; init; }
    public bool? IsActive { get; init; }
    public SetupReferenceStatus ReferenceStatus { get; init; } =
        TradingSetupId is null ? SetupReferenceStatus.Unclassified : SetupReferenceStatus.NotLoaded;
}

/// <summary>There is deliberately no combined-currency P&amp;L total.</summary>
public sealed record CurrencyTradeMetrics(
    string Currency,
    int ExcludedOpenTradeCount,
    ClosedTradeMetrics Metrics,
    IReadOnlyList<DailyTradeMetrics> Days,
    IReadOnlyList<WeeklyTradeMetrics> Weeks,
    IReadOnlyList<SetupTradeMetrics> Setups,
    IReadOnlyList<PnlChartPoint> DailyPnl,
    IReadOnlyList<PnlChartPoint> CumulativeRealizedPnl)
{
    public bool HasClosedTrades => Metrics.ClosedTradeCount > 0;
    public bool HasClassifiedTrades => Setups.Any(setup => setup.TradingSetupId.HasValue);
}

/// <summary>
/// Read-only calculations and period series only: no chart rendering, UI wiring or risk estimation.
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
