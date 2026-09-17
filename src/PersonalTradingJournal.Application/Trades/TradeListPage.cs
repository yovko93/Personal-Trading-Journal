namespace PersonalTradingJournal.Application.Trades;

public sealed record TradeListPage
{
    public TradeListPage(
        IReadOnlyList<TradeListItem> items,
        int pageNumber,
        int pageSize,
        int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount));
        }

        if (items.Count > pageSize)
        {
            throw new ArgumentException(
                "A Trade page cannot contain more items than its page size.",
                nameof(items));
        }

        Items = items;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    public IReadOnlyList<TradeListItem> Items { get; }

    public int PageNumber { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages => TotalCount == 0
        ? 0
        : ((TotalCount - 1) / PageSize) + 1;
}
