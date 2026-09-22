namespace PersonalTradingJournal.Application.Common.Time;

/// <summary>
/// Defines the application's fixed broker-source and trading-time policies.
/// UTC remains the canonical Domain and persistence representation.
/// </summary>
public static class TradingTimePolicy
{
    public const string TradovateSourceTimeZoneId = "Europe/Sofia";
    public const string TradingTimeZoneId = "America/New_York";

    private static readonly Lazy<TimeZoneInfo> TradovateSourceTimeZone =
        new(() => ResolveTimeZone(TradovateSourceTimeZoneId));
    private static readonly Lazy<TimeZoneInfo> TradingTimeZone =
        new(() => ResolveTimeZone(TradingTimeZoneId));

    public static LocalTimeConversionResult ConvertTradovateSourceToUtc(
        DateTime sourceLocalTimestamp) =>
        ConvertLocalToUtc(sourceLocalTimestamp, TradovateSourceTimeZone.Value);

    public static LocalTimeConversionResult ConvertTradingLocalToUtc(
        DateTime tradingLocalTimestamp) =>
        ConvertLocalToUtc(tradingLocalTimestamp, TradingTimeZone.Value);

    public static DateTimeOffset ConvertUtcToTradingTime(DateTimeOffset utcTimestamp) =>
        ConvertUtcToLocal(utcTimestamp, TradingTimeZone.Value);

    public static LocalTimeConversionResult ConvertLocalToUtc(
        DateTime localTimestamp,
        string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        return ConvertLocalToUtc(localTimestamp, ResolveTimeZone(timeZoneId));
    }

    public static DateTimeOffset ConvertUtcToLocal(
        DateTimeOffset utcTimestamp,
        string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        return ConvertUtcToLocal(utcTimestamp, ResolveTimeZone(timeZoneId));
    }

    private static LocalTimeConversionResult ConvertLocalToUtc(
        DateTime localTimestamp,
        TimeZoneInfo timeZone)
    {
        if (localTimestamp.Kind != DateTimeKind.Unspecified)
        {
            throw new ArgumentException(
                "A local wall-clock timestamp must have DateTimeKind.Unspecified.",
                nameof(localTimestamp));
        }

        if (timeZone.IsInvalidTime(localTimestamp))
        {
            return new LocalTimeConversionResult(
                LocalTimeConversionStatus.Invalid,
                UtcTimestamp: null);
        }

        if (timeZone.IsAmbiguousTime(localTimestamp))
        {
            return new LocalTimeConversionResult(
                LocalTimeConversionStatus.Ambiguous,
                UtcTimestamp: null);
        }

        DateTime utc = TimeZoneInfo.ConvertTimeToUtc(localTimestamp, timeZone);
        return new LocalTimeConversionResult(
            LocalTimeConversionStatus.Success,
            new DateTimeOffset(utc));
    }

    private static DateTimeOffset ConvertUtcToLocal(
        DateTimeOffset utcTimestamp,
        TimeZoneInfo timeZone)
    {
        if (utcTimestamp.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The canonical timestamp must use a zero UTC offset.",
                nameof(utcTimestamp));
        }

        return TimeZoneInfo.ConvertTime(utcTimestamp, timeZone);
    }

    private static TimeZoneInfo ResolveTimeZone(string ianaTimeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId);
        }
        catch (TimeZoneNotFoundException) when (
            TimeZoneInfo.TryConvertIanaIdToWindowsId(
                ianaTimeZoneId,
                out string? windowsTimeZoneId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsTimeZoneId);
        }
    }
}
