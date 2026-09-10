using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence;

internal sealed class ReaderTestDatabase : IAsyncDisposable
{
    private readonly string _testDirectory;

    private ReaderTestDatabase(
        string testDirectory,
        ServiceProvider serviceProvider,
        IDbContextFactory<JournalDbContext> contextFactory)
    {
        _testDirectory = testDirectory;
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
            var services = new ServiceCollection();
            services.AddPersistence(applicationPaths);
            serviceProvider = services.BuildServiceProvider();

            JournalDatabaseInitializer initializer =
                serviceProvider.GetRequiredService<JournalDatabaseInitializer>();
            await initializer.InitializeAsync();

            IDbContextFactory<JournalDbContext> contextFactory =
                serviceProvider.GetRequiredService<IDbContextFactory<JournalDbContext>>();

            return new ReaderTestDatabase(
                testDirectory,
                serviceProvider,
                contextFactory);
        }
        catch
        {
            if (serviceProvider is not null)
            {
                await serviceProvider.DisposeAsync();
            }

            SqliteConnection.ClearAllPools();
            Directory.Delete(testDirectory, recursive: true);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ServiceProvider.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_testDirectory, recursive: true);
    }
}
