using Microsoft.EntityFrameworkCore;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Backups.BackupArchiveTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Backups;

public sealed class MigratedBackupTestDatabaseTests
{
    [Fact]
    public async Task ConcurrentFixturesRetainCompleteSchemaAndIndependentMutableData()
    {
        var fixtures = new System.Collections.Concurrent.ConcurrentBag<Fixture>();
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 4).Select(async index =>
            {
                var fixture = await Fixture.Create();
                fixtures.Add(fixture);
                await using var db = fixture.Context();
                Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                Assert.Empty(await db.Trades.ToArrayAsync());
                Assert.Empty(await db.TradingAccounts.ToArrayAsync());
                Assert.Empty(await db.CoachingAnalyses.ToArrayAsync());
                await fixture.Add($"synthetic-{index}.png");
                Assert.Single(await db.Trades.AsNoTracking().ToArrayAsync());
                await db.Database.OpenConnectionAsync();
                using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "PRAGMA foreign_keys;";
                Assert.Equal(1L, await command.ExecuteScalarAsync());
                command.CommandText = "PRAGMA integrity_check;";
                Assert.Equal("ok", await command.ExecuteScalarAsync());
                command.CommandText = "PRAGMA foreign_key_check;";
                Assert.Null(await command.ExecuteScalarAsync());
            }));

            Assert.Equal(4, fixtures.Select(f => f.Paths.DatabasePath).Distinct().Count());
            using (var first = fixtures.First().Context())
                await first.Database.ExecuteSqlRawAsync("DROP TABLE CoachingAnalyses;");
            await using var fresh = await Fixture.Create();
            await using var context = fresh.Context();
            Assert.Empty(await context.Trades.ToArrayAsync());
            Assert.Empty(await context.CoachingAnalyses.ToArrayAsync());
        }
        finally
        {
            foreach (var fixture in fixtures) await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task CloneNeverOverwritesAnExistingFixtureDatabase()
    {
        await using var fixture = await Fixture.Create();
        await fixture.Add("synthetic.png");
        await Assert.ThrowsAsync<IOException>(() => MigratedBackupTestDatabase.CreateAsync(fixture.Paths.DatabasePath));
        await using var db = fixture.Context();
        Assert.Single(await db.Trades.AsNoTracking().ToArrayAsync());
    }
}
