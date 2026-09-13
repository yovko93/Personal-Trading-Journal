using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;

namespace PersonalTradingJournal.Infrastructure.Screenshots;

public sealed class TradeScreenshotStore : ITradeScreenshotStore
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradeScreenshotStore(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);

        _contextFactory = contextFactory;
    }

    public async Task AddAsync(
        TradeScreenshot screenshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(screenshot);

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        context.TradeScreenshots.Add(
            TradeScreenshotPersistenceMapper.ToRecord(screenshot));
        await context.SaveChangesAsync(cancellationToken);
    }
}
