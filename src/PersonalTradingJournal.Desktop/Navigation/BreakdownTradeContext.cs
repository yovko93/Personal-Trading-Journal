using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Desktop.Navigation;

/// <summary>Immutable click-time attribution and exact membership. The retained Breakdown VM owns return UI state.</summary>
public sealed record BreakdownTradeContext(Guid NavigationId, BreakdownGroupSelection Selection,
    string GroupLabel, string AppliedContext, string FinancialSource, TradeIdCohort Cohort)
{
    public int OriginalCount => Cohort.TradeIds.Count;
    public string GroupingLabel => Selection.Key.Grouping switch
    {
        BreakdownGrouping.TradingSetup => "Setup",
        BreakdownGrouping.EntryWeekday => "Entry weekday",
        BreakdownGrouping.EntryHour => "Entry hour",
        BreakdownGrouping.HoldingDuration => "Hold time",
        _ => Selection.Key.Grouping.ToString(),
    };
}
