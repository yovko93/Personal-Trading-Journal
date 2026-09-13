using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Screenshots;

public sealed class TradeScreenshotContentReader : ITradeScreenshotContentReader
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;
    private readonly ITradeScreenshotFileStorage _fileStorage;

    public TradeScreenshotContentReader(
        IDbContextFactory<JournalDbContext> contextFactory,
        ITradeScreenshotFileStorage fileStorage)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(fileStorage);

        _contextFactory = contextFactory;
        _fileStorage = fileStorage;
    }

    public async Task<TradeScreenshotContent?> OpenAsync(
        Guid screenshotId,
        CancellationToken cancellationToken = default)
    {
        if (screenshotId == Guid.Empty)
        {
            throw new ArgumentException(
                "A screenshot identifier cannot be empty.",
                nameof(screenshotId));
        }

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        var metadata = await context.TradeScreenshots
            .AsNoTracking()
            .Where(record => record.Id == screenshotId)
            .Select(record => new
            {
                record.Id,
                record.TradeId,
                record.StorageKey,
                record.FileName,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (metadata is null)
        {
            return null;
        }

        Stream? content = await _fileStorage.OpenReadAsync(
            metadata.StorageKey,
            cancellationToken);
        if (content is null)
        {
            throw new FileNotFoundException(
                $"The physical file for Trade screenshot '{metadata.Id}' was not found.");
        }

        try
        {
            return new TradeScreenshotContent(
                metadata.Id,
                metadata.TradeId,
                metadata.FileName,
                content);
        }
        catch
        {
            await content.DisposeAsync();
            throw;
        }
    }
}
