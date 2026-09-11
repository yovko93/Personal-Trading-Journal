using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;

namespace PersonalTradingJournal.Infrastructure.Trades;

public sealed class TradeStore : ITradeStore
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradeStore(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);

        _contextFactory = contextFactory;
    }

    public async Task AddAsync(
        Trade trade,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trade);

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        context.Trades.Add(TradePersistenceMapper.ToRecord(trade));
        context.TradeExecutions.AddRange(
            trade.Executions.Select(TradeExecutionPersistenceMapper.ToRecord));

        await context.SaveChangesAsync(cancellationToken);
    }
}
