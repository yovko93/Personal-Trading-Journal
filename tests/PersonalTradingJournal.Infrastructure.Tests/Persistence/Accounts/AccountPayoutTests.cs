using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Accounts;

public sealed class AccountPayoutTests
{
    [Fact]
    public async Task MultiplePayoutsAreExactAccountCurrencyScopedAndRemovalRefreshesBalance()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var accounts = db.ServiceProvider.GetRequiredService<ITradingAccountStore>();
        var payouts = db.ServiceProvider.GetRequiredService<IAccountPayoutStore>();
        var reader = db.ServiceProvider.GetRequiredService<ITradingAccountBalanceReader>();
        var a = new TradingAccount("A", TradingAccountType.Personal, null, null, "USD", 1000, DateTimeOffset.UtcNow);
        var b = new TradingAccount("B", TradingAccountType.Personal, null, null, "EUR", 1000, DateTimeOffset.UtcNow);
        var c = new TradingAccount("C", TradingAccountType.Personal, null, null, "USD", 1000, DateTimeOffset.UtcNow);
        await accounts.AddAsync(a); await accounts.AddAsync(b); await accounts.AddAsync(c);
        Assert.All(await reader.GetAllWithBalancesAsync(), x => { Assert.Equal(0, x.PayoutTotal); Assert.Equal(1000, x.CurrentBalance!.Value); });
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => payouts.AddAsync(a.Id, "USD", 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => payouts.AddAsync(a.Id, "USD", -1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => payouts.AddAsync(a.Id, "EUR", 5));
        var before = DateTimeOffset.UtcNow;
        await payouts.AddAsync(a.Id, "USD", 10.25m); await payouts.AddAsync(a.Id, "USD", 20.50m);
        await payouts.AddAsync(b.Id, "EUR", 7);
        var history = await payouts.GetAsync(a.Id);
        Assert.Equal(2, history.Count);
        Assert.All(history, p => { Assert.Equal(a.Id, p.AccountId); Assert.Equal("USD", p.Currency); Assert.InRange(p.CreatedAtUtc, before, DateTimeOffset.UtcNow); });
        var rows = await reader.GetAllWithBalancesAsync();
        Assert.Equal(30.75m, rows.Single(x => x.Id == a.Id).PayoutTotal);
        Assert.Equal(969.25m, rows.Single(x => x.Id == a.Id).CurrentBalance!.Value);
        Assert.Equal(993, rows.Single(x => x.Id == b.Id).CurrentBalance!.Value);
        Assert.Equal(1000, rows.Single(x => x.Id == c.Id).CurrentBalance!.Value);
        Assert.Equal(0, rows.Single(x => x.Id == c.Id).PayoutTotal);
        await Assert.ThrowsAsync<TradingAccountDeleteBlockedException>(() =>
            db.ServiceProvider.GetRequiredService<ITradingAccountDeletionStore>().DeleteAsync(a.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => payouts.RemoveAsync(b.Id, history[0].Id));
        await payouts.RemoveAsync(a.Id, history[0].Id);
        Assert.Single(await payouts.GetAsync(a.Id));
        Assert.Equal(1000 - history[1].Amount, (await reader.GetAllWithBalancesAsync()).Single(x => x.Id == a.Id).CurrentBalance!.Value);
        var update = new UpdateTradingAccountUseCase(accounts, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => update.ExecuteAsync(new(a.Id, a.Name, a.AccountType, null, null, "EUR", 1000)));
        Assert.Equal("USD", (await accounts.GetByIdAsync(a.Id))!.Currency);
        await payouts.RemoveAsync(a.Id, history[1].Id);
        await update.ExecuteAsync(new(a.Id, a.Name, a.AccountType, null, null, "EUR", 1000));
        Assert.Equal("EUR", (await accounts.GetByIdAsync(a.Id))!.Currency);
    }

    [Fact]
    public void PayoutDeductionRetainsEstimatedCostCoverageAndDoesNotMutateTradeInputs()
    {
        AccountBalanceTrade[] trades = [new(Guid.NewGuid(), "USD", PersonalTradingJournal.Domain.Trades.TradeStatus.Closed, 50, [new(null, 2)])];
        var before = AccountCurrentBalanceCalculator.Calculate(1000, "USD", trades);
        var after = AccountCurrentBalanceCalculator.Calculate(1000, "USD", trades, payouts: 20);
        Assert.Equal(before with { Value = 1028 }, after);
        Assert.True(after.IsEstimated); Assert.Equal(1, after.UnknownCommissionCount);
        Assert.Equal(before, AccountCurrentBalanceCalculator.Calculate(1000, "USD", trades));
        Assert.Null(AccountCurrentBalanceCalculator.Calculate(null, "USD", trades, payouts: 20).Value);
    }

    [Fact]
    public async Task AdditiveMigrationPreservesExistingAccountsAndDoesNotInferPayouts()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        var migrations = context.Database.GetMigrations().ToArray();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[^2]);
        var account = new TradingAccount("Existing", TradingAccountType.Personal, null, null, "USD", 1234.56m, DateTimeOffset.UtcNow);
        await db.ServiceProvider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        await migrator.MigrateAsync();
        Assert.Empty(await context.AccountPayouts.ToArrayAsync());
        var row = Assert.Single(await db.ServiceProvider.GetRequiredService<ITradingAccountBalanceReader>().GetAllWithBalancesAsync());
        Assert.Equal(account.Id, row.Id); Assert.Equal(1234.56m, row.CurrentBalance!.Value); Assert.Equal(0, row.PayoutTotal);
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
