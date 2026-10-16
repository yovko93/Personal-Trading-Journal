using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Storage;
using PersonalTradingJournal.Infrastructure.Tests.Backups;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence;

internal sealed class ReaderTestDatabase : IAsyncDisposable
{
    private readonly string _testDirectory;
    private readonly string _databasePath;

    private ReaderTestDatabase(
        string testDirectory,
        string databasePath,
        ServiceProvider serviceProvider,
        IDbContextFactory<JournalDbContext> contextFactory)
    {
        _testDirectory = testDirectory;
        _databasePath = databasePath;
        ServiceProvider = serviceProvider;
        ContextFactory = contextFactory;
    }

    public ServiceProvider ServiceProvider { get; }

    public IDbContextFactory<JournalDbContext> ContextFactory { get; }

    public static async Task<ReaderTestDatabase> CreateAsync()
    {
        string testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(ReaderTestDatabase)}-{Guid.NewGuid():N}");
        var applicationPaths = new LocalApplicationPaths(testDirectory);
        applicationPaths.EnsureDirectoriesExist();

        ServiceProvider? serviceProvider = null;

        try
        {
            // Reader assertions need independent current-schema databases, not another
            // replay of every historical migration. The existing SQLite-backup template
            // shares immutable bytes only; dedicated migration tests still start empty.
            using (SyntheticFixtureTiming.Start("reader template acquisition"))
                await MigratedBackupTestDatabase.CreateAsync(applicationPaths.DatabasePath);
            var services = new ServiceCollection();
            services.AddPersistence(applicationPaths);
            serviceProvider = services.BuildServiceProvider();

            JournalDatabaseInitializer initializer =
                serviceProvider.GetRequiredService<JournalDatabaseInitializer>();
            using (SyntheticFixtureTiming.Start("reader initializer"))
                await initializer.InitializeAsync();

            IDbContextFactory<JournalDbContext> contextFactory =
                serviceProvider.GetRequiredService<IDbContextFactory<JournalDbContext>>();

            // The portable template is deliberately standalone DELETE-mode. Fresh EF
            // databases use WAL; restore that reader/writer concurrency behavior before
            // returning the fixture (not when a concurrent-read assertion is underway).
            await using (var context = await contextFactory.CreateDbContextAsync())
            {
                await context.Database.OpenConnectionAsync();
                using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = "PRAGMA journal_mode=WAL;";
                if (!string.Equals(await command.ExecuteScalarAsync() as string, "wal", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The synthetic reader fixture must use WAL mode.");
            }

            return new ReaderTestDatabase(
                testDirectory,
                applicationPaths.DatabasePath,
                serviceProvider,
                contextFactory);
        }
        catch
        {
            if (serviceProvider is not null)
            {
                await serviceProvider.DisposeAsync();
            }

            SqliteTestPoolCleanup.ClearPersistencePools(applicationPaths.DatabasePath);
            Directory.Delete(testDirectory, recursive: true);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ServiceProvider.DisposeAsync();
        SqliteTestPoolCleanup.ClearPersistencePools(_databasePath);
        Directory.Delete(_testDirectory, recursive: true);
    }
}
