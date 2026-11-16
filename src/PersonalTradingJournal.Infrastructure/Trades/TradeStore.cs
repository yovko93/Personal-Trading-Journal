using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;

namespace PersonalTradingJournal.Infrastructure.Trades;

public sealed class TradeStore : ITradeStore, IManualTradeScreenshotStore
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradeStore(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);

        _contextFactory = contextFactory;
    }

    public Task AddAsync(
        Trade trade,
        CancellationToken cancellationToken = default) => AddAsync(trade, [], cancellationToken);

    public async Task AddAsync(Trade trade,
        IReadOnlyList<PersonalTradingJournal.Domain.Screenshots.TradeScreenshot> screenshots,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trade);

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        context.Trades.Add(TradePersistenceMapper.ToRecord(trade));
        context.TradeExecutions.AddRange(
            trade.Executions.Select(TradeExecutionPersistenceMapper.ToRecord));
        context.TradeBrowse.Add(TradeBrowsePersistenceMapper.ToRecord(trade));
        if (screenshots.Any(s => s.TradeId != trade.Id))
            throw new ArgumentException("All screenshots must belong to the new Trade.", nameof(screenshots));
        context.TradeScreenshots.AddRange(screenshots.Select(TradeScreenshotPersistenceMapper.ToRecord));

        await context.SaveChangesAsync(cancellationToken);
    }
}
