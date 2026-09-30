using System.Globalization;
using PersonalTradingJournal.Application.Analytics;

namespace PersonalTradingJournal.Desktop.ViewModels.Dashboard;

public sealed record DashboardCard(string Label, string Value, string Explanation,
    bool IsEstimated = false, string Date = "", WinRatePresentation? Ring = null, ProfitFactorPresentation? Factor = null,
    AverageWinLossPresentation? Averages = null, decimal? PnlValue = null);

public interface IOutcomeRingPresentation
{
    bool IsAvailable { get; }
    decimal WinsShare { get; }
    decimal LossesShare { get; }
    decimal BreakEvenShare { get; }
    string Description { get; }
}

public sealed record WinRatePresentation(PnlMetrics? Metrics) : IOutcomeRingPresentation
{
    public bool IsEmpty => Metrics is null || Metrics.Coverage.ClosedTradeCount == 0;
    public bool IsAvailable => Metrics is { WinRatePercent: not null, Coverage.ClosedTradeCount: > 0 };
    public decimal WinsShare => IsAvailable ? (decimal)Metrics!.KnownWins / Metrics.Coverage.ClosedTradeCount : 0m;
    public decimal LossesShare => IsAvailable ? (decimal)Metrics!.KnownLosses / Metrics.Coverage.ClosedTradeCount : 0m;
    public decimal BreakEvenShare => IsAvailable ? (decimal)Metrics!.KnownBreakEvens / Metrics.Coverage.ClosedTradeCount : 0m;
    public string WinsText => $"{Metrics?.KnownWins ?? 0} {(IsAvailable || IsEmpty ? "wins" : "known wins")}";
    public string LossesText => $"{Metrics?.KnownLosses ?? 0} {(IsAvailable || IsEmpty ? "losses" : "known losses")}";
    public string BreakEvensText => $"{Metrics?.KnownBreakEvens ?? 0} break-even";
    public bool HasBreakEvens => Metrics?.KnownBreakEvens > 0;
    public string Value => IsEmpty ? "0%" : IsAvailable ? $"{Metrics!.WinRatePercent:N1}%" : "N/A";
    public string Description => IsAvailable
        ? $"Win rate {Value}. {Metrics!.KnownWins} wins, {Metrics.KnownLosses} losses, {Metrics.KnownBreakEvens} break-even Trades.{(Metrics.IsEstimated ? " Estimated — commission/fees unknown." : "")}"
        : IsEmpty ? "No closed Trades. Zero counts and 0% are empty-display values; no win rate has been calculated."
        : "Win rate unavailable: incomplete outcome coverage. Counts show known outcomes only.";
}

public sealed record ProfitFactorPresentation(PnlMetrics? Metrics, string Currency) : IOutcomeRingPresentation
{
    public bool IsEmpty => Metrics is null || Metrics.Coverage.ClosedTradeCount == 0;
    public bool HasCompleteEconomics => Metrics?.Coverage.Status == MetricCoverageStatus.Complete;
    public decimal? Profit => IsEmpty ? 0m : HasCompleteEconomics ? Metrics!.KnownProfitSum : null;
    public decimal? Loss => IsEmpty ? 0m : HasCompleteEconomics ? Metrics!.KnownLossMagnitude : null;
    public bool IsAvailable => HasCompleteEconomics && (Profit > 0m || Loss > 0m);
    // Normalize before adding so two individually valid decimal sums cannot overflow their visual denominator.
    public decimal WinsShare
    {
        get
        {
            if (!IsAvailable) return 0m;
            decimal profit = Profit!.Value, loss = Loss!.Value, scale = Math.Max(profit, loss);
            return (profit / scale) / (profit / scale + loss / scale);
        }
    }
    public decimal LossesShare => IsAvailable ? 1m - WinsShare : 0m;
    public decimal BreakEvenShare => 0m;
    public string Value => Metrics?.ProfitFactor.Value is { } factor ? $"{factor:N2}" : "N/A";
    public string ProfitText => Profit is { } value ? DashboardCurrencyPresentation.Money(value, Currency) : "N/A";
    public string LossText => Loss is { } value ? DashboardCurrencyPresentation.Money(value, Currency) : "N/A";
    public string Description => $"Profit Factor {Value}: winning EffectiveNet {ProfitText}; absolute losing EffectiveNet {LossText}. " +
        $"{(IsEmpty ? "No closed Trades." : Metrics!.ProfitFactor.Status.ToString())}" +
        (Metrics?.IsEstimated == true ? " Estimated — commission/fees unknown." : "");
}
public sealed record DashboardChartRow(DateOnly Date, decimal? Value, bool IsEstimated, string Currency, string Coverage, int TradeCount)
{
    public string AmountText => DashboardCurrencyPresentation.Money(Value, Currency);
    public string TradeCountText => $"{TradeCount.ToString(CultureInfo.CurrentCulture)} Trades";
    public string Description => $"{Date:yyyy-MM-dd}: {AmountText} · {TradeCountText}{(IsEstimated ? " · estimated — unknown costs use Gross as Net" : "")} · {Coverage}";
}
public sealed record DashboardSetupRow(string Name, string Summary);

