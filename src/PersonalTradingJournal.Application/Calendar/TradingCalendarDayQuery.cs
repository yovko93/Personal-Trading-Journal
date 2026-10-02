using PersonalTradingJournal.Application.Analytics;

namespace PersonalTradingJournal.Application.Calendar;

/// <summary>One inclusive New York closure date; UTC bounds are [midnight, next midnight).</summary>
public sealed record TradingCalendarDayQuery
{
    public TradingCalendarDayQuery(DateOnly date, Guid? tradingAccountId = null)
    {
        var bounds = new DashboardAnalyticsQuery(tradingAccountId, null, date, date);
        Date = date;
        TradingAccountId = tradingAccountId;
        ClosedFromUtc = bounds.ClosedFromUtc!.Value;
        ClosedBeforeUtc = bounds.ClosedBeforeUtc!.Value;
    }

    public DateOnly Date { get; }
    public Guid? TradingAccountId { get; }
    public DateTimeOffset ClosedFromUtc { get; }
    public DateTimeOffset ClosedBeforeUtc { get; }
}
