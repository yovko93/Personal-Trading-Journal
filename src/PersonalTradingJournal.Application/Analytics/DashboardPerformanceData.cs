namespace PersonalTradingJournal.Application.Analytics;

/// <summary>
/// One occupied New York date. Plot the chosen basis's Total, not KnownSubtotal;
/// null denotes incomplete/unavailable data, not zero. Currency belongs to the enclosing bucket.
/// Cumulative points are realized Trade P&L from the selection, never account equity/balance.
/// </summary>
public sealed record PnlChartPoint(DateOnly NewYorkDate, ClosedTradeMetrics Metrics);

/// <summary>Current reference metadata, not a historical name or an economics input.</summary>
public sealed record TradingSetupAnalyticsReference(Guid TradingSetupId, string Name, bool IsActive);

public enum SetupReferenceStatus { Unclassified, Available, Missing, NotLoaded }
