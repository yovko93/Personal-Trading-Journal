using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Persistence.Sorting;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Migrations;

public sealed class TradeBrowseProjectionMigrationTests
{
    private const string MigrationTwo = "20260914212911_RemoveStrategies";
    private static readonly DateTimeOffset Timestamp =
        new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MigrationThreeAndReconciliationBackfillExistingTradeExactly()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(TradeBrowseProjectionMigrationTests)}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string databasePath = Path.Combine(directory, "migration.db");
        string connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
        }.ToString();
        ServiceProvider? provider = null;

        try
        {
            var services = new ServiceCollection();
            services.AddDbContextFactory<JournalDbContext>(options =>
                options.UseSqlite(connectionString));
            provider = services.BuildServiceProvider();
            IDbContextFactory<JournalDbContext> factory = provider
                .GetRequiredService<IDbContextFactory<JournalDbContext>>();
            Guid tradeId = Guid.NewGuid();
            Guid accountId = Guid.NewGuid();
            Guid instrumentId = Guid.NewGuid();

            await using (JournalDbContext context =
                         await factory.CreateDbContextAsync())
            {
                IMigrator migrator = context.Database.GetService<IMigrator>();
                await migrator.MigrateAsync(MigrationTwo);
                context.TradingAccounts.Add(new TradingAccountRecord
                {
                    Id = accountId,
                    Name = "Existing Account",
                    AccountType = TradingAccountType.Personal,
                    Currency = "USD",
                    IsActive = true,
                    CreatedAtUtc = Timestamp,
                    UpdatedAtUtc = Timestamp,
                });
                context.Instruments.Add(new InstrumentRecord
                {
                    Id = instrumentId,
                    Symbol = "ES",
                    DisplayName = "E-mini",
                    AssetClass = AssetClass.Futures,
                    Currency = "USD",
                    TickSize = 0.25m,
                    TickValue = 12.50m,
                    IsActive = true,
                    CreatedAtUtc = Timestamp,
                    UpdatedAtUtc = Timestamp,
                });
                context.Trades.Add(new TradeRecord
                {
                    Id = tradeId,
                    TradingAccountId = accountId,
                    InstrumentId = instrumentId,
                    PricingPointValue = 1.1m,
                    PricingCurrency = "USD",
                    CreatedAtUtc = Timestamp.AddHours(1),
                    UpdatedAtUtc = Timestamp.AddHours(1),
                });
                context.TradeExecutions.AddRange(
                    Execution(tradeId, 1, ExecutionSide.Buy, 0.3m, 29131.25m, 0.1m, 0.01m),
                    Execution(tradeId, 2, ExecutionSide.Sell, 0.3m, 29200.50m, 0.2m, 0.02m));
                await context.SaveChangesAsync();

                Assert.Equal(
                    [
                        "20260908122839_InitialCreate",
                        MigrationTwo,
                    ],
                    await context.Database.GetAppliedMigrationsAsync());

                await migrator.MigrateAsync();
            }

            var reconciler = new TradeBrowseProjectionReconciler(factory);
            await reconciler.ReconcileAsync();
            await reconciler.ReconcileAsync();

            await using JournalDbContext verification =
                await factory.CreateDbContextAsync();
            TradeBrowseRecord projection = await verification.TradeBrowse
                .AsNoTracking()
                .SingleAsync(record => record.TradeId == tradeId);
            TradeRecord tradeRecord = await verification.Trades
                .AsNoTracking()
                .SingleAsync(record => record.Id == tradeId);
            List<TradeExecutionRecord> executions = await verification.TradeExecutions
                .AsNoTracking()
                .Where(record => record.TradeId == tradeId)
                .OrderBy(record => record.Sequence)
                .ToListAsync();
            Trade trade = PersonalTradingJournal.Infrastructure.Persistence.Mapping
                .TradePersistenceMapper.ToDomain(tradeRecord, executions);

            Assert.Equal(1, projection.ProjectionVersion);
            Assert.Equal(trade.OpenedAtUtc, projection.OpenedAtUtc);
            Assert.Equal(trade.ClosedAtUtc, projection.ClosedAtUtc);
            Assert.Equal(trade.Direction, projection.Direction);
            Assert.Equal(trade.Status, projection.Status);
            Assert.Equal(trade.OpenQuantity, projection.OpenQuantity);
            Assert.Equal(trade.AverageEntryPrice, projection.AverageEntryPrice);
            Assert.Equal(trade.AverageExitPrice, projection.AverageExitPrice);
            Assert.Equal(trade.TotalCosts, projection.TotalCosts);
            Assert.Equal(trade.GrossPnL, projection.GrossPnL);
            Assert.Equal(trade.NetPnL, projection.NetPnL);
            Assert.Equal(
                DecimalSortKey.Encode(trade.OpenQuantity),
                projection.OpenQuantitySortKey);
            Assert.Equal(
                DecimalSortKey.Encode(trade.AverageEntryPrice),
                projection.AverageEntryPriceSortKey);
            Assert.Equal(
                DecimalSortKey.Encode(trade.NetPnL!.Value),
                projection.NetPnLSortKey);
            Assert.Equal(3, (await verification.Database
                .GetAppliedMigrationsAsync()).Count());
            Assert.Equal(1, await verification.TradeBrowse.CountAsync());
        }
        finally
        {
            if (provider is not null)
            {
                await provider.DisposeAsync();
            }

            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static TradeExecutionRecord Execution(
        Guid tradeId,
        int sequence,
        ExecutionSide side,
        decimal quantity,
        decimal price,
        decimal commission,
        decimal fees) => new()
    {
        Id = Guid.NewGuid(),
        TradeId = tradeId,
        Sequence = sequence,
        ExecutedAtUtc = Timestamp.AddMinutes(sequence * 10),
        Side = side,
        Quantity = quantity,
        Price = price,
        Commission = commission,
        Fees = fees,
    };
}
