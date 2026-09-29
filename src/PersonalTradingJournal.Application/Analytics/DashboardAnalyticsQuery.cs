using PersonalTradingJournal.Application.Common.Time;

namespace PersonalTradingJournal.Application.Analytics;

/// <summary>
/// Explicit, stateless filters over closed Trades. Null means unrestricted, never a retained selection.
/// New York dates are inclusive; UTC bounds are [start, next-day start).
/// Invalid filters throw parameter-specific argument exceptions before any database access.
/// </summary>
public sealed record DashboardAnalyticsQuery
{
    public DashboardAnalyticsQuery(
        Guid? tradingAccountId = null,
        Guid? instrumentId = null,
        DateOnly? closedFromNewYork = null,
        DateOnly? closedThroughNewYork = null)
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
        InstrumentId = instrumentId;
        ClosedFromNewYork = closedFromNewYork;
        ClosedThroughNewYork = closedThroughNewYork;
        ClosedFromUtc = closedFromNewYork is { } first ? MidnightUtc(first, nameof(closedFromNewYork)) : null;
        ClosedBeforeUtc = closedThroughNewYork is { } last
            ? MidnightUtc(last.AddDays(1), nameof(closedThroughNewYork)) : null;
    }

    public Guid? TradingAccountId { get; }
    public Guid? InstrumentId { get; }
    public DateOnly? ClosedFromNewYork { get; }
    public DateOnly? ClosedThroughNewYork { get; }
    public DateTimeOffset? ClosedFromUtc { get; }
    public DateTimeOffset? ClosedBeforeUtc { get; }

    private static DateTimeOffset MidnightUtc(DateOnly date, string parameterName)
    {
        LocalTimeConversionResult result = TradingTimePolicy.ConvertTradingLocalToUtc(
            date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified));
        if (!result.IsSuccess)
            throw new ArgumentException("The New York date boundary cannot be resolved unambiguously to UTC.", parameterName);
        return result.UtcTimestamp!.Value;
    }
}
