using System.Globalization;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Analytics.Breakdown;

// Values identify dimensions, not display positions. There is no Strategy source in M20.
public enum BreakdownGrouping
{
    Instrument = 1,
    TradingSetup = 2,
    Direction = 3,
    EntryWeekday = 4,
    EntryHour = 5,
    Account = 6,
    HoldingDuration = 7,
}

/// <summary>
/// Immutable discriminated key. Factories admit only the payload appropriate to its dimension.
/// Labels/activity are deliberately absent from equality, hashing and ordering.
/// </summary>
public sealed record BreakdownGroupKey : IComparable<BreakdownGroupKey>
{
    private BreakdownGroupKey(BreakdownGrouping grouping, Guid? referenceId = null,
        TradeDirection? direction = null, DayOfWeek? entryWeekday = null,
        int? entryHour = null, HoldingDurationBucket? durationBucket = null)
    {
        Grouping = grouping;
        ReferenceId = referenceId;
        Direction = direction;
        EntryWeekday = entryWeekday;
        EntryHour = entryHour;
        DurationBucket = durationBucket;
    }

    public BreakdownGrouping Grouping { get; }
    public Guid? ReferenceId { get; }
    public TradeDirection? Direction { get; }
    public DayOfWeek? EntryWeekday { get; }
    public int? EntryHour { get; }
    public HoldingDurationBucket? DurationBucket { get; }
    public bool IsUnclassified => Grouping == BreakdownGrouping.TradingSetup && ReferenceId is null;

    public static BreakdownGroupKey ForInstrument(Guid instrumentId) =>
        new(BreakdownGrouping.Instrument, RequiredId(instrumentId, nameof(instrumentId)));

    public static BreakdownGroupKey ForAccount(Guid tradingAccountId) =>
        new(BreakdownGrouping.Account, RequiredId(tradingAccountId, nameof(tradingAccountId)));

    /// <summary>Null means truly unassigned; a retained unavailable ID is still a specific Setup.</summary>
    public static BreakdownGroupKey ForSetup(Guid? tradingSetupId) =>
        new(BreakdownGrouping.TradingSetup,
            tradingSetupId is { } id ? RequiredId(id, nameof(tradingSetupId)) : null);

    public static BreakdownGroupKey ForDirection(TradeDirection direction)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        return new(BreakdownGrouping.Direction, direction: direction);
    }

    public static BreakdownGroupKey ForEntryWeekday(DayOfWeek weekday)
    {
        if (!Enum.IsDefined(weekday)) throw new ArgumentOutOfRangeException(nameof(weekday));
        return new(BreakdownGrouping.EntryWeekday, entryWeekday: weekday);
    }

    public static BreakdownGroupKey ForEntryHour(int hour)
    {
        if (hour is < 0 or > 23) throw new ArgumentOutOfRangeException(nameof(hour));
        return new(BreakdownGrouping.EntryHour, entryHour: hour);
    }

    public static BreakdownGroupKey ForHoldingDuration(HoldingDurationBucket bucket)
    {
        _ = HoldingDurationRange.For(bucket); // Validates supported, explicit bucket identity.
        return new(BreakdownGrouping.HoldingDuration, durationBucket: bucket);
    }

    /// <summary>Uses the original opening instant, independently of closure-date membership.</summary>
    public static BreakdownGroupKey FromNewYorkEntryWeekday(DateTimeOffset openedAtUtc) =>
        ForEntryWeekday(TradingTimePolicy.ConvertUtcToTradingTime(openedAtUtc).DayOfWeek);

    /// <summary>Both occurrences of a repeated autumn hour belong to the same 00–23 group.</summary>
    public static BreakdownGroupKey FromNewYorkEntryHour(DateTimeOffset openedAtUtc) =>
        ForEntryHour(TradingTimePolicy.ConvertUtcToTradingTime(openedAtUtc).Hour);

    /// <summary>Deterministic display fallback only. Never use a label to filter or merge groups.</summary>
    public string GetLabel(string? referenceName = null, bool? isActive = null) => Grouping switch
    {
        BreakdownGrouping.Instrument => ReferenceLabel("Instrument", referenceName, isActive),
        BreakdownGrouping.Account => ReferenceLabel("Account", referenceName, isActive),
        BreakdownGrouping.TradingSetup => IsUnclassified ? "Unclassified" : ReferenceLabel("Setup", referenceName, isActive),
        BreakdownGrouping.Direction => Direction!.Value.ToString(),
        BreakdownGrouping.EntryWeekday => EntryWeekday!.Value.ToString(),
        BreakdownGrouping.EntryHour => EntryHour!.Value.ToString("00", CultureInfo.InvariantCulture) + ":00 hour · New York",
        BreakdownGrouping.HoldingDuration => HoldingDurationRange.For(DurationBucket!.Value).Label,
        _ => throw new InvalidOperationException("Unsupported group identity."),
    };

    /// <summary>Stable tie/order key: IDs, Long/Short, Monday–Sunday, 00–23, increasing duration.</summary>
    public int CompareTo(BreakdownGroupKey? other)
    {
        if (other is null) return 1;
        int dimension = Grouping.CompareTo(other.Grouping);
        if (dimension != 0) return dimension;
        return Grouping switch
        {
            BreakdownGrouping.Instrument or BreakdownGrouping.Account or BreakdownGrouping.TradingSetup =>
                Nullable.Compare(ReferenceId, other.ReferenceId),
            BreakdownGrouping.Direction => Nullable.Compare(Direction, other.Direction),
            BreakdownGrouping.EntryWeekday => WeekdayOrder(EntryWeekday!.Value).CompareTo(WeekdayOrder(other.EntryWeekday!.Value)),
            BreakdownGrouping.EntryHour => Nullable.Compare(EntryHour, other.EntryHour),
            BreakdownGrouping.HoldingDuration => Nullable.Compare(DurationBucket, other.DurationBucket),
            _ => throw new InvalidOperationException("Unsupported group identity."),
        };
    }

    private string ReferenceLabel(string kind, string? name, bool? isActive) =>
        string.IsNullOrWhiteSpace(name) ? $"Missing {kind} ({ReferenceId:D})"
        : name.Trim() + (isActive == false ? " · inactive" : "");

    private static int WeekdayOrder(DayOfWeek day) => ((int)day + 6) % 7;
    private static Guid RequiredId(Guid id, string parameter) => id != Guid.Empty ? id
        : throw new ArgumentException("Select a valid reference ID.", parameter);
}
