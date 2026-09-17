using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Mistakes;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Mistakes;

public sealed class TradingMistakeDeletionStoreTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HasTradeMistakesAsyncUsesAssociationScopedExistenceCheck()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingMistake referenced, _, _) = await SeedReferencedMistakeAsync(database);
        var unused = new TradingMistake("Unused", null, Timestamp);
        await database.ServiceProvider.GetRequiredService<ITradingMistakeStore>()
            .AddAsync(unused);
        ITradingMistakeDeletionStore store =
            database.ServiceProvider.GetRequiredService<ITradingMistakeDeletionStore>();

        Assert.True(await store.HasTradeMistakesAsync(referenced.Id));
        Assert.False(await store.HasTradeMistakesAsync(unused.Id));
    }

    [Fact]
    public async Task DeleteAsyncRemovesUnusedMistake()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var mistake = new TradingMistake("Unused", null, Timestamp);
        await database.ServiceProvider.GetRequiredService<ITradingMistakeStore>()
            .AddAsync(mistake);

        await database.ServiceProvider.GetRequiredService<ITradingMistakeDeletionStore>()
            .DeleteAsync(mistake.Id);

        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.TradingMistakes.AsNoTracking()
            .AnyAsync(candidate => candidate.Id == mistake.Id));
    }

    [Fact]
    public async Task DeleteAsyncTranslatesForeignKeyRestrictionAndPreservesAssociation()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingMistake mistake, Trade trade, TradeMistake association) =
            await SeedReferencedMistakeAsync(database);
        ITradingMistakeDeletionStore store =
            database.ServiceProvider.GetRequiredService<ITradingMistakeDeletionStore>();

        await Assert.ThrowsAsync<TradingMistakeDeleteBlockedException>(() =>
            store.DeleteAsync(mistake.Id));

        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.True(await context.TradingMistakes.AsNoTracking()
            .AnyAsync(candidate => candidate.Id == mistake.Id));
        var persisted = await context.TradeMistakes.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == association.Id);
        Assert.Equal(trade.Id, persisted.TradeId);
        Assert.Equal(mistake.Id, persisted.TradingMistakeId);
    }

    [Fact]
    public async Task ReferencedMistakeCanBeEditedAndDeactivatedWithoutRewritingAssociation()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingMistake mistake, _, TradeMistake association) =
            await SeedReferencedMistakeAsync(database);
        ITradingMistakeStore store =
            database.ServiceProvider.GetRequiredService<ITradingMistakeStore>();
        TradingMistake loaded = Assert.IsType<TradingMistake>(
            await store.GetByIdAsync(mistake.Id));
        loaded.UpdateDetails("Renamed Mistake", "Clarified", Timestamp.AddHours(2));
        loaded.Deactivate(Timestamp.AddHours(3));

        await store.UpdateAsync(loaded);

        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(mistake.Id, (await context.TradeMistakes.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == association.Id)).TradingMistakeId);
        Assert.False((await context.TradingMistakes.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == mistake.Id)).IsActive);
    }

    [Fact]
    public async Task OperationsPropagatePreCancelledToken()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var mistake = new TradingMistake("Unused", null, Timestamp);
        await database.ServiceProvider.GetRequiredService<ITradingMistakeStore>()
            .AddAsync(mistake);
        ITradingMistakeDeletionStore store =
            database.ServiceProvider.GetRequiredService<ITradingMistakeDeletionStore>();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.HasTradeMistakesAsync(mistake.Id, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.DeleteAsync(mistake.Id, source.Token));
    }

    private static async Task<(TradingMistake Mistake, Trade Trade, TradeMistake Association)>
        SeedReferencedMistakeAsync(ReaderTestDatabase database)
    {
        var account = new TradingAccount(
            "Primary", TradingAccountType.Personal, null, null, "USD", null, Timestamp);
        var instrument = new Instrument(
            "NQ", "Nasdaq", AssetClass.Futures, "CME", "USD", 0.25m, 5m, Timestamp);
        var mistake = new TradingMistake("FOMO Entry", "Entered early", Timestamp);
        Guid tradeId = Guid.NewGuid();
        var execution = new TradeExecution(
            tradeId, 1, Timestamp, ExecutionSide.Buy, 1m, 20000m,
            1m, 0m, null, null, "NQ");
        Trade trade = Trade.Start(
            account.Id, instrument.Id, new TradePricingSnapshot(20m, "USD"),
            execution, Timestamp);
        var association = new TradeMistake(
            trade.Id, mistake.Id, "Observed", Timestamp.AddHours(1));

        await database.ServiceProvider.GetRequiredService<ITradingAccountStore>()
            .AddAsync(account);
        await database.ServiceProvider.GetRequiredService<IInstrumentStore>()
            .AddAsync(instrument);
        await database.ServiceProvider.GetRequiredService<ITradingMistakeStore>()
            .AddAsync(mistake);
        await database.ServiceProvider.GetRequiredService<ITradeStore>()
            .AddAsync(trade);
        await database.ServiceProvider.GetRequiredService<ITradeMistakeStore>()
            .AddAsync(association);
        return (mistake, trade, association);
    }
}
