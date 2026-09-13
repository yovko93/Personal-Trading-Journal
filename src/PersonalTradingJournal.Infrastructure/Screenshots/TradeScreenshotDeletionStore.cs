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

        var deletionInfo = new TradeScreenshotDeletionInfo(
            record.Id,
            record.TradeId,
            record.StorageKey);
        context.TradeScreenshots.Remove(record);
        await context.SaveChangesAsync(cancellationToken);

        return deletionInfo;
    }
}
