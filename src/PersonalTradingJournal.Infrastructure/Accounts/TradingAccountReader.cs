using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Accounts;

public sealed class TradingAccountReader : ITradingAccountReader
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradingAccountReader(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);

        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<AccountListItem>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.TradingAccounts
            .AsNoTracking()
            .OrderBy(record => EF.Functions.Collate(record.Name, "NOCASE"))
            .ThenBy(record => record.Id)
            .Select(record => new AccountListItem(
                record.Id,
                record.Name,
                record.AccountType,
                record.ProviderName,
                record.ExternalAccountId,
                record.Currency,
                record.StartingBalance,
                record.IsActive))
            .ToListAsync(cancellationToken);
    }
}
