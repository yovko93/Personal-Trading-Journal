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

    public async Task<IReadOnlyList<TradeListItem>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                "The Trade list limit must be greater than zero.");
        }

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await (
                from tradeRecord in context.Trades.AsNoTracking()
                join browseRecord in context.TradeBrowse.AsNoTracking()
                    on tradeRecord.Id equals browseRecord.TradeId
                join accountRecord in context.TradingAccounts.AsNoTracking()
                    on tradeRecord.TradingAccountId equals accountRecord.Id
                join instrumentRecord in context.Instruments.AsNoTracking()
                    on tradeRecord.InstrumentId equals instrumentRecord.Id
                orderby browseRecord.OpenedAtUtc descending, tradeRecord.Id
                select new TradeListItem(
                    tradeRecord.Id,
                    tradeRecord.TradingAccountId,
                    accountRecord.Name,
                    tradeRecord.InstrumentId,
                    instrumentRecord.Symbol,
                    browseRecord.Direction,
                    browseRecord.Status,
                    browseRecord.OpenedAtUtc,
                    browseRecord.ClosedAtUtc,
                    browseRecord.OpenQuantity,
                    browseRecord.AverageEntryPrice,
                    browseRecord.AverageExitPrice,
                    browseRecord.TotalCosts,
                    browseRecord.GrossPnL,
                    browseRecord.NetPnL,
                    tradeRecord.PricingCurrency))
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
