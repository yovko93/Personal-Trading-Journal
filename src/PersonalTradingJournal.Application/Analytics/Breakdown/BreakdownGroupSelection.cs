namespace PersonalTradingJournal.Application.Analytics.Breakdown;

/// <summary>
/// Original analytics query/group identity. M20.8 drill-down freezes contributing IDs separately;
/// these predicates describe their original attribution, not continuously reevaluated membership.
/// Entry keys describe New York opening time, duration keys describe UTC elapsed time,
/// and null Setup identity means unassigned, never a missing reference label.
/// No label, displayed metric, page or cached count can broaden this selection.
/// </summary>
public sealed record BreakdownGroupSelection
{
    public BreakdownGroupSelection(BreakdownQuery query, BreakdownGroupKey key)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(key);
        if (query.Grouping != key.Grouping)
            throw new ArgumentException("The selected group must match the query's grouping dimension.", nameof(key));
        if (key.Grouping == BreakdownGrouping.Account && query.TradeFilter.TradingAccountId is { } account && key.ReferenceId != account)
            throw new ArgumentException("The selected Account group conflicts with the original Account filter.", nameof(key));
        if (key.Grouping == BreakdownGrouping.Instrument && query.TradeFilter.InstrumentId is { } instrument && key.ReferenceId != instrument)
            throw new ArgumentException("The selected Instrument group conflicts with the original Instrument filter.", nameof(key));
        Query = query;
        Key = key;
    }

    public BreakdownQuery Query { get; }
    public BreakdownGroupKey Key { get; }
}
