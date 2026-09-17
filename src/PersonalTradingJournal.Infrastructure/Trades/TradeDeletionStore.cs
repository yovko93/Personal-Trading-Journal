using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Trades;

public sealed class TradeDeletionStore : ITradeDeletionStore
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradeDeletionStore(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    public async Task<TradeDeletionInfo?> DeleteAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default)
    {
        if (tradeId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trade identifier is required.",
                nameof(tradeId));
        }

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        TradeRecord? trade = await context.Trades.SingleOrDefaultAsync(
            candidate => candidate.Id == tradeId,
            cancellationToken);
        if (trade is null)
        {
            return null;
        }

        List<TradeExecutionRecord> executions = await context.TradeExecutions
            .Where(candidate => candidate.TradeId == tradeId)
            .ToListAsync(cancellationToken);
        List<TradeMistakeRecord> mistakes = await context.TradeMistakes
            .Where(candidate => candidate.TradeId == tradeId)
            .ToListAsync(cancellationToken);
        List<TradeScreenshotRecord> screenshots = await context.TradeScreenshots
            .Where(candidate => candidate.TradeId == tradeId)
            .ToListAsync(cancellationToken);
        IReadOnlyList<string> storageKeys = screenshots
            .Select(screenshot => screenshot.StorageKey)
            .ToList();

        await using IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            context.TradeMistakes.RemoveRange(mistakes);
            context.TradeScreenshots.RemoveRange(screenshots);
            context.TradeExecutions.RemoveRange(executions);
            context.Trades.Remove(trade);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return new TradeDeletionInfo(tradeId, storageKeys);
    }
}
