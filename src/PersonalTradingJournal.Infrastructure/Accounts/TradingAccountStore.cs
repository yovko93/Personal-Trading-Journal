using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;

namespace PersonalTradingJournal.Infrastructure.Accounts;

public sealed class TradingAccountStore : ITradingAccountStore
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradingAccountStore(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);

        _contextFactory = contextFactory;
    }

    public async Task AddAsync(
        TradingAccount account,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        context.TradingAccounts.Add(TradingAccountPersistenceMapper.ToRecord(account));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<TradingAccount?> GetByIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        var record = await context.TradingAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == accountId,
                cancellationToken);

        return record is null
            ? null
            : TradingAccountPersistenceMapper.ToDomain(record);
    }

    public async Task UpdateAsync(
        TradingAccount account,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        context.TradingAccounts.Update(TradingAccountPersistenceMapper.ToRecord(account));
        await context.SaveChangesAsync(cancellationToken);
    }
}
