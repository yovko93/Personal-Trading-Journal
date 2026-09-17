using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
                .Where(record => record.TradeId == trade.Id)
                .OrderBy(record => record.Sequence)
                .ToListAsync(cancellationToken);
        TradeBrowseRecord? browseRecord = await context.TradeBrowse
            .SingleOrDefaultAsync(
                record => record.TradeId == trade.Id,
                cancellationToken);
        Trade currentTrade = TradePersistenceMapper.ToDomain(
            currentRecord,
            currentExecutionRecords);
        var persistedExecutionIds = currentExecutionRecords
            .Select(record => record.Id)
            .ToHashSet();
        bool introducesNewExecution = trade.Executions.Any(
            execution => !persistedExecutionIds.Contains(execution.Id));
        if (currentTrade.Status == TradeStatus.Closed && introducesNewExecution)
        {
            throw new InvalidOperationException(
                $"Trade '{trade.Id}' is already closed.");
        }

        TradeRecord replacementRecord = TradePersistenceMapper.ToRecord(trade);
        currentRecord.TradingAccountId = replacementRecord.TradingAccountId;
        currentRecord.InstrumentId = replacementRecord.InstrumentId;
        currentRecord.PricingPointValue = replacementRecord.PricingPointValue;
        currentRecord.PricingCurrency = replacementRecord.PricingCurrency;
        currentRecord.TradingSetupId = trade.TradingSetupId;
        currentRecord.UpdatedAtUtc = trade.UpdatedAtUtc;

        // Replacing immutable execution rows in two phases avoids unique-sequence
        // collisions while preserving execution identifiers. The explicit transaction
        // keeps the replacement and scalar update atomic.
        await using IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            context.TradeExecutions.RemoveRange(currentExecutionRecords);
            await context.SaveChangesAsync(cancellationToken);

            context.TradeExecutions.AddRange(
                trade.Executions.Select(TradeExecutionPersistenceMapper.ToRecord));
            if (browseRecord is null)
            {
                context.TradeBrowse.Add(
                    TradeBrowsePersistenceMapper.ToRecord(trade));
            }
            else
            {
                TradeBrowsePersistenceMapper.Apply(browseRecord, trade);
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
