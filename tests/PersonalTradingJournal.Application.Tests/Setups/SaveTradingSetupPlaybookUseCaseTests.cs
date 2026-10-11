using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Tests.Setups;

public sealed class SaveTradingSetupPlaybookUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static PlaybookGuidance Guidance() => new("context", "entry", "confirmation", "invalidation", "execution");

    [Fact]
    public async Task CreatesAndAppendsOwnedRevisionsWithoutChangingSetup()
    {
        var store = new Store();
        var useCase = new SaveTradingSetupPlaybookUseCase(store, store, new Clock());
        var first = await useCase.ExecuteAsync(new(store.Setup!.Id, 0, Guidance()));
        var second = await useCase.ExecuteAsync(new(store.Setup.Id, 1, Guidance()));
        Assert.Equal(new[] { 1, 2 }, store.History.Select(item => item.Revision));
        Assert.Same(first, store.History[0]);
        Assert.Same(second, store.History[1]);
        Assert.Equal(store.Setup.Id, second.TradingSetupId);
        Assert.Equal(Now, store.Setup.UpdatedAtUtc);
        Assert.Equal(Now.AddHours(1), second.RecordedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleReadOrCommitRaceNeverOverwritesHistory(bool commitRace)
    {
        var store = new Store();
        var useCase = new SaveTradingSetupPlaybookUseCase(store, store, new Clock());
        var first = await useCase.ExecuteAsync(new(store.Setup!.Id, 0, Guidance()));
        store.RejectAppend = commitRace;
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(new(store.Setup.Id, commitRace ? 1 : 0, Guidance())));
        Assert.Same(first, Assert.Single(store.History));
    }

    [Fact]
    public async Task MissingOrArchivedOwnerCannotBeAuthored()
    {
        var store = new Store();
        var useCase = new SaveTradingSetupPlaybookUseCase(store, store, new Clock());
        store.Setup!.Deactivate(Now);
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(new(store.Setup.Id, 0, Guidance())));
        store.Setup = null;
        await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(new(Guid.NewGuid(), 0, Guidance())));
        Assert.Empty(store.History);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeReadOrAfterReadNeverAppends(bool afterRead)
    {
        using var cancellation = new CancellationTokenSource();
        var store = new Store { CancelAfterRead = afterRead ? cancellation : null };
        if (!afterRead) cancellation.Cancel();
        var useCase = new SaveTradingSetupPlaybookUseCase(store, store, new Clock());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => useCase.ExecuteAsync(new(store.Setup!.Id, 0, Guidance()), cancellation.Token));
        Assert.Equal(afterRead ? 1 : 0, store.Reads);
        Assert.Empty(store.History);
        if (afterRead) Assert.Equal(cancellation.Token, store.ReadToken);
    }

    [Fact]
    public async Task InvalidCommandsFailBeforeReading()
    {
        var store = new Store();
        var useCase = new SaveTradingSetupPlaybookUseCase(store, store, new Clock());
        await Assert.ThrowsAsync<ArgumentNullException>(() => useCase.ExecuteAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(new(Guid.Empty, 0, Guidance())));
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(new(store.Setup!.Id, -1, Guidance())));
        await Assert.ThrowsAsync<ArgumentNullException>(() => useCase.ExecuteAsync(new(store.Setup!.Id, 0, null!)));
        Assert.Equal(0, store.Reads);
    }

    [Fact]
    public async Task WriteFailureLeavesOriginalRevisionAvailable()
    {
        var store = new Store();
        var useCase = new SaveTradingSetupPlaybookUseCase(store, store, new Clock());
        var first = await useCase.ExecuteAsync(new(store.Setup!.Id, 0, Guidance()));
        store.FailWrite = true;
        await Assert.ThrowsAsync<IOException>(() => useCase.ExecuteAsync(new(store.Setup.Id, 1, Guidance())));
        Assert.Same(first, Assert.Single(store.History));
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now.AddHours(1);
    }

    private sealed class Store : ITradingSetupStore, ITradingSetupPlaybookStore
    {
        public TradingSetup? Setup { get; set; } = new("Synthetic NQ", null, Now);
        public List<TradingSetupPlaybookRevision> History { get; } = [];
        public bool RejectAppend { get; set; }
        public bool FailWrite { get; set; }
        public int Reads { get; private set; }
        public CancellationToken ReadToken { get; private set; }
        public CancellationTokenSource? CancelAfterRead { get; init; }
        public Task<TradingSetup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Reads++;
            ReadToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Setup?.Id == id ? Setup : null);
        }
        public Task<TradingSetupPlaybookRevision?> GetLatestAsync(Guid id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CancelAfterRead?.Cancel();
            return Task.FromResult(History.LastOrDefault(item => item.TradingSetupId == id));
        }
        public Task<bool> TryAppendAsync(TradingSetupPlaybookRevision revision, int expectedRevision,
            DateTimeOffset expectedSetupUpdatedAtUtc, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailWrite) throw new IOException("Synthetic failure");
            if (RejectAppend || Setup is null || !Setup.IsActive || Setup.Id != revision.TradingSetupId ||
                Setup.UpdatedAtUtc != expectedSetupUpdatedAtUtc || (History.LastOrDefault()?.Revision ?? 0) != expectedRevision)
                return Task.FromResult(false);
            History.Add(revision);
            return Task.FromResult(true);
        }
        public Task AddAsync(TradingSetup setup, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(TradingSetup setup, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
