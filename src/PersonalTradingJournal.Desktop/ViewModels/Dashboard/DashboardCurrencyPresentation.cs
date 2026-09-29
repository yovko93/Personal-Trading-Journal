using System.Globalization;
using PersonalTradingJournal.Application.Analytics;

namespace PersonalTradingJournal.Desktop.ViewModels.Dashboard;

public sealed record DashboardCard(string Label, string Value, string Explanation,
    string Badge = "", string Date = "", WinRatePresentation? Ring = null);

public sealed record WinRatePresentation(PnlMetrics Metrics)
{
    public bool IsAvailable => Metrics.WinRatePercent.HasValue;
    public decimal WinsShare => IsAvailable ? (decimal)Metrics.KnownWins / Metrics.Coverage.ClosedTradeCount : 0m;
    public decimal LossesShare => IsAvailable ? (decimal)Metrics.KnownLosses / Metrics.Coverage.ClosedTradeCount : 0m;
    public decimal BreakEvenShare => IsAvailable ? (decimal)Metrics.KnownBreakEvens / Metrics.Coverage.ClosedTradeCount : 0m;
    public string Value => IsAvailable ? $"{Metrics.WinRatePercent:N1}%" : "—";
    public string Description => IsAvailable
        ? $"Win rate {Value}. {Metrics.KnownWins} wins, {Metrics.KnownLosses} losses, {Metrics.KnownBreakEvens} break-even Trades.{(Metrics.IsEstimated ? " Estimated — commission/fees unknown." : "")}"
        : "Win rate unavailable: no Trades or incomplete outcome coverage.";
}
public sealed record DashboardChartRow(DateOnly Date, decimal? Value, bool IsEstimated, string Currency, string Coverage)
{
    public string Text => $"{Date:yyyy-MM-dd}: {DashboardCurrencyPresentation.Money(Value, Currency)}{(IsEstimated ? " · estimated" : "")} · {Coverage}";
}
public sealed record DashboardSetupRow(string Name, string Summary);

public sealed class DashboardCurrencyPresentation
{
    public DashboardCurrencyPresentation(CurrencyTradeMetrics source, DateOnly? periodStart = null)
    {
        Source = source;
        PeriodStart = periodStart ?? source.DailyPnl.FirstOrDefault()?.NewYorkDate;
        PnlMetrics net = source.Metrics.EffectiveNet;
        string note = Coverage(net);
        string badge = net.IsEstimated ? "Estimated" : "";
        DailyPnl = source.DailyPnl.Select(p => Row(p, source.Currency)).ToArray();
        CumulativePnl = source.CumulativeRealizedPnl.Select(p => Row(p, source.Currency)).ToArray();
        // Do not rank a known subset as a complete Best/Worst result. Earliest date wins ties.
        bool rankable = DailyPnl.Count > 0 && DailyPnl.All(p => p.Value.HasValue);
        BestDay = rankable ? DailyPnl.OrderByDescending(p => p.Value).ThenBy(p => p.Date).First() : null;
        WorstDay = rankable ? DailyPnl.OrderBy(p => p.Value).ThenBy(p => p.Date).First() : null;
        Cards = [
            new("Net P&L", Money(net.Total, source.Currency), note, badge),
            new("Win Rate", Number(net.WinRatePercent, "%"), note, badge, Ring: new(net)),
            new("Profit Factor", Number(net.ProfitFactor.Value), $"{note} · {net.ProfitFactor.Status}", badge),
            new("Average Win", Money(net.AverageWin.Value, source.Currency), $"{note} · {net.AverageWin.Status}", badge),
            new("Average Loss", Money(net.AverageLoss.Value, source.Currency), $"Positive loss magnitude · {note} · {net.AverageLoss.Status}", badge),
            DayCard("Best Day", BestDay),
            DayCard("Worst Day", WorstDay),
            new("Total Trades", source.Metrics.ClosedTradeCount.ToString(CultureInfo.CurrentCulture), "Fully closed Trades in this period and currency; excludes open and partially exited Trades."),
            new("Avg R", "Unavailable", "Authoritative initial risk per Trade is not recorded.")
        ];
        Setups = source.Setups.Select(s => new DashboardSetupRow(
            s.ReferenceStatus == SetupReferenceStatus.Unclassified ? "Unclassified"
            : $"{s.Name ?? $"Missing Setup ({s.TradingSetupId})"}{(s.IsActive == false ? " · inactive" : "")}",
            $"{s.Metrics.ClosedTradeCount} Trades · Net {Money(s.Metrics.EffectiveNet.Total, source.Currency)} · " +
            $"Win rate {Number(s.Metrics.EffectiveNet.WinRatePercent, "%")} · Avg win {Money(s.Metrics.EffectiveNet.AverageWin.Value, source.Currency)} ({s.Metrics.EffectiveNet.AverageWin.Status}) · " +
            $"Avg loss {Money(s.Metrics.EffectiveNet.AverageLoss.Value, source.Currency)} ({s.Metrics.EffectiveNet.AverageLoss.Status}) · " +
            $"PF {Number(s.Metrics.EffectiveNet.ProfitFactor.Value)} ({s.Metrics.EffectiveNet.ProfitFactor.Status}) · {Coverage(s.Metrics.EffectiveNet)} · " +
            $"Verified Net {Money(s.Metrics.Net.Total, source.Currency)} ({s.Metrics.Net.Coverage.KnownTradeCount}/{s.Metrics.ClosedTradeCount})")).ToArray();
    }

    public CurrencyTradeMetrics Source { get; }
    public DateOnly? PeriodStart { get; }
    public string Currency => Source.Currency;
    public IReadOnlyList<DashboardCard> Cards { get; }
    public IReadOnlyList<DashboardChartRow> DailyPnl { get; }
    public IReadOnlyList<DashboardChartRow> CumulativePnl { get; }
    public DashboardChartRow? BestDay { get; }
    public DashboardChartRow? WorstDay { get; }
    public IReadOnlyList<DashboardSetupRow> Setups { get; }
    public string CoverageNote => Coverage(Source.Metrics.EffectiveNet);
    public static string Money(decimal? value, string currency) => value is { } amount ? $"{amount:N2} {currency}" : "—";
    private static string Number(decimal? value, string suffix = "") => value is { } amount ? $"{amount:N2}{suffix}" : "—";
    private DashboardCard DayCard(string label, DashboardChartRow? day) => new(label,
        Money(day?.Value, Currency), day?.Text ?? "Unavailable: no closed Trades or incomplete daily outcome coverage.",
        day?.IsEstimated == true ? "Estimated" : "", day?.Date.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture) ?? "");
    private static string Coverage(PnlMetrics metrics) =>
        $"{(metrics.IsEstimated ? "Estimated — commission/fees unknown" : metrics.Basis == PnlBasis.Gross ? "Gross" : "Verified Net")} · " +
        $"{metrics.Coverage.KnownTradeCount}/{metrics.Coverage.ClosedTradeCount} available · {metrics.EstimatedTradeCount} estimated · {metrics.Coverage.Status}";
    private static DashboardChartRow Row(PnlChartPoint point, string currency) =>
        new(point.NewYorkDate, point.Metrics.EffectiveNet.Total, point.Metrics.EffectiveNet.IsEstimated,
            currency, Coverage(point.Metrics.EffectiveNet));
}
