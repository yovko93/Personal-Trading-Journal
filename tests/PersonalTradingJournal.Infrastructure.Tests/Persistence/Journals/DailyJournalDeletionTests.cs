using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Infrastructure.Journals;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Journals;

public sealed partial class DailyJournalRepositoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletePermanentlyRemovesOnlyExactScopeAndHistoryAndAllowsRecreation(bool accountSpecific)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        Guid account = await SeedAccountAsync(db, "Inactive journal account", false);
        var repository = GetRepository(db);
        var all = (await repository.CreateAsync(new(TradingDate, null, "All"))).Journal!.Entry;
        var scoped = (await repository.CreateAsync(new(TradingDate, account, "Scoped"))).Journal!.Entry;
        var selected = accountSpecific ? scoped : all;
        await repository.UpdateAsync(new(selected.Id, 1, "Completed", false, CompleteReview));

        Assert.Equal(DailyJournalWriteStatus.Deleted, (await repository.DeleteAsync(new(selected.Id, 2))).Status);
        Assert.Null(await repository.GetAsync(TradingDate, selected.TradingAccountId));
        Assert.Empty(await repository.GetHistoryAsync(selected.Id));
        Assert.Single(await repository.GetHistoryAsync(accountSpecific ? all.Id : scoped.Id));
        var recreated = (await repository.CreateAsync(new(TradingDate, selected.TradingAccountId, "New"))).Journal!.Entry;
        Assert.NotEqual(selected.Id, recreated.Id);
        Assert.Equal(1, recreated.Revision);
        Assert.Equal(DailyJournalWriteStatus.NotFound, (await repository.DeleteAsync(new(selected.Id, 2))).Status);
        Assert.NotNull(await repository.GetAsync(TradingDate, selected.TradingAccountId));
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(2, await context.DailyJournals.CountAsync());
        Assert.Equal(2, await context.DailyJournalRevisions.CountAsync());
        Assert.Empty(await context.Trades.ToArrayAsync());
    }

    [Fact]
    public async Task StaleDeletionPreservesNewRevisionAndPreCancellationMakesNoWrites()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var repository = GetRepository(db);
        var entry = (await repository.CreateAsync(new(TradingDate, null, "Original"))).Journal!.Entry;
        await repository.UpdateAsync(new(entry.Id, 1, "Newer", true));
        Assert.Equal(DailyJournalWriteStatus.Conflict, (await repository.DeleteAsync(new(entry.Id, 1))).Status);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.DeleteAsync(new(entry.Id, 2), cancellation.Token));
        Assert.Equal("Newer", (await repository.GetAsync(TradingDate))!.Entry.Text);
        Assert.Equal(2, (await repository.GetHistoryAsync(entry.Id)).Count);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.DeleteAsync(new(Guid.Empty, 1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.DeleteAsync(new(entry.Id, 0)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletionFailureOrCancellationAfterHistoryRemovalRollsBackBothTables(bool cancel)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var reader = GetRepository(db);
        var entry = (await reader.CreateAsync(new(TradingDate, null, "Original"))).Journal!.Entry;
        await reader.UpdateAsync(new(entry.Id, 1, "Newer", true));
        using var cancellation = new CancellationTokenSource();
        var interceptor = cancel ? (Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor)new CancelAfterSaveInterceptor(cancellation)
            : new FailAfterSaveInterceptor();
        var repository = new DailyJournalRepository(await InterceptingFactory.CreateAsync(db, interceptor));
        if (cancel)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.DeleteAsync(new(entry.Id, 2), cancellation.Token));
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.DeleteAsync(new(entry.Id, 2)));
        Assert.Equal("Newer", (await reader.GetAsync(TradingDate))!.Entry.Text);
        Assert.Equal(2, (await reader.GetHistoryAsync(entry.Id)).Count);
    }

    [Fact]
    public async Task ConcurrentUpdateAndDeleteCannotSilentlyRemoveNewerRevision()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var repository = GetRepository(db);
        var entry = (await repository.CreateAsync(new(TradingDate, null, "Original"))).Journal!.Entry;
        var update = Task.Run(() => repository.UpdateAsync(new(entry.Id, 1, "Newer", true)));
        var delete = Task.Run(() => repository.DeleteAsync(new(entry.Id, 1)));
        await Task.WhenAll(update, delete);
        var updated = await update;
        var deleted = await delete;
        if (updated.Status == DailyJournalWriteStatus.Updated)
        {
            Assert.Equal(DailyJournalWriteStatus.Conflict, deleted.Status);
            Assert.Equal("Newer", (await repository.GetAsync(TradingDate))!.Entry.Text);
            Assert.Equal(2, (await repository.GetHistoryAsync(entry.Id)).Count);
        }
        else
        {
            Assert.Equal(DailyJournalWriteStatus.NotFound, updated.Status);
            Assert.Equal(DailyJournalWriteStatus.Deleted, deleted.Status);
            Assert.Null(await repository.GetAsync(TradingDate));
            Assert.Empty(await repository.GetHistoryAsync(entry.Id));
        }
    }
}
