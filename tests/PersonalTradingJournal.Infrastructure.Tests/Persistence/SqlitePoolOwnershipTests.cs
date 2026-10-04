using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence;

public sealed class SqlitePoolOwnershipTests
{
    [Fact]
    public async Task FixtureCleanupReleasesItsOwnReadWriteAndReadOnlyHandlesBeforeDeletingDatabase()
    {
        var handles = new List<SQLitePCL.sqlite3>();
        string databasePath;
        await using (ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync())
        {
            await using var context = await database.ContextFactory.CreateDbContextAsync();
            databasePath = context.Database.GetDbConnection().DataSource;
            foreach (bool readOnly in new[] { false, true })
            {
                var builder = new SqliteConnectionStringBuilder(
                    context.Database.GetDbConnection().ConnectionString);
                if (readOnly)
                {
                    builder.Mode = SqliteOpenMode.ReadOnly;
                }

                await using var connection = new SqliteConnection(builder.ToString());
                await connection.OpenAsync();
                handles.Add(Assert.IsType<SQLitePCL.sqlite3>(connection.Handle));
            }

            Assert.All(handles, handle => Assert.False(handle.IsClosed));
        }

        Assert.All(handles, handle => Assert.True(handle.IsClosed));
        Assert.False(File.Exists(databasePath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(databasePath)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FixtureCleanupLeavesOtherFixturesActiveAndIdlePoolsUntouched(
        bool readOnly,
        bool keepConnectionOpen)
    {
        await using ReaderTestDatabase survivor = await ReaderTestDatabase.CreateAsync();
        await using var context = await survivor.ContextFactory.CreateDbContextAsync();
        var builder = new SqliteConnectionStringBuilder(
            context.Database.GetDbConnection().ConnectionString);
        if (readOnly)
        {
            builder.Mode = SqliteOpenMode.ReadOnly;
        }

        await using var connection = new SqliteConnection(builder.ToString());
        connection.CreateAggregate<int, int>("sum_probe", 0, (sum, value) => sum + value);
        await connection.OpenAsync();
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TEMP TABLE pool_sentinel (value INTEGER); " +
                "INSERT INTO pool_sentinel VALUES (19), (23);";
            await command.ExecuteNonQueryAsync();
        }

        var originalHandle = connection.Handle;
        Assert.NotNull(originalHandle);
        if (!keepConnectionOpen)
        {
            await connection.CloseAsync();
        }

        string retiredPath;
        await using (ReaderTestDatabase retiring = await ReaderTestDatabase.CreateAsync())
        {
            await using var retiringContext = await retiring.ContextFactory.CreateDbContextAsync();
            retiredPath = retiringContext.Database.GetDbConnection().DataSource;
        }

        Assert.False(File.Exists(retiredPath));
        if (keepConnectionOpen)
        {
            await connection.CloseAsync();
        }

        // Teardown must neither dispose an unrelated idle handle nor mark an active
        // one nonpoolable. TEMP data and native identity prove reuse, not just reopen.
        Assert.False(originalHandle.IsClosed);
        await connection.OpenAsync();
        Assert.Same(originalHandle, connection.Handle);
        using SqliteCommand verification = connection.CreateCommand();
        verification.CommandText = "SELECT sum_probe(value) FROM pool_sentinel";
        Assert.Equal(42L, await verification.ExecuteScalarAsync());
    }
}
