using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Screenshots;

public sealed class TradeScreenshotDeletionStore :
    ITradeScreenshotDeletionStore
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradeScreenshotDeletionStore(
        IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    public async Task<TradeScreenshotDeletionInfo?> DeleteAsync(
        Guid tradeId,
        Guid screenshotId,
        CancellationToken cancellationToken = default)
    {
        if (tradeId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trade identifier cannot be empty.",
                nameof(tradeId));
        }

        if (screenshotId == Guid.Empty)
        {
            throw new ArgumentException(
                "A screenshot identifier cannot be empty.",
                nameof(screenshotId));
        }

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = ((SqliteConnection)context.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await context.Database.UseTransactionAsync(transaction, cancellationToken);
        TradeScreenshotRecord? record = await context.TradeScreenshots
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.TradeId == tradeId &&
                    candidate.Id == screenshotId,
                cancellationToken);

        if (record is null)
        {
            return null;
        }

        if (await context.PlaybookExamples.AnyAsync(e => e.SourceScreenshotId == screenshotId, cancellationToken) ||
            await context.CurrentPlaybookExamples.AnyAsync(e => e.SourceScreenshotId == screenshotId, cancellationToken))
            throw new InvalidOperationException("This screenshot is retained by Playbook history and cannot be deleted.");
        // SQLite NOCASE only folds ASCII, but legacy storage keys may contain Unicode.
        // Match the other deletion paths' ordinal case-insensitive ownership rule, streaming keys.
        bool retained = false;
        await foreach (string key in context.TradeScreenshots.Where(s => s.Id != screenshotId)
                           .Select(s => s.StorageKey).Concat(context.PlaybookExamples.Where(e => e.ImageStorageKey != null).Select(e => e.ImageStorageKey!))
                           .Concat(context.CurrentPlaybookExamples.Where(e => e.ImageStorageKey != null).Select(e => e.ImageStorageKey!))
                           .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            if (!StringComparer.OrdinalIgnoreCase.Equals(key, record.StorageKey)) continue;
            retained = true;
            break;
        }

        var deletionInfo = new TradeScreenshotDeletionInfo(
            record.Id,
            record.TradeId,
            record.StorageKey,
            DeletePhysicalFile: !retained);
        context.TradeScreenshots.Remove(record);
        await context.SaveChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(CancellationToken.None);

        return deletionInfo;
    }
}
