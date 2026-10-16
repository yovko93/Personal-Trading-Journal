using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Analytics.Breakdown;

public sealed class BreakdownGroupKeyTests
{
    [Theory]
    [InlineData(BreakdownGrouping.Instrument)]
    [InlineData(BreakdownGrouping.Account)]
    [InlineData(BreakdownGrouping.TradingSetup)]
    public void DuplicateDisplayNamesNeverMergeIdentitiesAndReferenceChangesDoNotChangeKeys(BreakdownGrouping dimension)
    {
        var first = Reference(dimension, Guid.NewGuid());
        var second = Reference(dimension, Guid.NewGuid());
        Assert.Equal(first.GetLabel("Same name"), second.GetLabel("Same name"));
        Assert.NotEqual(first, second);
        var values = new Dictionary<BreakdownGroupKey, string> { [first] = "first", [second] = "second" };
        Assert.Equal("Renamed · inactive", first.GetLabel("Renamed", isActive: false));
        Assert.StartsWith("Missing ", first.GetLabel());
        Assert.Contains(first.ReferenceId!.Value.ToString("D"), first.GetLabel());
        Assert.Equal("first", values[Reference(dimension, first.ReferenceId.Value)]);
        Assert.Equal(2, values.Count);
        Assert.Equal(0, first.CompareTo(Reference(dimension, first.ReferenceId.Value)));
    }

    [Fact]
    public void UnclassifiedIsOnlyTheAbsentSetupIdAndMissingMetadataRetainsItsSpecificIdentity()
    {
        var unclassified = BreakdownGroupKey.ForSetup(null);
        Guid id = Guid.NewGuid();
        var retained = BreakdownGroupKey.ForSetup(id);
        Assert.True(unclassified.IsUnclassified);
        Assert.Equal("Unclassified", unclassified.GetLabel());
        Assert.False(retained.IsUnclassified);
        Assert.Equal($"Missing Setup ({id:D})", retained.GetLabel());
        Assert.Equal("Historical · inactive", retained.GetLabel("Historical", false));
        Assert.NotEqual(unclassified, retained);
        Assert.NotEqual(BreakdownGroupKey.ForAccount(id), BreakdownGroupKey.ForInstrument(id));
        Assert.NotEqual(retained, BreakdownGroupKey.ForInstrument(id));
    }

    [Fact]
    public void InvalidReferenceIdsAndTypedPayloadsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => BreakdownGroupKey.ForAccount(Guid.Empty));
        Assert.Throws<ArgumentException>(() => BreakdownGroupKey.ForInstrument(Guid.Empty));
        Assert.Throws<ArgumentException>(() => BreakdownGroupKey.ForSetup(Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => BreakdownGroupKey.ForDirection((TradeDirection)0));
        Assert.Throws<ArgumentOutOfRangeException>(() => BreakdownGroupKey.ForEntryWeekday((DayOfWeek)7));
        Assert.Throws<ArgumentOutOfRangeException>(() => BreakdownGroupKey.ForEntryHour(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BreakdownGroupKey.ForEntryHour(24));
        Assert.Throws<ArgumentOutOfRangeException>(() => BreakdownGroupKey.ForHoldingDuration((HoldingDurationBucket)0));
    }

    [Fact]
    public void NaturalOrderingIsChronologicalAndReferenceTiesUseIdsRatherThanLabels()
    {
        DayOfWeek[] week = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
        Assert.Equal(week, Enum.GetValues<DayOfWeek>().Select(BreakdownGroupKey.ForEntryWeekday).Order().Select(k => k.EntryWeekday!.Value));
        Assert.Equal(Enumerable.Range(0, 24), Enumerable.Range(0, 24).Reverse()
            .Select(BreakdownGroupKey.ForEntryHour).Order().Select(k => k.EntryHour!.Value));
        Assert.Equal(new[] { TradeDirection.Long, TradeDirection.Short }, new[] { TradeDirection.Short, TradeDirection.Long }
            .Select(BreakdownGroupKey.ForDirection).Order().Select(k => k.Direction!.Value));
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        Assert.Equal(ids.Order(), ids.Select(BreakdownGroupKey.ForAccount).Order().Select(k => k.ReferenceId!.Value));
        Assert.Equal(Enum.GetValues<HoldingDurationBucket>(), Enum.GetValues<HoldingDurationBucket>().Reverse()
            .Select(BreakdownGroupKey.ForHoldingDuration).Order().Select(k => k.DurationBucket!.Value));
    }

    [Theory]
    [InlineData(3, 8, 6, 59, 1)]
    [InlineData(3, 8, 7, 0, 3)]
    [InlineData(11, 1, 5, 30, 1)]
    [InlineData(11, 1, 6, 30, 1)]
    public void EntryHoursUseNewYorkIncludingSkippedAndRepeatedDstHours(int month, int day, int hour, int minute, int expected)
    {
        var entry = new DateTimeOffset(2026, month, day, hour, minute, 0, TimeSpan.Zero);
        Assert.Equal(BreakdownGroupKey.ForEntryHour(expected), BreakdownGroupKey.FromNewYorkEntryHour(entry));
        Assert.Equal(BreakdownGroupKey.ForEntryWeekday(DayOfWeek.Sunday), BreakdownGroupKey.FromNewYorkEntryWeekday(entry));
    }

    [Fact]
    public void ClosurePeriodMembershipDoesNotRetargetEntryGroupingAcrossMidnight()
    {
        // Monday 23:30 EDT entry, Tuesday 00:15 EDT closure.
        var entry = new DateTimeOffset(2026, 10, 6, 3, 30, 0, TimeSpan.Zero);
        var close = entry.AddMinutes(45);
        var query = new BreakdownQuery("USD", closedFromNewYork: new(2026, 10, 6), closedThroughNewYork: new(2026, 10, 6),
            grouping: BreakdownGrouping.EntryWeekday);
        Assert.True(close >= query.TradeFilter.ClosedFromUtc && close < query.TradeFilter.ClosedBeforeUtc);
        Assert.True(entry < query.TradeFilter.ClosedFromUtc);
        Assert.Equal(BreakdownGroupKey.ForEntryWeekday(DayOfWeek.Monday), BreakdownGroupKey.FromNewYorkEntryWeekday(entry));
        Assert.Equal(BreakdownGroupKey.ForEntryHour(23), BreakdownGroupKey.FromNewYorkEntryHour(entry));
    }

    [Fact]
    public void EntryHelpersRequireCanonicalUtcInstants()
    {
        var local = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.FromHours(-4));
        Assert.Throws<ArgumentException>(() => BreakdownGroupKey.FromNewYorkEntryHour(local));
        Assert.Throws<ArgumentException>(() => BreakdownGroupKey.FromNewYorkEntryWeekday(local));
    }

    private static BreakdownGroupKey Reference(BreakdownGrouping dimension, Guid id) => dimension switch
    {
        BreakdownGrouping.Instrument => BreakdownGroupKey.ForInstrument(id),
        BreakdownGrouping.Account => BreakdownGroupKey.ForAccount(id),
        BreakdownGrouping.TradingSetup => BreakdownGroupKey.ForSetup(id),
        _ => throw new ArgumentOutOfRangeException(nameof(dimension)),
    };
}
