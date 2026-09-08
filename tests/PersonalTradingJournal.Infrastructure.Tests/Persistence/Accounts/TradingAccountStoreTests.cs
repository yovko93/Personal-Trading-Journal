using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Accounts;

public sealed class TradingAccountStoreTests
{
    [Fact]
    public async Task AddAsyncPersistsEveryTradingAccountField()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradingAccountStore store =
            database.ServiceProvider.GetRequiredService<ITradingAccountStore>();
        DateTimeOffset createdAtUtc =
            new(2026, 9, 9, 11, 45, 30, TimeSpan.Zero);
        var account = new TradingAccount(
            "Funded Account",
            TradingAccountType.PropFunded,
            "Provider",
            "ACCOUNT-42",
            "USD",
            100000.125m,
            createdAtUtc);

        await store.AddAsync(account);

        await using JournalDbContext readContext =
            await database.ContextFactory.CreateDbContextAsync();
        TradingAccountRecord persisted = await readContext.TradingAccounts
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == account.Id);

        Assert.Equal(account.Id, persisted.Id);
        Assert.Equal("Funded Account", persisted.Name);
        Assert.Equal(TradingAccountType.PropFunded, persisted.AccountType);
        Assert.Equal("Provider", persisted.ProviderName);
        Assert.Equal("ACCOUNT-42", persisted.ExternalAccountId);
        Assert.Equal("USD", persisted.Currency);
        Assert.Equal(100000.125m, persisted.StartingBalance);
        Assert.True(persisted.IsActive);
        Assert.Equal(createdAtUtc, persisted.CreatedAtUtc);
        Assert.Equal(createdAtUtc, persisted.UpdatedAtUtc);
    }

    [Fact]
    public async Task AddAsyncDoesNotPersistWhenCancellationIsRequested()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradingAccountStore store =
            database.ServiceProvider.GetRequiredService<ITradingAccountStore>();
        var account = new TradingAccount(
            "Cancelled Account",
            TradingAccountType.Demo,
            null,
            null,
            "USD",
            null,
            new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero));
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.AddAsync(account, cancellationSource.Token));

        await using JournalDbContext readContext =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.False(await readContext.TradingAccounts
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id == account.Id));
    }
}
