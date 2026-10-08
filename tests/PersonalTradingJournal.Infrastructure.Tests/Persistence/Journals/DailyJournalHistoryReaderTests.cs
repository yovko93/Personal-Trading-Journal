using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Infrastructure.Journals;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Journals;

public sealed partial class DailyJournalRepositoryTests
{
    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(20)]
    public async Task TenEntryPagesKeepNewestDateOrderingAndDoNotWrite(int count)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var repository = GetRepository(db);
        Guid p21 = await SeedAccountAsync(db, "P 21", false);
        Guid other = await SeedAccountAsync(db, "Other account");
        var expected = new List<DailyJournalEntry>();
        for (int i = 0; i < count; i++)
        {
            Guid? scope = i % 3 == 0 ? null : i % 3 == 1 ? p21 : other;
            expected.Add((await repository.CreateAsync(new(TradingDate.AddDays(-(i / 3)), scope, "history"))).Journal!.Entry);
        }
        var probe = new HistoryReadProbe();
        var factory = await InterceptingFactory.CreateAsync(db, probe);
        var reader = new DailyJournalHistoryReader(factory);
        var first = await reader.BrowseAsync(null, 1, 10);
        var second = await reader.BrowseAsync(null, 2, 10);
        Assert.Equal(Math.Min(count, 10), first.Items.Count);
        Assert.Equal(Math.Max(count - 10, 0), second.Items.Count);
        Assert.Equal(count > 10, first.HasNext);
        Assert.False(second.HasNext);
        Assert.Equal(count, first.TotalCount);
        Assert.Equal(expected.OrderByDescending(i => i.TradingDate).ThenBy(i => i.Id).Select(i => i.Id),
            first.Items.Concat(second.Items).Select(i => i.Id));
        Assert.Contains(first.Items, i => i.AccountId is null);
        Assert.Contains(first.Items, i => i.AccountId == p21 && i.AccountName == "P 21" && i.AccountState == DailyJournalAccountState.Inactive);
        Assert.Contains(first.Items, i => i.AccountId == other && i.AccountName == "Other account");
        var repeated = await reader.BrowseAsync(null, 1, 10);
        Assert.Equal(first.Items.Select(i => i.Id), repeated.Items.Select(i => i.Id));
        Assert.Equal(6, probe.Sql.Count);
        Assert.All(probe.Sql, sql => Assert.StartsWith("SELECT", sql));
        Assert.Equal(3, probe.Sql.Count(sql => sql.Contains("LIMIT") && sql.Contains("OFFSET")));
    }

    [Fact]
    public async Task HistoryPagesUseExactScopeNewestDateAndBoundedMetadataWithoutTradeRequirement()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var repository = GetRepository(db);
        var reader = db.ServiceProvider.GetRequiredService<IDailyJournalHistoryReader>();
        Guid account = await SeedAccountAsync(db, "Retired account", false);
        Guid other = await SeedAccountAsync(db, "Other account");
        for (int i = 0; i < 43; i++)
        {
            await repository.CreateAsync(new(TradingDate.AddDays(-i), null, $"all {i}"));
            await repository.CreateAsync(new(TradingDate.AddDays(-i), account, $"account {i}", i % 2 == 0, CompleteReview));
        }
        await repository.CreateAsync(new(TradingDate, other, "other"));
        var first = await reader.BrowseAsync(account);
        var second = await reader.BrowseAsync(account, 2);
        var third = await reader.BrowseAsync(account, 3);
        var all = await reader.BrowseAsync(null, pageSize: 50);
        Assert.Equal(43, first.TotalCount);
        Assert.Equal(20, first.Items.Count);
        Assert.Equal(20, second.Items.Count);
        Assert.Equal(3, third.Items.Count);
        Assert.True(first.HasNext);
        Assert.False(third.HasNext);
        Assert.Equal(TradingDate, first.Items[0].TradingDate);
        Assert.Equal(TradingDate.AddDays(-20), second.Items[0].TradingDate);
        Assert.Equal(43, first.Items.Concat(second.Items).Concat(third.Items).Select(r => r.Id).Distinct().Count());
        Assert.All(first.Items, r => { Assert.Equal(account, r.AccountId); Assert.Equal("Retired account", r.AccountName); Assert.Equal(DailyJournalAccountState.Inactive, r.AccountState); });
        Assert.Equal(87, all.TotalCount);
        Assert.Equal(50, all.Items.Count);
        Assert.Contains(all.Items, r => r.AccountId is null && r.AccountState == DailyJournalAccountState.AllAccounts);
        Assert.Contains(all.Items, r => r.AccountId == account && r.AccountState == DailyJournalAccountState.Inactive);
        Assert.Contains(all.Items, r => r.AccountId == other);
        Assert.Empty((await reader.BrowseAsync(Guid.NewGuid())).Items);
        Assert.Empty((await reader.BrowseAsync(account, 4)).Items);
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await context.Trades.ToArrayAsync());
    }

    [Fact]
    public async Task RevisionPagesAndSingleSnapshotPreserveExactAnswersStateAndAuditWithoutWrites()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var repository = GetRepository(db);
        var clock = new JournalTestClock(CreatedAtUtc);
        repository = new DailyJournalRepository(db.ContextFactory, clock);
        var first = (await repository.CreateAsync(new(TradingDate, null, "  first\r\n", true, new("draft", "", "")))).Journal!.Entry;
        for (int i = 2; i <= 23; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await repository.UpdateAsync(new(first.Id, i - 1, $" text {(i == 23 ? 22 : i)}\n", i != 22, CompleteReview));
        }
        // A save interceptor and SQL recording prove history reads issue SELECTs only,
        // and page projections do not retrieve the four potentially large text fields.
        var probe = new HistoryReadProbe();
        var factory = await InterceptingFactory.CreateAsync(db, probe);
        var reader = new DailyJournalHistoryReader(factory);
        var entries = await reader.BrowseAsync(null);
        var page = await reader.BrowseRevisionsAsync(first.Id);
        var tail = await reader.BrowseRevisionsAsync(first.Id, 2);
        Assert.Equal(23, page.TotalCount);
        Assert.Equal(23, page.Items[0].Revision);
        Assert.Equal(3, tail.Items.Count);
        Assert.Equal(1, tail.Items[^1].Revision);
        Assert.All(probe.Sql, sql => { Assert.StartsWith("SELECT", sql); Assert.DoesNotContain("\"Text\"", sql); Assert.DoesNotContain("\"WentWell\"", sql); });
        Assert.Equal(6, probe.Sql.Count); // Two bounded queries per metadata page, not per row.
        var completed = await reader.GetRevisionAsync(first.Id, 22);
        var original = await reader.GetRevisionAsync(first.Id, 1);
        Assert.False(completed!.IsDraft);
        Assert.Equal(CompleteReview, completed.Review);
        Assert.Equal(" text 22\n", completed.Text);
        Assert.Equal("  first\r\n", original!.Text);
        Assert.Equal(new DailyReviewAnswers("draft", "", ""), original.Review);
        Assert.Equal(CreatedAtUtc, original.SavedAtUtc);
        Assert.Null(await reader.GetRevisionAsync(first.Id, 99));
        Assert.Null(await reader.GetRevisionAsync(Guid.NewGuid(), 1));
        Assert.Equal(23, (await repository.GetAsync(TradingDate))!.Entry.Revision);
        Assert.Equal(23, (await repository.GetHistoryAsync(first.Id)).Count);
        Assert.Single(entries.Items);
    }

    [Fact]
    public async Task HistoryUnavailableAccountRetainsExactOrphanScope()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        Guid account = await SeedAccountAsync(db, "Missing reference");
        var repository = GetRepository(db);
        var entry = (await repository.CreateAsync(new(TradingDate, account, "historic"))).Journal!.Entry;
        await repository.CreateAsync(new(TradingDate, null, "global"));
        Guid missing = Guid.NewGuid();
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            await context.Database.OpenConnectionAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
            try { await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE DailyJournals SET TradingAccountId = {missing} WHERE Id = {entry.Id}"); }
            finally { await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;"); }
        }
        var reader = new DailyJournalHistoryReader(db.ContextFactory);
        var item = Assert.Single((await reader.BrowseAsync(missing)).Items);
        Assert.Equal(missing, item.AccountId);
        Assert.Equal(DailyJournalAccountState.Unavailable, item.AccountState);
        Assert.Null(item.AccountName);
        var all = await reader.BrowseAsync(null);
        Assert.Equal(2, all.TotalCount);
        Assert.Contains(all.Items, r => r.AccountId is null);
        Assert.Contains(all.Items, r => r.AccountId == missing && r.AccountName is null && r.AccountState == DailyJournalAccountState.Unavailable);
        Assert.Equal("historic", (await reader.GetRevisionAsync(entry.Id, 1))!.Text);
    }

    [Fact]
    public async Task HistoryCancellationAndValidationNeverChangeRevisions()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var repository = GetRepository(db);
        var entry = (await repository.CreateAsync(new(TradingDate, null, "kept"))).Journal!.Entry;
        var reader = new DailyJournalHistoryReader(db.ContextFactory);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.BrowseAsync(null, cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.BrowseRevisionsAsync(entry.Id, cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetRevisionAsync(entry.Id, 1, cancellation.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.BrowseAsync(Guid.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.BrowseRevisionsAsync(Guid.Empty));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.BrowseAsync(null, pageSize: 51));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.GetRevisionAsync(entry.Id, 0));
        Assert.Single(await repository.GetHistoryAsync(entry.Id));
    }

    private sealed class HistoryReadProbe : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        { Sql.Add(command.CommandText); return ValueTask.FromResult(result); }
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> NonQueryExecutingAsync(
            System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("History must not write.");
    }
}
