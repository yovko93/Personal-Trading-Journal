namespace PersonalTradingJournal.Application.Calendar;

/// <summary>A displayed New York calendar month and optional destination Account filter.</summary>
public sealed record TradingCalendarQuery
{
    public TradingCalendarQuery(int year, int month, Guid? tradingAccountId = null)
    {
        if (year is < 1 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(year));
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month));
        if (tradingAccountId == Guid.Empty)
            throw new ArgumentException("An Account filter must be a non-empty ID.", nameof(tradingAccountId));

        MonthStart = new DateOnly(year, month, 1);
        DateOnly last = new(year, month, DateTime.DaysInMonth(year, month));
        int before = ((int)MonthStart.DayOfWeek + 6) % 7;
        int after = 6 - (((int)last.DayOfWeek + 6) % 7);
        // DashboardAnalyticsQuery uses the exclusive midnight after GridEnd.
        if (last.DayNumber + after >= DateOnly.MaxValue.DayNumber)
            throw new ArgumentOutOfRangeException(nameof(year), "The calendar grid must have a representable exclusive end date.");
        GridStart = MonthStart.AddDays(-before);
        GridEnd = last.AddDays(after);
        TradingAccountId = tradingAccountId;
    }

    public DateOnly MonthStart { get; }
    public DateOnly GridStart { get; }
    public DateOnly GridEnd { get; }
    public Guid? TradingAccountId { get; }
}
