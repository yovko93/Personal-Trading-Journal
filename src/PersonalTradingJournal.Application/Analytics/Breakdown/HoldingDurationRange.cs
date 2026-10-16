namespace PersonalTradingJournal.Application.Analytics.Breakdown;

public enum HoldingDurationBucket
{
    UnderOneMinute = 1,
    OneToFiveMinutes = 2,
    FiveToFifteenMinutes = 3,
    FifteenToSixtyMinutes = 4,
    OneToFourHours = 5,
    FourHoursOrMore = 6,
}

/// <summary>Fixed [inclusive lower, exclusive upper) elapsed-time predicate; null upper is unbounded.</summary>
public sealed record HoldingDurationRange
{
    private HoldingDurationRange(HoldingDurationBucket bucket, string label, TimeSpan fromInclusive, TimeSpan? before)
    {
        Bucket = bucket;
        Label = label;
        FromInclusive = fromInclusive;
        Before = before;
    }

    public HoldingDurationBucket Bucket { get; }
    public string Label { get; }
    public TimeSpan FromInclusive { get; }
    public TimeSpan? Before { get; }

    public static HoldingDurationRange For(HoldingDurationBucket bucket) => bucket switch
    {
        HoldingDurationBucket.UnderOneMinute => new(bucket, "Under 1 minute", TimeSpan.Zero, TimeSpan.FromMinutes(1)),
        HoldingDurationBucket.OneToFiveMinutes => new(bucket, "1–5 minutes", TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)),
        HoldingDurationBucket.FiveToFifteenMinutes => new(bucket, "5–15 minutes", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)),
        HoldingDurationBucket.FifteenToSixtyMinutes => new(bucket, "15–60 minutes", TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)),
        HoldingDurationBucket.OneToFourHours => new(bucket, "1–4 hours", TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(240)),
        HoldingDurationBucket.FourHoursOrMore => new(bucket, "4 hours or more", TimeSpan.FromMinutes(240), null),
        _ => throw new ArgumentOutOfRangeException(nameof(bucket), "Select a supported holding-duration bucket."),
    };

    public bool Contains(TimeSpan duration) => duration >= FromInclusive && (Before is null || duration < Before.Value);

    public static HoldingDurationBucket FromDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration), "Holding duration cannot be negative.");
        if (duration < TimeSpan.FromMinutes(1)) return HoldingDurationBucket.UnderOneMinute;
        if (duration < TimeSpan.FromMinutes(5)) return HoldingDurationBucket.OneToFiveMinutes;
        if (duration < TimeSpan.FromMinutes(15)) return HoldingDurationBucket.FiveToFifteenMinutes;
        if (duration < TimeSpan.FromMinutes(60)) return HoldingDurationBucket.FifteenToSixtyMinutes;
        if (duration < TimeSpan.FromMinutes(240)) return HoldingDurationBucket.OneToFourHours;
        return HoldingDurationBucket.FourHoursOrMore;
    }

    /// <summary>
    /// UTC elapsed duration, unaffected by DST. Missing closure returns null (unavailable);
    /// noncanonical instants or a closure before opening throw, never selecting a valid bucket.
    /// </summary>
    public static HoldingDurationBucket? FromTimestamps(DateTimeOffset openedAtUtc, DateTimeOffset? closedAtUtc)
    {
        if (openedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("The opening instant must use a zero UTC offset.", nameof(openedAtUtc));
        if (closedAtUtc is not { } close) return null;
        if (close.Offset != TimeSpan.Zero || close < openedAtUtc)
            throw new ArgumentException("The closure instant must be UTC and not precede opening.", nameof(closedAtUtc));
        return FromDuration(close - openedAtUtc);
    }
}
