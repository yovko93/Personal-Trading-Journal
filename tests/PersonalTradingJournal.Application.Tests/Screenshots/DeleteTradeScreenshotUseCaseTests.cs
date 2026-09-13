using PersonalTradingJournal.Application.Screenshots;

namespace PersonalTradingJournal.Application.Tests.Screenshots;

public sealed class DeleteTradeScreenshotUseCaseTests
{
    private static readonly Guid TradeId =
        Guid.Parse("89d41a6f-d36e-44cd-81f4-594558cad151");
    private static readonly Guid ScreenshotId =
        Guid.Parse("c1831e60-fef3-4bad-abbe-ef4bdf18d106");
    private const string StorageKey = "opaque/screenshots/asset-01.png";

    [Fact]
    public async Task ExecuteAsyncCommitsMetadataBeforeNonCancellableFileCleanup()
    {
        var operations = new List<string>();
        var deletionStore = new RecordingDeletionStore(operations);
        var fileStorage = new RecordingFileStorage(operations);
        var useCase = new DeleteTradeScreenshotUseCase(
            deletionStore,
            fileStorage);
        using var cancellationSource = new CancellationTokenSource();

        DeleteTradeScreenshotResult result = await useCase.ExecuteAsync(
            CreateCommand(),
            cancellationSource.Token);

        Assert.True(result.FileCleanupSucceeded);
        Assert.Equal(TradeId, deletionStore.TradeId);
        Assert.Equal(ScreenshotId, deletionStore.ScreenshotId);
        Assert.Equal(cancellationSource.Token, deletionStore.CancellationToken);
        Assert.Equal(StorageKey, fileStorage.DeletedStorageKey);
        Assert.Equal(CancellationToken.None, fileStorage.CancellationToken);
        Assert.False(fileStorage.CancellationToken.CanBeCanceled);
        Assert.Equal(["delete-metadata", "delete-file"], operations);
    }

    [Fact]
    public async Task CleanupFailureReturnsWarningResultWithoutThrowing()
    {
        var expectedException = new IOException("Physical cleanup failed.");
        var fileStorage = new RecordingFileStorage
        {
            Exception = expectedException,
        };
        var useCase = new DeleteTradeScreenshotUseCase(
            new RecordingDeletionStore(),
            fileStorage);

        DeleteTradeScreenshotResult result =
            await useCase.ExecuteAsync(CreateCommand());

        Assert.False(result.FileCleanupSucceeded);
        Assert.Equal(1, fileStorage.CallCount);
    }

    [Fact]
    public async Task MissingMetadataThrowsAndSkipsFileCleanup()
    {
        var deletionStore = new RecordingDeletionStore
        {
            DeletionInfo = null,
        };
        var fileStorage = new RecordingFileStorage();
        var useCase = new DeleteTradeScreenshotUseCase(
            deletionStore,
            fileStorage);

        KeyNotFoundException exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => useCase.ExecuteAsync(CreateCommand()));

        Assert.Contains(ScreenshotId.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(TradeId.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, fileStorage.CallCount);
    }

    [Fact]
    public async Task DatabaseFailurePropagatesAndSkipsFileCleanup()
    {
        var expectedException = new InvalidOperationException("Database failed.");
        var deletionStore = new RecordingDeletionStore
        {
            Exception = expectedException,
        };
        var fileStorage = new RecordingFileStorage();
        var useCase = new DeleteTradeScreenshotUseCase(
            deletionStore,
            fileStorage);

        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => useCase.ExecuteAsync(CreateCommand()));

