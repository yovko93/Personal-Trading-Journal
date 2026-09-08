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
}
