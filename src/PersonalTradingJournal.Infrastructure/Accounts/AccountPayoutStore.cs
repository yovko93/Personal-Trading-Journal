using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
namespace PersonalTradingJournal.Infrastructure.Accounts;
public sealed class AccountPayoutStore(IDbContextFactory<JournalDbContext> factory) : IAccountPayoutStore
{
    public async Task<IReadOnlyList<AccountPayout>> GetAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.AccountPayouts.AsNoTracking().Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Select(x => new AccountPayout(x.Id, x.AccountId, x.Amount, x.Currency, x.CreatedAtUtc)).ToArrayAsync(cancellationToken);
    }
    public async Task AddAsync(Guid accountId, string currency, decimal amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Enter a positive payout amount.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        // An immediate SQLite transaction owns currency validation and insertion together.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var account = await db.TradingAccounts.SingleAsync(x => x.Id == accountId, cancellationToken);
        if (account.Currency != currency) throw new InvalidOperationException("Account currency changed. Refresh the details.");
        var amounts = await db.AccountPayouts.Where(x => x.AccountId == accountId).Select(x => x.Amount).ToArrayAsync(cancellationToken);
        _ = checked(amounts.Sum() + amount);
        db.AccountPayouts.Add(new AccountPayoutRecord { Id = Guid.NewGuid(), AccountId = accountId,
            Currency = account.Currency, Amount = amount, CreatedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
    public async Task RemoveAsync(Guid accountId, Guid payoutId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var record = await db.AccountPayouts.SingleOrDefaultAsync(x => x.Id == payoutId && x.AccountId == accountId, cancellationToken);
        if (record is null) throw new InvalidOperationException("Payout no longer exists in this Account. Refresh the details.");
        db.AccountPayouts.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
    }
}
