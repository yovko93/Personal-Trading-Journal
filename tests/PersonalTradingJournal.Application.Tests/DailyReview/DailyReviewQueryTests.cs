using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.DailyReview;

namespace PersonalTradingJournal.Application.Tests.DailyReview;

public sealed class DailyReviewQueryTests
{
    [Theory]
    [InlineData(2026, 3, 8, 23, 5)]
    [InlineData(2026, 11, 1, 25, 4)]
    [InlineData(2026, 7, 1, 24, 4)]
    [InlineData(2026, 1, 1, 24, 5)]
    public void RequestReusesCalendarMidnightsRatherThanMachineTime(int year, int month, int day, int hours, int utcHour)
    {
        Guid account = Guid.NewGuid();
        var query = new DailyReviewQuery(new(year, month, day), account);
        var calendar = new TradingCalendarDayQuery(query.Date, account);
        Assert.Equal(calendar.ClosedFromUtc, query.FromUtc);
        Assert.Equal(calendar.ClosedBeforeUtc, query.BeforeUtc);
        Assert.Equal(TimeSpan.FromHours(hours), query.BeforeUtc - query.FromUtc);
        Assert.Equal(utcHour, query.FromUtc.Hour);
        Assert.Equal(TimeSpan.Zero, query.FromUtc.Offset);
        Assert.Equal(account, query.TradingAccountId);
        Assert.Null(new DailyReviewQuery(query.Date).TradingAccountId);
    }

    [Fact]
    public void InvalidAccountAndUnrepresentableEndDateFailBeforeReading()
    {
        Assert.Throws<ArgumentException>(() => new DailyReviewQuery(new(2026, 10, 8), Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DailyReviewQuery(DateOnly.MaxValue));
    }
}
