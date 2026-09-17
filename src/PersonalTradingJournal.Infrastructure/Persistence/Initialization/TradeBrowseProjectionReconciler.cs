using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Initialization;

public sealed class TradeBrowseProjectionReconciler
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradeBrowseProjectionReconciler(
        IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    public async Task ReconcileAsync(
        CancellationToken cancellationToken = default)
    {
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        List<Guid> staleTradeIds = await context.Trades
            .AsNoTracking()
            .Where(trade => !context.TradeBrowse.Any(projection =>
                projection.TradeId == trade.Id &&
                projection.ProjectionVersion ==
                    TradeBrowsePersistenceMapper.CurrentProjectionVersion))
            .Select(trade => trade.Id)
            .ToListAsync(cancellationToken);
        if (staleTradeIds.Count == 0)
        {
            return;
        }

        List<TradeRecord> tradeRecords = await context.Trades
            .AsNoTracking()
            .Where(record => staleTradeIds.Contains(record.Id))
            .ToListAsync(cancellationToken);
        List<TradeExecutionRecord> executionRecords =
            await context.TradeExecutions
                .AsNoTracking()
                .Where(record => staleTradeIds.Contains(record.TradeId))
                .OrderBy(record => record.TradeId)
                .ThenBy(record => record.Sequence)
                .ToListAsync(cancellationToken);
        ILookup<Guid, TradeExecutionRecord> executionsByTradeId =
            executionRecords.ToLookup(record => record.TradeId);
        Dictionary<Guid, TradeBrowseRecord> existingProjections =
            await context.TradeBrowse
                .Where(record => staleTradeIds.Contains(record.TradeId))
                .ToDictionaryAsync(record => record.TradeId, cancellationToken);

        await using IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (TradeRecord tradeRecord in tradeRecords)
            {
                Trade trade = TradePersistenceMapper.ToDomain(
                    tradeRecord,
                    executionsByTradeId[tradeRecord.Id]);
                if (existingProjections.TryGetValue(
                        tradeRecord.Id,
                        out TradeBrowseRecord? projection))
                {
                    TradeBrowsePersistenceMapper.Apply(projection, trade);
                }
                else
                {
                    context.TradeBrowse.Add(
                        TradeBrowsePersistenceMapper.ToRecord(trade));
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
