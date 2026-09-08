using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Mistakes;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Strategies;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Integration;

public sealed class ProductionPersistenceIntegrationTests
{
    private const string InitialMigrationId = "20260908122839_InitialCreate";

    private static readonly Guid InstrumentId =
        Guid.Parse("14efc5de-748c-4f80-8df7-a18f55d89657");

    private static readonly Guid TradingAccountId =
        Guid.Parse("8937009a-74d9-42ca-9e82-c0d243d28f8c");

    private static readonly Guid StrategyId =
        Guid.Parse("d65b5ecf-3c30-494e-b7d2-38a4b3d81154");

    private static readonly Guid TradingSetupId =
        Guid.Parse("6b154e7d-192c-4cd9-a80d-8a074a630456");

    private static readonly Guid TradingMistakeId =
        Guid.Parse("fa0be3bd-cdbf-4366-a0f6-9ea3f44a5a6c");

    private static readonly Guid TradeId =
        Guid.Parse("e5e3b4ee-7b16-45b4-8eec-c6504b69539c");

    private static readonly Guid[] ExecutionIds =
    [
        Guid.Parse("f274708e-51f8-4b80-90c6-eb636be7b2f3"),
        Guid.Parse("1d52defb-6f19-4c41-980a-44ab5e60c7fb"),
        Guid.Parse("af9bc410-69d0-4bb5-9ee4-d1e620f82ccc"),
    ];

    private static readonly Guid ScreenshotId =
        Guid.Parse("68c719ce-e92d-4c25-a9dc-37574e8f87ec");

    private static readonly Guid TradeMistakeId =
        Guid.Parse("d941451b-1224-414a-9a89-4c1eaab89f53");