public sealed class DashboardCurrencyPresentation
{
    public DashboardCurrencyPresentation(CurrencyTradeMetrics? source, DateOnly? periodStart = null)
    {
        Source = source;
        PeriodStart = periodStart ?? source?.DailyPnl.FirstOrDefault()?.NewYorkDate;
        PnlMetrics? net = source?.Metrics.EffectiveNet;
        string note = net is null ? "No closed Trades in this scope. No currency or economics are inferred." : Coverage(net);
        bool estimated = net?.IsEstimated == true;
        DailyPnl = (source?.DailyPnl ?? []).Select(p => Row(p, Currency)).ToArray();
        CumulativePnl = (source?.CumulativeRealizedPnl ?? []).Select(p => Row(p, Currency)).ToArray();
        // Do not rank a known subset as a complete Best/Worst result. Earliest date wins ties.
        bool rankable = DailyPnl.Count > 0 && DailyPnl.All(p => p.Value.HasValue);
        BestDay = rankable ? DailyPnl.OrderByDescending(p => p.Value).ThenBy(p => p.Date).First() : null;
        WorstDay = rankable ? DailyPnl.OrderBy(p => p.Value).ThenBy(p => p.Date).First() : null;
        var averages = new AverageWinLossPresentation(net, Currency);
        Cards = [
            new("Net P&L", IsEmpty ? Money(0m, Currency) : Money(net!.Total, Currency), note, estimated,
                PnlValue: IsEmpty ? 0m : net!.Total),
            new("Win Rate", new WinRatePresentation(net).Value, note, estimated, Ring: new(net)),
            new("Profit Factor", new ProfitFactorPresentation(net, Currency).Value, $"{note} · {net?.ProfitFactor.Status}", estimated, Factor: new(net, Currency)),
            new("Avg Win / Avg Loss", averages.RatioText, averages.Description, estimated, Averages: averages),
            DayCard("Best Day", BestDay),
            DayCard("Worst Day", WorstDay),
            new("Total Trades", (source?.Metrics.ClosedTradeCount ?? 0).ToString(CultureInfo.CurrentCulture), "Fully closed Trades in this period and currency; excludes open and partially exited Trades."),
            new("Avg R", "Unavailable", "Authoritative initial risk per Trade is not recorded.")
        ];
        Setups = (source?.Setups ?? []).Select(s => new DashboardSetupRow(
            s.ReferenceStatus == SetupReferenceStatus.Unclassified ? "Unclassified"
            : $"{s.Name ?? $"Missing Setup ({s.TradingSetupId})"}{(s.IsActive == false ? " · inactive" : "")}",
            $"{s.Metrics.ClosedTradeCount} Trades · Net {Money(s.Metrics.EffectiveNet.Total, Currency)} · " +
            $"Win rate {Number(s.Metrics.EffectiveNet.WinRatePercent, "%")} · Avg win {Money(s.Metrics.EffectiveNet.AverageWin.Value, Currency)} ({s.Metrics.EffectiveNet.AverageWin.Status}) · " +
            $"Avg loss {Money(s.Metrics.EffectiveNet.AverageLoss.Value, Currency)} ({s.Metrics.EffectiveNet.AverageLoss.Status}) · " +
            $"PF {Number(s.Metrics.EffectiveNet.ProfitFactor.Value)} ({s.Metrics.EffectiveNet.ProfitFactor.Status}) · {Coverage(s.Metrics.EffectiveNet)} · " +
            $"Verified Net {Money(s.Metrics.Net.Total, Currency)} ({s.Metrics.Net.Coverage.KnownTradeCount}/{s.Metrics.ClosedTradeCount})")).ToArray();
    }

    public CurrencyTradeMetrics? Source { get; }
    public bool IsEmpty => Source is null || !Source.HasClosedTrades;
    public bool HasNoSetups => Setups.Count == 0;
    public DateOnly? PeriodStart { get; }
    public string Currency => Source?.Currency ?? "";
    public IReadOnlyList<DashboardCard> Cards { get; }
    public IReadOnlyList<DashboardChartRow> DailyPnl { get; }
    public IReadOnlyList<DashboardChartRow> CumulativePnl { get; }
    public DashboardChartRow? BestDay { get; }
    public DashboardChartRow? WorstDay { get; }
    public IReadOnlyList<DashboardSetupRow> Setups { get; }
    public string CoverageNote => Source is null ? "No closed Trades in this scope." : Coverage(Source.Metrics.EffectiveNet);
    public static string Money(decimal? value, string currency) => value is { } amount ? $"{amount:N2} {currency}".TrimEnd() : "—";
    private string CardMoney(decimal? value) => value.HasValue ? Money(value, Currency) : "N/A";
    private static string Number(decimal? value, string suffix = "") => value is { } amount ? $"{amount:N2}{suffix}" : "—";
    private DashboardCard DayCard(string label, DashboardChartRow? day) => new(label,
        CardMoney(day?.Value), day?.Description ?? "Unavailable: no closed Trades or incomplete daily outcome coverage.",
        day?.IsEstimated == true, day?.Date.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture) ?? "", PnlValue: day?.Value);
    private static string Coverage(PnlMetrics metrics) =>
        $"{(metrics.IsEstimated ? "Estimated — commission/fees unknown; Gross used as Net" : metrics.Basis == PnlBasis.Gross ? "Gross" : "Verified Net")} · " +
        $"{metrics.Coverage.KnownTradeCount}/{metrics.Coverage.ClosedTradeCount} available · {metrics.EstimatedTradeCount} estimated · {metrics.Coverage.Status}";
    private static DashboardChartRow Row(PnlChartPoint point, string currency) =>
        new(point.NewYorkDate, point.Metrics.EffectiveNet.Total, point.Metrics.EffectiveNet.IsEstimated,
            currency, Coverage(point.Metrics.EffectiveNet), point.Metrics.ClosedTradeCount);
}
