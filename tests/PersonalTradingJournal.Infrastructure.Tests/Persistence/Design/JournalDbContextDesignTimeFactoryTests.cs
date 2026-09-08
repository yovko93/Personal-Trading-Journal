using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Design;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Design;

public sealed class JournalDbContextDesignTimeFactoryTests
{
    [Fact]
    public void CreatesSqliteContextWithIsolatedInMemoryConnection()
    {
        var factory = new JournalDbContextDesignTimeFactory();

        using JournalDbContext context =
            factory.CreateDbContext(Array.Empty<string>());
        var connectionString = new SqliteConnectionStringBuilder(
            context.Database.GetDbConnection().ConnectionString);

        Assert.Equal(
            "Microsoft.EntityFrameworkCore.Sqlite",
            context.Database.ProviderName);
        Assert.Equal(":memory:", connectionString.DataSource);
        Assert.True(connectionString.ForeignKeys);
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Fact]
    public void CreatingAndDisposingContextDoesNotCreatePhysicalDatabase()
    {
        string testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(JournalDbContextDesignTimeFactoryTests)}-{Guid.NewGuid():N}");
        string databasePath = Path.Combine(testDirectory, "journal.db");
        Directory.CreateDirectory(testDirectory);

        try
        {
            Assert.False(File.Exists(databasePath));

            var factory = new JournalDbContextDesignTimeFactory();
            using (JournalDbContext context =
                   factory.CreateDbContext(Array.Empty<string>()))
            {
                _ = context.Model;
                Assert.Equal(
                    ConnectionState.Closed,
                    context.Database.GetDbConnection().State);
            }

            Assert.False(File.Exists(databasePath));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }

        Assert.False(Directory.Exists(testDirectory));
    }

    [Fact]
    public void ExposesAuthoritativeTimestampAndIntegrityModel()
    {
        var factory = new JournalDbContextDesignTimeFactory();
        using JournalDbContext context =
            factory.CreateDbContext(Array.Empty<string>());

        Assert.NotNull(context.Model.FindEntityType(typeof(TradeRecord)));
        Assert.NotNull(context.Model.FindEntityType(typeof(TradeExecutionRecord)));
        Assert.NotNull(context.Model.FindEntityType(typeof(TradeScreenshotRecord)));
        Assert.NotNull(context.Model.FindEntityType(typeof(TradeMistakeRecord)));

        IEntityType executionType =
            context.Model.FindEntityType(typeof(TradeExecutionRecord))!;
        IProperty executedAtProperty = executionType.FindProperty(
            nameof(TradeExecutionRecord.ExecutedAtUtc))!;
        Assert.Equal(typeof(DateTimeOffset), executedAtProperty.ClrType);
        Assert.Equal(
            typeof(DateTime),
            executedAtProperty.GetValueConverter()!.ProviderClrType);

        IForeignKey tradeForeignKey = Assert.Single(executionType.GetForeignKeys());
        Assert.Equal(typeof(TradeRecord), tradeForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, tradeForeignKey.DeleteBehavior);

        IIndex sequenceIndex = Assert.Single(
            executionType.GetIndexes(),
            index => index.Properties.Select(property => property.Name)
                .SequenceEqual(
                [
                    nameof(TradeExecutionRecord.TradeId),
                    nameof(TradeExecutionRecord.Sequence),
                ]));
        Assert.True(sequenceIndex.IsUnique);
    }
}
