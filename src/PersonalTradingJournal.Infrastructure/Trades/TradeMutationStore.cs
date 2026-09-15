using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Trades;

public sealed class TradeMutationStore : ITradeMutationStore
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradeMutationStore(
        IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    public async Task<Trade?> GetByIdAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default)
    {
        if (tradeId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trade identifier cannot be empty.",
                nameof(tradeId));
        }

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        TradeRecord? tradeRecord = await context.Trades
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.Id == tradeId,
                cancellationToken);
        if (tradeRecord is null)
        {
            return null;
        }

        List<TradeExecutionRecord> executionRecords =
            await context.TradeExecutions
                .AsNoTracking()
                .Where(record => record.TradeId == tradeId)
                .OrderBy(record => record.Sequence)
                .ToListAsync(cancellationToken);

        return TradePersistenceMapper.ToDomain(tradeRecord, executionRecords);
    }

    public async Task SaveAsync(
        Trade trade,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trade);

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        TradeRecord? currentRecord = await context.Trades
            .SingleOrDefaultAsync(
                record => record.Id == trade.Id,
                cancellationToken);
        if (currentRecord is null)
        {
            throw new KeyNotFoundException(
                $"Trade '{trade.Id}' could not be found.");
        }

        List<TradeExecutionRecord> currentExecutionRecords =
            await context.TradeExecutions
                .AsNoTracking()
                .Where(record => record.TradeId == trade.Id)
                .OrderBy(record => record.Sequence)
                .ToListAsync(cancellationToken);
        Trade currentTrade = TradePersistenceMapper.ToDomain(
            currentRecord,
            currentExecutionRecords);
        var persistedExecutionIds = currentExecutionRecords
            .Select(record => record.Id)
            .ToHashSet();
        List<TradeExecution> newExecutions = trade.Executions
            .Where(execution => !persistedExecutionIds.Contains(execution.Id))
            .OrderBy(execution => execution.Sequence)
            .ToList();

        if (currentTrade.Status == TradeStatus.Closed && newExecutions.Count > 0)
        {
            throw new InvalidOperationException(
                $"Trade '{trade.Id}' is already closed.");
        }

        foreach (TradeExecution newExecution in newExecutions)
        {
            currentTrade.AddExecution(newExecution, trade.UpdatedAtUtc);
        }

        currentRecord.TradingSetupId = trade.TradingSetupId;
        currentRecord.UpdatedAtUtc = trade.UpdatedAtUtc;
        context.TradeExecutions.AddRange(
            newExecutions.Select(TradeExecutionPersistenceMapper.ToRecord));

        await context.SaveChangesAsync(cancellationToken);
    }
}
