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

public sealed class TradeMutationStoreTests
{
    private static readonly DateTimeOffset ReferenceAtUtc =
        new(2026, 9, 14, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EntryAtUtc =
        new(2026, 9, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ClosedAtUtc =
        new(2026, 9, 14, 11, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAtUtc =
        new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetByIdAsyncRejectsEmptyIdentifier()
    {
        await using ReaderTestDatabase database =
            await ReaderTestDatabase.CreateAsync();
        ITradeMutationStore store = GetStore(database);

        _ = await Assert.ThrowsAsync<ArgumentException>(
            () => store.GetByIdAsync(Guid.Empty));
    }

    [Fact]
    public async Task GetByIdAsyncReturnsNullForMissingTrade()
    {
        await using ReaderTestDatabase database =
            await ReaderTestDatabase.CreateAsync();

        Trade? trade = await GetStore(database).GetByIdAsync(Guid.NewGuid());

        Assert.Null(trade);
    }

    [Fact]
    public async Task GetByIdAsyncRehydratesExactOrderedAuthoritativeState()
    {
        await using ReaderTestDatabase database =
            await ReaderTestDatabase.CreateAsync();
        Trade expected = await PersistClassifiedPartiallyExitedTradeAsync(database);

        Trade? actual = await GetStore(database).GetByIdAsync(expected.Id);

        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.TradingAccountId, actual.TradingAccountId);
        Assert.Equal(expected.InstrumentId, actual.InstrumentId);
        Assert.Equal(expected.Pricing.PointValue, actual.Pricing.PointValue);
        Assert.Equal(expected.Pricing.Currency, actual.Pricing.Currency);
        Assert.Equal(expected.StrategyId, actual.StrategyId);
        Assert.Equal(expected.TradingSetupId, actual.TradingSetupId);
        Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        Assert.Equal(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
        Assert.Equal(TradeDirection.Long, actual.Direction);
        Assert.Equal(TradeStatus.Open, actual.Status);
        Assert.Equal(2m, actual.OpenQuantity);
        Assert.Equal([1, 2], actual.Executions.Select(execution => execution.Sequence));
        Assert.Equal(
            expected.Executions.Select(execution => execution.Id),
            actual.Executions.Select(execution => execution.Id));
        Assert.Equal("ENTRY-EXT", actual.Executions[0].ExternalExecutionId);
        Assert.Equal("PARTIAL-EXT", actual.Executions[1].ExternalExecutionId);
    }

    [Fact]
    public async Task SaveAsyncAddsOnlyClosingExecutionAndPersistsAtomicClose()
    {
        await using ReaderTestDatabase database =
            await ReaderTestDatabase.CreateAsync();
        Trade seeded = await PersistClassifiedPartiallyExitedTradeAsync(database);
        ITradeMutationStore store = GetStore(database);
        Trade trade = Assert.IsType<Trade>(await store.GetByIdAsync(seeded.Id));
        Guid closingExecutionId = Guid.NewGuid();
        TradeExecution closingExecution = TradeExecution.Rehydrate(
            closingExecutionId,
            trade.Id,
            sequence: 3,
            ClosedAtUtc,
            ExecutionSide.Sell,
            quantity: 2m,
            price: 110m,
            commission: 2m,
            fees: 0.75m,
            externalExecutionId: null,
            externalOrderId: null,
            brokerSymbol: null);
        trade.AddExecution(closingExecution, UpdatedAtUtc);

        await store.SaveAsync(trade);

        await using JournalDbContext freshContext =
            await database.ContextFactory.CreateDbContextAsync();
        TradeRecord persistedTrade = await freshContext.Trades
            .AsNoTracking()
            .SingleAsync(record => record.Id == trade.Id);
        List<TradeExecutionRecord> executions = await freshContext.TradeExecutions
            .AsNoTracking()
            .Where(record => record.TradeId == trade.Id)
            .OrderBy(record => record.Sequence)
            .ToListAsync();
        Assert.Equal(3, executions.Count);
        Assert.Equal(
            seeded.Executions.Select(execution => execution.Id),
            executions.Take(2).Select(record => record.Id));
        Assert.Equal([1, 2, 3], executions.Select(record => record.Sequence));
        Assert.Equal(100m, executions[0].Price);
        Assert.Equal(105m, executions[1].Price);
        TradeExecutionRecord persistedClose = executions[2];
        Assert.Equal(closingExecutionId, persistedClose.Id);
        Assert.Equal(trade.Id, persistedClose.TradeId);
        Assert.Equal(ClosedAtUtc, persistedClose.ExecutedAtUtc);
        Assert.Equal(ExecutionSide.Sell, persistedClose.Side);
        Assert.Equal(2m, persistedClose.Quantity);
        Assert.Equal(110m, persistedClose.Price);
        Assert.Equal(2m, persistedClose.Commission);
        Assert.Equal(0.75m, persistedClose.Fees);
        Assert.Null(persistedClose.ExternalExecutionId);
        Assert.Null(persistedClose.ExternalOrderId);
        Assert.Null(persistedClose.BrokerSymbol);
        Assert.Equal(seeded.TradingAccountId, persistedTrade.TradingAccountId);
        Assert.Equal(seeded.InstrumentId, persistedTrade.InstrumentId);
        Assert.Equal(20m, persistedTrade.PricingPointValue);
        Assert.Equal("USD", persistedTrade.PricingCurrency);
        Assert.Equal(seeded.StrategyId, persistedTrade.StrategyId);
        Assert.Equal(seeded.TradingSetupId, persistedTrade.TradingSetupId);
        Assert.Equal(seeded.CreatedAtUtc, persistedTrade.CreatedAtUtc);
        Assert.Equal(UpdatedAtUtc, persistedTrade.UpdatedAtUtc);

        Trade reloaded = Assert.IsType<Trade>(
            await GetStore(database).GetByIdAsync(trade.Id));
        Assert.Equal(TradeStatus.Closed, reloaded.Status);
        Assert.Equal(0m, reloaded.OpenQuantity);
        Assert.Equal(ClosedAtUtc, reloaded.ClosedAtUtc);
        Assert.Equal(110m, reloaded.Executions[^1].Price);
    }

    [Fact]
    public async Task SaveAsyncRejectsNullAndMissingTrade()
    {
        await using ReaderTestDatabase database =
            await ReaderTestDatabase.CreateAsync();
        ITradeMutationStore store = GetStore(database);

        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.SaveAsync(null!));
        Trade missingTrade = CreateOpenTrade(
            Guid.NewGuid(),
            Guid.NewGuid(),
            strategyId: null,
            tradingSetupId: null);
        KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => store.SaveAsync(missingTrade));

        Assert.Contains(missingTrade.Id.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsyncRejectsStaleMutationWhenPersistedTradeIsAlreadyClosed()
    {
        await using ReaderTestDatabase database =
            await ReaderTestDatabase.CreateAsync();
        Trade seeded = await PersistClassifiedPartiallyExitedTradeAsync(database);
        ITradeMutationStore store = GetStore(database);
        Trade staleTrade = Assert.IsType<Trade>(await store.GetByIdAsync(seeded.Id));
        Trade winningTrade = Assert.IsType<Trade>(await store.GetByIdAsync(seeded.Id));
        winningTrade.AddExecution(
            CreateClosingExecution(winningTrade, Guid.NewGuid(), 110m),
            UpdatedAtUtc);
        await store.SaveAsync(winningTrade);
        staleTrade.AddExecution(
            CreateClosingExecution(staleTrade, Guid.NewGuid(), 111m),
            UpdatedAtUtc.AddMinutes(1));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveAsync(staleTrade));

        Trade authoritative = Assert.IsType<Trade>(
            await store.GetByIdAsync(seeded.Id));
        Assert.Equal(3, authoritative.Executions.Count);
        Assert.Equal(110m, authoritative.Executions[^1].Price);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OperationsPropagatePreCancelledToken(bool save)
    {
        await using ReaderTestDatabase database =
            await ReaderTestDatabase.CreateAsync();
        Trade trade = await PersistClassifiedPartiallyExitedTradeAsync(database);
        ITradeMutationStore store = GetStore(database);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        if (save)
        {
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => store.SaveAsync(trade, cancellationSource.Token));
        }
        else
        {
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => store.GetByIdAsync(trade.Id, cancellationSource.Token));
        }
    }

    private static ITradeMutationStore GetStore(ReaderTestDatabase database) =>
        database.ServiceProvider.GetRequiredService<ITradeMutationStore>();

    private static async Task<Trade> PersistClassifiedPartiallyExitedTradeAsync(
        ReaderTestDatabase database)
    {
        var account = new TradingAccount(
            "Mutation Account",
            TradingAccountType.Demo,
            "Provider",
            "ACCOUNT-1",
            "USD",
            50000m,
            ReferenceAtUtc);
        var instrument = new Instrument(
            "NQ",
            "Nasdaq-100 E-mini",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5m,
            ReferenceAtUtc);
        await database.ServiceProvider
            .GetRequiredService<ITradingAccountStore>()
            .AddAsync(account);
        await database.ServiceProvider
            .GetRequiredService<IInstrumentStore>()
            .AddAsync(instrument);
        Guid strategyId = Guid.NewGuid();
        Guid tradingSetupId = Guid.NewGuid();
        await using (JournalDbContext context =
                     await database.ContextFactory.CreateDbContextAsync())
        {
            context.Strategies.Add(new StrategyRecord
            {
                Id = strategyId,
                Name = "Momentum",
                IsActive = true,
                CreatedAtUtc = ReferenceAtUtc,
                UpdatedAtUtc = ReferenceAtUtc,
            });
            context.TradingSetups.Add(new TradingSetupRecord
            {
                Id = tradingSetupId,
                Name = "Breakout",
                IsActive = true,
                CreatedAtUtc = ReferenceAtUtc,
                UpdatedAtUtc = ReferenceAtUtc,
            });
            await context.SaveChangesAsync();
        }

        Trade trade = CreateOpenTrade(
            account.Id,
            instrument.Id,
            strategyId,
            tradingSetupId);
        trade.AddExecution(
            TradeExecution.Rehydrate(
                Guid.NewGuid(),
                trade.Id,
                sequence: 2,
                EntryAtUtc.AddMinutes(30),
                ExecutionSide.Sell,
                quantity: 1m,
                price: 105m,
                commission: 0.5m,
                fees: 0.1m,
                externalExecutionId: "PARTIAL-EXT",
                externalOrderId: "PARTIAL-ORDER",
                brokerSymbol: "NQ"),
            CreatedAtUtc.AddMinutes(2));
        await database.ServiceProvider
            .GetRequiredService<ITradeStore>()
            .AddAsync(trade);

        return trade;
    }

    private static Trade CreateOpenTrade(
        Guid accountId,
        Guid instrumentId,
        Guid? strategyId,
        Guid? tradingSetupId)
    {
        Guid tradeId = Guid.NewGuid();
        TradeExecution openingExecution = TradeExecution.Rehydrate(
            Guid.NewGuid(),
            tradeId,
            sequence: 1,
            EntryAtUtc,
            ExecutionSide.Buy,
            quantity: 3m,
            price: 100m,
            commission: 1m,
            fees: 0.25m,
            externalExecutionId: "ENTRY-EXT",
            externalOrderId: "ENTRY-ORDER",
            brokerSymbol: "NQ");
        Trade trade = Trade.Start(
            accountId,
            instrumentId,
            new TradePricingSnapshot(20m, "USD"),
            openingExecution,
            CreatedAtUtc);
        trade.SetClassification(
            strategyId,
            tradingSetupId,
            CreatedAtUtc.AddMinutes(1));
        return trade;
    }

    private static TradeExecution CreateClosingExecution(
        Trade trade,
        Guid executionId,
        decimal price) =>
        TradeExecution.Rehydrate(
            executionId,
            trade.Id,
            trade.Executions[^1].Sequence + 1,
            ClosedAtUtc,
            ExecutionSide.Sell,
            trade.OpenQuantity,
            price,
            commission: 2m,
            fees: 0.75m,
            externalExecutionId: null,
            externalOrderId: null,
            brokerSymbol: null);
}
