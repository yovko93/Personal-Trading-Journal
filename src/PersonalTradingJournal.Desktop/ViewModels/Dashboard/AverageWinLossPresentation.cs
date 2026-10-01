using PersonalTradingJournal.Application.Analytics;

namespace PersonalTradingJournal.Desktop.ViewModels.Dashboard;

/// <summary>Comparison of authoritative EffectiveNet averages, never their totals or Profit Factor.</summary>
public sealed record AverageWinLossPresentation(PnlMetrics? Metrics, string Currency)
{
    public decimal? Win => Metrics?.AverageWin.Value;
    public decimal? LossMagnitude => Metrics?.AverageLoss.Value;
    public decimal? Ratio
    {
        get
        {
            if (Win is not { } win || LossMagnitude is not > 0m) return null;
            try { return win / LossMagnitude.Value; }
            catch (OverflowException) { return null; }
        }
    }
    public bool HasComparison => Ratio.HasValue;
    public decimal WinShare
    {
        get
        {
            if (!HasComparison) return 0m;
            decimal win = Win!.Value, loss = LossMagnitude!.Value, scale = Math.Max(win, loss);
            return (win / scale) / (win / scale + loss / scale);
        }
    }
    public string WinText => Win is { } value ? DashboardCurrencyPresentation.Money(value, Currency) : "N/A";
    public string LossText => LossMagnitude is { } value ? DashboardCurrencyPresentation.Money(-value, Currency) : "N/A";
    public string RatioText => Ratio is { } ratio ? $"{ratio:N2}" : "N/A";
    public string Description => $"Average Win {WinText}; Average Loss {LossText}. Win/loss ratio {RatioText}. " +
        (HasComparison ? "Average win divided by positive average loss magnitude; comparison bar uses these averages, not total P&L."
            : "Ratio/comparison requires both known averages and a positive loss denominator within decimal range.") +
        (Metrics?.IsEstimated == true ? " Estimated — commission/fees unknown." : "");
}
