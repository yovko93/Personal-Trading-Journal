using PersonalTradingJournal.Application.Analytics.Breakdown;

namespace PersonalTradingJournal.Application.Tests.Analytics.Breakdown;

public sealed class HoldingDurationRangeTests
{
    [Theory]
    [InlineData(0, HoldingDurationBucket.UnderOneMinute)]
    [InlineData(1, HoldingDurationBucket.OneToFiveMinutes)]
    [InlineData(5, HoldingDurationBucket.FiveToFifteenMinutes)]
    [InlineData(15, HoldingDurationBucket.FifteenToSixtyMinutes)]
    [InlineData(60, HoldingDurationBucket.OneToFourHours)]
    [InlineData(240, HoldingDurationBucket.FourHoursOrMore)]
    public void EachBoundaryAndItsAdjacentTicksBelongToExactlyOneBucket(int minutes, HoldingDurationBucket expected)
    {
        TimeSpan boundary = TimeSpan.FromMinutes(minutes);
        AssertMembership(boundary, expected);
        AssertMembership(boundary.Add(TimeSpan.FromTicks(1)), expected);
        if (minutes > 0) AssertMembership(boundary.Subtract(TimeSpan.FromTicks(1)), (HoldingDurationBucket)((int)expected - 1));
    }

    [Fact]
    public void RangesAreContiguousThroughUnboundedLongPositionsAndNeverAcceptNegativeDuration()
    {
        var ranges = Enum.GetValues<HoldingDurationBucket>().Select(HoldingDurationRange.For).ToArray();
        Assert.Equal(TimeSpan.Zero, ranges[0].FromInclusive);
        for (int index = 1; index < ranges.Length; index++)
            Assert.Equal(ranges[index - 1].Before, ranges[index].FromInclusive);
        Assert.Null(ranges[^1].Before);
        AssertMembership(TimeSpan.FromDays(400), HoldingDurationBucket.FourHoursOrMore);
        AssertMembership(TimeSpan.MaxValue, HoldingDurationBucket.FourHoursOrMore);
        Assert.All(ranges, range => Assert.False(range.Contains(TimeSpan.FromTicks(-1))));
        Assert.Throws<ArgumentOutOfRangeException>(() => HoldingDurationRange.FromDuration(TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => HoldingDurationRange.For((HoldingDurationBucket)7));
    }

    [Theory]
    [InlineData(3, 8, 6)]
    [InlineData(11, 1, 5)]
    public void DurationUsesUtcElapsedTimeAcrossBothDstTransitions(int month, int day, int hour)
    {
        var start = new DateTimeOffset(2026, month, day, hour, 30, 0, TimeSpan.Zero);
        Assert.Equal(HoldingDurationBucket.OneToFourHours, HoldingDurationRange.FromTimestamps(start, start.AddHours(1)));
    }

    [Fact]
    public void MissingAndInvalidInstantsCannotBeAssignedToAValidDurationBucket()
    {
        var start = new DateTimeOffset(2026, 10, 2, 14, 0, 0, TimeSpan.Zero);
        Assert.Null(HoldingDurationRange.FromTimestamps(start, null));
        Assert.Throws<ArgumentException>(() => HoldingDurationRange.FromTimestamps(start, start.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => HoldingDurationRange.FromTimestamps(start.ToOffset(TimeSpan.FromHours(-4)), null));
        Assert.Throws<ArgumentException>(() => HoldingDurationRange.FromTimestamps(start, start.AddMinutes(2).ToOffset(TimeSpan.FromHours(-4))));
    }

    private static void AssertMembership(TimeSpan duration, HoldingDurationBucket expected)
    {
        var matches = Enum.GetValues<HoldingDurationBucket>().Select(HoldingDurationRange.For).Where(r => r.Contains(duration));
        Assert.Equal(expected, Assert.Single(matches).Bucket);
        Assert.Equal(expected, HoldingDurationRange.FromDuration(duration));
    }
}
