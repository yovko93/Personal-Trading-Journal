using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

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

        var candidates = await (
                from tradeRecord in context.Trades.AsNoTracking()
                join openingExecution in context.TradeExecutions
                        .AsNoTracking()
                        .Where(record => record.Sequence == 1)
                    on tradeRecord.Id equals openingExecution.TradeId
                join accountRecord in context.TradingAccounts.AsNoTracking()
                    on tradeRecord.TradingAccountId equals accountRecord.Id
                join instrumentRecord in context.Instruments.AsNoTracking()
                    on tradeRecord.InstrumentId equals instrumentRecord.Id
                orderby openingExecution.ExecutedAtUtc descending, tradeRecord.Id
                select new
                {
                    TradeRecord = tradeRecord,
                    TradingAccountName = accountRecord.Name,
                    InstrumentSymbol = instrumentRecord.Symbol,
                })
            .Take(limit)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return Array.Empty<TradeListItem>();
        }

        Guid[] tradeIds = candidates
            .Select(candidate => candidate.TradeRecord.Id)
            .ToArray();
        List<TradeExecutionRecord> executionRecords = await context.TradeExecutions
            .AsNoTracking()
            .Where(record => tradeIds.Contains(record.TradeId))
            .OrderBy(record => record.TradeId)
            .ThenBy(record => record.Sequence)
            .ToListAsync(cancellationToken);
        ILookup<Guid, TradeExecutionRecord> executionsByTradeId = executionRecords
            .ToLookup(record => record.TradeId);

        return candidates
            .Select(candidate =>
            {
                Trade trade = TradePersistenceMapper.ToDomain(
                    candidate.TradeRecord,
                    executionsByTradeId[candidate.TradeRecord.Id]);

                return new TradeListItem(
                    trade.Id,
                    trade.TradingAccountId,
                    candidate.TradingAccountName,
                    trade.InstrumentId,
                    candidate.InstrumentSymbol,
                    trade.Direction,
                    trade.Status,
                    trade.OpenedAtUtc,
                    trade.ClosedAtUtc,
                    trade.OpenQuantity,
                    trade.AverageEntryPrice,
                    trade.AverageExitPrice,
                    trade.TotalCosts,
                    trade.GrossPnL,
                    trade.NetPnL,
                    trade.Pricing.Currency);
            })
            .ToList();
    }
}
