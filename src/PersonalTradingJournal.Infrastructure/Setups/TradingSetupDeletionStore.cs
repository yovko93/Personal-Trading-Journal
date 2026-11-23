using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Setups;

public sealed class TradingSetupDeletionStore(
    IDbContextFactory<JournalDbContext> contextFactory, IPlaybookImageStorage files)
    : ITradingSetupDeletionStore
{
    private const int SqliteConstraintErrorCode = 19;
    private readonly IDbContextFactory<JournalDbContext> _contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<bool> HasTradesAsync(
        Guid setupId,
        CancellationToken cancellationToken = default)
    {
        ValidateSetupId(setupId);
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Trades.AsNoTracking().AnyAsync(
            trade => trade.TradingSetupId == setupId,
            cancellationToken);
    }

    public async Task DeleteAsync(
        Guid setupId,
        CancellationToken cancellationToken = default)
    {
        ValidateSetupId(setupId);
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        string[] keys = await context.SetupExamples.Where(e => e.SetupId == setupId)
            .Select(e => e.StorageKey).Distinct().ToArrayAsync(cancellationToken);
        TradingSetupRecord? record = await context.TradingSetups
            .SingleOrDefaultAsync(candidate => candidate.Id == setupId, cancellationToken);
        if (record is null)
        {
            throw new KeyNotFoundException(
                $"Trading setup '{setupId}' was not found.");
        }

        context.TradingSetups.Remove(record);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqliteException
            {
                SqliteErrorCode: SqliteConstraintErrorCode,
            })
        {
            throw new TradingSetupDeleteBlockedException(
                "The trading setup is referenced by existing Trades. Archive it instead.",
                exception);
        }
        foreach (var key in keys)
            await files.DeleteUnreferencedAsync(key, CancellationToken.None);
    }

    private static void ValidateSetupId(Guid setupId)
    {
        if (setupId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trading setup identifier is required.",
                nameof(setupId));
        }
    }
}
