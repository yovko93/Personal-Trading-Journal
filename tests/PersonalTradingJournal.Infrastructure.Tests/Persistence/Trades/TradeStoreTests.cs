using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

public sealed class TradeStoreTests
{
    private static readonly DateTimeOffset ReferenceCreatedAtUtc =
        new(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset EntryExecutedAtUtc =
        new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset TradeCreatedAtUtc =
        new(2026, 9, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAsyncPersistsOpenTradeAndExecutionExactly()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) =
            await PersistReferenceDataAsync(database);
        ITradeStore store = database.ServiceProvider.GetRequiredService<ITradeStore>();
        Trade trade = CreateOpenTrade(account.Id, instrument.Id);

        await store.AddAsync(trade);

        await using JournalDbContext readContext =
            await database.ContextFactory.CreateDbContextAsync();
        TradeRecord persistedTrade = await readContext.Trades
            .AsNoTracking()
            .SingleAsync(record => record.Id == trade.Id);
        TradeExecutionRecord persistedExecution = await readContext.TradeExecutions
            .AsNoTracking()
            .SingleAsync(record => record.TradeId == trade.Id);
        TradeExecution execution = Assert.Single(trade.Executions);

        Assert.Equal(trade.Id, persistedTrade.Id);
        Assert.Equal(account.Id, persistedTrade.TradingAccountId);
        Assert.Equal(instrument.Id, persistedTrade.InstrumentId);
        Assert.Equal(20m, persistedTrade.PricingPointValue);
        Assert.Equal("USD", persistedTrade.PricingCurrency);
        Assert.Null(persistedTrade.StrategyId);
        Assert.Null(persistedTrade.TradingSetupId);
        Assert.Equal(trade.CreatedAtUtc, persistedTrade.CreatedAtUtc);
        Assert.Equal(trade.UpdatedAtUtc, persistedTrade.UpdatedAtUtc);

        Assert.Equal(execution.Id, persistedExecution.Id);
        Assert.Equal(trade.Id, persistedExecution.TradeId);
        Assert.Equal(1, persistedExecution.Sequence);
        Assert.Equal(execution.ExecutedAtUtc, persistedExecution.ExecutedAtUtc);
        Assert.Equal(ExecutionSide.Buy, persistedExecution.Side);
        Assert.Equal(2.5m, persistedExecution.Quantity);
        Assert.Equal(21900.25m, persistedExecution.Price);
        Assert.Equal(1.50m, persistedExecution.Commission);
        Assert.Equal(0.25m, persistedExecution.Fees);
        Assert.Equal("EXEC-1", persistedExecution.ExternalExecutionId);
        Assert.Equal("ORDER-1", persistedExecution.ExternalOrderId);
        Assert.Equal("NQ", persistedExecution.BrokerSymbol);
    }

