using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence;

public sealed class PersistenceRegistrationTests
{
    [Fact]
    public void AddPersistenceRegistersFactoryWithoutCreatingDatabase()
    {
        string testDirectory = CreateTestDirectory();
        var applicationPaths = new LocalApplicationPaths(testDirectory);
        applicationPaths.EnsureDirectoriesExist();
        var services = new ServiceCollection();

        try
        {
            IServiceCollection returnedServices = services.AddPersistence(applicationPaths);

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IDbContextFactory<JournalDbContext> factory =
                serviceProvider.GetRequiredService<IDbContextFactory<JournalDbContext>>();

            Assert.Same(services, returnedServices);
            Assert.False(File.Exists(applicationPaths.DatabasePath));

            using JournalDbContext context = factory.CreateDbContext();
            var connectionString = new SqliteConnectionStringBuilder(
                context.Database.GetDbConnection().ConnectionString);

            Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", context.Database.ProviderName);
            Assert.Equal(applicationPaths.DatabasePath, connectionString.DataSource);
            Assert.True(connectionString.ForeignKeys);
            Assert.False(File.Exists(applicationPaths.DatabasePath));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void AddPersistenceRejectsNullServices()
    {
        IServiceCollection services = null!;
        IApplicationPaths applicationPaths = new LocalApplicationPaths(Path.GetTempPath());

        Assert.Throws<ArgumentNullException>(() => services.AddPersistence(applicationPaths));
    }

    [Fact]
    public void AddPersistenceRejectsNullApplicationPaths()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddPersistence(null!));
    }

    private static string CreateTestDirectory()
    {
        string testDirectory = Path.Combine(
            Path.GetTempPath(),
            nameof(PersistenceRegistrationTests),
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(testDirectory);
        return testDirectory;
    }
}