    private static readonly DateTimeOffset ReferenceCreatedAtUtc =
        new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero)
            .AddTicks(101);

    private static readonly DateTimeOffset FirstExecutionAtUtc =
        new DateTimeOffset(2026, 9, 20, 11, 0, 0, TimeSpan.Zero)
            .AddTicks(202);

    private static readonly DateTimeOffset TradeCreatedAtUtc =
        new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero)
            .AddTicks(303);

    private static readonly DateTimeOffset DeactivatedAtUtc =
        new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.Zero)
            .AddTicks(909);

    [Fact]
    public async Task CompleteGraphSurvivesProductionMigrationAndFreshContextRoundTrip()
    {
        await RunWithProductionPersistenceAsync(async contextFactory =>
        {
            CompleteGraph graph = CreateCompleteGraph();
            await PersistCompleteGraphAsync(contextFactory, graph);

            JournalDbContext firstContext = await contextFactory.CreateDbContextAsync();
            await firstContext.DisposeAsync();
            await using JournalDbContext readContext =
                await contextFactory.CreateDbContextAsync();

            Assert.NotSame(firstContext, readContext);
            Assert.Equal(
                [InitialMigrationId],
                await readContext.Database.GetAppliedMigrationsAsync());

            Instrument instrument = InstrumentPersistenceMapper.ToDomain(
                await readContext.Instruments.AsNoTracking().SingleAsync(
                    record => record.Id == InstrumentId));
            TradingAccount tradingAccount = TradingAccountPersistenceMapper.ToDomain(
                await readContext.TradingAccounts.AsNoTracking().SingleAsync(
                    record => record.Id == TradingAccountId));
            Strategy strategy = StrategyPersistenceMapper.ToDomain(
                await readContext.Strategies.AsNoTracking().SingleAsync(
                    record => record.Id == StrategyId));
            TradingSetup tradingSetup = TradingSetupPersistenceMapper.ToDomain(
                await readContext.TradingSetups.AsNoTracking().SingleAsync(
                    record => record.Id == TradingSetupId));
            TradingMistake tradingMistake = TradingMistakePersistenceMapper.ToDomain(
                await readContext.TradingMistakes.AsNoTracking().SingleAsync(
                    record => record.Id == TradingMistakeId));
            TradeRecord tradeRecord = await readContext.Trades
                .AsNoTracking()
                .SingleAsync(record => record.Id == TradeId);
            List<TradeExecutionRecord> executionRecords = await readContext.TradeExecutions
                .AsNoTracking()
                .Where(record => record.TradeId == TradeId)
                .OrderBy(record => record.Sequence)
                .ToListAsync();
            Trade trade = TradePersistenceMapper.ToDomain(tradeRecord, executionRecords);
            TradeScreenshot screenshot = TradeScreenshotPersistenceMapper.ToDomain(
                await readContext.TradeScreenshots.AsNoTracking().SingleAsync(
                    record => record.Id == ScreenshotId));
            TradeMistake tradeMistake = TradeMistakePersistenceMapper.ToDomain(
                await readContext.TradeMistakes.AsNoTracking().SingleAsync(
                    record => record.Id == TradeMistakeId));

            Assert.Equal(graph.Instrument.Id, instrument.Id);
            Assert.Equal("NQ", instrument.Symbol);
            Assert.Equal("Nasdaq-100 E-mini", instrument.DisplayName);
            Assert.Equal(AssetClass.Futures, instrument.AssetClass);
            Assert.Equal("CME", instrument.Exchange);
            Assert.Equal("USD", instrument.Currency);
            Assert.Equal(0.25m, instrument.TickSize);
            Assert.Equal(5m, instrument.TickValue);
            Assert.Equal(20m, instrument.PointValue);
            Assert.True(instrument.IsActive);

            Assert.Equal(graph.TradingAccount.Id, tradingAccount.Id);
            Assert.Equal("Production Integration Account", tradingAccount.Name);
            Assert.Equal(TradingAccountType.Demo, tradingAccount.AccountType);
            Assert.Equal("Integration Broker", tradingAccount.ProviderName);
            Assert.Equal("PTJ-TEST-001", tradingAccount.ExternalAccountId);
            Assert.Equal("USD", tradingAccount.Currency);
            Assert.Equal(50_000.1234m, tradingAccount.StartingBalance);
            Assert.True(tradingAccount.IsActive);

            Assert.Equal(graph.Strategy.Id, strategy.Id);
            Assert.Equal("Opening Range Breakout", strategy.Name);
            Assert.True(strategy.IsActive);
            Assert.Equal(graph.TradingSetup.Id, tradingSetup.Id);
            Assert.Equal("Liquidity Sweep", tradingSetup.Name);
            Assert.True(tradingSetup.IsActive);
            Assert.Equal(graph.TradingMistake.Id, tradingMistake.Id);
            Assert.Equal("FOMO", tradingMistake.Name);
            Assert.True(tradingMistake.IsActive);

            Assert.Equal(TradeId, trade.Id);
            Assert.Equal(TradingAccountId, trade.TradingAccountId);
            Assert.Equal(InstrumentId, trade.InstrumentId);
            Assert.Equal(StrategyId, trade.StrategyId);
            Assert.Equal(TradingSetupId, trade.TradingSetupId);
            Assert.Equal(20m, trade.Pricing.PointValue);
            Assert.Equal("USD", trade.Pricing.Currency);
            Assert.Equal(TradeCreatedAtUtc, trade.CreatedAtUtc);
            Assert.Equal(TradeCreatedAtUtc.AddMinutes(3), trade.UpdatedAtUtc);
            Assert.Equal(3, trade.Executions.Count);
            for (int index = 0; index < trade.Executions.Count; index++)
            {
                AssertExecution(graph.Trade.Executions[index], trade.Executions[index]);
            }

            Assert.Equal(TradeDirection.Long, trade.Direction);
            Assert.Equal(TradeStatus.Closed, trade.Status);
            Assert.Equal(0m, trade.OpenQuantity);
            Assert.Equal(FirstExecutionAtUtc, trade.OpenedAtUtc);
            Assert.Equal(FirstExecutionAtUtc.AddMinutes(2), trade.ClosedAtUtc);
            Assert.Equal(20_001m, trade.AverageEntryPrice);
            Assert.Equal(20_005.50m, trade.AverageExitPrice);
            Assert.Equal(2.79m, trade.TotalCosts);
            Assert.Equal(180m, trade.GrossPnL);
            Assert.Equal(177.21m, trade.NetPnL);

            AssertScreenshot(graph.Screenshot, screenshot);
            AssertTradeMistake(graph.TradeMistake, tradeMistake);
            Assert.True(trade.NetPnL > 0m);
        });
    }

    [Fact]
    public async Task HistoricalPricingAndInactiveReferencesRemainStable()
    {
        await RunWithProductionPersistenceAsync(async contextFactory =>
        {
            CompleteGraph graph = CreateCompleteGraph();
            await PersistCompleteGraphAsync(contextFactory, graph);

            await using (JournalDbContext updateContext =
                         await contextFactory.CreateDbContextAsync())
            {
                InstrumentRecord instrumentRecord = await updateContext.Instruments
                    .SingleAsync(record => record.Id == InstrumentId);
                instrumentRecord.TickValue = 2.5m;

                StrategyRecord strategyRecord = await updateContext.Strategies
                    .SingleAsync(record => record.Id == StrategyId);
                Strategy strategy = StrategyPersistenceMapper.ToDomain(strategyRecord);
                strategy.Deactivate(DeactivatedAtUtc);
                updateContext.Entry(strategyRecord).CurrentValues.SetValues(
                    StrategyPersistenceMapper.ToRecord(strategy));

                TradingSetupRecord setupRecord = await updateContext.TradingSetups
                    .SingleAsync(record => record.Id == TradingSetupId);
                TradingSetup setup = TradingSetupPersistenceMapper.ToDomain(setupRecord);
                setup.Deactivate(DeactivatedAtUtc.AddTicks(1));
                updateContext.Entry(setupRecord).CurrentValues.SetValues(
                    TradingSetupPersistenceMapper.ToRecord(setup));

                TradingMistakeRecord mistakeRecord = await updateContext.TradingMistakes
                    .SingleAsync(record => record.Id == TradingMistakeId);
                TradingMistake mistake = TradingMistakePersistenceMapper.ToDomain(mistakeRecord);
                mistake.Deactivate(DeactivatedAtUtc.AddTicks(2));
                updateContext.Entry(mistakeRecord).CurrentValues.SetValues(
                    TradingMistakePersistenceMapper.ToRecord(mistake));

                await updateContext.SaveChangesAsync();
            }

            await using var readContext = await contextFactory.CreateDbContextAsync();
            Instrument currentInstrument = InstrumentPersistenceMapper.ToDomain(
                await readContext.Instruments.AsNoTracking().SingleAsync(
                    record => record.Id == InstrumentId));
            Strategy currentStrategy = StrategyPersistenceMapper.ToDomain(
                await readContext.Strategies.AsNoTracking().SingleAsync(
                    record => record.Id == StrategyId));
            TradingSetup currentSetup = TradingSetupPersistenceMapper.ToDomain(
                await readContext.TradingSetups.AsNoTracking().SingleAsync(
                    record => record.Id == TradingSetupId));
            TradingMistake currentMistake = TradingMistakePersistenceMapper.ToDomain(
                await readContext.TradingMistakes.AsNoTracking().SingleAsync(
                    record => record.Id == TradingMistakeId));
            TradeRecord tradeRecord = await readContext.Trades.AsNoTracking().SingleAsync(
                record => record.Id == TradeId);
            List<TradeExecutionRecord> executionRecords = await readContext.TradeExecutions
                .AsNoTracking()
                .Where(record => record.TradeId == TradeId)
                .OrderBy(record => record.Sequence)
                .ToListAsync();
            Trade trade = TradePersistenceMapper.ToDomain(tradeRecord, executionRecords);
            TradeMistake tradeMistake = TradeMistakePersistenceMapper.ToDomain(
                await readContext.TradeMistakes.AsNoTracking().SingleAsync(
                    record => record.Id == TradeMistakeId));

            Assert.Equal(10m, currentInstrument.PointValue);
            Assert.False(currentStrategy.IsActive);
            Assert.Equal(DeactivatedAtUtc, currentStrategy.UpdatedAtUtc);
            Assert.False(currentSetup.IsActive);
            Assert.Equal(DeactivatedAtUtc.AddTicks(1), currentSetup.UpdatedAtUtc);
            Assert.False(currentMistake.IsActive);
            Assert.Equal(DeactivatedAtUtc.AddTicks(2), currentMistake.UpdatedAtUtc);

            Assert.Equal(StrategyId, trade.StrategyId);
            Assert.Equal(TradingSetupId, trade.TradingSetupId);
            Assert.Equal(20m, trade.Pricing.PointValue);
            Assert.Equal(180m, trade.GrossPnL);
            Assert.Equal(177.21m, trade.NetPnL);
            Assert.Equal(TradeId, tradeMistake.TradeId);
            Assert.Equal(TradingMistakeId, tradeMistake.TradingMistakeId);
            Assert.Equal("Entered before confirmation despite a profitable outcome.", tradeMistake.Note);
        });
    }

    [Fact]
    public async Task FailedMultiTableSaveRollsBackAndDatabaseRemainsUsable()
    {
        await RunWithProductionPersistenceAsync(async contextFactory =>
        {
            AtomicGraph graph = CreateAtomicGraph();

            using (JournalDbContext failedContext =
                   await contextFactory.CreateDbContextAsync())
            {
                failedContext.TradingAccounts.Add(
                    TradingAccountPersistenceMapper.ToRecord(graph.TradingAccount));
                failedContext.Instruments.Add(
                    InstrumentPersistenceMapper.ToRecord(graph.Instrument));
                failedContext.Trades.Add(TradePersistenceMapper.ToRecord(graph.Trade));
                failedContext.TradeExecutions.AddRange(
                    graph.Trade.Executions.Select(TradeExecutionPersistenceMapper.ToRecord));
                failedContext.TradeScreenshots.Add(
                    TradeScreenshotPersistenceMapper.ToRecord(graph.Screenshot));
                failedContext.TradeMistakes.Add(
                    TradeMistakePersistenceMapper.ToRecord(graph.InvalidTradeMistake));

                await Assert.ThrowsAsync<DbUpdateException>(
                    () => failedContext.SaveChangesAsync());
            }

            await using (JournalDbContext rollbackContext =
                         await contextFactory.CreateDbContextAsync())
            {
                Assert.False(await rollbackContext.TradingAccounts.AsNoTracking().AnyAsync(
                    record => record.Id == graph.TradingAccount.Id));
                Assert.False(await rollbackContext.Instruments.AsNoTracking().AnyAsync(
                    record => record.Id == graph.Instrument.Id));
                Assert.False(await rollbackContext.Trades.AsNoTracking().AnyAsync(
                    record => record.Id == graph.Trade.Id));
                Assert.False(await rollbackContext.TradeExecutions.AsNoTracking().AnyAsync(
                    record => record.TradeId == graph.Trade.Id));
                Assert.False(await rollbackContext.TradeScreenshots.AsNoTracking().AnyAsync(
                    record => record.Id == graph.Screenshot.Id));
                Assert.False(await rollbackContext.TradeMistakes.AsNoTracking().AnyAsync(
                    record => record.Id == graph.InvalidTradeMistake.Id));
            }

            await using (JournalDbContext recoveryWriteContext =
                         await contextFactory.CreateDbContextAsync())
            {
                recoveryWriteContext.TradingAccounts.Add(
                    TradingAccountPersistenceMapper.ToRecord(graph.TradingAccount));
                recoveryWriteContext.Instruments.Add(
                    InstrumentPersistenceMapper.ToRecord(graph.Instrument));
                recoveryWriteContext.Trades.Add(TradePersistenceMapper.ToRecord(graph.Trade));
                recoveryWriteContext.TradeExecutions.AddRange(
                    graph.Trade.Executions.Select(TradeExecutionPersistenceMapper.ToRecord));

                await recoveryWriteContext.SaveChangesAsync();
            }

            await using var recoveryReadContext =
                await contextFactory.CreateDbContextAsync();
            TradeRecord tradeRecord = await recoveryReadContext.Trades
                .AsNoTracking()
                .SingleAsync(record => record.Id == graph.Trade.Id);
            List<TradeExecutionRecord> executionRecords =
                await recoveryReadContext.TradeExecutions
                    .AsNoTracking()
                    .Where(record => record.TradeId == graph.Trade.Id)
                    .OrderBy(record => record.Sequence)
                    .ToListAsync();
            Trade rehydrated = TradePersistenceMapper.ToDomain(
                tradeRecord,
                executionRecords);

            Assert.Equal(graph.Trade.Id, rehydrated.Id);
            Assert.Equal(TradeStatus.Open, rehydrated.Status);
            Assert.Equal(1m, rehydrated.OpenQuantity);
            Assert.Single(rehydrated.Executions);
        });
    }

    private static CompleteGraph CreateCompleteGraph()
    {
        Instrument instrument = Instrument.Rehydrate(
            InstrumentId,
            "NQ",
            "Nasdaq-100 E-mini",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5m,
            isActive: true,
            ReferenceCreatedAtUtc,
            ReferenceCreatedAtUtc);
        TradingAccount tradingAccount = TradingAccount.Rehydrate(
            TradingAccountId,
            "Production Integration Account",
            TradingAccountType.Demo,
            "Integration Broker",
            "PTJ-TEST-001",
            "USD",
            50_000.1234m,
            isActive: true,
            ReferenceCreatedAtUtc.AddTicks(1),
            ReferenceCreatedAtUtc.AddTicks(1));
        Strategy strategy = Strategy.Rehydrate(
            StrategyId,
            "Opening Range Breakout",
            "Breakout from the initial session range.",
            isActive: true,
            ReferenceCreatedAtUtc.AddTicks(2),
            ReferenceCreatedAtUtc.AddTicks(2));
        TradingSetup tradingSetup = TradingSetup.Rehydrate(
            TradingSetupId,
            "Liquidity Sweep",
            "Sweep followed by confirmation.",
            isActive: true,
            ReferenceCreatedAtUtc.AddTicks(3),
            ReferenceCreatedAtUtc.AddTicks(3));
        TradingMistake tradingMistake = TradingMistake.Rehydrate(
            TradingMistakeId,
            "FOMO",
            "Entered before planned confirmation.",
            isActive: true,
            ReferenceCreatedAtUtc.AddTicks(4),
            ReferenceCreatedAtUtc.AddTicks(4));

        TradeExecution firstExecution = CreateExecution(
            ExecutionIds[0],
            TradeId,
            sequence: 1,
            FirstExecutionAtUtc,
            ExecutionSide.Buy,
            quantity: 1m,
            price: 20_000.25m,
            commission: 0.55m,
            fees: 0.12m);
        Trade trade = Trade.Start(
            TradingAccountId,
            InstrumentId,
            new TradePricingSnapshot(20m, "USD"),
            firstExecution,
            TradeCreatedAtUtc);
        trade.AddExecution(
            CreateExecution(
                ExecutionIds[1],
                TradeId,
                sequence: 2,
                FirstExecutionAtUtc.AddMinutes(1),
                ExecutionSide.Buy,
                quantity: 1m,
                price: 20_001.75m,
                commission: 0.65m,
                fees: 0.13m),
            TradeCreatedAtUtc.AddMinutes(1));
        trade.AddExecution(
            CreateExecution(
                ExecutionIds[2],
                TradeId,
                sequence: 3,
                FirstExecutionAtUtc.AddMinutes(2),
                ExecutionSide.Sell,
                quantity: 2m,
                price: 20_005.50m,
                commission: 1.10m,
                fees: 0.24m),
            TradeCreatedAtUtc.AddMinutes(2));
        trade.SetClassification(
            StrategyId,
            TradingSetupId,
            TradeCreatedAtUtc.AddMinutes(3));

        TradeScreenshot screenshot = TradeScreenshot.Rehydrate(
            ScreenshotId,
            TradeId,
            TradeScreenshotType.Entry,
            "trades/2026/example/chart-a",
            "chart-a.png",
            FirstExecutionAtUtc.AddSeconds(30).AddTicks(4),
            "1m",
            "Entry and liquidity sweep.",
            TradeCreatedAtUtc.AddMinutes(4).AddTicks(5),
            TradeCreatedAtUtc.AddMinutes(5).AddTicks(6));
        TradeMistake tradeMistake = TradeMistake.Rehydrate(
            TradeMistakeId,
            TradeId,
            TradingMistakeId,
            "Entered before confirmation despite a profitable outcome.",
            TradeCreatedAtUtc.AddMinutes(6).AddTicks(7),
            TradeCreatedAtUtc.AddMinutes(7).AddTicks(8));

        return new CompleteGraph(
            instrument,
            tradingAccount,
            strategy,
            tradingSetup,
            tradingMistake,
            trade,
            screenshot,
            tradeMistake);
    }

    private static AtomicGraph CreateAtomicGraph()
    {
        Guid accountId = Guid.Parse("407154ed-d2a9-43eb-b591-3bc6eb4069e2");
        Guid instrumentId = Guid.Parse("c2130af8-9fdc-46cf-8ae2-ff113645a455");
        Guid tradeId = Guid.Parse("ec30762e-34bd-4dc8-870e-e9c897b37979");
        DateTimeOffset createdAtUtc =
            new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero)
                .AddTicks(111);

        TradingAccount account = TradingAccount.Rehydrate(
            accountId,
            "Atomicity Account",
            TradingAccountType.Demo,
            null,
            null,
            "USD",
            null,
            isActive: true,
            createdAtUtc,
            createdAtUtc);
        Instrument instrument = Instrument.Rehydrate(
            instrumentId,
            "MES",
            "Micro E-mini S&P 500",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            1.25m,
            isActive: true,
            createdAtUtc.AddTicks(1),
            createdAtUtc.AddTicks(1));
        TradeExecution openingExecution = CreateExecution(
            Guid.Parse("e53ce265-fe93-49c3-9c65-eb168473cfea"),
            tradeId,
            sequence: 1,
            createdAtUtc.AddMinutes(1),
            ExecutionSide.Buy,
            quantity: 1m,
            price: 6_000.25m,
            commission: 0.35m,
            fees: 0.10m,
            brokerSymbol: "MESZ6");
        Trade trade = Trade.Start(
            accountId,
            instrumentId,
            new TradePricingSnapshot(5m, "USD"),
            openingExecution,
            createdAtUtc.AddMinutes(2));
        TradeScreenshot screenshot = TradeScreenshot.Rehydrate(
            Guid.Parse("7420d86d-9917-436f-92ea-e2a7cd72c8fa"),
            tradeId,
            TradeScreenshotType.Entry,
            "trades/2026/atomic/chart-a",
            "atomic-chart.png",
            createdAtUtc.AddMinutes(1).AddTicks(2),
            "5m",
            null,
            createdAtUtc.AddMinutes(3),
            createdAtUtc.AddMinutes(3));
        TradeMistake invalidTradeMistake = TradeMistake.Rehydrate(
            Guid.Parse("76a6d62c-4344-4dfa-aadd-1c7920f421a3"),
            tradeId,
            Guid.Parse("c6d81f63-0735-4440-97e3-61a825f4fec0"),
            "References a deliberately missing mistake definition.",
            createdAtUtc.AddMinutes(4),
            createdAtUtc.AddMinutes(4));

        return new AtomicGraph(
            account,
            instrument,
            trade,
            screenshot,
            invalidTradeMistake);
    }

    private static TradeExecution CreateExecution(
        Guid id,
        Guid tradeId,
        int sequence,
        DateTimeOffset executedAtUtc,
        ExecutionSide side,
        decimal quantity,
        decimal price,
        decimal commission,
        decimal fees,
        string brokerSymbol = "NQZ6")
    {
        return TradeExecution.Rehydrate(
            id,
            tradeId,
            sequence,
            executedAtUtc,
            side,
            quantity,
            price,
            commission,
            fees,
            $"EXEC-{sequence}",
            $"ORDER-{sequence}",
            brokerSymbol);
    }

    private static async Task PersistCompleteGraphAsync(
        IDbContextFactory<JournalDbContext> contextFactory,
        CompleteGraph graph)
    {
        await using JournalDbContext context =
            await contextFactory.CreateDbContextAsync();

        context.Instruments.Add(InstrumentPersistenceMapper.ToRecord(graph.Instrument));
        context.TradingAccounts.Add(
            TradingAccountPersistenceMapper.ToRecord(graph.TradingAccount));
        context.Strategies.Add(StrategyPersistenceMapper.ToRecord(graph.Strategy));
        context.TradingSetups.Add(
            TradingSetupPersistenceMapper.ToRecord(graph.TradingSetup));
        context.TradingMistakes.Add(
            TradingMistakePersistenceMapper.ToRecord(graph.TradingMistake));
        context.Trades.Add(TradePersistenceMapper.ToRecord(graph.Trade));
        context.TradeExecutions.AddRange(
            graph.Trade.Executions.Select(TradeExecutionPersistenceMapper.ToRecord));
        context.TradeScreenshots.Add(
            TradeScreenshotPersistenceMapper.ToRecord(graph.Screenshot));
        context.TradeMistakes.Add(
            TradeMistakePersistenceMapper.ToRecord(graph.TradeMistake));

        await context.SaveChangesAsync();
    }

    private static async Task RunWithProductionPersistenceAsync(
        Func<IDbContextFactory<JournalDbContext>, Task> test)
    {
        string testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(ProductionPersistenceIntegrationTests)}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var applicationPaths = new TestApplicationPaths(testDirectory);

        try
        {
            Assert.False(File.Exists(applicationPaths.DatabasePath));

            var services = new ServiceCollection();
            services.AddPersistence(applicationPaths);

            await using (ServiceProvider serviceProvider = services.BuildServiceProvider())
            {
                JournalDatabaseInitializer initializer =
                    serviceProvider.GetRequiredService<JournalDatabaseInitializer>();
                IDbContextFactory<JournalDbContext> contextFactory =
                    serviceProvider.GetRequiredService<IDbContextFactory<JournalDbContext>>();

                await initializer.InitializeAsync();
                Assert.True(File.Exists(applicationPaths.DatabasePath));

                await test(contextFactory);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testDirectory, recursive: true);
            Assert.False(Directory.Exists(testDirectory));
        }
    }

    private static void AssertExecution(
        TradeExecution expected,
        TradeExecution actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.TradeId, actual.TradeId);
        Assert.Equal(expected.Sequence, actual.Sequence);
        Assert.Equal(expected.ExecutedAtUtc, actual.ExecutedAtUtc);
        Assert.Equal(expected.Side, actual.Side);
        Assert.Equal(expected.Quantity, actual.Quantity);
        Assert.Equal(expected.Price, actual.Price);
        Assert.Equal(expected.Commission, actual.Commission);
        Assert.Equal(expected.Fees, actual.Fees);
        Assert.Equal(expected.ExternalExecutionId, actual.ExternalExecutionId);
        Assert.Equal(expected.ExternalOrderId, actual.ExternalOrderId);
        Assert.Equal(expected.BrokerSymbol, actual.BrokerSymbol);
    }

    private static void AssertScreenshot(
        TradeScreenshot expected,
        TradeScreenshot actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.TradeId, actual.TradeId);
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.StorageKey, actual.StorageKey);
        Assert.Equal(expected.FileName, actual.FileName);
        Assert.Equal(expected.CapturedAtUtc, actual.CapturedAtUtc);
        Assert.Equal(expected.Timeframe, actual.Timeframe);
        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        Assert.Equal(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
    }

    private static void AssertTradeMistake(
        TradeMistake expected,
        TradeMistake actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.TradeId, actual.TradeId);
        Assert.Equal(expected.TradingMistakeId, actual.TradingMistakeId);
        Assert.Equal(expected.Note, actual.Note);
        Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        Assert.Equal(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
    }

    private sealed record CompleteGraph(
        Instrument Instrument,
        TradingAccount TradingAccount,
        Strategy Strategy,
        TradingSetup TradingSetup,
        TradingMistake TradingMistake,
        Trade Trade,
        TradeScreenshot Screenshot,
        TradeMistake TradeMistake);

    private sealed record AtomicGraph(
        TradingAccount TradingAccount,
        Instrument Instrument,
        Trade Trade,
        TradeScreenshot Screenshot,
        TradeMistake InvalidTradeMistake);

    private sealed class TestApplicationPaths : IApplicationPaths
    {
        public TestApplicationPaths(string dataDirectory)
        {
            DataDirectory = dataDirectory;
            DatabasePath = Path.Combine(dataDirectory, "production-integration.db");
            ScreenshotsDirectory = Path.Combine(dataDirectory, "screenshots");
            LogsDirectory = Path.Combine(dataDirectory, "logs");
            BackupsDirectory = Path.Combine(dataDirectory, "backups");
        }

        public string DataDirectory { get; }

        public string DatabasePath { get; }

        public string ScreenshotsDirectory { get; }

        public string LogsDirectory { get; }

        public string BackupsDirectory { get; }
    }
}
