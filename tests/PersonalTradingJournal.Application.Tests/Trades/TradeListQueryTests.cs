using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Trades;

public sealed class TradeListQueryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorRejectsInvalidPageNumber(int pageNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradeListQuery(
            pageNumber,
            20,
            TradeListSortColumn.OpenedAtUtc,
            TradeListSortDirection.Descending));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorRejectsInvalidPageSize(int pageSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradeListQuery(
            1,
            pageSize,
            TradeListSortColumn.OpenedAtUtc,
            TradeListSortDirection.Descending));
    }

    [Fact]
    public void ConstructorRejectsInvalidSortColumn()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradeListQuery(
            1,
            20,
            (TradeListSortColumn)int.MaxValue,
            TradeListSortDirection.Descending));
    }

    [Fact]
    public void ConstructorRejectsInvalidSortDirection()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradeListQuery(
            1,
            20,
            TradeListSortColumn.OpenedAtUtc,
            (TradeListSortDirection)int.MaxValue));
    }

    [Fact]
    public void ConstructorPreservesExplicitQueryValues()
    {
        var query = new TradeListQuery(
            3,
            20,
            TradeListSortColumn.NetPnL,
            TradeListSortDirection.Ascending);

        Assert.Equal(3, query.PageNumber);
        Assert.Equal(20, query.PageSize);
        Assert.Equal(TradeListSortColumn.NetPnL, query.SortColumn);
        Assert.Equal(TradeListSortDirection.Ascending, query.SortDirection);
    }

    [Fact]
    public void PageExposesMetadataAndCalculatesTotalPages()
    {
        TradeListItem[] items = [CreateItem()];

        var page = new TradeListPage(items, 2, 20, 45);

        Assert.Same(items, page.Items);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(45, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
    }

    [Fact]
    public void EmptyPageHasZeroTotalPages()
    {
        var page = new TradeListPage([], 1, 20, 0);

        Assert.Equal(0, page.TotalPages);
    }

    private static TradeListItem CreateItem() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Account",
        Guid.NewGuid(),
        "ES",
        TradeDirection.Long,
        TradeStatus.Open,
        new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero),
        null,
        1m,
        100m,
        null,
        0m,
        null,
        null,
        "USD");
}
