using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Instruments;

public sealed class InstrumentDeletionStoreTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HasTradesAsyncUsesInstrumentScopedExistenceCheck()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (Instrument referenced, _) = await SeedReferencedInstrumentAsync(database);
        Instrument unused = CreateInstrument("ES");
        await database.ServiceProvider.GetRequiredService<IInstrumentStore>().AddAsync(unused);
        IInstrumentDeletionStore store = database.ServiceProvider.GetRequiredService<IInstrumentDeletionStore>();

        Assert.True(await store.HasTradesAsync(referenced.Id));
        Assert.False(await store.HasTradesAsync(unused.Id));
    }

    [Fact]
    public async Task DeleteAsyncRemovesUnusedInstrument()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Instrument instrument = CreateInstrument("ES");
        await database.ServiceProvider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        IInstrumentDeletionStore store = database.ServiceProvider.GetRequiredService<IInstrumentDeletionStore>();

        await store.DeleteAsync(instrument.Id);

        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.Instruments.AsNoTracking()
            .AnyAsync(candidate => candidate.Id == instrument.Id));
    }

    [Fact]
    public async Task DeleteAsyncTranslatesForeignKeyRestrictionAndPreservesHistoricalTrade()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (Instrument instrument, Trade trade) = await SeedReferencedInstrumentAsync(database);
        IInstrumentDeletionStore store = database.ServiceProvider.GetRequiredService<IInstrumentDeletionStore>();

        await Assert.ThrowsAsync<InstrumentDeleteBlockedException>(() => store.DeleteAsync(instrument.Id));

        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.True(await context.Instruments.AsNoTracking().AnyAsync(candidate => candidate.Id == instrument.Id));
        Assert.True(await context.Trades.AsNoTracking().AnyAsync(candidate => candidate.Id == trade.Id));
    }

    [Fact]
    public async Task EditingReferencedInstrumentDoesNotRewriteTradePricingSnapshot()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (Instrument instrument, Trade trade) = await SeedReferencedInstrumentAsync(database);
        IInstrumentStore store = database.ServiceProvider.GetRequiredService<IInstrumentStore>();
        Instrument loaded = Assert.IsType<Instrument>(await store.GetByIdAsync(instrument.Id));
        loaded.UpdateDetails(
            "MNQ", "Micro Nasdaq", AssetClass.Futures, null, "EUR",
            0.25m, 0.50m, Timestamp.AddDays(1));

        await store.UpdateAsync(loaded);

        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        var tradeRecord = await context.Trades.AsNoTracking().SingleAsync(candidate => candidate.Id == trade.Id);
        Assert.Equal(20m, tradeRecord.PricingPointValue);
        Assert.Equal("USD", tradeRecord.PricingCurrency);
    }

    [Fact]
    public async Task OperationsPropagatePreCancelledToken()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Instrument instrument = CreateInstrument("ES");
        await database.ServiceProvider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        IInstrumentDeletionStore store = database.ServiceProvider.GetRequiredService<IInstrumentDeletionStore>();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.HasTradesAsync(instrument.Id, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.DeleteAsync(instrument.Id, source.Token));
    }

    private static async Task<(Instrument Instrument, Trade Trade)> SeedReferencedInstrumentAsync(
        ReaderTestDatabase database)
    {
        var account = new TradingAccount(
            "Primary", TradingAccountType.Personal, null, null, "USD", null, Timestamp);
        Instrument instrument = CreateInstrument("NQ");
        Guid tradeId = Guid.NewGuid();
        var execution = new TradeExecution(
            tradeId, 1, Timestamp, ExecutionSide.Buy, 1m, 20000m,
            1m, 0m, null, null, "NQ");
        Trade trade = Trade.Start(
            account.Id, instrument.Id, new TradePricingSnapshot(20m, "USD"),
            execution, Timestamp);
        await database.ServiceProvider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        await database.ServiceProvider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        await database.ServiceProvider.GetRequiredService<ITradeStore>().AddAsync(trade);
        return (instrument, trade);
    }

    private static Instrument CreateInstrument(string symbol) => new(
        symbol, $"{symbol} Instrument", AssetClass.Futures, "CME", "USD",
        0.25m, 5m, Timestamp);
}
