using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;

namespace PersonalTradingJournal.Infrastructure.Instruments;

public sealed class InstrumentStore : IInstrumentStore
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public InstrumentStore(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);

        _contextFactory = contextFactory;
    }

    public async Task AddAsync(
        Instrument instrument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        context.Instruments.Add(InstrumentPersistenceMapper.ToRecord(instrument));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Instrument?> GetByIdAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        var record = await context.Instruments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == instrumentId,
                cancellationToken);

        return record is null
            ? null
            : InstrumentPersistenceMapper.ToDomain(record);
    }

    public async Task UpdateAsync(
        Instrument instrument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        context.Instruments.Update(InstrumentPersistenceMapper.ToRecord(instrument));
        await context.SaveChangesAsync(cancellationToken);
    }
}
