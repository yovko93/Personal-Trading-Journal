using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Initialization;

public sealed class JournalDatabaseInitializerTests
{
    private const string InitialMigrationId = "20260908122839_InitialCreate";

    [Fact]
    public async Task InitializeAsyncCreatesMigratedUsableEmptyDatabase()
    {
        await RunWithTemporaryPathsAsync(async applicationPaths =>
        {
            var services = new ServiceCollection();
            services.AddPersistence(applicationPaths);

            await using ServiceProvider serviceProvider = services.BuildServiceProvider();
            JournalDatabaseInitializer initializer =
                serviceProvider.GetRequiredService<JournalDatabaseInitializer>();
            JournalDatabaseInitializer secondInitializer =
                serviceProvider.GetRequiredService<JournalDatabaseInitializer>();

            Assert.NotSame(initializer, secondInitializer);
            Assert.False(File.Exists(applicationPaths.DatabasePath));

            await initializer.InitializeAsync();

            Assert.True(File.Exists(applicationPaths.DatabasePath));

            IDbContextFactory<JournalDbContext> contextFactory =
                serviceProvider.GetRequiredService<IDbContextFactory<JournalDbContext>>();
            await using JournalDbContext context =
                await contextFactory.CreateDbContextAsync();

            Assert.Equal(
                [InitialMigrationId],
                await context.Database.GetAppliedMigrationsAsync());
            Assert.Equal(0, await context.Instruments.CountAsync());
            Assert.Equal(0, await context.TradingAccounts.CountAsync());
            Assert.Equal(0, await context.Trades.CountAsync());
            Assert.Equal(0, await context.TradeExecutions.CountAsync());
            Assert.Equal(0, await context.TradeScreenshots.CountAsync());
            Assert.Equal(0, await context.TradeMistakes.CountAsync());

            IProperty executedAtProperty = context.Model
                .FindEntityType(typeof(TradeExecutionRecord))!
                .FindProperty(nameof(TradeExecutionRecord.ExecutedAtUtc))!;
            Assert.Equal(
                typeof(DateTime),
                executedAtProperty.GetValueConverter()!.ProviderClrType);

            context.TradeExecutions.Add(new TradeExecutionRecord
            {
                Id = Guid.NewGuid(),
                TradeId = Guid.NewGuid(),
                Sequence = 1,
                ExecutedAtUtc = DateTimeOffset.UtcNow,
                Side = ExecutionSide.Buy,
                Quantity = 1m,
                Price = 20_000m,
                Commission = 1m,
                Fees = 0.25m,
                ExternalExecutionId = null,
                ExternalOrderId = null,
                BrokerSymbol = "NQ",
            });

            await Assert.ThrowsAsync<DbUpdateException>(
                () => context.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task InitializeAsyncIsIdempotent()
    {
        await RunWithTemporaryPathsAsync(async applicationPaths =>
        {
            var services = new ServiceCollection();
            services.AddPersistence(applicationPaths);

            await using ServiceProvider serviceProvider = services.BuildServiceProvider();
            JournalDatabaseInitializer initializer =
                serviceProvider.GetRequiredService<JournalDatabaseInitializer>();

            await initializer.InitializeAsync();
            await initializer.InitializeAsync();

            IDbContextFactory<JournalDbContext> contextFactory =
                serviceProvider.GetRequiredService<IDbContextFactory<JournalDbContext>>();
            await using (JournalDbContext context =
                         await contextFactory.CreateDbContextAsync())
            {
                Assert.Equal(
                    [InitialMigrationId],
                    await context.Database.GetAppliedMigrationsAsync());
            }

            await using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = applicationPaths.DatabasePath,
                    ForeignKeys = true,
                }.ToString());
            await connection.OpenAsync();

            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";";
            Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
        });
    }

    [Fact]
    public async Task InitializeAsyncPropagatesCancellation()
    {
        await RunWithTemporaryPathsAsync(async applicationPaths =>
        {
            var services = new ServiceCollection();
            services.AddPersistence(applicationPaths);

            await using ServiceProvider serviceProvider = services.BuildServiceProvider();
            JournalDatabaseInitializer initializer =
                serviceProvider.GetRequiredService<JournalDatabaseInitializer>();
            using var cancellationSource = new CancellationTokenSource();
            await cancellationSource.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => initializer.InitializeAsync(cancellationSource.Token));
        });
    }

    private static async Task RunWithTemporaryPathsAsync(
        Func<TestApplicationPaths, Task> test)
    {
        string testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(JournalDatabaseInitializerTests)}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var applicationPaths = new TestApplicationPaths(testDirectory);

        try
        {
            await test(applicationPaths);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testDirectory, recursive: true);
            Assert.False(Directory.Exists(testDirectory));
        }
    }

    private sealed class TestApplicationPaths : IApplicationPaths
    {
        public TestApplicationPaths(string dataDirectory)
        {
            DataDirectory = dataDirectory;
            DatabasePath = Path.Combine(dataDirectory, "runtime-initialization.db");
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
