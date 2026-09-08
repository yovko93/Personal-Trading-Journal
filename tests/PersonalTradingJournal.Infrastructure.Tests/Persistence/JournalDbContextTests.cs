using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence;

public sealed class JournalDbContextTests
{
    [Fact]
    public void ConstructorConfiguresSqliteWithoutCreatingDatabase()
    {
        string testDirectory = CreateTestDirectory();
        string databasePath = Path.Combine(testDirectory, "journal.db");

        try
        {
            var options = new DbContextOptionsBuilder<JournalDbContext>()
                .UseSqlite(new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                }.ToString())
                .Options;

            using var context = new JournalDbContext(options);

            Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", context.Database.ProviderName);

            var connectionString = new SqliteConnectionStringBuilder(
                context.Database.GetDbConnection().ConnectionString);

            Assert.Equal(databasePath, connectionString.DataSource);
            Assert.False(File.Exists(databasePath));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private static string CreateTestDirectory()
    {
        string testDirectory = Path.Combine(
            Path.GetTempPath(),
            nameof(JournalDbContextTests),
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(testDirectory);
        return testDirectory;
    }
}
