using PersonalTradingJournal.Application.Analytics;

namespace PersonalTradingJournal.Application.Calendar;

/// <summary>One New York closure date, with a half-open UTC interval.</summary>
public sealed record TradingCalendarDayQuery
{
    public TradingCalendarDayQuery(DateOnly date, Guid? tradingAccountId = null)
        : this(date, new DashboardAnalyticsQuery(tradingAccountId, null, date, date))
    {
    }

    /// <summary>Calendar's trading day starts at 18:00 on the previous New York civil date.</summary>
    public static TradingCalendarDayQuery ForTradingDate(DateOnly date, Guid? tradingAccountId = null) =>
        new(date, DashboardAnalyticsQuery.ForCalendarTradingDays(tradingAccountId, date, date));

    private TradingCalendarDayQuery(DateOnly date, DashboardAnalyticsQuery bounds)
    {
        Date = date;
        TradingAccountId = bounds.TradingAccountId;
        ClosedFromUtc = bounds.ClosedFromUtc!.Value;
        ClosedBeforeUtc = bounds.ClosedBeforeUtc!.Value;
    }

    public DateOnly Date { get; }
    public Guid? TradingAccountId { get; }
    public DateTimeOffset ClosedFromUtc { get; }
    public DateTimeOffset ClosedBeforeUtc { get; }
}