        Assert.Same(expectedException, actualException);
        Assert.Equal(0, fileStorage.CallCount);
    }

    [Fact]
    public async Task DatabaseCancellationPropagatesAndSkipsFileCleanup()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var expectedException =
            new OperationCanceledException(cancellationSource.Token);
        var deletionStore = new RecordingDeletionStore
        {
            Exception = expectedException,
        };
        var fileStorage = new RecordingFileStorage();
        var useCase = new DeleteTradeScreenshotUseCase(
            deletionStore,
            fileStorage);

        OperationCanceledException actualException =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => useCase.ExecuteAsync(
                    CreateCommand(),
                    cancellationSource.Token));

        Assert.Same(expectedException, actualException);
        Assert.Equal(0, fileStorage.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EmptyIdentifiersAreRejectedBeforeDependencies(bool emptyTradeId)
    {
        var deletionStore = new RecordingDeletionStore();
        var fileStorage = new RecordingFileStorage();
        var useCase = new DeleteTradeScreenshotUseCase(
            deletionStore,
            fileStorage);
        var command = new DeleteTradeScreenshotCommand(
            emptyTradeId ? Guid.Empty : TradeId,
            emptyTradeId ? ScreenshotId : Guid.Empty);

        _ = await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal(0, deletionStore.CallCount);
        Assert.Equal(0, fileStorage.CallCount);
    }

    [Fact]
    public async Task NullCommandIsRejectedBeforeDependencies()
    {
        var deletionStore = new RecordingDeletionStore();
        var fileStorage = new RecordingFileStorage();
        var useCase = new DeleteTradeScreenshotUseCase(
            deletionStore,
            fileStorage);

        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(null!));

        Assert.Equal(0, deletionStore.CallCount);
        Assert.Equal(0, fileStorage.CallCount);
    }

    [Fact]
    public async Task CancellationAfterCommittedDeleteDoesNotPreventCleanupOrReclassifyResult()
    {
        using var cancellationSource = new CancellationTokenSource();
        var deletionStore = new RecordingDeletionStore
        {
            OnDelete = cancellationSource.Cancel,
        };
        var fileStorage = new RecordingFileStorage();
        var useCase = new DeleteTradeScreenshotUseCase(
            deletionStore,
            fileStorage);

        DeleteTradeScreenshotResult result = await useCase.ExecuteAsync(
            CreateCommand(),
            cancellationSource.Token);

        Assert.True(cancellationSource.IsCancellationRequested);
        Assert.True(result.FileCleanupSucceeded);
        Assert.Equal(1, fileStorage.CallCount);
        Assert.Equal(CancellationToken.None, fileStorage.CancellationToken);
    }

    private static DeleteTradeScreenshotCommand CreateCommand() =>
        new(TradeId, ScreenshotId);

    private sealed class RecordingDeletionStore(List<string>? operations = null)
        : ITradeScreenshotDeletionStore
    {
        public TradeScreenshotDeletionInfo? DeletionInfo { get; set; } =
            new(
                DeleteTradeScreenshotUseCaseTests.ScreenshotId,
                DeleteTradeScreenshotUseCaseTests.TradeId,
                StorageKey);

        public Exception? Exception { get; set; }

        public Action? OnDelete { get; set; }

        public int CallCount { get; private set; }

        public Guid TradeId { get; private set; }

        public Guid ScreenshotId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<TradeScreenshotDeletionInfo?> DeleteAsync(
            Guid tradeId,
            Guid screenshotId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            TradeId = tradeId;
            ScreenshotId = screenshotId;
            CancellationToken = cancellationToken;
            operations?.Add("delete-metadata");
            OnDelete?.Invoke();

            return Exception is null
                ? Task.FromResult(DeletionInfo)
                : Task.FromException<TradeScreenshotDeletionInfo?>(Exception);
        }
    }

    private sealed class RecordingFileStorage(List<string>? operations = null)
        : ITradeScreenshotFileStorage
    {
        public int CallCount { get; private set; }

        public string? DeletedStorageKey { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Exception? Exception { get; set; }

        public Task<string> StoreAsync(
            Stream content,
            string fileExtension,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Stream?> OpenReadAsync(
            string storageKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteIfExistsAsync(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            DeletedStorageKey = storageKey;
            CancellationToken = cancellationToken;
            operations?.Add("delete-file");

            return Exception is null
                ? Task.CompletedTask
                : Task.FromException(Exception);
        }
    }
}
