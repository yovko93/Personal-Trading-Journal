using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
        await context.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = ((SqliteConnection)context.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await context.Database.UseTransactionAsync(transaction, cancellationToken);
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
        TradeBrowseRecord? browseRecord = await context.TradeBrowse
            .SingleOrDefaultAsync(
                candidate => candidate.TradeId == tradeId,
                cancellationToken);
        List<TradeMistakeRecord> mistakes = await context.TradeMistakes
            .Where(candidate => candidate.TradeId == tradeId)
            .ToListAsync(cancellationToken);
        List<TradeScreenshotRecord> screenshots = await context.TradeScreenshots
            .Where(candidate => candidate.TradeId == tradeId)
            .ToListAsync(cancellationToken);
        var screenshotIds = screenshots.Select(s => s.Id).ToArray();
        if (await context.PlaybookExamples.AnyAsync(e => e.SourceScreenshotId.HasValue && screenshotIds.Contains(e.SourceScreenshotId.Value), cancellationToken))
            throw new InvalidOperationException("This Trade owns a screenshot retained by Playbook history and cannot be deleted.");
        var retainedKeys = await context.TradeScreenshots.Where(s => s.TradeId != tradeId)
            .Select(s => s.StorageKey).Concat(context.PlaybookExamples.Where(e => e.ImageStorageKey != null).Select(e => e.ImageStorageKey!))
            .ToArrayAsync(cancellationToken);
        IReadOnlyList<string> storageKeys = screenshots
            .Select(screenshot => screenshot.StorageKey)
            .Except(retainedKeys, StringComparer.OrdinalIgnoreCase)
            .ToList();
        try
        {
            context.TradeMistakes.RemoveRange(mistakes);
            context.TradeScreenshots.RemoveRange(screenshots);
            context.TradeExecutions.RemoveRange(executions);
            if (browseRecord is not null)
            {
                context.TradeBrowse.Remove(browseRecord);
            }

            context.Trades.Remove(trade);
            await context.SaveChangesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(CancellationToken.None);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return new TradeDeletionInfo(tradeId, storageKeys);
    }
}
