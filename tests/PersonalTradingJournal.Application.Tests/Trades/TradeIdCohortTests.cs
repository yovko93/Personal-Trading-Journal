using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Application.Tests.Trades;

public sealed class TradeIdCohortTests
{
    [Fact]
    public void MembershipIsDefensivelyFrozenAndEmptyIsNotUnscoped()
    {
        Guid original = Guid.NewGuid();
        var source = new[] { original };
        var cohort = new TradeIdCohort(source);
        source[0] = Guid.NewGuid();
        Assert.Equal(original, Assert.Single(cohort.TradeIds));
        Assert.Throws<NotSupportedException>(() => ((IList<Guid>)cohort.TradeIds).Clear());
        var empty = new TradeListQuery(1, 20, TradeListSortColumn.OpenedAtUtc, TradeListSortDirection.Descending, cohort: new([]));
        Assert.NotNull(empty.Cohort);
        Assert.Empty(empty.Cohort.TradeIds);
        Assert.Null(new TradeListQuery(1, 20, TradeListSortColumn.OpenedAtUtc, TradeListSortDirection.Descending).Cohort);
    }

    [Fact]
    public void InvalidOrDuplicateIdentitiesAreRejectedRatherThanSilentlyChanged()
    {
        Guid id = Guid.NewGuid();
        Assert.Throws<ArgumentNullException>(() => new TradeIdCohort(null!));
        Assert.Throws<ArgumentException>(() => new TradeIdCohort([Guid.Empty]));
        Assert.Throws<ArgumentException>(() => new TradeIdCohort([id, id]));
    }
}
