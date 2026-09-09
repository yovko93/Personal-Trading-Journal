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

    [Fact]
    public async Task GetByIdAsyncReturnsNullWhenAccountDoesNotExist()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradingAccountStore store =
            database.ServiceProvider.GetRequiredService<ITradingAccountStore>();

        TradingAccount? account = await store.GetByIdAsync(Guid.NewGuid());

        Assert.Null(account);
    }

    [Fact]
    public async Task GetByIdAsyncRehydratesEveryPersistedField()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradingAccountStore store =
            database.ServiceProvider.GetRequiredService<ITradingAccountStore>();
        DateTimeOffset createdAtUtc =
            new(2026, 9, 7, 8, 30, 0, TimeSpan.Zero);
        DateTimeOffset updatedAtUtc = createdAtUtc.AddDays(1);
        var original = new TradingAccount(
            "Evaluation Account",
            TradingAccountType.PropEvaluation,
            "Provider",
            "EVAL-17",
            "EUR",
            50000.75m,
            createdAtUtc);
        original.Deactivate(updatedAtUtc);
        await store.AddAsync(original);

        TradingAccount? loaded = await store.GetByIdAsync(original.Id);

        TradingAccount account = Assert.IsType<TradingAccount>(loaded);
        Assert.Equal(original.Id, account.Id);
        Assert.Equal(original.Name, account.Name);
        Assert.Equal(original.AccountType, account.AccountType);
        Assert.Equal(original.ProviderName, account.ProviderName);
        Assert.Equal(original.ExternalAccountId, account.ExternalAccountId);
        Assert.Equal(original.Currency, account.Currency);
        Assert.Equal(original.StartingBalance, account.StartingBalance);
        Assert.False(account.IsActive);
        Assert.Equal(createdAtUtc, account.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, account.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetByIdAsyncPropagatesCancellation()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradingAccountStore store =
            database.ServiceProvider.GetRequiredService<ITradingAccountStore>();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.GetByIdAsync(Guid.NewGuid(), cancellationSource.Token));
    }

    [Fact]
    public async Task UpdateAsyncPersistsLifecycleStateAndPreservesReferenceData()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradingAccountStore store =
            database.ServiceProvider.GetRequiredService<ITradingAccountStore>();
        DateTimeOffset createdAtUtc =
            new(2026, 9, 7, 9, 15, 0, TimeSpan.Zero);
        DateTimeOffset updatedAtUtc = createdAtUtc.AddDays(2);
        var original = new TradingAccount(
            "Funded Account",
            TradingAccountType.PropFunded,
            "Provider",
            "FUNDED-9",
            "GBP",
            75000.625m,
            createdAtUtc);
        await store.AddAsync(original);
        TradingAccount account = Assert.IsType<TradingAccount>(
            await store.GetByIdAsync(original.Id));
        account.Deactivate(updatedAtUtc);

        await store.UpdateAsync(account);

        await using JournalDbContext readContext =
            await database.ContextFactory.CreateDbContextAsync();
        TradingAccountRecord persisted = await readContext.TradingAccounts
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == original.Id);
        Assert.Equal(original.Id, persisted.Id);
        Assert.Equal("Funded Account", persisted.Name);
        Assert.Equal(TradingAccountType.PropFunded, persisted.AccountType);
        Assert.Equal("Provider", persisted.ProviderName);
        Assert.Equal("FUNDED-9", persisted.ExternalAccountId);
        Assert.Equal("GBP", persisted.Currency);
        Assert.Equal(75000.625m, persisted.StartingBalance);
        Assert.False(persisted.IsActive);
        Assert.Equal(createdAtUtc, persisted.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, persisted.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsyncDoesNotPersistWhenCancellationIsRequested()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradingAccountStore store =
            database.ServiceProvider.GetRequiredService<ITradingAccountStore>();
        DateTimeOffset createdAtUtc =
            new(2026, 9, 7, 10, 45, 0, TimeSpan.Zero);
        var account = new TradingAccount(
            "Cancellation Account",
            TradingAccountType.Demo,
            null,
            null,
            "USD",
            null,
            createdAtUtc);
        await store.AddAsync(account);
        account.Deactivate(createdAtUtc.AddDays(1));
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.UpdateAsync(account, cancellationSource.Token));

        await using JournalDbContext readContext =
            await database.ContextFactory.CreateDbContextAsync();
        TradingAccountRecord persisted = await readContext.TradingAccounts
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == account.Id);
        Assert.True(persisted.IsActive);
        Assert.Equal(createdAtUtc, persisted.UpdatedAtUtc);
    }
}
