using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Screenshots;

public sealed class TradeScreenshotReader : ITradeScreenshotReader
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradeScreenshotReader(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);

        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<TradeScreenshotListItem>> GetForTradeAsync(
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

        return await context.TradeScreenshots
            .AsNoTracking()
            .Where(record => record.TradeId == tradeId)
            .OrderBy(record => record.CapturedAtUtc.HasValue ? 0 : 1)
            .ThenBy(record => record.CapturedAtUtc)
            .ThenBy(record => record.CreatedAtUtc)
            .ThenBy(record => record.Id)
            .Select(record => new TradeScreenshotListItem(
                record.Id,
                record.TradeId,
                record.Type,
                record.FileName,
                record.CapturedAtUtc,
                record.Timeframe,
                record.Description,
                record.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
