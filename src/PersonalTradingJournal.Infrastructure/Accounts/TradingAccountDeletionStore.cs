using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Accounts;

public sealed class TradingAccountDeletionStore : ITradingAccountDeletionStore
{
    private const int SqliteConstraintErrorCode = 19;

    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradingAccountDeletionStore(
        IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    public async Task<bool> HasTradesAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        ValidateAccountId(accountId);

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Trades
            .AsNoTracking()
            .AnyAsync(
                trade => trade.TradingAccountId == accountId,
                cancellationToken);
    }

    public async Task DeleteAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        ValidateAccountId(accountId);

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        TradingAccountRecord? record = await context.TradingAccounts
            .SingleOrDefaultAsync(
                candidate => candidate.Id == accountId,
                cancellationToken);

        if (record is null)
        {
            throw new KeyNotFoundException(
                $"Trading account '{accountId}' was not found.");
        }

        context.TradingAccounts.Remove(record);

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
            throw new TradingAccountDeleteBlockedException(
                "The trading account is referenced by existing trades.",
                exception);
        }
    }

    private static void ValidateAccountId(Guid accountId)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trading account identifier is required.",
                nameof(accountId));
        }
    }
}
