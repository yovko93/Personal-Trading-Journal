using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Accounts;

public sealed class TradingAccountDeletionStoreTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HasTradesAsyncUsesAccountScopedExistenceCheck()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount referenced, _, _) = await SeedReferencedAccountAsync(database);
        TradingAccount unused = CreateAccount("Unused");
        await database.ServiceProvider
            .GetRequiredService<ITradingAccountStore>()
            .AddAsync(unused);
        ITradingAccountDeletionStore store = database.ServiceProvider
            .GetRequiredService<ITradingAccountDeletionStore>();

        Assert.True(await store.HasTradesAsync(referenced.Id));
        Assert.False(await store.HasTradesAsync(unused.Id));
    }

    [Fact]
    public async Task DeleteAsyncRemovesUnusedAccount()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        TradingAccount account = CreateAccount("Unused");
        await database.ServiceProvider
            .GetRequiredService<ITradingAccountStore>()
            .AddAsync(account);
        ITradingAccountDeletionStore store = database.ServiceProvider
            .GetRequiredService<ITradingAccountDeletionStore>();

        await store.DeleteAsync(account.Id);

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.TradingAccounts
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id == account.Id));
    }

    [Fact]
    public async Task DeleteAsyncTranslatesForeignKeyRestrictionAndPreservesHistory()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, _, Trade trade) =
            await SeedReferencedAccountAsync(database);
        ITradingAccountDeletionStore store = database.ServiceProvider
            .GetRequiredService<ITradingAccountDeletionStore>();

        await Assert.ThrowsAsync<TradingAccountDeleteBlockedException>(
            () => store.DeleteAsync(account.Id));

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.True(await context.TradingAccounts.AsNoTracking()
            .AnyAsync(candidate => candidate.Id == account.Id));
        Assert.True(await context.Trades.AsNoTracking()
            .AnyAsync(candidate => candidate.Id == trade.Id));
    }

    [Fact]
    public async Task OperationsPropagatePreCancelledToken()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        TradingAccount account = CreateAccount("Cancellation");
        await database.ServiceProvider
            .GetRequiredService<ITradingAccountStore>()
            .AddAsync(account);
        ITradingAccountDeletionStore store = database.ServiceProvider
            .GetRequiredService<ITradingAccountDeletionStore>();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.HasTradesAsync(account.Id, cancellationSource.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.DeleteAsync(account.Id, cancellationSource.Token));

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.True(await context.TradingAccounts.AsNoTracking()
            .AnyAsync(candidate => candidate.Id == account.Id));
    }

    private static async Task<(TradingAccount Account, Instrument Instrument, Trade Trade)>
        SeedReferencedAccountAsync(ReaderTestDatabase database)
    {
        TradingAccount account = CreateAccount("Referenced");
        var instrument = new Instrument(
            "NQ",
            "Nasdaq-100 E-mini",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5m,
            Timestamp);
        Guid tradeId = Guid.NewGuid();
        var execution = new TradeExecution(
            tradeId,
            1,
            Timestamp,
            ExecutionSide.Buy,
            1m,
            20000m,
            1m,
            0m,
            null,
            null,
            "NQ");
        Trade trade = Trade.Start(
            account.Id,
            instrument.Id,
            new TradePricingSnapshot(20m, "USD"),
            execution,
            Timestamp);

        await database.ServiceProvider
            .GetRequiredService<ITradingAccountStore>()
            .AddAsync(account);
        await database.ServiceProvider
            .GetRequiredService<IInstrumentStore>()
            .AddAsync(instrument);
        await database.ServiceProvider
            .GetRequiredService<ITradeStore>()
            .AddAsync(trade);

        return (account, instrument, trade);
    }

    private static TradingAccount CreateAccount(string name) => new(
        name,
        TradingAccountType.Personal,
        null,
        null,
        "USD",
        null,
        Timestamp);
}
