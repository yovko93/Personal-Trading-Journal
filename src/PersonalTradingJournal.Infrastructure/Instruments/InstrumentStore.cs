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
}
