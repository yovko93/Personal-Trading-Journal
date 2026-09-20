using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Tests.Setups;

public sealed class TradingSetupManagementUseCaseTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAtUtc = CreatedAtUtc.AddDays(1);

    [Fact]
    public async Task GetDetailsReturnsProjectionAndMissingReturnsNull()
    {
        TradingSetupDetails details = Details();
        var reader = new RecordingReader(details);

        Assert.Same(details, await new GetTradingSetupDetailsUseCase(reader)
            .ExecuteAsync(details.Id));
        Assert.Equal(details.Id, reader.RequestedId);
        Assert.Null(await new GetTradingSetupDetailsUseCase(
            new RecordingReader(null)).ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateNormalizesAndPersistsReferencedOrUnusedSetup()
    {
        TradingSetup setup = Setup();
        var store = new RecordingStore(setup);
        var checker = new RecordingNameChecker();

        UpdateTradingSetupResult result = await new UpdateTradingSetupUseCase(
            store, checker, new FixedTimeProvider(UpdatedAtUtc)).ExecuteAsync(
                new(setup.Id, "  NY Open Reversal  ", "  Opening reversal  "));

        Assert.True(result.WasChanged);
        Assert.Same(setup, store.UpdatedSetup);
        Assert.Equal("NY Open Reversal", result.TradingSetup.Name);
        Assert.Equal("Opening reversal", result.TradingSetup.Description);
        Assert.Equal(UpdatedAtUtc, result.TradingSetup.UpdatedAtUtc);
        Assert.Equal(setup.Id, checker.ExcludingSetupId);
    }

    [Fact]
    public async Task UpdateClearsDescriptionAndCanonicalNoOpDoesNotPersist()
    {
        TradingSetup setup = Setup();
        var store = new RecordingStore(setup);
        var useCase = new UpdateTradingSetupUseCase(
            store, new RecordingNameChecker(), new FixedTimeProvider(UpdatedAtUtc));

        UpdateTradingSetupResult changed = await useCase.ExecuteAsync(
            new(setup.Id, setup.Name, "   "));
        Assert.True(changed.WasChanged);
        Assert.Null(changed.TradingSetup.Description);

        UpdateTradingSetupResult noOp = await useCase.ExecuteAsync(
            new(setup.Id, $"  {setup.Name}  ", null));
        Assert.False(noOp.WasChanged);
        Assert.Equal(1, store.UpdateCallCount);
        Assert.Equal(UpdatedAtUtc, noOp.TradingSetup.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateRejectsInvalidDuplicateAndMissingWithoutPersisting()
    {
        TradingSetup setup = Setup();
        var store = new RecordingStore(setup);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new UpdateTradingSetupUseCase(
                store, new RecordingNameChecker(), new FixedTimeProvider(UpdatedAtUtc))
                .ExecuteAsync(new(setup.Id, " ", null)));
        Assert.Equal(0, store.UpdateCallCount);

        var duplicateChecker = new RecordingNameChecker { Exists = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateTradingSetupUseCase(
                store, duplicateChecker, new FixedTimeProvider(UpdatedAtUtc))
                .ExecuteAsync(new(setup.Id, "Duplicate", setup.Description)));
        Assert.Equal(0, store.UpdateCallCount);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new UpdateTradingSetupUseCase(
                new RecordingStore(null), new RecordingNameChecker(),
                new FixedTimeProvider(UpdatedAtUtc))
                .ExecuteAsync(new(Guid.NewGuid(), "Missing", null)));
    }

    [Fact]
    public async Task DeleteUnusedSucceedsReferencedIsBlockedAndMissingThrows()
    {
        TradingSetup setup = Setup();
        var deletionStore = new RecordingDeletionStore();
        Assert.Equal(DeleteTradingSetupResult.Deleted,
            await new DeleteTradingSetupUseCase(
                new RecordingStore(setup), deletionStore).ExecuteAsync(setup.Id));
        Assert.Equal(1, deletionStore.DeleteCallCount);

        var referenced = new RecordingDeletionStore { HasTrades = true };
        Assert.Equal(DeleteTradingSetupResult.Referenced,
            await new DeleteTradingSetupUseCase(
                new RecordingStore(setup), referenced).ExecuteAsync(setup.Id));
        Assert.Equal(0, referenced.DeleteCallCount);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new DeleteTradingSetupUseCase(
                new RecordingStore(null), new RecordingDeletionStore())
                .ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteConstraintRaceReturnsReferenced()
    {
        TradingSetup setup = Setup();
        var deletionStore = new RecordingDeletionStore
        {
            DeleteException = new TradingSetupDeleteBlockedException("Referenced."),
        };

        Assert.Equal(DeleteTradingSetupResult.Referenced,
            await new DeleteTradingSetupUseCase(
                new RecordingStore(setup), deletionStore).ExecuteAsync(setup.Id));
    }

    private static TradingSetup Setup() =>
        new("Silver Bullet", "Timed liquidity model", CreatedAtUtc);

    private static TradingSetupDetails Details() =>
        new(Guid.NewGuid(), "Silver Bullet", "Timed liquidity model", true,
            CreatedAtUtc, CreatedAtUtc);

    private sealed class RecordingReader(TradingSetupDetails? details)
        : ITradingSetupReader
    {
        public Guid RequestedId { get; private set; }
        public Task<IReadOnlyList<TradingSetupListItem>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<TradingSetupDetails?> GetByIdAsync(
            Guid setupId,
            CancellationToken cancellationToken = default)
        {
            RequestedId = setupId;
            return Task.FromResult(details?.Id == setupId ? details : null);
        }
    }

    private sealed class RecordingStore(TradingSetup? setup) : ITradingSetupStore
    {
        public TradingSetup? UpdatedSetup { get; private set; }
        public int UpdateCallCount { get; private set; }
        public Task AddAsync(TradingSetup value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<TradingSetup?> GetByIdAsync(
            Guid setupId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(setup?.Id == setupId ? setup : null);
        public Task UpdateAsync(
            TradingSetup value,
            CancellationToken cancellationToken = default)
        {
            UpdateCallCount++;
            UpdatedSetup = value;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingNameChecker : ITradingSetupNameChecker
    {
        public bool Exists { get; init; }
        public Guid? ExcludingSetupId { get; private set; }
        public Task<bool> ExistsAsync(
            string normalizedName,
            Guid? excludingSetupId = null,
            CancellationToken cancellationToken = default)
        {
            ExcludingSetupId = excludingSetupId;
            return Task.FromResult(Exists);
        }
    }

    private sealed class RecordingDeletionStore : ITradingSetupDeletionStore
    {
        public bool HasTrades { get; init; }
        public Exception? DeleteException { get; init; }
        public int DeleteCallCount { get; private set; }
        public Task<bool> HasTradesAsync(
            Guid setupId,
            CancellationToken cancellationToken = default) => Task.FromResult(HasTrades);
        public Task DeleteAsync(
            Guid setupId,
            CancellationToken cancellationToken = default)
        {
            DeleteCallCount++;
            return DeleteException is null
                ? Task.CompletedTask
                : Task.FromException(DeleteException);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
