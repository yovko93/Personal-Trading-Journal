using PersonalTradingJournal.Application.Analytics;

namespace PersonalTradingJournal.Application.Tests.Analytics;

public sealed class DashboardAnalyticsQueryTests
{
    [Fact]
    public void OmittedFiltersAreUnrestricted()
    {
        var query = new DashboardAnalyticsQuery();
        Assert.Null(query.TradingAccountId);
        Assert.Null(query.InstrumentId);
        Assert.Null(query.ClosedFromUtc);
        Assert.Null(query.ClosedBeforeUtc);
    }

    [Fact]
    public void InvalidFiltersHaveParameterSpecificValidation()
    {
        Assert.Equal("tradingAccountId", Assert.Throws<ArgumentException>(() => new DashboardAnalyticsQuery(Guid.Empty)).ParamName);
        Assert.Equal("instrumentId", Assert.Throws<ArgumentException>(() => new DashboardAnalyticsQuery(instrumentId: Guid.Empty)).ParamName);
        Assert.Equal("closedThroughNewYork", Assert.Throws<ArgumentException>(() => new DashboardAnalyticsQuery(
            closedFromNewYork: new(2026, 3, 9), closedThroughNewYork: new(2026, 3, 8))).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => new DashboardAnalyticsQuery(closedThroughNewYork: DateOnly.MaxValue));
    }

    [Theory]
    [InlineData(3, 7, 5, 5, 24)]
    [InlineData(3, 8, 5, 4, 23)]
    [InlineData(3, 9, 4, 4, 24)]
    [InlineData(10, 31, 4, 4, 24)]
    [InlineData(11, 1, 4, 5, 25)]
    [InlineData(11, 2, 5, 5, 24)]
    public void InclusiveLocalDayUsesNextLocalMidnightNotUtcPlus24Hours(int month, int day, int startHour, int endHour, int hours)
    {
        DateOnly date = new(2026, month, day);
        var query = new DashboardAnalyticsQuery(closedFromNewYork: date, closedThroughNewYork: date);
        Assert.Equal(startHour, query.ClosedFromUtc!.Value.Hour);
        Assert.Equal(endHour, query.ClosedBeforeUtc!.Value.Hour);
        Assert.Equal(TimeSpan.FromHours(hours), query.ClosedBeforeUtc - query.ClosedFromUtc);
        Assert.Equal(TimeSpan.Zero, query.ClosedFromUtc.Value.Offset);
        Assert.Equal(TimeSpan.Zero, query.ClosedBeforeUtc.Value.Offset);
    }

    [Fact]
    public void OneSidedDateFiltersRetainAnUnboundedOtherSide()
    {
        Assert.Null(new DashboardAnalyticsQuery(closedFromNewYork: new(2026, 1, 1)).ClosedBeforeUtc);
        Assert.Null(new DashboardAnalyticsQuery(closedThroughNewYork: new(2026, 1, 1)).ClosedFromUtc);
    }
}
