using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

public sealed class TradeLifecyclePersistenceTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveAsyncPersistsCompleteCorrectionWithoutTouchingAssociations()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        SeededGraph graph = await SeedGraphAsync(database);
        ITradeMutationStore store = database.ServiceProvider
            .GetRequiredService<ITradeMutationStore>();
        Trade trade = Assert.IsType<Trade>(await store.GetByIdAsync(graph.TradeId));
        Guid entryId = trade.Executions[0].Id;
        Guid exitId = trade.Executions[1].Id;
        DateTimeOffset updatedAt = CreatedAt.AddHours(4);
        TradeExecution correctedEntry = TradeExecution.Rehydrate(
            entryId, trade.Id, 1, CreatedAt.AddMinutes(5), ExecutionSide.Sell,
            2m, 110m, 2m, 0.5m, "EXT-1", "ORDER-1", "ESU6");
        TradeExecution correctedExit = TradeExecution.Rehydrate(
            exitId, trade.Id, 2, CreatedAt.AddHours(2), ExecutionSide.Buy,
            2m, 100m, 3m, 0.75m, null, null, null);

        bool changed = trade.CorrectDetails(
            graph.OtherAccountId,
            graph.OtherInstrumentId,
            new TradePricingSnapshot(50m, "EUR"),
            null,
            [correctedEntry, correctedExit],
            updatedAt);
        Assert.True(changed);
        await store.SaveAsync(trade);

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        TradeRecord record = await context.Trades.AsNoTracking()
            .SingleAsync(item => item.Id == graph.TradeId);
        List<TradeExecutionRecord> executions = await context.TradeExecutions
            .AsNoTracking()
            .Where(item => item.TradeId == graph.TradeId)
            .OrderBy(item => item.Sequence)
            .ToListAsync();
        Assert.Equal(graph.OtherAccountId, record.TradingAccountId);
        Assert.Equal(graph.OtherInstrumentId, record.InstrumentId);
        Assert.Equal(50m, record.PricingPointValue);
        Assert.Equal("EUR", record.PricingCurrency);
        Assert.Null(record.TradingSetupId);
        Assert.Equal(CreatedAt, record.CreatedAtUtc);
        Assert.Equal(updatedAt, record.UpdatedAtUtc);
        Assert.Equal([entryId, exitId], executions.Select(item => item.Id));
        Assert.Equal([110m, 100m], executions.Select(item => item.Price));
        Assert.Equal([2m, 2m], executions.Select(item => item.Quantity));
        Assert.Equal([2m, 3m], executions.Select(item => item.Commission));
        Assert.Equal([0.5m, 0.75m], executions.Select(item => item.Fees));
        Assert.Equal("EXT-1", executions[0].ExternalExecutionId);
        Assert.Equal("ORDER-1", executions[0].ExternalOrderId);
        Assert.Equal("ESU6", executions[0].BrokerSymbol);
        Assert.Equal(1, await context.TradeMistakes.CountAsync(item => item.TradeId == graph.TradeId));
        Assert.Equal(1, await context.TradeScreenshots.CountAsync(item => item.TradeId == graph.TradeId));

        Trade reloaded = Assert.IsType<Trade>(await store.GetByIdAsync(graph.TradeId));
        Assert.Equal(TradeDirection.Short, reloaded.Direction);
        Assert.Equal(TradeStatus.Closed, reloaded.Status);
        Assert.Equal(1000m, reloaded.GrossPnL);
        Assert.Equal(993.75m, reloaded.NetPnL);
    }

    [Fact]
    public async Task SaveAsyncRemovesExecutionWhenCorrectionReopensTrade()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        SeededGraph graph = await SeedGraphAsync(database);
        ITradeMutationStore store = database.ServiceProvider
            .GetRequiredService<ITradeMutationStore>();
        Trade trade = Assert.IsType<Trade>(await store.GetByIdAsync(graph.TradeId));
        TradeExecution currentEntry = trade.Executions[0];
        TradeExecution correctedEntry = TradeExecution.Rehydrate(
            currentEntry.Id,
            trade.Id,
            1,
            CreatedAt.AddMinutes(15),
            ExecutionSide.Buy,
            3m,
            101m,
            2m,
            1m,
            currentEntry.ExternalExecutionId,
            currentEntry.ExternalOrderId,
            currentEntry.BrokerSymbol);

        Assert.True(trade.CorrectDetails(
            trade.TradingAccountId,
            trade.InstrumentId,
            trade.Pricing,
            trade.TradingSetupId,
            [correctedEntry],
            CreatedAt.AddHours(3)));
        await store.SaveAsync(trade);

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        TradeExecutionRecord persisted = await context.TradeExecutions
            .AsNoTracking()
            .SingleAsync(item => item.TradeId == graph.TradeId);
        Assert.Equal(currentEntry.Id, persisted.Id);
        Assert.Equal(1, persisted.Sequence);
        Assert.Equal(3m, persisted.Quantity);
        Assert.Equal(101m, persisted.Price);

        Trade reloaded = Assert.IsType<Trade>(await store.GetByIdAsync(graph.TradeId));
        Assert.Single(reloaded.Executions);
        Assert.Equal(TradeStatus.Open, reloaded.Status);
        Assert.Equal(3m, reloaded.OpenQuantity);
    }

    [Fact]
    public async Task SaveAsyncHonorsPreCancelledToken()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        SeededGraph graph = await SeedGraphAsync(database);
        ITradeMutationStore store = database.ServiceProvider
            .GetRequiredService<ITradeMutationStore>();
        Trade trade = Assert.IsType<Trade>(await store.GetByIdAsync(graph.TradeId));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.SaveAsync(trade, source.Token));
    }

    [Fact]
    public async Task DeleteAsyncRemovesAllTradeDependentsAndPreservesCatalogAndUnrelatedRows()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        SeededGraph graph = await SeedGraphAsync(database);
        ITradeDeletionStore store = database.ServiceProvider
            .GetRequiredService<ITradeDeletionStore>();

        TradeDeletionInfo? result = await store.DeleteAsync(graph.TradeId);

        Assert.NotNull(result);
        Assert.Equal(graph.TradeId, result.TradeId);
        Assert.Equal(["target.png"], result.ScreenshotStorageKeys);
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.Trades.AnyAsync(item => item.Id == graph.TradeId));
        Assert.False(await context.TradeExecutions.AnyAsync(item => item.TradeId == graph.TradeId));
        Assert.False(await context.TradeMistakes.AnyAsync(item => item.TradeId == graph.TradeId));
        Assert.False(await context.TradeScreenshots.AnyAsync(item => item.TradeId == graph.TradeId));
        Assert.True(await context.Trades.AnyAsync(item => item.Id == graph.OtherTradeId));
        Assert.True(await context.TradeExecutions.AnyAsync(item => item.TradeId == graph.OtherTradeId));
        Assert.True(await context.TradeMistakes.AnyAsync(item => item.TradeId == graph.OtherTradeId));
        Assert.True(await context.TradeScreenshots.AnyAsync(item => item.TradeId == graph.OtherTradeId));
        Assert.Equal(2, await context.TradingAccounts.CountAsync());
        Assert.Equal(2, await context.Instruments.CountAsync());
        Assert.Single(await context.TradingSetups.ToListAsync());
        Assert.Single(await context.TradingMistakes.ToListAsync());
    }

    [Fact]
    public async Task DeleteAsyncReturnsNullForMissingAndHonorsCancellation()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradeDeletionStore store = database.ServiceProvider
            .GetRequiredService<ITradeDeletionStore>();

        Assert.Null(await store.DeleteAsync(Guid.NewGuid()));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.DeleteAsync(Guid.NewGuid(), source.Token));
    }

    private static async Task<SeededGraph> SeedGraphAsync(ReaderTestDatabase database)
    {
        Guid accountId = Guid.NewGuid();
        Guid otherAccountId = Guid.NewGuid();
        Guid instrumentId = Guid.NewGuid();
        Guid otherInstrumentId = Guid.NewGuid();
        Guid setupId = Guid.NewGuid();
        Guid mistakeCatalogId = Guid.NewGuid();
        Guid tradeId = Guid.NewGuid();
        Guid otherTradeId = Guid.NewGuid();

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        context.TradingAccounts.AddRange(
            Account(accountId, "Target Account"),
            Account(otherAccountId, "Other Account"));
        context.Instruments.AddRange(
            Instrument(instrumentId, "ES", "USD"),
            Instrument(otherInstrumentId, "DAX", "EUR"));
        context.TradingSetups.Add(new TradingSetupRecord
        {
            Id = setupId,
            Name = "Breakout",
            IsActive = true,
            CreatedAtUtc = CreatedAt,
            UpdatedAtUtc = CreatedAt,
        });
        context.TradingMistakes.Add(new TradingMistakeRecord
        {
            Id = mistakeCatalogId,
            Name = "Late Entry",
            IsActive = true,
            CreatedAtUtc = CreatedAt,
            UpdatedAtUtc = CreatedAt,
        });
        context.Trades.AddRange(
            TradeRecord(tradeId, accountId, instrumentId, setupId),
            TradeRecord(otherTradeId, accountId, instrumentId, setupId));
        context.TradeExecutions.AddRange(
            Execution(tradeId, 1, ExecutionSide.Buy, 100m),
            Execution(tradeId, 2, ExecutionSide.Sell, 105m),
            Execution(otherTradeId, 1, ExecutionSide.Buy, 200m));
        context.TradeMistakes.AddRange(
            TradeMistake(tradeId, mistakeCatalogId),
            TradeMistake(otherTradeId, mistakeCatalogId));
        context.TradeScreenshots.AddRange(
            Screenshot(tradeId, "target.png"),
            Screenshot(otherTradeId, "other.png"));
        await context.SaveChangesAsync();

        return new SeededGraph(
            tradeId,
            otherTradeId,
            otherAccountId,
            otherInstrumentId);
    }

    private static TradingAccountRecord Account(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        AccountType = TradingAccountType.Personal,
        Currency = "USD",
        IsActive = true,
        CreatedAtUtc = CreatedAt,
        UpdatedAtUtc = CreatedAt,
    };

    private static InstrumentRecord Instrument(Guid id, string symbol, string currency) => new()
    {
        Id = id,
        Symbol = symbol,
        DisplayName = symbol,
        AssetClass = AssetClass.Futures,
        Currency = currency,
        TickSize = 0.25m,
        TickValue = 12.50m,
        IsActive = true,
        CreatedAtUtc = CreatedAt,
        UpdatedAtUtc = CreatedAt,
    };

    private static TradeRecord TradeRecord(Guid id, Guid accountId, Guid instrumentId, Guid setupId) => new()
    {
        Id = id,
        TradingAccountId = accountId,
        InstrumentId = instrumentId,
        PricingPointValue = 10m,
        PricingCurrency = "USD",
        TradingSetupId = setupId,
        CreatedAtUtc = CreatedAt,
        UpdatedAtUtc = CreatedAt,
    };

    private static TradeExecutionRecord Execution(
        Guid tradeId,
        int sequence,
        ExecutionSide side,
        decimal price) => new()
    {
        Id = Guid.NewGuid(),
        TradeId = tradeId,
        Sequence = sequence,
        ExecutedAtUtc = CreatedAt.AddHours(sequence - 1),
        Side = side,
        Quantity = 1m,
        Price = price,
        Commission = 1m,
        Fees = 0m,
        ExternalExecutionId = sequence == 1 ? "EXT-1" : null,
        ExternalOrderId = sequence == 1 ? "ORDER-1" : null,
        BrokerSymbol = sequence == 1 ? "ESU6" : null,
    };

    private static TradeMistakeRecord TradeMistake(Guid tradeId, Guid mistakeId) => new()
    {
        Id = Guid.NewGuid(),
        TradeId = tradeId,
        TradingMistakeId = mistakeId,
        CreatedAtUtc = CreatedAt,
        UpdatedAtUtc = CreatedAt,
    };

    private static TradeScreenshotRecord Screenshot(Guid tradeId, string key) => new()
    {
        Id = Guid.NewGuid(),
        TradeId = tradeId,
        Type = TradeScreenshotType.Entry,
        StorageKey = key,
        FileName = key,
        CreatedAtUtc = CreatedAt,
        UpdatedAtUtc = CreatedAt,
    };

    private sealed record SeededGraph(
        Guid TradeId,
        Guid OtherTradeId,
        Guid OtherAccountId,
        Guid OtherInstrumentId);
}
