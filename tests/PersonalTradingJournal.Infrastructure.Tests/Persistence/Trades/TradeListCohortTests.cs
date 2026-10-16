using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Infrastructure.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

public sealed partial class TradeListPagingTests
{
    [Fact]
    public async Task CountPageAndPositionSizeShareSnapshotWhileConcurrentWriterDeletesMember()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await PersistReferencesAsync(db);
        var id = Guid.NewGuid();
        await PersistTradeAsync(db, ClosedTrade(account.Id, instrument.Id, id, CreatedAtUtc, 10m));
        await using var setup = await db.ContextFactory.CreateDbContextAsync();
        int commits = 0;
        var observer = new CohortCountObserver(async () =>
        {
            await using var writer = await db.ContextFactory.CreateDbContextAsync();
            await writer.Trades.Where(t => t.Id == id).ExecuteDeleteAsync();
            commits++;
        });
        var factory = new CohortFactory(new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite(setup.Database.GetConnectionString()).AddInterceptors(observer).Options);
        var reader = new TradeListReader(factory);
        var query = new TradeListQuery(1, 20, TradeListSortColumn.OpenedAtUtc, TradeListSortDirection.Descending, cohort: new([id]));
        var snapshot = await reader.GetPageAsync(query);
        Assert.Equal(1, commits); Assert.Equal(1, snapshot.TotalCount);
        Assert.Equal(id, Assert.Single(snapshot.Items).Id); Assert.Equal(1m, snapshot.Items[0].Size);
        Assert.Empty((await GetReader(db).GetPageAsync(query)).Items);
        Assert.All(factory.Connections, c => Assert.Equal(System.Data.ConnectionState.Closed, c.State));
    }

    private sealed class CohortFactory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    {
        public List<DbConnection> Connections { get; } = [];
        public JournalDbContext CreateDbContext()
        {
            var context = new JournalDbContext(options); Connections.Add(context.Database.GetDbConnection()); return context;
        }
    }
    private sealed class CohortCountObserver(Func<Task> afterCount) : DbCommandInterceptor
    {
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("COUNT(*)", StringComparison.Ordinal)) await afterCount();
            return result;
        }
    }

    [Fact]
    public async Task CohortPagesCurrentRecordsWithOneLargeMembershipParameterAndStableTies()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await PersistReferencesAsync(db);
        var members = Enumerable.Range(1, 25).Select(n => Guid.Parse($"00000000-0000-0000-0000-{n:D12}")).ToArray();
        foreach (var id in members)
            await PersistTradeAsync(db, ClosedTrade(account.Id, instrument.Id, id, CreatedAtUtc.AddDays(-2), 10m));
        // No parameter-per-ID limit, including missing originals; count is the current available count.
        var cohort = new TradeIdCohort(members.Concat(Enumerable.Range(0, 33000).Select(_ => Guid.NewGuid())));
        var newMatch = Guid.NewGuid();
        await PersistTradeAsync(db, ClosedTrade(account.Id, instrument.Id, newMatch, CreatedAtUtc, 10m));
        await using (var writer = await db.ContextFactory.CreateDbContextAsync())
        {
            (await writer.TradingAccounts.SingleAsync()).Name = "Renamed current Account";
            // Current values may differ from the original analysis, without changing membership.
            (await writer.TradeBrowse.SingleAsync(t => t.TradeId == members[0])).GrossPnL = -42m;
            await writer.SaveChangesAsync();
        }
        var reader = GetReader(db);
        TradeListQuery QueryPage(int page) => new(page, 20, TradeListSortColumn.OpenedAtUtc, TradeListSortDirection.Descending, cohort: cohort);
        var first = await reader.GetPageAsync(QueryPage(1));
        var second = await reader.GetPageAsync(QueryPage(2));
        Assert.Equal(25, first.TotalCount); Assert.Equal(25, second.TotalCount);
        Assert.Equal(members, first.Items.Concat(second.Items).Select(t => t.Id));
        Assert.All(first.Items, t => Assert.Equal("Renamed current Account", t.TradingAccountName));
        Assert.Equal(-42m, first.Items[0].GrossPnL);
        Assert.DoesNotContain(first.Items.Concat(second.Items), t => t.Id == newMatch);
        await using (var writer = await db.ContextFactory.CreateDbContextAsync())
            await writer.Trades.Where(t => members.Contains(t.Id)).ExecuteDeleteAsync();
        var deleted = await reader.GetPageAsync(QueryPage(1));
        Assert.Equal(0, deleted.TotalCount); Assert.Empty(deleted.Items);
        Assert.Single((await reader.GetPageAsync(Query(1))).Items);
        var empty = await reader.GetPageAsync(new(1, 20, TradeListSortColumn.OpenedAtUtc, TradeListSortDirection.Descending, cohort: new([])));
        Assert.Empty(empty.Items); Assert.Equal(0, empty.TotalCount);
    }

    [Fact]
    public async Task CohortRetainsOvernightOriginalAcrossAccountEditsAndCancellation()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await PersistReferencesAsync(db);
        var (other, _) = await PersistReferencesAsync(db, "Other", "OTHER", "2");
        var id = Guid.NewGuid();
        await PersistTradeAsync(db, ClosedTrade(account.Id, instrument.Id, id,
            new DateTimeOffset(2026, 11, 1, 3, 59, 30, TimeSpan.Zero), 10m));
        var query = new TradeListQuery(1, 20, TradeListSortColumn.OpenedAtUtc, TradeListSortDirection.Descending, cohort: new([id]));
        await using (var writer = await db.ContextFactory.CreateDbContextAsync())
        {
            (await writer.Trades.SingleAsync()).TradingAccountId = other.Id;
            await writer.SaveChangesAsync();
        }
        var reader = GetReader(db);
        var row = Assert.Single((await reader.GetPageAsync(query)).Items);
        Assert.Equal(id, row.Id); Assert.Equal(other.Id, row.TradingAccountId);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetPageAsync(query, cancelled.Token));
        Assert.Single((await reader.GetPageAsync(query)).Items);
    }
}
