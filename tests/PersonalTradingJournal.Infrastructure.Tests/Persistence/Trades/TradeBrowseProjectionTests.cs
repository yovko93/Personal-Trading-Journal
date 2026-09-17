using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Persistence.Sorting;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

public sealed class TradeBrowseProjectionTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 9, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAsyncPersistsDomainDerivedProjectionForRepresentativeLifecycles()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ReferenceIds references = await SeedReferencesAsync(database);
        Trade[] trades =
        [
            CreateTrade(references, 1m, Execution(ExecutionSide.Buy, 1m, 100m)),
            CreateTrade(references, 1m, Execution(ExecutionSide.Sell, 2m, 101m)),
            CreateTrade(references, 20m,
                Execution(ExecutionSide.Buy, 1m, 100m),
                Execution(ExecutionSide.Sell, 1m, 110m)),
            CreateTrade(references, 20m,
                Execution(ExecutionSide.Buy, 1m, 110m),
                Execution(ExecutionSide.Sell, 1m, 100m)),
            CreateTrade(references, 20m,
                Execution(ExecutionSide.Sell, 1m, 110m),
                Execution(ExecutionSide.Buy, 1m, 100m)),
            CreateTrade(references, 20m,
                Execution(ExecutionSide.Sell, 1m, 100m),
                Execution(ExecutionSide.Buy, 1m, 110m)),
            CreateTrade(references, 1m,
                Execution(ExecutionSide.Buy, 1m, 100m),
                Execution(ExecutionSide.Sell, 1m, 100m)),
            CreateTrade(references, 1.1m,
                Execution(ExecutionSide.Buy, 0.3m, 29131.25m, 0.1m, 0.01m),
                Execution(ExecutionSide.Sell, 0.3m, 29200.50m, 0.2m, 0.02m)),
            CreateTrade(references, 0.1m,
                Execution(ExecutionSide.Buy, 0.2m, 1.01m, 0.1m, 0m),
                Execution(ExecutionSide.Buy, 0.1m, 1.1m, 0.2m, 0m),
                Execution(ExecutionSide.Sell, 0.1m, 1.2m, 0.3m, 0m)),
        ];
        ITradeStore store = database.ServiceProvider.GetRequiredService<ITradeStore>();
        foreach (Trade trade in trades)
        {
            await store.AddAsync(trade);
        }

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Dictionary<Guid, TradeBrowseRecord> projections = await context.TradeBrowse
            .AsNoTracking()
            .ToDictionaryAsync(record => record.TradeId);

        Assert.Equal(trades.Length, projections.Count);
        foreach (Trade trade in trades)
        {
            AssertMatches(trade, projections[trade.Id]);
        }
    }

    [Fact]
    public async Task SaveAsyncRegeneratesProjectionForCloseAndCorrectionOnlyForTargetTrade()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ReferenceIds references = await SeedReferencesAsync(database);
        Trade target = CreateTrade(
            references,
            20m,
            Execution(ExecutionSide.Buy, 2m, 100m, 0.1m, 0.01m));
        Trade unrelated = CreateTrade(
            references,
            1m,
            Execution(ExecutionSide.Buy, 1m, 50m));
        ITradeStore addStore = database.ServiceProvider.GetRequiredService<ITradeStore>();
        await addStore.AddAsync(target);
        await addStore.AddAsync(unrelated);
        ProjectionSnapshot unrelatedBefore = await SnapshotAsync(database, unrelated.Id);
        ITradeMutationStore mutationStore = database.ServiceProvider
            .GetRequiredService<ITradeMutationStore>();

        Trade loaded = Assert.IsType<Trade>(await mutationStore.GetByIdAsync(target.Id));
        loaded.AddExecution(
            TradeExecution.Rehydrate(
                Guid.NewGuid(), loaded.Id, 2, Timestamp.AddMinutes(10),
                ExecutionSide.Sell, 2m, 110m, 0.2m, 0.02m,
                null, null, null),
            Timestamp.AddHours(1));
        await mutationStore.SaveAsync(loaded);

        await using (JournalDbContext context =
                     await database.ContextFactory.CreateDbContextAsync())
        {
            TradeBrowseRecord projection = await context.TradeBrowse
                .AsNoTracking()
                .SingleAsync(record => record.TradeId == target.Id);
            AssertMatches(loaded, projection);
            Assert.Equal(TradeStatus.Closed, projection.Status);
            Assert.Equal(0m, projection.OpenQuantity);
            Assert.NotNull(projection.NetPnL);
        }

        Trade corrected = Assert.IsType<Trade>(
            await mutationStore.GetByIdAsync(target.Id));
        TradeExecution entry = corrected.Executions[0];
        TradeExecution exit = corrected.Executions[1];
        Assert.True(corrected.CorrectDetails(
            corrected.TradingAccountId,
            corrected.InstrumentId,
            new TradePricingSnapshot(1.1m, "USD"),
            corrected.TradingSetupId,
            [
                TradeExecution.Rehydrate(
                    entry.Id, corrected.Id, 1, entry.ExecutedAtUtc,
                    entry.Side, 0.3m, 29131.25m, 0.1m, 0.01m,
                    null, null, null),
                TradeExecution.Rehydrate(
                    exit.Id, corrected.Id, 2, exit.ExecutedAtUtc,
                    exit.Side, 0.3m, 29200.50m, 0.2m, 0.02m,
                    null, null, null),
            ],
            Timestamp.AddHours(2)));
        await mutationStore.SaveAsync(corrected);

        await using (JournalDbContext context =
                     await database.ContextFactory.CreateDbContextAsync())
        {
            TradeBrowseRecord projection = await context.TradeBrowse
                .AsNoTracking()
                .SingleAsync(record => record.TradeId == target.Id);
            AssertMatches(corrected, projection);
        }

        Assert.Equal(unrelatedBefore, await SnapshotAsync(database, unrelated.Id));
    }

    [Fact]
    public async Task SetupOnlySaveLeavesEconomicProjectionValuesUnchanged()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ReferenceIds references = await SeedReferencesAsync(database);
        Trade trade = CreateTrade(
            references,
            1m,
            Execution(ExecutionSide.Buy, 1m, 100m));
        await database.ServiceProvider.GetRequiredService<ITradeStore>()
            .AddAsync(trade);
        ProjectionSnapshot before = await SnapshotAsync(database, trade.Id);
        Guid setupId = Guid.NewGuid();
        await using (JournalDbContext context =
                     await database.ContextFactory.CreateDbContextAsync())
        {
            context.TradingSetups.Add(new TradingSetupRecord
            {
                Id = setupId,
                Name = "Breakout",
                IsActive = true,
                CreatedAtUtc = Timestamp,
                UpdatedAtUtc = Timestamp,
            });
            await context.SaveChangesAsync();
        }

        ITradeMutationStore mutationStore = database.ServiceProvider
            .GetRequiredService<ITradeMutationStore>();
        Trade loaded = Assert.IsType<Trade>(await mutationStore.GetByIdAsync(trade.Id));
        loaded.SetTradingSetup(setupId, Timestamp.AddHours(1));
        await mutationStore.SaveAsync(loaded);

        Assert.Equal(before, await SnapshotAsync(database, trade.Id));
    }

    [Fact]
    public async Task DeleteAsyncRemovesProjectionAndLeavesUnrelatedProjection()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ReferenceIds references = await SeedReferencesAsync(database);
        Trade target = CreateTrade(references, 1m, Execution(ExecutionSide.Buy, 1m, 100m));
        Trade unrelated = CreateTrade(references, 1m, Execution(ExecutionSide.Buy, 1m, 200m));
        ITradeStore addStore = database.ServiceProvider.GetRequiredService<ITradeStore>();
        await addStore.AddAsync(target);
        await addStore.AddAsync(unrelated);

        _ = await database.ServiceProvider.GetRequiredService<ITradeDeletionStore>()
            .DeleteAsync(target.Id);

        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.TradeBrowse.AnyAsync(
            record => record.TradeId == target.Id));
        Assert.True(await context.TradeBrowse.AnyAsync(
            record => record.TradeId == unrelated.Id));
    }

    [Fact]
    public async Task ReconciliationRepairsMissingAndVersionStaleProjectionAndIsIdempotent()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ReferenceIds references = await SeedReferencesAsync(database);
        Trade trade = CreateTrade(
            references,
            1.1m,
            Execution(ExecutionSide.Buy, 0.3m, 29131.25m, 0.1m, 0.01m),
            Execution(ExecutionSide.Sell, 0.3m, 29200.50m, 0.2m, 0.02m));
        await SeedCanonicalTradeAsync(database, trade);
        TradeBrowseProjectionReconciler reconciler = database.ServiceProvider
            .GetRequiredService<TradeBrowseProjectionReconciler>();

        await reconciler.ReconcileAsync();
        ProjectionSnapshot expected = await SnapshotAsync(database, trade.Id);
        await using (JournalDbContext context =
                     await database.ContextFactory.CreateDbContextAsync())
        {
            TradeBrowseRecord projection = await context.TradeBrowse
                .SingleAsync(record => record.TradeId == trade.Id);
            projection.ProjectionVersion = 0;
            projection.OpenQuantity = 999m;
            projection.OpenQuantitySortKey = DecimalSortKey.Encode(999m);
            projection.NetPnL = null;
            projection.NetPnLSortKey = null;
            await context.SaveChangesAsync();
        }

        await reconciler.ReconcileAsync();
        Assert.Equal(expected, await SnapshotAsync(database, trade.Id));
        await reconciler.ReconcileAsync();
        Assert.Equal(expected, await SnapshotAsync(database, trade.Id));
        await using JournalDbContext finalContext =
            await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await finalContext.TradeBrowse.CountAsync(
            record => record.TradeId == trade.Id));
    }

    [Fact]
    public async Task ReconciliationHonorsPreCancelledToken()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            database.ServiceProvider
                .GetRequiredService<TradeBrowseProjectionReconciler>()
                .ReconcileAsync(source.Token));
    }

    private static void AssertMatches(Trade trade, TradeBrowseRecord projection)
    {
        Assert.Equal(trade.Id, projection.TradeId);
        Assert.Equal(1, projection.ProjectionVersion);
        Assert.Equal(trade.OpenedAtUtc, projection.OpenedAtUtc);
        Assert.Equal(trade.ClosedAtUtc, projection.ClosedAtUtc);
        Assert.Equal(trade.Direction, projection.Direction);
        Assert.Equal(trade.Status, projection.Status);
        Assert.Equal(trade.OpenQuantity, projection.OpenQuantity);
        Assert.Equal(
            DecimalSortKey.Encode(trade.OpenQuantity),
            projection.OpenQuantitySortKey);
        Assert.Equal(trade.AverageEntryPrice, projection.AverageEntryPrice);
        Assert.Equal(
            DecimalSortKey.Encode(trade.AverageEntryPrice),
            projection.AverageEntryPriceSortKey);
        Assert.Equal(trade.AverageExitPrice, projection.AverageExitPrice);
        Assert.Equal(trade.TotalCosts, projection.TotalCosts);
        Assert.Equal(trade.GrossPnL, projection.GrossPnL);
        Assert.Equal(trade.NetPnL, projection.NetPnL);
        Assert.Equal(
            trade.NetPnL.HasValue
                ? DecimalSortKey.Encode(trade.NetPnL.Value)
                : null,
            projection.NetPnLSortKey);
    }

    private static async Task<ReferenceIds> SeedReferencesAsync(
        ReaderTestDatabase database)
    {
        var references = new ReferenceIds(Guid.NewGuid(), Guid.NewGuid());
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        context.TradingAccounts.Add(new TradingAccountRecord
        {
            Id = references.AccountId,
            Name = "Projection Account",
            AccountType = TradingAccountType.Personal,
            Currency = "USD",
            IsActive = true,
            CreatedAtUtc = Timestamp,
            UpdatedAtUtc = Timestamp,
        });
        context.Instruments.Add(new InstrumentRecord
        {
            Id = references.InstrumentId,
            Symbol = "NQ",
            DisplayName = "Nasdaq",
            AssetClass = AssetClass.Futures,
            Currency = "USD",
            TickSize = 0.25m,
            TickValue = 5m,
            IsActive = true,
            CreatedAtUtc = Timestamp,
            UpdatedAtUtc = Timestamp,
        });
        await context.SaveChangesAsync();
        return references;
    }

    private static Trade CreateTrade(
        ReferenceIds references,
        decimal pointValue,
        params ExecutionFact[] facts)
    {
        Guid tradeId = Guid.NewGuid();
        TradeExecution[] executions = facts.Select((fact, index) =>
            TradeExecution.Rehydrate(
                Guid.NewGuid(), tradeId, index + 1,
                Timestamp.AddMinutes(index * 10), fact.Side, fact.Quantity,
                fact.Price, fact.Commission, fact.Fees,
                null, null, null)).ToArray();
        return Trade.Rehydrate(
            tradeId,
            references.AccountId,
            references.InstrumentId,
            new TradePricingSnapshot(pointValue, "USD"),
            null,
            executions,
            Timestamp.AddHours(1),
            Timestamp.AddHours(1));
    }

    private static ExecutionFact Execution(
        ExecutionSide side,
        decimal quantity,
        decimal price,
        decimal commission = 0m,
        decimal fees = 0m) =>
        new(side, quantity, price, commission, fees);

    private static async Task SeedCanonicalTradeAsync(
        ReaderTestDatabase database,
        Trade trade)
    {
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        context.Trades.Add(new TradeRecord
        {
            Id = trade.Id,
            TradingAccountId = trade.TradingAccountId,
            InstrumentId = trade.InstrumentId,
            PricingPointValue = trade.Pricing.PointValue,
            PricingCurrency = trade.Pricing.Currency,
            CreatedAtUtc = trade.CreatedAtUtc,
            UpdatedAtUtc = trade.UpdatedAtUtc,
        });
        context.TradeExecutions.AddRange(trade.Executions.Select(execution =>
            new TradeExecutionRecord
            {
                Id = execution.Id,
                TradeId = execution.TradeId,
                Sequence = execution.Sequence,
                ExecutedAtUtc = execution.ExecutedAtUtc,
                Side = execution.Side,
                Quantity = execution.Quantity,
                Price = execution.Price,
                Commission = execution.Commission,
                Fees = execution.Fees,
                ExternalExecutionId = execution.ExternalExecutionId,
                ExternalOrderId = execution.ExternalOrderId,
                BrokerSymbol = execution.BrokerSymbol,
            }));
        await context.SaveChangesAsync();
    }

    private static async Task<ProjectionSnapshot> SnapshotAsync(
        ReaderTestDatabase database,
        Guid tradeId)
    {
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        return await context.TradeBrowse
            .AsNoTracking()
            .Where(record => record.TradeId == tradeId)
            .Select(record => new ProjectionSnapshot(
                record.ProjectionVersion,
                record.OpenedAtUtc,
                record.ClosedAtUtc,
                record.Direction,
                record.Status,
                record.OpenQuantity,
                record.OpenQuantitySortKey,
                record.AverageEntryPrice,
                record.AverageEntryPriceSortKey,
                record.AverageExitPrice,
                record.TotalCosts,
                record.GrossPnL,
                record.NetPnL,
                record.NetPnLSortKey))
            .SingleAsync();
    }

    private sealed record ReferenceIds(Guid AccountId, Guid InstrumentId);

    private sealed record ExecutionFact(
        ExecutionSide Side,
        decimal Quantity,
        decimal Price,
        decimal Commission,
        decimal Fees);

    private sealed record ProjectionSnapshot(
        int ProjectionVersion,
        DateTimeOffset OpenedAtUtc,
        DateTimeOffset? ClosedAtUtc,
        TradeDirection Direction,
        TradeStatus Status,
        decimal OpenQuantity,
        string OpenQuantitySortKey,
        decimal AverageEntryPrice,
        string AverageEntryPriceSortKey,
        decimal? AverageExitPrice,
        decimal TotalCosts,
        decimal? GrossPnL,
        decimal? NetPnL,
        string? NetPnLSortKey);
}
