using PersonalTradingJournal.Application.Calendar;

namespace PersonalTradingJournal.Application.DailyReview;

/// <summary>One New York date. Null Account aggregates all scopes, including null-scoped Journals.</summary>
public sealed record DailyReviewQuery
{
    public DailyReviewQuery(DateOnly date, Guid? tradingAccountId = null)
    {
        var calendar = new TradingCalendarDayQuery(date, tradingAccountId);
        Date = calendar.Date;
        TradingAccountId = calendar.TradingAccountId;
        FromUtc = calendar.ClosedFromUtc;
        BeforeUtc = calendar.ClosedBeforeUtc;
    }

    public DateOnly Date { get; }
    public Guid? TradingAccountId { get; }
    public DateTimeOffset FromUtc { get; }
    public DateTimeOffset BeforeUtc { get; }
}
