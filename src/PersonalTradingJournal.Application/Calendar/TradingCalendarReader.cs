using PersonalTradingJournal.Application.Analytics;

namespace PersonalTradingJournal.Application.Calendar;

/// <summary>Shapes existing M12 daily/weekly metrics into a visible month; never recalculates Trade economics.</summary>
public sealed class TradingCalendarReader(IDashboardAnalyticsReader analyticsReader) : ITradingCalendarReader
{
    public async Task<TradingCalendarMonth> GetAsync(
        TradingCalendarQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        DashboardAnalyticsSnapshot snapshot = await analyticsReader.GetAsync(
            new DashboardAnalyticsQuery(query.TradingAccountId, null, query.GridStart, query.GridEnd), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        DateOnly[] gridDates = Enumerable.Range(0, query.GridEnd.DayNumber - query.GridStart.DayNumber + 1)
            .Select(offset => query.GridStart.AddDays(offset)).ToArray();
        var currencies = new List<TradingCalendarCurrency>(snapshot.Currencies.Count);
        foreach (CurrencyTradeMetrics currency in snapshot.Currencies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Dictionary<DateOnly, ClosedTradeMetrics> days = currency.Days.ToDictionary(x => x.NewYorkDate, x => x.Metrics);
            Dictionary<DateOnly, ClosedTradeMetrics> weeks = currency.Weeks.ToDictionary(x => x.WeekStartingMonday, x => x.Metrics);
            var rows = new List<TradingCalendarWeek>(gridDates.Length / 7);
            for (int start = 0; start < gridDates.Length; start += 7)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DateOnly monday = gridDates[start];
                TradingCalendarDay[] rowDays = gridDates.Skip(start).Take(7)
                    .Select(date => new TradingCalendarDay(date, date.Year == query.MonthStart.Year &&
                        date.Month == query.MonthStart.Month, days.GetValueOrDefault(date))).ToArray();
                rows.Add(new TradingCalendarWeek(monday, gridDates[start + 6], Array.AsReadOnly(rowDays),
                    weeks.GetValueOrDefault(monday)));
            }
            currencies.Add(new TradingCalendarCurrency(currency.Currency, rows.AsReadOnly()));
        }
        return new TradingCalendarMonth(query.MonthStart, query.GridStart, query.GridEnd,
            Array.AsReadOnly(gridDates), currencies.AsReadOnly());
    }
}
