using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence;

public sealed class ReaderTestDatabaseTests
{
    [Fact]
    public async Task ConcurrentReadersHaveFullMigratedSchemaAndIndependentMutableData()
    {
        var databases = new System.Collections.Concurrent.ConcurrentBag<ReaderTestDatabase>();
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 4).Select(async index =>
            {
                var database = await ReaderTestDatabase.CreateAsync();
                databases.Add(database);
                await using var context = await database.ContextFactory.CreateDbContextAsync();
                Assert.Equal(context.Database.GetMigrations(), await context.Database.GetAppliedMigrationsAsync());
                Assert.Empty(await context.Database.GetPendingMigrationsAsync());
                Assert.Empty(await context.Trades.ToArrayAsync());
                Assert.Empty(await context.TradingSetups.ToArrayAsync());
                Assert.Empty(await context.CoachingAnalyses.ToArrayAsync());
                await database.ServiceProvider.GetRequiredService<ITradingSetupStore>().AddAsync(
                    new TradingSetup($"Synthetic reader {index}", null, DateTimeOffset.UtcNow));
                Assert.Equal($"Synthetic reader {index}", (await context.TradingSetups.SingleAsync()).Name);
                await context.Database.OpenConnectionAsync();
                using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = "PRAGMA journal_mode;";
                Assert.Equal("wal", await command.ExecuteScalarAsync());
                command.CommandText = "PRAGMA foreign_keys;";
                Assert.Equal(1L, await command.ExecuteScalarAsync());
                command.CommandText = "PRAGMA integrity_check;";
                Assert.Equal("ok", await command.ExecuteScalarAsync());
                command.CommandText = "PRAGMA foreign_key_check;";
                Assert.Null(await command.ExecuteScalarAsync());
            }));
        }
        finally
        {
            foreach (var database in databases) await database.DisposeAsync();
        }
    }

    [Fact]
    public async Task DisposalRemovesOwnedFilesAndMutatingACloneCannotChangeTheTemplate()
    {
        string path;
        await using (var first = await ReaderTestDatabase.CreateAsync())
        {
            await using var context = await first.ContextFactory.CreateDbContextAsync();
            path = context.Database.GetDbConnection().DataSource;
            await context.Database.ExecuteSqlRawAsync("DROP TABLE CoachingAnalyses;");
        }
        Assert.False(File.Exists(path));
        Assert.False(Directory.Exists(Path.GetDirectoryName(Path.GetDirectoryName(path))));
        await using var fresh = await ReaderTestDatabase.CreateAsync();
        await using var freshContext = await fresh.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await freshContext.CoachingAnalyses.ToArrayAsync());
        Assert.Empty(await freshContext.TradingSetups.ToArrayAsync());
        Assert.NotEqual(path, freshContext.Database.GetDbConnection().DataSource);
    }
}
