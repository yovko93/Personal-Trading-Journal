namespace PersonalTradingJournal.Application.Trades;

public sealed record TradeListQuery
{
    public TradeListQuery(
        int pageNumber,
        int pageSize,
        TradeListSortColumn sortColumn,
        TradeListSortDirection sortDirection,
        Guid? tradingAccountId = null)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                pageNumber,
                "The Trade page number must be at least one.");
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                "The Trade page size must be greater than zero.");
        }

        if (!Enum.IsDefined(sortColumn))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sortColumn),
                sortColumn,
                "The Trade sort column is invalid.");
        }

        if (!Enum.IsDefined(sortDirection))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sortDirection),
                sortDirection,
                "The Trade sort direction is invalid.");
        }

        if (tradingAccountId == Guid.Empty)
            throw new ArgumentException("Select a valid Account ID or omit the Account filter.", nameof(tradingAccountId));

        TradingAccountId = tradingAccountId;
        PageNumber = pageNumber;
        PageSize = pageSize;
        SortColumn = sortColumn;
        SortDirection = sortDirection;
    }

    public int PageNumber { get; }
    public Guid? TradingAccountId { get; }

    public int PageSize { get; }

    public TradeListSortColumn SortColumn { get; }

    public TradeListSortDirection SortDirection { get; }
}
