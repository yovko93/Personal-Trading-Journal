using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Calendar;

public sealed record TradingCalendarDayCurrency(string Currency, ClosedTradeMetrics Metrics);

/// <summary>Complete date-scoped closed Trade rows and M12 metrics over those same rows.</summary>
public sealed record TradingCalendarDayDetails(DateOnly Date, IReadOnlyList<TradeListItem> Trades,
    IReadOnlyList<TradingCalendarDayCurrency> Currencies)
{
    public int ClosedTradeCount => Trades.Count;

    public static TradingCalendarDayDetails Create(DateOnly date, IEnumerable<TradeListItem> trades,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trades);
        cancellationToken.ThrowIfCancellationRequested();
        var rows = new List<TradeListItem>();
        foreach (TradeListItem row in trades)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row.Status != TradeStatus.Closed || row.ClosedAtUtc is not { } closed ||
                DashboardMetricCalculator.GetNewYorkCloseDate(closed) != date)
                throw new ArgumentException("Provide only fully closed Trades attributed to the selected New York date.", nameof(trades));
            rows.Add(row);
        }
        TradeListItem[] ordered = rows.OrderByDescending(t => t.ClosedAtUtc).ThenBy(t => t.Id).ToArray();
        DashboardAnalyticsSnapshot metrics = DashboardMetricCalculator.Calculate(ordered.Select(t =>
            new TradeAnalyticsFact(t.Id, t.Status, t.OpenedAtUtc, t.ClosedAtUtc, t.Currency,
                null, t.GrossPnL, t.TotalCosts, t.NetPnL)), cancellationToken);
        return new(date, Array.AsReadOnly(ordered), Array.AsReadOnly(metrics.Currencies
            .Select(c => new TradingCalendarDayCurrency(c.Currency, c.Metrics)).ToArray()));
    }
}
