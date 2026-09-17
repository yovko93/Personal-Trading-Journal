using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Trades;

public sealed class TradeListReader : ITradeListReader
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradeListReader(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);

        _contextFactory = contextFactory;
    }

    public async Task<TradeListPage> GetPageAsync(
        TradeListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        var rows =
            from tradeRecord in context.Trades.AsNoTracking()
            join browseRecord in context.TradeBrowse.AsNoTracking()
                on tradeRecord.Id equals browseRecord.TradeId
            join accountRecord in context.TradingAccounts.AsNoTracking()
                on tradeRecord.TradingAccountId equals accountRecord.Id
            join instrumentRecord in context.Instruments.AsNoTracking()
                on tradeRecord.InstrumentId equals instrumentRecord.Id
            select new
            {
                Trade = tradeRecord,
                Browse = browseRecord,
                AccountName = accountRecord.Name,
                InstrumentSymbol = instrumentRecord.Symbol,
            };

        int totalCount = await rows.CountAsync(cancellationToken);

        var orderedRows = (query.SortColumn, query.SortDirection) switch
        {
            (TradeListSortColumn.OpenedAtUtc, TradeListSortDirection.Ascending) =>
                rows.OrderBy(row => row.Browse.OpenedAtUtc)
                    .ThenBy(row => row.Trade.Id),
            (TradeListSortColumn.OpenedAtUtc, TradeListSortDirection.Descending) =>
                rows.OrderByDescending(row => row.Browse.OpenedAtUtc)
                    .ThenBy(row => row.Trade.Id),
            (TradeListSortColumn.Instrument, TradeListSortDirection.Ascending) =>
                rows.OrderBy(row => row.InstrumentSymbol)
                    .ThenBy(row => row.Trade.Id),
            (TradeListSortColumn.Instrument, TradeListSortDirection.Descending) =>
                rows.OrderByDescending(row => row.InstrumentSymbol)
                    .ThenBy(row => row.Trade.Id),
            (TradeListSortColumn.Account, TradeListSortDirection.Ascending) =>
                rows.OrderBy(row => row.AccountName)
                    .ThenBy(row => row.Trade.Id),
            (TradeListSortColumn.Account, TradeListSortDirection.Descending) =>
                rows.OrderByDescending(row => row.AccountName)
                    .ThenBy(row => row.Trade.Id),
            (TradeListSortColumn.AverageEntryPrice, TradeListSortDirection.Ascending) =>
                rows.OrderBy(row => row.Browse.AverageEntryPriceSortKey)
                    .ThenBy(row => row.Trade.Id),
            (TradeListSortColumn.AverageEntryPrice, TradeListSortDirection.Descending) =>
                rows.OrderByDescending(row => row.Browse.AverageEntryPriceSortKey)
                    .ThenBy(row => row.Trade.Id),
            (TradeListSortColumn.OpenQuantity, TradeListSortDirection.Ascending) =>
                rows.OrderBy(row => row.Browse.OpenQuantitySortKey)
                    .ThenBy(row => row.Trade.Id),
            (TradeListSortColumn.OpenQuantity, TradeListSortDirection.Descending) =>
                rows.OrderByDescending(row => row.Browse.OpenQuantitySortKey)
                    .ThenBy(row => row.Trade.Id),
            (TradeListSortColumn.NetPnL, TradeListSortDirection.Ascending) =>
                rows.OrderBy(row => row.Browse.NetPnLSortKey == null)
                    .ThenBy(row => row.Browse.NetPnLSortKey)
                    .ThenBy(row => row.Trade.Id),
            (TradeListSortColumn.NetPnL, TradeListSortDirection.Descending) =>
                rows.OrderBy(row => row.Browse.NetPnLSortKey == null)
                    .ThenByDescending(row => row.Browse.NetPnLSortKey)
                    .ThenBy(row => row.Trade.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        int skip = checked((query.PageNumber - 1) * query.PageSize);
        List<TradeListItem> items = await orderedRows
            .Skip(skip)
            .Take(query.PageSize)
            .Select(row => new TradeListItem(
                row.Trade.Id,
                row.Trade.TradingAccountId,
                row.AccountName,
                row.Trade.InstrumentId,
                row.InstrumentSymbol,
                row.Browse.Direction,
                row.Browse.Status,
                row.Browse.OpenedAtUtc,
                row.Browse.ClosedAtUtc,
                row.Browse.OpenQuantity,
                row.Browse.AverageEntryPrice,
                row.Browse.AverageExitPrice,
                row.Browse.TotalCosts,
                row.Browse.GrossPnL,
                row.Browse.NetPnL,
                row.Trade.PricingCurrency))
            .ToListAsync(cancellationToken);

        return new TradeListPage(
            items,
            query.PageNumber,
            query.PageSize,
            totalCount);
    }
}
