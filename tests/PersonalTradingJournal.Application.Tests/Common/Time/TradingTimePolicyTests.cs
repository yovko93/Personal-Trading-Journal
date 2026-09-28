using PersonalTradingJournal.Application.Common.Time;

namespace PersonalTradingJournal.Application.Tests.Common.Time;

public sealed class TradingTimePolicyTests
{
    [Theory]
    [InlineData(2026, 1, 15, 9, 30, 2026, 1, 15, 14, 30)]
    [InlineData(2026, 9, 10, 9, 30, 2026, 9, 10, 13, 30)]
    public void NewYorkLocalTimeConvertsToExactCanonicalUtc(
        int localYear,
        int localMonth,
        int localDay,
        int localHour,
        int localMinute,
        int utcYear,
        int utcMonth,
        int utcDay,
        int utcHour,
        int utcMinute)
    {
        DateTime local = Unspecified(
            localYear, localMonth, localDay, localHour, localMinute);

        LocalTimeConversionResult result =
            TradingTimePolicy.ConvertTradingLocalToUtc(local);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new DateTimeOffset(
                utcYear, utcMonth, utcDay, utcHour, utcMinute, 0, TimeSpan.Zero),
            result.UtcTimestamp);
        Assert.Equal(TimeSpan.Zero, result.UtcTimestamp!.Value.Offset);
    }

    [Theory]
    [InlineData(2026, 3, 8, 2, 30, LocalTimeConversionStatus.Invalid)]
    [InlineData(2026, 11, 1, 1, 30, LocalTimeConversionStatus.Ambiguous)]
    public void NewYorkDstUncertaintyIsRejected(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        LocalTimeConversionStatus expectedStatus)
    {
        LocalTimeConversionResult result =
            TradingTimePolicy.ConvertTradingLocalToUtc(
                Unspecified(year, month, day, hour, minute));

        Assert.Equal(expectedStatus, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Null(result.UtcTimestamp);
    }

    [Theory]
    [InlineData(2026, 1, 15, 16, 30, 2026, 1, 15, 14, 30, -5)]
    [InlineData(2026, 9, 10, 16, 30, 2026, 9, 10, 13, 30, -4)]
    [InlineData(2026, 3, 20, 15, 30, 2026, 3, 20, 13, 30, -4)]
    public void SofiaSourceConvertsThroughUtcToExactNewYorkRepresentation(
        int sourceYear,
        int sourceMonth,
        int sourceDay,
        int sourceHour,
        int sourceMinute,
        int utcYear,
        int utcMonth,
        int utcDay,
        int utcHour,
        int utcMinute,
        int expectedNewYorkOffsetHours)
    {
        LocalTimeConversionResult sourceResult =
            TradingTimePolicy.ConvertTradovateSourceToUtc(
                Unspecified(
                    sourceYear,
                    sourceMonth,
                    sourceDay,
                    sourceHour,
                    sourceMinute));

        Assert.True(sourceResult.IsSuccess);
        DateTimeOffset expectedUtc = new(
            utcYear, utcMonth, utcDay, utcHour, utcMinute, 0, TimeSpan.Zero);
        Assert.Equal(expectedUtc, sourceResult.UtcTimestamp);

        DateTimeOffset newYork = TradingTimePolicy.ConvertUtcToTradingTime(expectedUtc);
        Assert.Equal(new DateTime(sourceYear, sourceMonth, sourceDay, 9, 30, 0), newYork.DateTime);
        Assert.Equal(TimeSpan.FromHours(expectedNewYorkOffsetHours), newYork.Offset);
    }

    [Theory]
    [InlineData(2026, 3, 29, 3, 30, LocalTimeConversionStatus.Invalid)]
    [InlineData(2026, 10, 25, 3, 30, LocalTimeConversionStatus.Ambiguous)]
    public void SofiaDstUncertaintyIsRejected(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        LocalTimeConversionStatus expectedStatus)
    {
        LocalTimeConversionResult result =
            TradingTimePolicy.ConvertTradovateSourceToUtc(
                Unspecified(year, month, day, hour, minute));

        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.UtcTimestamp);
    }

    [Fact]
    public void LocalInputRequiresUnspecifiedDateTimeKind()
    {
        Assert.Throws<ArgumentException>(() =>
            TradingTimePolicy.ConvertTradingLocalToUtc(
                new DateTime(2026, 9, 10, 9, 30, 0, DateTimeKind.Local)));
    }

    private static DateTime Unspecified(
        int year,
        int month,
        int day,
        int hour,
        int minute) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
}
