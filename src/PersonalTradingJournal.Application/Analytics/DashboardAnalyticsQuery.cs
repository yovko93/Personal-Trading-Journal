using PersonalTradingJournal.Application.Common.Time;

namespace PersonalTradingJournal.Application.Analytics;

/// <summary>
/// Explicit, stateless filters over closed Trades. Null means unrestricted, never a retained selection.
/// New York dates are inclusive; UTC bounds are [start, next-day start).
/// Invalid filters throw parameter-specific argument exceptions before any database access.
/// </summary>
public sealed record DashboardAnalyticsQuery
{
    private static readonly TimeOnly CalendarCutoff = new(18, 0);
    private readonly bool _useCalendarTradingDays;

    public DashboardAnalyticsQuery(
        Guid? tradingAccountId = null,
        Guid? instrumentId = null,
        DateOnly? closedFromNewYork = null,
        DateOnly? closedThroughNewYork = null)
        : this(tradingAccountId, instrumentId, closedFromNewYork, closedThroughNewYork, useCalendarTradingDays: false)
    {
    }

    internal static DashboardAnalyticsQuery ForCalendarTradingDays(Guid? tradingAccountId, DateOnly first, DateOnly last) =>
        new(tradingAccountId, null, first, last, useCalendarTradingDays: true);

    private DashboardAnalyticsQuery(Guid? tradingAccountId, Guid? instrumentId,
        DateOnly? closedFromNewYork, DateOnly? closedThroughNewYork, bool useCalendarTradingDays)
    {
        if (tradingAccountId == Guid.Empty)
            throw new ArgumentException("Select a valid Account ID or omit the Account filter.", nameof(tradingAccountId));
        if (instrumentId == Guid.Empty)
            throw new ArgumentException("Select a valid Instrument ID or omit the Instrument filter.", nameof(instrumentId));
        if (closedFromNewYork > closedThroughNewYork)
            throw new ArgumentException("The last New York closure date must not precede the first.", nameof(closedThroughNewYork));
        if (closedThroughNewYork == DateOnly.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(closedThroughNewYork),
                "The last closure date must be before 9999-12-31 so its exclusive UTC boundary is representable.");

        TradingAccountId = tradingAccountId;
        _useCalendarTradingDays = useCalendarTradingDays;
        InstrumentId = instrumentId;
        ClosedFromNewYork = closedFromNewYork;
        ClosedThroughNewYork = closedThroughNewYork;
        ClosedFromUtc = closedFromNewYork is { } first ? StartUtc(first, nameof(closedFromNewYork)) : null;
        ClosedBeforeUtc = closedThroughNewYork is { } last ? StartUtc(last.AddDays(1), nameof(closedThroughNewYork)) : null;
    }

    public Guid? TradingAccountId { get; }
    public Guid? InstrumentId { get; }
    public DateOnly? ClosedFromNewYork { get; }
    public DateOnly? ClosedThroughNewYork { get; }
    public DateTimeOffset? ClosedFromUtc { get; }
    public DateTimeOffset? ClosedBeforeUtc { get; }

    public DateOnly GetCloseDate(DateTimeOffset closedAtUtc)
    {
        DateTimeOffset local = TradingTimePolicy.ConvertUtcToTradingTime(closedAtUtc);
        var date = DateOnly.FromDateTime(local.DateTime);
        return _useCalendarTradingDays && TimeOnly.FromDateTime(local.DateTime) >= CalendarCutoff
            ? date.AddDays(1) : date;
    }

    private DateTimeOffset StartUtc(DateOnly date, string parameterName)
    {
        // The earliest representable date has no previous evening to convert.
        if (_useCalendarTradingDays && date == DateOnly.MinValue)
            return DateTimeOffset.MinValue;

        DateOnly boundaryDate = _useCalendarTradingDays ? date.AddDays(-1) : date;
        TimeOnly boundaryTime = _useCalendarTradingDays ? CalendarCutoff : TimeOnly.MinValue;
        LocalTimeConversionResult result = TradingTimePolicy.ConvertTradingLocalToUtc(
            boundaryDate.ToDateTime(boundaryTime, DateTimeKind.Unspecified));
        if (!result.IsSuccess)
            throw new ArgumentException("The New York date boundary cannot be resolved unambiguously to UTC.", parameterName);
        return result.UtcTimestamp!.Value;
    }
}
