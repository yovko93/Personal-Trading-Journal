using System.Globalization;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Desktop.ViewModels.Dashboard;

public sealed record DashboardCard(string Label, string Value, string Explanation);
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
        DailyPnl = source.DailyPnl.Select(p => Row(p, source.Currency)).ToArray();
        CumulativePnl = source.CumulativeRealizedPnl.Select(p => Row(p, source.Currency)).ToArray();
        // Do not rank a known subset as a complete Best/Worst result. Earliest date wins ties.
        bool rankable = DailyPnl.Count > 0 && DailyPnl.All(p => p.Value.HasValue);
        BestDay = rankable ? DailyPnl.OrderByDescending(p => p.Value).ThenBy(p => p.Date).First() : null;
        WorstDay = rankable ? DailyPnl.OrderBy(p => p.Value).ThenBy(p => p.Date).First() : null;
        Cards = [
            new("Net P&L", Money(net.Total, source.Currency), note),
            new("Win Rate", Number(net.WinRatePercent, "%"), note),
            new("Profit Factor", Number(net.ProfitFactor.Value), $"{note} · {net.ProfitFactor.Status}"),
            new("Average Win", Money(net.AverageWin.Value, source.Currency), $"{note} · {net.AverageWin.Status}"),
            new("Average Loss", Money(net.AverageLoss.Value, source.Currency), $"Positive loss magnitude · {note} · {net.AverageLoss.Status}"),
            new("Best Day", BestDay?.Text ?? "Unavailable", "Daily Effective Net · earliest New York date wins ties; incomplete daily coverage prevents ranking."),
            new("Worst Day", WorstDay?.Text ?? "Unavailable", "Daily Effective Net · earliest New York date wins ties; incomplete daily coverage prevents ranking."),
            new("Total Trades", source.Metrics.ClosedTradeCount.ToString(CultureInfo.CurrentCulture), "Fully closed Trades in this period and currency; excludes open and partially exited Trades."),
            new("Gross P&L", Money(source.Metrics.Gross.Total, source.Currency), Coverage(source.Metrics.Gross)),
            new("Verified Net P&L", Money(source.Metrics.Net.Total, source.Currency), $"{Coverage(source.Metrics.Net)} · known subtotal: {Money(source.Metrics.Net.KnownSubtotal, source.Currency)}"),
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
        RecentTrades = source.RecentTrades.Select(t =>
        {
            EffectiveNetPnL effective = EffectiveNetPnL.Resolve(t.Status, t.GrossPnL, t.NetPnL);
            return $"{t.InstrumentSymbol ?? "Trade"} · {t.TradeId} · closed {TradingTimePolicy.ConvertUtcToTradingTime(t.ClosedAtUtc!.Value):yyyy-MM-dd HH:mm:ss} New York · " +
                $"Net {Money(effective.Value, source.Currency)}{(effective.IsEstimated ? " · estimated — commission/fees unknown" : effective.Value.HasValue ? " · verified" : " · unavailable")}";
        }).ToArray();
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
    public IReadOnlyList<string> RecentTrades { get; }
    public string CoverageNote => Coverage(Source.Metrics.EffectiveNet);
    public static string Money(decimal? value, string currency) => value is { } amount ? $"{amount:N2} {currency}" : "—";
    private static string Number(decimal? value, string suffix = "") => value is { } amount ? $"{amount:N2}{suffix}" : "—";
    private static string Coverage(PnlMetrics metrics) =>
        $"{(metrics.IsEstimated ? "Estimated — commission/fees unknown" : metrics.Basis == PnlBasis.Gross ? "Gross" : "Verified Net")} · " +
        $"{metrics.Coverage.KnownTradeCount}/{metrics.Coverage.ClosedTradeCount} available · {metrics.EstimatedTradeCount} estimated · {metrics.Coverage.Status}";
    private static DashboardChartRow Row(PnlChartPoint point, string currency) =>
        new(point.NewYorkDate, point.Metrics.EffectiveNet.Total, point.Metrics.EffectiveNet.IsEstimated,
            currency, Coverage(point.Metrics.EffectiveNet));
}
