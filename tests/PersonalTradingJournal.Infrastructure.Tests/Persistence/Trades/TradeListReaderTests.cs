using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

public sealed class TradeListReaderTests
{
    private static readonly DateTimeOffset ReferenceCreatedAtUtc =
        new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetRecentAsyncReturnsEmptyWhenNoTradesExist()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradeListReader reader = GetReader(database);

        IReadOnlyList<TradeListItem> items = await reader.GetRecentAsync(10);

        Assert.Empty(items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetRecentAsyncRejectsNonPositiveLimit(int limit)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradeListReader reader = GetReader(database);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => reader.GetRecentAsync(limit));
    }

    [Fact]
    public async Task GetRecentAsyncProjectsOpenTradeExactly()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(
            database,
            "Open Account",
            "NQ");
        DateTimeOffset openedAtUtc = Utc(10, 9);
        Trade trade = CreateTrade(
            account.Id,
            instrument.Id,
            Guid.NewGuid(),
            openedAtUtc.AddHours(2),
            20m,
            "USD",
            new ExecutionFact(openedAtUtc, ExecutionSide.Buy, 2m, 100m, 1.5m, 0.5m));
        await PersistTradeAsync(database, trade);

        TradeListItem item = Assert.Single(await GetReader(database).GetRecentAsync(10));

        Assert.Equal(trade.Id, item.Id);
        Assert.Equal(account.Id, item.TradingAccountId);
        Assert.Equal("Open Account", item.TradingAccountName);
        Assert.Equal(instrument.Id, item.InstrumentId);
        Assert.Equal("NQ", item.InstrumentSymbol);
        Assert.Equal(TradeDirection.Long, item.Direction);
        Assert.Equal(TradeStatus.Open, item.Status);
        Assert.Equal(openedAtUtc, item.OpenedAtUtc);
        Assert.Null(item.ClosedAtUtc);
        Assert.Equal(2m, item.OpenQuantity);
        Assert.Equal(100m, item.AverageEntryPrice);
        Assert.Null(item.AverageExitPrice);
        Assert.Equal(2m, item.TotalCosts);
        Assert.Null(item.GrossPnL);
        Assert.Null(item.NetPnL);
        Assert.Equal("USD", item.Currency);
    }

    [Fact]
    public async Task GetRecentAsyncProjectsClosedTradeEconomicsExactly()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(database);
        DateTimeOffset openedAtUtc = Utc(10, 9);
        DateTimeOffset closedAtUtc = openedAtUtc.AddHours(1);
        Trade trade = CreateTrade(
            account.Id,
            instrument.Id,
            Guid.NewGuid(),
            closedAtUtc.AddHours(1),
            20m,
            "USD",
            new ExecutionFact(openedAtUtc, ExecutionSide.Buy, 2m, 100m, 1m, 0.5m),
            new ExecutionFact(closedAtUtc, ExecutionSide.Sell, 2m, 110m, 1.5m, 0.25m));
        await PersistTradeAsync(database, trade);

        TradeListItem item = Assert.Single(await GetReader(database).GetRecentAsync(10));

        Assert.Equal(TradeStatus.Closed, item.Status);
        Assert.Equal(0m, item.OpenQuantity);
        Assert.Equal(closedAtUtc, item.ClosedAtUtc);
        Assert.Equal(100m, item.AverageEntryPrice);
        Assert.Equal(110m, item.AverageExitPrice);
        Assert.Equal(3.25m, item.TotalCosts);
        Assert.Equal(400m, item.GrossPnL);
        Assert.Equal(396.75m, item.NetPnL);
    }

    [Fact]
    public async Task GetRecentAsyncOrdersByMarketTimeInsteadOfAuditTime()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(database);
        Trade newestMarketTrade = CreateTrade(
            account.Id,
            instrument.Id,
            Guid.NewGuid(),
            Utc(2, 8),
            20m,
            "USD",
            new ExecutionFact(Utc(10, 12), ExecutionSide.Buy, 1m, 100m, 0m, 0m));
        Trade olderMarketTrade = CreateTrade(
            account.Id,
            instrument.Id,
            Guid.NewGuid(),
            Utc(11, 8),
            20m,
            "USD",
            new ExecutionFact(Utc(9, 12), ExecutionSide.Buy, 1m, 100m, 0m, 0m));
        await PersistTradeAsync(database, olderMarketTrade);
        await PersistTradeAsync(database, newestMarketTrade);

        IReadOnlyList<TradeListItem> items = await GetReader(database).GetRecentAsync(10);

        Assert.Equal(
            [newestMarketTrade.Id, olderMarketTrade.Id],
            items.Select(item => item.Id));
    }

    [Fact]
    public async Task GetRecentAsyncUsesTradeIdAsDeterministicTieBreaker()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(database);
        Guid lowerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid higherId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        DateTimeOffset openedAtUtc = Utc(10, 9);
        Trade higher = CreateOpenTrade(account.Id, instrument.Id, higherId, openedAtUtc);
        Trade lower = CreateOpenTrade(account.Id, instrument.Id, lowerId, openedAtUtc);
        await PersistTradeAsync(database, higher);
        await PersistTradeAsync(database, lower);

        IReadOnlyList<TradeListItem> items = await GetReader(database).GetRecentAsync(10);

        Assert.Equal([lowerId, higherId], items.Select(item => item.Id));
    }

    [Fact]
    public async Task GetRecentAsyncTakesLimitAfterApplyingAuthoritativeOrder()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(database);
        var trades = new List<Trade>();

        for (int index = 1; index <= 5; index++)
        {
            Guid tradeId = Guid.Parse($"00000000-0000-0000-0000-{index:D12}");
            Trade trade = CreateOpenTrade(
                account.Id,
                instrument.Id,
                tradeId,
                Utc(10, index));
            trades.Add(trade);
            await PersistTradeAsync(database, trade);
        }

        IReadOnlyList<TradeListItem> items = await GetReader(database).GetRecentAsync(3);

        Assert.Equal(
            trades.OrderByDescending(trade => trade.OpenedAtUtc)
                .Take(3)
                .Select(trade => trade.Id),
            items.Select(item => item.Id));
    }

    [Fact]
    public async Task GetRecentAsyncIncludesTradesWithInactiveReferences()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(
            database,
            "Retired Account",
            "ES");
        Trade trade = CreateOpenTrade(
            account.Id,
            instrument.Id,
            Guid.NewGuid(),
            Utc(10, 9));
        await PersistTradeAsync(database, trade);
        DateTimeOffset deactivatedAtUtc = Utc(11, 12);
        account.Deactivate(deactivatedAtUtc);
        instrument.Deactivate(deactivatedAtUtc);
        await database.ServiceProvider.GetRequiredService<ITradingAccountStore>()
            .UpdateAsync(account);
        await database.ServiceProvider.GetRequiredService<IInstrumentStore>()
            .UpdateAsync(instrument);

        TradeListItem item = Assert.Single(await GetReader(database).GetRecentAsync(10));

        Assert.Equal("Retired Account", item.TradingAccountName);
        Assert.Equal("ES", item.InstrumentSymbol);
    }

    [Fact]
    public async Task GetRecentAsyncUsesHistoricalPricingForCurrencyAndEconomics()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(database);
        DateTimeOffset openedAtUtc = Utc(10, 9);
        Trade trade = CreateTrade(
            account.Id,
            instrument.Id,
            Guid.NewGuid(),
            Utc(10, 12),
            20m,
            "USD",
            new ExecutionFact(openedAtUtc, ExecutionSide.Buy, 2m, 100m, 1m, 0m),
            new ExecutionFact(openedAtUtc.AddHours(1), ExecutionSide.Sell, 2m, 110m, 1m, 0m));
        await PersistTradeAsync(database, trade);

        await using (JournalDbContext context =
                     await database.ContextFactory.CreateDbContextAsync())
        {
            InstrumentRecord currentInstrument = await context.Instruments
                .SingleAsync(record => record.Id == instrument.Id);
            currentInstrument.Currency = "EUR";
            currentInstrument.TickValue = 2.5m;
            await context.SaveChangesAsync();
        }

        TradeListItem item = Assert.Single(await GetReader(database).GetRecentAsync(10));

        Assert.Equal("USD", item.Currency);
        Assert.Equal(400m, item.GrossPnL);
        Assert.Equal(398m, item.NetPnL);
    }

    [Fact]
    public async Task GetRecentAsyncProjectsScaleInAndPartialExitThroughDomainTrade()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(database);
        DateTimeOffset openedAtUtc = Utc(10, 9);
        Trade trade = CreateTrade(
            account.Id,
            instrument.Id,
            Guid.NewGuid(),
            Utc(10, 13),
            20m,
            "USD",
            new ExecutionFact(openedAtUtc, ExecutionSide.Buy, 2m, 100m, 1m, 0.5m),
            new ExecutionFact(openedAtUtc.AddMinutes(10), ExecutionSide.Buy, 1m, 110m, 0.75m, 0.25m),
            new ExecutionFact(openedAtUtc.AddMinutes(20), ExecutionSide.Sell, 1m, 120m, 0.5m, 0.25m));
        await PersistTradeAsync(database, trade);

        TradeListItem item = Assert.Single(await GetReader(database).GetRecentAsync(10));

        Assert.Equal(TradeDirection.Long, item.Direction);
        Assert.Equal(TradeStatus.Open, item.Status);
        Assert.Equal(2m, item.OpenQuantity);
        Assert.Equal(310m / 3m, item.AverageEntryPrice);
        Assert.Equal(120m, item.AverageExitPrice);
        Assert.Equal(3.25m, item.TotalCosts);
        Assert.Null(item.ClosedAtUtc);
        Assert.Null(item.GrossPnL);
        Assert.Null(item.NetPnL);
    }

    [Fact]
    public async Task GetRecentAsyncUsesFreshContextForSubsequentRead()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) = await PersistReferencesAsync(database);
        ITradeListReader reader = GetReader(database);
        Assert.Empty(await reader.GetRecentAsync(10));
        Trade trade = CreateOpenTrade(
            account.Id,
            instrument.Id,
            Guid.NewGuid(),
            Utc(10, 9));
        await PersistTradeAsync(database, trade);

        TradeListItem item = Assert.Single(await reader.GetRecentAsync(10));

        Assert.Equal(trade.Id, item.Id);
    }

    [Fact]
    public async Task GetRecentAsyncPropagatesPreCancelledToken()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradeListReader reader = GetReader(database);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.GetRecentAsync(10, cancellationSource.Token));
    }

    private static ITradeListReader GetReader(ReaderTestDatabase database) =>
        database.ServiceProvider.GetRequiredService<ITradeListReader>();

    private static async Task<(TradingAccount Account, Instrument Instrument)>
        PersistReferencesAsync(
            ReaderTestDatabase database,
            string accountName = "List Account",
            string instrumentSymbol = "NQ")
    {
        var account = new TradingAccount(
            accountName,
            TradingAccountType.Personal,
            "Reference Provider",
            "ACCOUNT-1",
            "USD",
            100000m,
            ReferenceCreatedAtUtc);
        var instrument = new Instrument(
            instrumentSymbol,
            $"{instrumentSymbol} Display",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5m,
            ReferenceCreatedAtUtc);
        await database.ServiceProvider.GetRequiredService<ITradingAccountStore>()
            .AddAsync(account);
        await database.ServiceProvider.GetRequiredService<IInstrumentStore>()
            .AddAsync(instrument);

        return (account, instrument);
    }

    private static Task PersistTradeAsync(ReaderTestDatabase database, Trade trade) =>
        database.ServiceProvider.GetRequiredService<ITradeStore>().AddAsync(trade);

    private static Trade CreateOpenTrade(
        Guid accountId,
        Guid instrumentId,
        Guid tradeId,
        DateTimeOffset openedAtUtc)
    {
        return CreateTrade(
            accountId,
            instrumentId,
            tradeId,
            openedAtUtc.AddHours(1),
            20m,
            "USD",
            new ExecutionFact(openedAtUtc, ExecutionSide.Buy, 1m, 100m, 0m, 0m));
    }

    private static Trade CreateTrade(
        Guid accountId,
        Guid instrumentId,
        Guid tradeId,
        DateTimeOffset createdAtUtc,
        decimal pointValue,
        string currency,
        params ExecutionFact[] executionFacts)
    {
        TradeExecution[] executions = executionFacts
            .Select((fact, index) => TradeExecution.Rehydrate(
                Guid.NewGuid(),
                tradeId,
                index + 1,
                fact.ExecutedAtUtc,
                fact.Side,
                fact.Quantity,
                fact.Price,
                fact.Commission,
                fact.Fees,
                externalExecutionId: null,
                externalOrderId: null,
                brokerSymbol: "BROKER-SYMBOL"))
            .ToArray();

        return Trade.Rehydrate(
            tradeId,
            accountId,
            instrumentId,
            new TradePricingSnapshot(pointValue, currency),
            strategyId: null,
            tradingSetupId: null,
            executions,
            createdAtUtc,
            createdAtUtc);
    }

    private static DateTimeOffset Utc(int day, int hour) =>
        new(2026, 9, day, hour, 0, 0, TimeSpan.Zero);

    private sealed record ExecutionFact(
        DateTimeOffset ExecutedAtUtc,
        ExecutionSide Side,
        decimal Quantity,
        decimal Price,
        decimal Commission,
        decimal Fees);
}
