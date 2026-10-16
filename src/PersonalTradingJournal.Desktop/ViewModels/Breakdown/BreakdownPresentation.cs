using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;

namespace PersonalTradingJournal.Desktop.ViewModels.Breakdown;

public enum BreakdownSortColumn { Group, Trades, WinRate, Pnl, Average, ProfitFactor, Coverage }

/// <summary>Formatting only: never derive analytics from rows or enumerate contributing Trade IDs.</summary>
public sealed record BreakdownMetricPresentation(BreakdownMetrics Source, string Currency)
{
    public int Trades => Source.EligibleTradeCount;
    public string Pnl => DashboardCurrencyPresentation.Money(Source.Selected.Total, Currency);
    public string Average => DashboardCurrencyPresentation.Money(Source.Selected.AverageTrade.Value, Currency);
    public string WinRate => Source.Selected.WinRatePercent is { } rate ? $"{rate:N1}%" : "—";
    public string ProfitFactor => Source.Selected.ProfitFactor.Status == ProfitFactorStatus.NoLosses ? "∞"
        : Source.Selected.ProfitFactor.Value is { } factor ? $"{factor:N2}" : "—";
    public string FactorExplanation => Source.Selected.ProfitFactor.Status switch
    {
        ProfitFactorStatus.NoLosses => "Unbounded: positive profit and no losses.",
        ProfitFactorStatus.AllBreakEven => "Undefined: all evaluable outcomes are break-even.",
        ProfitFactorStatus.NoTrades => "Unavailable: no closed Trades.",
        ProfitFactorStatus.IncompleteCoverage => "Unavailable: incomplete financial coverage.",
        _ => "Selected-basis profits divided by loss magnitude, with complete financial coverage."
    };
    public string UnavailableExplanation => Trades == 0 ? "No matching closed Trades."
        : Source.Selected.Coverage.UnknownTradeCount > 0 ? "Unavailable metrics require complete financial coverage; no partial total is shown."
        : "Win Rate uses all eligible closed Trades, including break-evens; average uses the same population and selected basis.";
    public string Basis => Source.FinancialSources.Source switch
    {
        BreakdownFinancialSource.Net => "Net", BreakdownFinancialSource.NetGross => "Net/Gross",
        BreakdownFinancialSource.GrossFallback => "Gross fallback", BreakdownFinancialSource.Gross => "Gross",
        _ => "Unavailable"
    };
    public string Coverage => $"{Basis} · {Source.FinancialSources.EvaluableTradeCount}/{Trades} evaluable" +
        (Source.FinancialSources.GrossFallbackCount > 0 ? $" · {Source.FinancialSources.GrossFallbackCount} Gross fallback" : "") +
        (Source.FinancialSources.UnavailableCount > 0 ? $" · {Source.FinancialSources.UnavailableCount} unavailable" : "");
    public string CoverageDetails => $"Actual Net used: {Source.FinancialSources.NetUsedCount}; Gross fallback: {Source.FinancialSources.GrossFallbackCount}; " +
        $"explicit Gross used: {Source.FinancialSources.GrossUsedCount}; unavailable: {Source.FinancialSources.UnavailableCount}; " +
        $"missing total costs: {Source.Data.MissingCostTradeCount}. Gross fallback is not estimated Net. {UnavailableExplanation}";
    public bool IsSmallSample => Source.Selected.EvaluableTradeCount is > 0 and < 20;
    public string Sample => IsSmallSample ? "Small sample" : "";
}

public sealed record BreakdownGroupPresentation(BreakdownGroupMetrics Source, string Currency)
{
    public BreakdownGroupKey Key => Source.Key;
    public string Label => Source.Label;
    public BreakdownMetricPresentation Metrics { get; } = new(Source.Metrics, Currency);
    public string AccessibleDescription => $"{Label}. {Metrics.Trades} Trades. P&L {Metrics.Pnl}. Win Rate {Metrics.WinRate}. " +
        $"Average {Metrics.Average}. Profit Factor {Metrics.ProfitFactor}. {Metrics.FactorExplanation} {Metrics.Coverage}. {Metrics.Sample}";
}

public sealed record BreakdownPresentation(BreakdownAnalyticsSnapshot Source)
{
    public BreakdownMetricPresentation Summary { get; } = new(Source.Summary, Source.Query.Currency);
    public IReadOnlyList<BreakdownGroupPresentation> Rows { get; } = Source.Groups.Select(g => new BreakdownGroupPresentation(g, Source.Query.Currency)).ToArray();
    public string GroupingNote => Source.Query.Grouping is BreakdownGrouping.EntryWeekday or BreakdownGrouping.EntryHour
        ? "Grouped by entry time in New York; period membership still uses closure dates." : "";
    public string SetupCoverage => Source.Query.Grouping == BreakdownGrouping.TradingSetup
        ? $"Setup assigned: {Source.Summary.Data.SetupClassification.KnownTradeCount}/{Source.Summary.EligibleTradeCount} · " +
          $"Unclassified: {Source.Summary.Data.UnclassifiedTradeCount} · Missing Setup metadata: {Source.Summary.Data.UnavailableSetupMetadataCount}." : "";

    public IReadOnlyList<BreakdownGroupPresentation> Sort(BreakdownSortColumn? column, bool descending)
    {
        if (column is null) return Rows; // Calculator's deterministic initial order, not label order.
        return Rows.Order(Comparer<BreakdownGroupPresentation>.Create((a, b) =>
        {
            int result;
            if (column == BreakdownSortColumn.Group)
                result = a.Key.Grouping is BreakdownGrouping.EntryWeekday or BreakdownGrouping.EntryHour or BreakdownGrouping.HoldingDuration
                    ? a.Key.CompareTo(b.Key) : StringComparer.OrdinalIgnoreCase.Compare(a.Label, b.Label);
            else if (column == BreakdownSortColumn.Coverage)
                result = StringComparer.Ordinal.Compare(a.Metrics.Basis, b.Metrics.Basis);
            else
            {
                decimal? av = Value(a), bv = Value(b);
                bool ai = column == BreakdownSortColumn.ProfitFactor && a.Source.Metrics.Selected.ProfitFactor.Status == ProfitFactorStatus.NoLosses;
                bool bi = column == BreakdownSortColumn.ProfitFactor && b.Source.Metrics.Selected.ProfitFactor.Status == ProfitFactorStatus.NoLosses;
                bool am = av is null && !ai, bm = bv is null && !bi;
                if (am != bm) return am ? 1 : -1; // Missing last in BOTH directions.
                result = ai != bi ? (ai ? 1 : -1) : Nullable.Compare(av, bv);
            }
            if (result != 0) return descending ? -result : result;
            return a.Key.CompareTo(b.Key); // Stable identity tie-break, independent of direction/labels.
        })).ToArray();

        decimal? Value(BreakdownGroupPresentation row) => column switch
        {
            BreakdownSortColumn.Trades => row.Metrics.Trades,
            BreakdownSortColumn.WinRate => row.Source.Metrics.Selected.WinRatePercent,
            BreakdownSortColumn.Pnl => row.Source.Metrics.Selected.Total,
            BreakdownSortColumn.Average => row.Source.Metrics.Selected.AverageTrade.Value,
            BreakdownSortColumn.ProfitFactor => row.Source.Metrics.Selected.ProfitFactor.Value,
            _ => null
        };
    }
}
