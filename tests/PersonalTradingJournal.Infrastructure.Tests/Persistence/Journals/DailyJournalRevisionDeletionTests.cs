using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Infrastructure.Journals;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Journals;

public sealed partial class DailyJournalRepositoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OlderRevisionDeletionPreservesHeadAndScopeAndFutureNumbers(bool accountScoped)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        Guid? account = accountScoped ? await SeedAccountAsync(db, "P 21", false) : null;
        var repository = GetRepository(db);
        var writer = Assert.IsAssignableFrom<IDailyJournalRevisionWriter>(repository);
        var entry = (await repository.CreateAsync(new(TradingDate, account, "First"))).Journal!.Entry;
        await repository.UpdateAsync(new(entry.Id, 1, "Second", true));
        var head = (await repository.UpdateAsync(new(entry.Id, 2, "Completed current", false, CompleteReview))).Journal!.Entry;
        var other = (await repository.CreateAsync(new(TradingDate.AddDays(1), account, "Other"))).Journal!.Entry;

        Assert.Equal(DeleteJournalRevisionStatus.Deleted, await writer.DeleteRevisionAsync(new(entry.Id, 2, 3)));
        var unchanged = (await repository.GetAsync(TradingDate, account))!.Entry;
        Assert.Equal(head.Text, unchanged.Text);
        Assert.Equal(head.Review, unchanged.Review);
        Assert.Equal(head.UpdatedAtUtc, unchanged.UpdatedAtUtc);
        Assert.False(unchanged.IsDraft);
        Assert.Equal(3, unchanged.Revision);
        Assert.Equal(new long[] { 1, 3 }, (await repository.GetHistoryAsync(entry.Id)).Select(r => r.Revision));
        Assert.Single(await repository.GetHistoryAsync(other.Id));
        Assert.Equal(DeleteJournalRevisionStatus.CurrentRevisionProtected, await writer.DeleteRevisionAsync(new(entry.Id, 3, 3)));
        Assert.Equal(DeleteJournalRevisionStatus.NotFound, await writer.DeleteRevisionAsync(new(entry.Id, 2, 3)));
        Assert.Equal(DailyJournalWriteStatus.Conflict, (await repository.UpdateAsync(new(entry.Id, 2, "Stale", true))).Status);
        var reopened = (await repository.UpdateAsync(new(entry.Id, 3, head.Text, true, head.Review))).Journal!.Entry;
        Assert.Equal(4, reopened.Revision);
        var saved = (await repository.UpdateAsync(new(entry.Id, 4, "Next completed", false))).Journal!.Entry;
        Assert.Equal(5, saved.Revision);
        Assert.Equal(new long[] { 1, 3, 4, 5 }, (await repository.GetHistoryAsync(entry.Id)).Select(r => r.Revision));
    }

    [Fact]
    public async Task RevisionDeletionChecksHeadTokenAndCancellationWithoutWrites()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var repository = GetRepository(db);
        var writer = Assert.IsAssignableFrom<IDailyJournalRevisionWriter>(repository);
        var entry = (await repository.CreateAsync(new(TradingDate, null, "Original"))).Journal!.Entry;
        Assert.Equal(DeleteJournalRevisionStatus.CurrentRevisionProtected, await writer.DeleteRevisionAsync(new(entry.Id, 1, 1)));
        await repository.UpdateAsync(new(entry.Id, 1, "Newer", true));
        Assert.Equal(DeleteJournalRevisionStatus.Conflict, await writer.DeleteRevisionAsync(new(entry.Id, 1, 1)));
        Assert.Equal(DeleteJournalRevisionStatus.NotFound, await writer.DeleteRevisionAsync(new(Guid.NewGuid(), 1, 2)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.DeleteRevisionAsync(new(entry.Id, 1, 2), cancellation.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => writer.DeleteRevisionAsync(new(Guid.Empty, 1, 2)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => writer.DeleteRevisionAsync(new(entry.Id, 0, 2)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => writer.DeleteRevisionAsync(new(entry.Id, 1, 0)));
        Assert.Equal(2, (await repository.GetHistoryAsync(entry.Id)).Count);
        Assert.Equal("Newer", (await repository.GetAsync(TradingDate))!.Entry.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OlderRevisionDeleteFailureAfterSqlRollsBackOnlySnapshotRemoval(bool cancel)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var reader = GetRepository(db);
        var entry = (await reader.CreateAsync(new(TradingDate, null, "First"))).Journal!.Entry;
        await reader.UpdateAsync(new(entry.Id, 1, "Second", true));
        using var cancellation = new CancellationTokenSource();
        var interceptor = cancel ? (Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor)new CancelAfterSaveInterceptor(cancellation)
            : new FailAfterSaveInterceptor();
        var repository = new DailyJournalRepository(await InterceptingFactory.CreateAsync(db, interceptor));
        if (cancel)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.DeleteRevisionAsync(new(entry.Id, 1, 2), cancellation.Token));
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.DeleteRevisionAsync(new(entry.Id, 1, 2)));
        Assert.Equal(new long[] { 1, 2 }, (await reader.GetHistoryAsync(entry.Id)).Select(r => r.Revision));
        Assert.Equal("Second", (await reader.GetAsync(TradingDate))!.Entry.Text);
    }
}
