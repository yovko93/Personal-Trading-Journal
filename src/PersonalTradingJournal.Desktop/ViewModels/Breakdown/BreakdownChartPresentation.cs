using PersonalTradingJournal.Desktop.ViewModels.Dashboard;

namespace PersonalTradingJournal.Desktop.ViewModels.Breakdown;

public enum BreakdownChartMetric { Pnl, Trades, WinRate }

/// <summary>Display coordinates only. Raw decimal values and metrics remain unchanged.</summary>
public sealed record BreakdownChartRow(BreakdownGroupPresentation Group, decimal? Value, string ValueText,
    double Minimum, double Maximum)
{
    public double ZeroPosition => -Minimum / (Maximum - Minimum);
    public double? ValuePosition => Value is { } v ? ((double)v - Minimum) / (Maximum - Minimum) : null;
    public string MetricName { get; init; } = "Selected P&L";
    public string AccessibleDescription => $"{Group.Label}. {MetricName}: {ValueText}. {Group.Metrics.Coverage}. {Group.Metrics.UnavailableExplanation}";
}

public sealed class BreakdownChartPresentation
{
    public BreakdownChartPresentation(IReadOnlyList<BreakdownGroupPresentation> groups, BreakdownChartMetric metric,
        string currency, string basis)
    {
        Metric = metric;
        Title = metric switch
        {
            BreakdownChartMetric.Trades => "Eligible closed Trades by group · count",
            BreakdownChartMetric.WinRate => "Win Rate by group · %",
            _ => $"Selected P&L by group · {currency} · {basis}"
        };
        // Convert only for geometry. Double safely spans the entire decimal range, including min/max together.
        double min = 0, max = metric == BreakdownChartMetric.WinRate ? 100 : 0;
        if (metric != BreakdownChartMetric.WinRate)
            foreach (var group in groups)
                if (Value(group) is { } value) { min = Math.Min(min, (double)value); max = Math.Max(max, (double)value); }
        if (min == max) max = 1; // Known zero and empty/unavailable populations have a finite zero-origin scale.
        Minimum = min; Maximum = max;
        Rows = groups.Select(g => new BreakdownChartRow(g, Value(g), Text(g), min, max)
        { MetricName = metric switch { BreakdownChartMetric.Trades => "Eligible closed Trades", BreakdownChartMetric.WinRate => "Win Rate", _ => "Selected P&L" } }).ToArray();
        AxisDescription = metric switch
        {
            BreakdownChartMetric.WinRate => "Common scale: 0–100%. — means unavailable, not 0%.",
            BreakdownChartMetric.Trades => $"Common scale: 0 to {max:N0} Trades. Zero baseline shown.",
            _ => $"Common scale: {min:N2} to {max:N2} {currency}. Zero baseline shown; — means unavailable."
        };
        EmptyMessage = groups.Count == 0 ? "No matching groups to compare." : "";

        decimal? Value(BreakdownGroupPresentation g) => metric switch
        {
            BreakdownChartMetric.Trades => g.Metrics.Trades,
            BreakdownChartMetric.WinRate => g.Source.Metrics.Selected.WinRatePercent,
            _ => g.Source.Metrics.Selected.Total
        };
        string Text(BreakdownGroupPresentation g) => metric switch
        {
            BreakdownChartMetric.Trades => $"{g.Metrics.Trades:N0} Trades",
            BreakdownChartMetric.WinRate => g.Metrics.WinRate == "—" ? "— Unavailable" : g.Metrics.WinRate,
            _ => g.Source.Metrics.Selected.Total is { } v ? (v > 0 ? "+" : "") + g.Metrics.Pnl : "— Unavailable"
        };
    }
    public BreakdownChartMetric Metric { get; }
    public string Title { get; }
    public string AxisDescription { get; }
    public string EmptyMessage { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public IReadOnlyList<BreakdownChartRow> Rows { get; }
}

public sealed record BreakdownGroupDetails(BreakdownGroupPresentation Group, string Context)
{
    public string Counts => $"{Group.Metrics.Trades} eligible · {Group.Source.Metrics.Selected.EvaluableTradeCount} financially evaluable";
    public string Outcomes => $"Known outcomes: {Group.Source.Metrics.Selected.Wins} wins · {Group.Source.Metrics.Selected.Losses} losses · {Group.Source.Metrics.Selected.BreakEvens} break-even";
    public string Averages => $"Average trade: {Group.Metrics.Average}\nAverage winner: {DashboardCurrencyPresentation.Money(Group.Source.Metrics.Selected.AverageWin.Value, Group.Currency)}\nAverage loss (magnitude): {DashboardCurrencyPresentation.Money(Group.Source.Metrics.Selected.AverageLoss.Value, Group.Currency)}";
    public string Performance => $"Selected P&L: {Group.Metrics.Pnl}\nWin Rate: {Group.Metrics.WinRate}\nProfit Factor: {Group.Metrics.ProfitFactor}";
    public string Classification => Group.Key.Grouping == Application.Analytics.Breakdown.BreakdownGrouping.TradingSetup
        ? $"Setup assigned: {Group.Source.Metrics.Data.SetupClassification.KnownTradeCount}/{Group.Metrics.Trades} · Unclassified: {Group.Source.Metrics.Data.UnclassifiedTradeCount} · Missing metadata: {Group.Source.Metrics.Data.UnavailableSetupMetadataCount}" : "";
}
