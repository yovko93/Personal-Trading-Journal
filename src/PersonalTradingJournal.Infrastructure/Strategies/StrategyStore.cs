using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Strategies;
using PersonalTradingJournal.Domain.Strategies;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Strategies;

public sealed class StrategyStore : IStrategyStore
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public StrategyStore(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    public async Task AddAsync(
        Strategy strategy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        context.Strategies.Add(StrategyPersistenceMapper.ToRecord(strategy));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Strategy?> GetByIdAsync(
        Guid strategyId,
        CancellationToken cancellationToken = default)
    {
        if (strategyId == Guid.Empty)
        {
            throw new ArgumentException(
                "A strategy identifier is required.",
                nameof(strategyId));
        }

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        StrategyRecord? record = await context.Strategies
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == strategyId,
                cancellationToken);

        return record is null
            ? null
            : StrategyPersistenceMapper.ToDomain(record);
    }

    public async Task UpdateAsync(
        Strategy strategy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        StrategyRecord record = await context.Strategies.SingleOrDefaultAsync(
                candidate => candidate.Id == strategy.Id,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Strategy '{strategy.Id}' was not found.");

        record.IsActive = strategy.IsActive;
        record.UpdatedAtUtc = strategy.UpdatedAtUtc;
        await context.SaveChangesAsync(cancellationToken);
    }
}
