using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Setups;

public sealed class TradingSetupStore(IDbContextFactory<JournalDbContext> contextFactory)
    : ITradingSetupStore
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task AddAsync(TradingSetup setup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setup);
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.TradingSetups.Add(TradingSetupPersistenceMapper.ToRecord(setup));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<TradingSetup?> GetByIdAsync(Guid setupId,
        CancellationToken cancellationToken = default)
    {
        if (setupId == Guid.Empty)
        {
            throw new ArgumentException("A trading setup identifier is required.", nameof(setupId));
        }

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        TradingSetupRecord? record = await context.TradingSetups.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == setupId, cancellationToken);
        return record is null ? null : TradingSetupPersistenceMapper.ToDomain(record);
    }

    public async Task UpdateAsync(TradingSetup setup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setup);
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.TradingSetups.Update(TradingSetupPersistenceMapper.ToRecord(setup));
        await context.SaveChangesAsync(cancellationToken);
    }
}
