using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Application.Tests.Mistakes;

public sealed class TradingMistakeManagementUseCaseTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 17, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAtUtc = CreatedAtUtc.AddDays(1);

    [Fact]
    public async Task GetDetailsReturnsProjectionAndMissingReturnsNull()
    {
        TradingMistakeDetails details = Details();
        var reader = new RecordingReader(details);

        Assert.Same(details, await new GetTradingMistakeDetailsUseCase(reader)
            .ExecuteAsync(details.Id));
        Assert.Equal(details.Id, reader.RequestedId);
        Assert.Null(await new GetTradingMistakeDetailsUseCase(
            new RecordingReader(null)).ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateNormalizesAndPersistsReferencedOrUnusedMistake()
    {
        TradingMistake mistake = Mistake();
        var store = new RecordingStore(mistake);
        var checker = new RecordingNameChecker();

        UpdateTradingMistakeResult result = await new UpdateTradingMistakeUseCase(
            store, checker, new FixedTimeProvider(UpdatedAtUtc)).ExecuteAsync(
                new(mistake.Id, "  No Confirmation  ", "  Missing signal  "));

        Assert.True(result.WasChanged);
        Assert.Same(mistake, store.UpdatedMistake);
        Assert.Equal("No Confirmation", result.TradingMistake.Name);
        Assert.Equal("Missing signal", result.TradingMistake.Description);
        Assert.Equal(UpdatedAtUtc, result.TradingMistake.UpdatedAtUtc);
        Assert.Equal(mistake.Id, checker.ExcludingMistakeId);
    }

    [Fact]
    public async Task UpdateClearsDescriptionAndCanonicalNoOpDoesNotPersist()
    {
        TradingMistake mistake = Mistake();
        var store = new RecordingStore(mistake);
        var useCase = new UpdateTradingMistakeUseCase(
            store, new RecordingNameChecker(), new FixedTimeProvider(UpdatedAtUtc));

        UpdateTradingMistakeResult changed = await useCase.ExecuteAsync(
            new(mistake.Id, mistake.Name, "   "));
        Assert.True(changed.WasChanged);
        Assert.Null(changed.TradingMistake.Description);

        UpdateTradingMistakeResult noOp = await useCase.ExecuteAsync(
            new(mistake.Id, $"  {mistake.Name}  ", null));
        Assert.False(noOp.WasChanged);
        Assert.Equal(1, store.UpdateCallCount);
        Assert.Equal(UpdatedAtUtc, noOp.TradingMistake.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateRejectsInvalidDuplicateAndMissingWithoutPersisting()
    {
        TradingMistake mistake = Mistake();
        var store = new RecordingStore(mistake);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new UpdateTradingMistakeUseCase(
                store, new RecordingNameChecker(), new FixedTimeProvider(UpdatedAtUtc))
                .ExecuteAsync(new(mistake.Id, " ", null)));
        Assert.Equal(0, store.UpdateCallCount);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateTradingMistakeUseCase(
                store,
                new RecordingNameChecker { Exists = true },
                new FixedTimeProvider(UpdatedAtUtc))
                .ExecuteAsync(new(mistake.Id, "Duplicate", mistake.Description)));
        Assert.Equal(0, store.UpdateCallCount);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new UpdateTradingMistakeUseCase(
                new RecordingStore(null),
                new RecordingNameChecker(),
                new FixedTimeProvider(UpdatedAtUtc))
                .ExecuteAsync(new(Guid.NewGuid(), "Missing", null)));
    }

    [Fact]
    public async Task DeleteUnusedSucceedsReferencedIsBlockedAndMissingThrows()
    {
        TradingMistake mistake = Mistake();
        var deletionStore = new RecordingDeletionStore();
        Assert.Equal(DeleteTradingMistakeResult.Deleted,
            await new DeleteTradingMistakeUseCase(
                new RecordingStore(mistake), deletionStore).ExecuteAsync(mistake.Id));
        Assert.Equal(1, deletionStore.DeleteCallCount);

        var referenced = new RecordingDeletionStore { HasTradeMistakes = true };
        Assert.Equal(DeleteTradingMistakeResult.Referenced,
            await new DeleteTradingMistakeUseCase(
                new RecordingStore(mistake), referenced).ExecuteAsync(mistake.Id));
        Assert.Equal(0, referenced.DeleteCallCount);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new DeleteTradingMistakeUseCase(
                new RecordingStore(null), new RecordingDeletionStore())
                .ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteConstraintRaceReturnsReferenced()
    {
        TradingMistake mistake = Mistake();
        var deletionStore = new RecordingDeletionStore
        {
            DeleteException = new TradingMistakeDeleteBlockedException("Referenced."),
        };

        Assert.Equal(DeleteTradingMistakeResult.Referenced,
            await new DeleteTradingMistakeUseCase(
                new RecordingStore(mistake), deletionStore).ExecuteAsync(mistake.Id));
    }

    private static TradingMistake Mistake() =>
        new("FOMO Entry", "Entered from fear of missing out", CreatedAtUtc);

    private static TradingMistakeDetails Details() =>
        new(Guid.NewGuid(), "FOMO Entry", "Entered from fear of missing out", true,
            CreatedAtUtc, CreatedAtUtc);

    private sealed class RecordingReader(TradingMistakeDetails? details)
        : ITradingMistakeReader
    {
        public Guid RequestedId { get; private set; }
        public Task<IReadOnlyList<TradingMistakeListItem>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<TradingMistakeDetails?> GetByIdAsync(
            Guid mistakeId,
            CancellationToken cancellationToken = default)
        {
            RequestedId = mistakeId;
            return Task.FromResult(details?.Id == mistakeId ? details : null);
        }
    }

    private sealed class RecordingStore(TradingMistake? mistake) : ITradingMistakeStore
    {
        public TradingMistake? UpdatedMistake { get; private set; }
        public int UpdateCallCount { get; private set; }
        public Task AddAsync(TradingMistake value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<TradingMistake?> GetByIdAsync(
            Guid mistakeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(mistake?.Id == mistakeId ? mistake : null);
        public Task UpdateAsync(
            TradingMistake value,
            CancellationToken cancellationToken = default)
        {
            UpdateCallCount++;
            UpdatedMistake = value;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingNameChecker : ITradingMistakeNameChecker
    {
        public bool Exists { get; init; }
        public Guid? ExcludingMistakeId { get; private set; }
        public Task<bool> ExistsAsync(
            string normalizedName,
            Guid? excludingMistakeId = null,
            CancellationToken cancellationToken = default)
        {
            ExcludingMistakeId = excludingMistakeId;
            return Task.FromResult(Exists);
        }
    }

    private sealed class RecordingDeletionStore : ITradingMistakeDeletionStore
    {
        public bool HasTradeMistakes { get; init; }
        public Exception? DeleteException { get; init; }
        public int DeleteCallCount { get; private set; }
        public Task<bool> HasTradeMistakesAsync(
            Guid mistakeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(HasTradeMistakes);
        public Task DeleteAsync(
            Guid mistakeId,
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
