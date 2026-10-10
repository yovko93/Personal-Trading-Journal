using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Application.Screenshots;

namespace PersonalTradingJournal.Application.Tests.Trades;

public sealed class DeleteAccountTradesUseCaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitPrecedesCleanupAndFailureIsCountedWithoutAbandoningOtherFiles(bool fail)
    {
        using var cancel = new CancellationTokenSource();
        var operations = new List<string>();
        var store = new Store(() => { operations.Add("commit"); cancel.Cancel(); return new(AccountTradeDeletionStatus.Deleted, 2, ["one.png", "one.png", "two.png"]); });
        var files = new Files((key, token) => {
            Assert.False(token.CanBeCanceled); operations.Add(key);
            if (fail && key == "one.png") throw new IOException("Synthetic");
        });
        var plan = new AccountTradeDeletionPlan(Guid.NewGuid(), "Synthetic", 2, "version");
        var result = await new DeleteAccountTradesUseCase(store, files).ExecuteAsync(plan, cancel.Token);
        Assert.Equal(["commit", "one.png", "two.png"], operations);
        Assert.Equal(fail ? 1 : 0, result.FailedFileCleanupCount);
        Assert.Equal(2, result.DeletedCount);
        Assert.Equal(AccountTradeDeletionStatus.Deleted, result.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedOrFailedTransactionNeverCleansFiles(bool throws)
    {
        var store = new Store(() => throws ? throw new IOException("Rollback") : new(AccountTradeDeletionStatus.Changed, 0, []));
        var files = new Files((_, _) => Assert.Fail("Must not touch files before commit."));
        var useCase = new DeleteAccountTradesUseCase(store, files);
        var plan = new AccountTradeDeletionPlan(Guid.NewGuid(), "Synthetic", 1, "version");
        if (throws) await Assert.ThrowsAsync<IOException>(() => useCase.ExecuteAsync(plan));
        else Assert.Equal(AccountTradeDeletionStatus.Changed, (await useCase.ExecuteAsync(plan)).Status);
    }

    private sealed class Store(Func<AccountTradeDeletionCommit> commit) : IAccountTradeDeletionStore
    {
        public Task<AccountTradeDeletionPlan?> PrepareAsync(Guid id, CancellationToken token) => throw new NotSupportedException();
        public Task<AccountTradeDeletionCommit> DeleteAsync(AccountTradeDeletionPlan plan, CancellationToken token) => Task.FromResult(commit());
    }
    private sealed class Files(Action<string, CancellationToken> delete) : ITradeScreenshotFileStorage
    {
        public Task<string> StoreAsync(Stream content, string extension, CancellationToken token) => throw new NotSupportedException();
        public Task<Stream?> OpenReadAsync(string key, CancellationToken token) => throw new NotSupportedException();
        public Task DeleteIfExistsAsync(string key, CancellationToken token) { delete(key, token); return Task.CompletedTask; }
    }
}
