using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Analytics.Breakdown;

/// <summary>
/// Filters one complete population of fully closed Trades with a non-null closure instant.
/// Period membership uses New York closure dates; entry-time grouping never changes it.
/// Missing P&amp;L/costs do not remove a Trade from the population or its coverage denominator.
/// </summary>
public sealed record BreakdownQuery
{
    public BreakdownQuery(
        string currency,
        Guid? tradingAccountId = null,
        Guid? instrumentId = null,
        DateOnly? closedFromNewYork = null,
        DateOnly? closedThroughNewYork = null,
        PnlBasis basis = PnlBasis.Net,
        BreakdownGrouping grouping = BreakdownGrouping.Instrument)
    {
        // Reuse the established identifier/range validation and DST-aware UTC bounds.
        TradeFilter = new(tradingAccountId, instrumentId, closedFromNewYork, closedThroughNewYork);
        if (basis is not (PnlBasis.Gross or PnlBasis.Net))
            throw new ArgumentOutOfRangeException(nameof(basis), "Breakdown supports Gross or strict Net, not estimated Net.");
        if (!Enum.IsDefined(grouping))
            throw new ArgumentOutOfRangeException(nameof(grouping), "Select a supported Breakdown grouping; Strategy is deferred.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Select one historical pricing currency.", nameof(currency));

        // Same normalization and maximum length as TradePricingSnapshot, not an ISO-only list.
        string normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length > 8)
            throw new ArgumentOutOfRangeException(nameof(currency), "The currency cannot exceed 8 characters.");

        Currency = normalized;
        Basis = basis;
        Grouping = grouping;
    }

    /// <summary>Null identifiers mean all; absent date bounds mean all history on that side.</summary>
    public DashboardAnalyticsQuery TradeFilter { get; }
    /// <summary>Exact historical pricing currency; no Account-currency substitution or FX.</summary>
    public string Currency { get; }
    /// <summary>Net always means authoritative Net with complete required costs.</summary>
    public PnlBasis Basis { get; }
    public BreakdownGrouping Grouping { get; }
    public TradeStatus IncludedStatus => TradeStatus.Closed;
}
