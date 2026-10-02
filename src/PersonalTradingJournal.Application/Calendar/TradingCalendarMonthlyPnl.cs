using PersonalTradingJournal.Application.Analytics;

namespace PersonalTradingJournal.Application.Calendar;

/// <summary>Displayed-month Effective Net only; adjacent days and weekly summaries never contribute.</summary>
public sealed record TradingCalendarMonthlyPnl(
    string Currency, MetricCoverage Coverage, decimal? Total, decimal? KnownSubtotal, int EstimatedTradeCount)
{
    public bool IsEstimated => EstimatedTradeCount > 0;

    public static IReadOnlyList<TradingCalendarMonthlyPnl> From(TradingCalendarMonth month)
    {
        ArgumentNullException.ThrowIfNull(month);
        var results = new List<TradingCalendarMonthlyPnl>();
        foreach (TradingCalendarCurrency currency in month.Currencies.OrderBy(c => c.Currency, StringComparer.Ordinal))
        {
            int count = 0, known = 0, estimated = 0;
            decimal subtotal = 0;
            foreach (TradingCalendarDay day in currency.Weeks.SelectMany(w => w.Days))
            {
                if (day.Date.Year != month.MonthStart.Year || day.Date.Month != month.MonthStart.Month || day.Metrics is null) continue;
                PnlMetrics net = day.Metrics.EffectiveNet;
                checked
                {
                    count += net.Coverage.ClosedTradeCount;
                    known += net.Coverage.KnownTradeCount;
                    estimated += net.EstimatedTradeCount;
                    subtotal += net.KnownSubtotal ?? 0m;
                }
            }
            // No invented currency or financial zero for an empty selected month.
            if (count == 0) continue;
            var coverage = new MetricCoverage(count, known);
            results.Add(new(currency.Currency, coverage, known == count ? subtotal : null,
                known == 0 ? null : subtotal, estimated));
        }
        return results.AsReadOnly();
    }
}