    [Fact]
    public async Task AddAsyncPersistsClosedTradeWithBothExecutions()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) =
            await PersistReferenceDataAsync(database);
        ITradeStore store = database.ServiceProvider.GetRequiredService<ITradeStore>();
        Trade trade = CreateClosedTrade(account.Id, instrument.Id);

        await store.AddAsync(trade);

        await using JournalDbContext readContext =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(
            1,
            await readContext.Trades.CountAsync(record => record.Id == trade.Id));
        List<TradeExecutionRecord> executions = await readContext.TradeExecutions
            .AsNoTracking()
            .Where(record => record.TradeId == trade.Id)
            .OrderBy(record => record.Sequence)
            .ToListAsync();

        Assert.Equal(2, executions.Count);
        Assert.Equal([1, 2], executions.Select(execution => execution.Sequence));
        Assert.All(executions, execution => Assert.Equal(trade.Id, execution.TradeId));
        Assert.Equal(ExecutionSide.Buy, executions[0].Side);
        Assert.Equal(ExecutionSide.Sell, executions[1].Side);
        Assert.Equal(2.5m, executions[0].Quantity);
        Assert.Equal(2.5m, executions[1].Quantity);
        Assert.Equal(21900.25m, executions[0].Price);
        Assert.Equal(21950.75m, executions[1].Price);
        Assert.Equal(1.50m, executions[0].Commission);
        Assert.Equal(1.75m, executions[1].Commission);
        Assert.Equal(0.25m, executions[0].Fees);
        Assert.Equal(0.35m, executions[1].Fees);
        Assert.Equal(EntryExecutedAtUtc, executions[0].ExecutedAtUtc);
        Assert.Equal(EntryExecutedAtUtc.AddHours(1), executions[1].ExecutedAtUtc);
    }

    [Fact]
    public async Task AddAsyncPersistsHistoricalPricingIndependentlyOfCurrentInstrument()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) =
            await PersistReferenceDataAsync(database);
        ITradeStore store = database.ServiceProvider.GetRequiredService<ITradeStore>();
        Trade trade = CreateOpenTrade(account.Id, instrument.Id);
        await store.AddAsync(trade);

        await using (JournalDbContext updateContext =
                     await database.ContextFactory.CreateDbContextAsync())
        {
            InstrumentRecord record = await updateContext.Instruments
                .SingleAsync(candidate => candidate.Id == instrument.Id);
            record.TickValue = 2.5m;
            await updateContext.SaveChangesAsync();
        }

        await using JournalDbContext readContext =
            await database.ContextFactory.CreateDbContextAsync();
        TradeRecord persistedTrade = await readContext.Trades
            .AsNoTracking()
            .SingleAsync(record => record.Id == trade.Id);
        InstrumentRecord currentInstrument = await readContext.Instruments
            .AsNoTracking()
            .SingleAsync(record => record.Id == instrument.Id);

        Assert.Equal(10m, currentInstrument.TickValue / currentInstrument.TickSize);
        Assert.Equal(20m, persistedTrade.PricingPointValue);
        Assert.Equal("USD", persistedTrade.PricingCurrency);
    }

    [Fact]
    public async Task AddAsyncRejectsNullTradeWithoutWriting()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradeStore store = database.ServiceProvider.GetRequiredService<ITradeStore>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.AddAsync(null!));

        await using JournalDbContext readContext =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await readContext.Trades.AsNoTracking().ToListAsync());
        Assert.Empty(await readContext.TradeExecutions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AddAsyncRollsBackTradeWhenExecutionInsertFails()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) =
            await PersistReferenceDataAsync(database);
        Guid collidingExecutionId = Guid.NewGuid();
        Trade existingTrade = CreateOpenTrade(
            account.Id,
            instrument.Id,
            Guid.NewGuid(),
            collidingExecutionId);

        await using (JournalDbContext seedContext =
                     await database.ContextFactory.CreateDbContextAsync())
        {
            seedContext.Trades.Add(TradePersistenceMapper.ToRecord(existingTrade));
            seedContext.TradeExecutions.Add(
                TradeExecutionPersistenceMapper.ToRecord(
                    Assert.Single(existingTrade.Executions)));
            await seedContext.SaveChangesAsync();
        }

        Guid newTradeId = Guid.NewGuid();
        Trade newTrade = CreateOpenTrade(
            account.Id,
            instrument.Id,
            newTradeId,
            collidingExecutionId);
        ITradeStore store = database.ServiceProvider.GetRequiredService<ITradeStore>();

        await Assert.ThrowsAsync<DbUpdateException>(() => store.AddAsync(newTrade));

        await using JournalDbContext readContext =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.False(await readContext.Trades
            .AsNoTracking()
            .AnyAsync(record => record.Id == newTradeId));
        Assert.True(await readContext.Trades
            .AsNoTracking()
            .AnyAsync(record => record.Id == existingTrade.Id));
        Assert.Equal(
            1,
            await readContext.TradeExecutions
                .AsNoTracking()
                .CountAsync(record => record.Id == collidingExecutionId));
    }

    [Fact]
    public async Task AddAsyncPropagatesPreCancelledTokenWithoutWriting()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        (TradingAccount account, Instrument instrument) =
            await PersistReferenceDataAsync(database);
        ITradeStore store = database.ServiceProvider.GetRequiredService<ITradeStore>();
        Trade trade = CreateOpenTrade(account.Id, instrument.Id);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.AddAsync(trade, cancellationSource.Token));

        await using JournalDbContext readContext =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.False(await readContext.Trades
            .AsNoTracking()
            .AnyAsync(record => record.Id == trade.Id));
    }

    private static async Task<(TradingAccount Account, Instrument Instrument)>
        PersistReferenceDataAsync(ReaderTestDatabase database)
    {
        var account = new TradingAccount(
            "Trade Store Account",
            TradingAccountType.Demo,
            null,
            null,
            "USD",
            50000m,
            ReferenceCreatedAtUtc);
        var instrument = new Instrument(
            "NQ",
            "Nasdaq-100 E-mini",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5m,
            ReferenceCreatedAtUtc);
        ITradingAccountStore accountStore =
            database.ServiceProvider.GetRequiredService<ITradingAccountStore>();
        IInstrumentStore instrumentStore =
            database.ServiceProvider.GetRequiredService<IInstrumentStore>();

        await accountStore.AddAsync(account);
        await instrumentStore.AddAsync(instrument);

        return (account, instrument);
    }

    private static Trade CreateClosedTrade(Guid accountId, Guid instrumentId)
    {
        Trade trade = CreateOpenTrade(accountId, instrumentId);
        TradeExecution closingExecution = TradeExecution.Rehydrate(
            Guid.NewGuid(),
            trade.Id,
            2,
            EntryExecutedAtUtc.AddHours(1),
            ExecutionSide.Sell,
            2.5m,
            21950.75m,
            1.75m,
            0.35m,
            "EXEC-2",
            "ORDER-2",
            "NQ");
        trade.AddExecution(closingExecution, TradeCreatedAtUtc.AddMinutes(1));
        return trade;
    }

    private static Trade CreateOpenTrade(
        Guid accountId,
        Guid instrumentId,
        Guid? tradeId = null,
        Guid? executionId = null)
    {
        Guid actualTradeId = tradeId ?? Guid.NewGuid();
        TradeExecution openingExecution = TradeExecution.Rehydrate(
            executionId ?? Guid.NewGuid(),
            actualTradeId,
            1,
            EntryExecutedAtUtc,
            ExecutionSide.Buy,
            2.5m,
            21900.25m,
            1.50m,
            0.25m,
            "EXEC-1",
            "ORDER-1",
            "NQ");

        return Trade.Start(
            accountId,
            instrumentId,
            new TradePricingSnapshot(20m, "USD"),
            openingExecution,
            TradeCreatedAtUtc);
    }
}
