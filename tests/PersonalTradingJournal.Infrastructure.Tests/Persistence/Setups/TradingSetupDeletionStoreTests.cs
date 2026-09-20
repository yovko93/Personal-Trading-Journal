using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Setups;

public sealed class TradingSetupDeletionStoreTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HasTradesAsyncUsesSetupScopedExistenceCheck()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingSetup referenced, _) = await SeedReferencedSetupAsync(database);
        var unused = new TradingSetup("Unused", null, Timestamp);
        await database.ServiceProvider.GetRequiredService<ITradingSetupStore>().AddAsync(unused);
        ITradingSetupDeletionStore store =
            database.ServiceProvider.GetRequiredService<ITradingSetupDeletionStore>();

        Assert.True(await store.HasTradesAsync(referenced.Id));
        Assert.False(await store.HasTradesAsync(unused.Id));
    }

    [Fact]
    public async Task DeleteAsyncRemovesUnusedSetup()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var setup = new TradingSetup("Unused", null, Timestamp);
        await database.ServiceProvider.GetRequiredService<ITradingSetupStore>().AddAsync(setup);

        await database.ServiceProvider.GetRequiredService<ITradingSetupDeletionStore>()
            .DeleteAsync(setup.Id);

        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.TradingSetups.AsNoTracking()
            .AnyAsync(candidate => candidate.Id == setup.Id));
    }

    [Fact]
    public async Task DeleteAsyncTranslatesForeignKeyRestrictionAndPreservesHistoricalClassification()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingSetup setup, Trade trade) = await SeedReferencedSetupAsync(database);
        ITradingSetupDeletionStore store =
            database.ServiceProvider.GetRequiredService<ITradingSetupDeletionStore>();

        await Assert.ThrowsAsync<TradingSetupDeleteBlockedException>(() =>
            store.DeleteAsync(setup.Id));

        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.True(await context.TradingSetups.AsNoTracking()
            .AnyAsync(candidate => candidate.Id == setup.Id));
        Assert.Equal(setup.Id, (await context.Trades.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == trade.Id)).TradingSetupId);
    }

    [Fact]
    public async Task ReferencedSetupCanBeEditedAndDeactivatedWithoutRewritingTrade()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingSetup setup, Trade trade) = await SeedReferencedSetupAsync(database);
        ITradingSetupStore store = database.ServiceProvider.GetRequiredService<ITradingSetupStore>();
        TradingSetup loaded = Assert.IsType<TradingSetup>(await store.GetByIdAsync(setup.Id));
        loaded.UpdateDetails("Renamed Setup", "Revised", Timestamp.AddHours(2));
        loaded.Deactivate(Timestamp.AddHours(3));

        await store.UpdateAsync(loaded);

        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(setup.Id, (await context.Trades.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == trade.Id)).TradingSetupId);
        Assert.False((await context.TradingSetups.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == setup.Id)).IsActive);
    }

    [Fact]
    public async Task OperationsPropagatePreCancelledToken()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var setup = new TradingSetup("Unused", null, Timestamp);
        await database.ServiceProvider.GetRequiredService<ITradingSetupStore>().AddAsync(setup);
        ITradingSetupDeletionStore store =
            database.ServiceProvider.GetRequiredService<ITradingSetupDeletionStore>();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.HasTradesAsync(setup.Id, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.DeleteAsync(setup.Id, source.Token));
    }

    private static async Task<(TradingSetup Setup, Trade Trade)> SeedReferencedSetupAsync(
        ReaderTestDatabase database)
    {
        var account = new TradingAccount(
            "Primary", TradingAccountType.Personal, null, null, "USD", null, Timestamp);
        var instrument = new Instrument(
            "NQ", "Nasdaq", AssetClass.Futures, "CME", "USD", 0.25m, 5m, Timestamp);
        var setup = new TradingSetup("Silver Bullet", "Timed model", Timestamp);
        Guid tradeId = Guid.NewGuid();
        var execution = new TradeExecution(
            tradeId, 1, Timestamp, ExecutionSide.Buy, 1m, 20000m,
            1m, 0m, null, null, "NQ");
        Trade trade = Trade.Start(
            account.Id, instrument.Id, new TradePricingSnapshot(20m, "USD"),
            execution, Timestamp);
        trade.SetTradingSetup(setup.Id, Timestamp.AddHours(1));

        await database.ServiceProvider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        await database.ServiceProvider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        await database.ServiceProvider.GetRequiredService<ITradingSetupStore>().AddAsync(setup);
        await database.ServiceProvider.GetRequiredService<ITradeStore>().AddAsync(trade);
        return (setup, trade);
    }
}
