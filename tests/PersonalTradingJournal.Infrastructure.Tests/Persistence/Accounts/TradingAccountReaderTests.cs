using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Accounts;

public sealed class TradingAccountReaderTests
{
    [Fact]
    public async Task GetAllAsyncReturnsEmptyListForEmptyDatabase()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradingAccountReader reader =
            database.ServiceProvider.GetRequiredService<ITradingAccountReader>();

        IReadOnlyList<AccountListItem> accounts = await reader.GetAllAsync();

        Assert.Empty(accounts);
    }

    [Fact]
    public async Task GetAllAsyncMapsAllFieldsAndIncludesActiveAndInactiveAccounts()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid activeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid inactiveId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        DateTimeOffset createdAtUtc =
            new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

        await using (JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync())
        {
            context.TradingAccounts.AddRange(
                new TradingAccountRecord
                {
                    Id = activeId,
                    Name = "Funded Account",
                    AccountType = TradingAccountType.PropFunded,
                    ProviderName = "Provider",
                    ExternalAccountId = "ACCOUNT-42",
                    Currency = "USD",
                    StartingBalance = 100000m,
                    IsActive = true,
                    CreatedAtUtc = createdAtUtc,
                    UpdatedAtUtc = createdAtUtc,
                },
                new TradingAccountRecord
                {
                    Id = inactiveId,
                    Name = "Archived Personal",
                    AccountType = TradingAccountType.Personal,
                    ProviderName = null,
                    ExternalAccountId = null,
                    Currency = "EUR",
                    StartingBalance = null,
                    IsActive = false,
                    CreatedAtUtc = createdAtUtc,
                    UpdatedAtUtc = createdAtUtc.AddDays(1),
                });
            await context.SaveChangesAsync();
        }

        ITradingAccountReader reader =
            database.ServiceProvider.GetRequiredService<ITradingAccountReader>();
        IReadOnlyList<AccountListItem> accounts = await reader.GetAllAsync();

        Assert.Equal(2, accounts.Count);
        AccountListItem active = Assert.Single(accounts, item => item.Id == activeId);
        Assert.Equal("Funded Account", active.Name);
        Assert.Equal(TradingAccountType.PropFunded, active.AccountType);
        Assert.Equal("Provider", active.ProviderName);
        Assert.Equal("ACCOUNT-42", active.ExternalAccountId);
        Assert.Equal("USD", active.Currency);
        Assert.Equal(100000m, active.StartingBalance);
        Assert.True(active.IsActive);

        AccountListItem inactive = Assert.Single(accounts, item => item.Id == inactiveId);
        Assert.Equal("Archived Personal", inactive.Name);
        Assert.Equal(TradingAccountType.Personal, inactive.AccountType);
        Assert.Null(inactive.ProviderName);
        Assert.Null(inactive.ExternalAccountId);
        Assert.Equal("EUR", inactive.Currency);
        Assert.Null(inactive.StartingBalance);
        Assert.False(inactive.IsActive);
    }

    [Fact]
    public async Task GetAllAsyncOrdersByNameNoCaseThenById()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid firstAlphaId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        Guid secondAlphaId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        Guid bravoId = Guid.Parse("20000000-0000-0000-0000-000000000003");

        await using (JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync())
        {
            context.TradingAccounts.AddRange(
                CreateRecord(bravoId, "bravo"),
                CreateRecord(secondAlphaId, "alpha"),
                CreateRecord(firstAlphaId, "Alpha"));
            await context.SaveChangesAsync();
        }

        ITradingAccountReader reader =
            database.ServiceProvider.GetRequiredService<ITradingAccountReader>();
        IReadOnlyList<AccountListItem> accounts = await reader.GetAllAsync();

        Assert.Equal(
            [firstAlphaId, secondAlphaId, bravoId],
            accounts.Select(account => account.Id));
    }

    [Fact]
    public async Task GetAllAsyncPropagatesCancellation()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradingAccountReader reader =
            database.ServiceProvider.GetRequiredService<ITradingAccountReader>();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.GetAllAsync(cancellationSource.Token));
    }

    [Fact]
    public async Task GetByIdAsyncReturnsEveryDetailField()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = Guid.NewGuid();
        DateTimeOffset createdAtUtc =
            new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset updatedAtUtc = createdAtUtc.AddDays(2);

        await using (JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync())
        {
            context.TradingAccounts.Add(new TradingAccountRecord
            {
                Id = accountId,
                Name = "Archived Funded",
                AccountType = TradingAccountType.PropFunded,
                ProviderName = "Provider",
                ExternalAccountId = "FUNDED-42",
                Currency = "EUR",
                StartingBalance = 100000.25m,
                IsActive = false,
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = updatedAtUtc,
            });
            await context.SaveChangesAsync();
        }

        ITradingAccountReader reader =
            database.ServiceProvider.GetRequiredService<ITradingAccountReader>();

        TradingAccountDetails? details = await reader.GetByIdAsync(accountId);

        TradingAccountDetails account = Assert.IsType<TradingAccountDetails>(details);
        Assert.Equal(accountId, account.Id);
        Assert.Equal("Archived Funded", account.Name);
        Assert.Equal(TradingAccountType.PropFunded, account.AccountType);
        Assert.Equal("Provider", account.ProviderName);
        Assert.Equal("FUNDED-42", account.ExternalAccountId);
        Assert.Equal("EUR", account.Currency);
        Assert.Equal(100000.25m, account.StartingBalance);
        Assert.False(account.IsActive);
        Assert.Equal(createdAtUtc, account.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, account.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetByIdAsyncReturnsNullWhenMissing()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradingAccountReader reader =
            database.ServiceProvider.GetRequiredService<ITradingAccountReader>();

        Assert.Null(await reader.GetByIdAsync(Guid.NewGuid()));
    }

    private static TradingAccountRecord CreateRecord(Guid id, string name)
    {
        DateTimeOffset timestamp =
            new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

        return new TradingAccountRecord
        {
            Id = id,
            Name = name,
            AccountType = TradingAccountType.Other,
            Currency = "USD",
            IsActive = true,
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp,
        };
    }
}
