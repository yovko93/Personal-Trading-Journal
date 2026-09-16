using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Instruments;

public sealed class InstrumentDeletionStore : IInstrumentDeletionStore
{
    private const int SqliteConstraintErrorCode = 19;
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public InstrumentDeletionStore(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    public async Task<bool> HasTradesAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        ValidateInstrumentId(instrumentId);
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Trades
            .AsNoTracking()
            .AnyAsync(
                trade => trade.InstrumentId == instrumentId,
                cancellationToken);
    }

    public async Task DeleteAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        ValidateInstrumentId(instrumentId);
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        InstrumentRecord? record = await context.Instruments
            .SingleOrDefaultAsync(
                candidate => candidate.Id == instrumentId,
                cancellationToken);

        if (record is null)
        {
            throw new KeyNotFoundException(
                $"Instrument '{instrumentId}' was not found.");
        }

        context.Instruments.Remove(record);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqliteException
            {
                SqliteErrorCode: SqliteConstraintErrorCode,
            })
        {
            throw new InstrumentDeleteBlockedException(
                "The instrument is referenced by existing trades.",
                exception);
        }
    }

    private static void ValidateInstrumentId(Guid instrumentId)
    {
        if (instrumentId == Guid.Empty)
        {
            throw new ArgumentException(
                "An instrument identifier is required.",
                nameof(instrumentId));
        }
    }
}
