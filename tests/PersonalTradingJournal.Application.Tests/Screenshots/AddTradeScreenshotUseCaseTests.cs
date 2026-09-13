using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Screenshots;

namespace PersonalTradingJournal.Application.Tests.Screenshots;

public sealed class AddTradeScreenshotUseCaseTests
{
    private static readonly Guid TradeId = Guid.Parse("2e394314-b081-4914-bb14-c6bb901e3cb1");
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 14, 13, 45, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 13, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsyncCoordinatesSuccessfulWorkflowAndReturnsScreenshotId()
    {
        var operations = new List<string>();
        var existenceReader = new RecordingTradeExistenceReader(operations);
        var fileStorage = new RecordingFileStorage(operations)
        {
            StorageKey = "09f85f6752854af49c9a596e834a8341.png",
        };
        var screenshotStore = new RecordingScreenshotStore(operations);
        var timeProvider = new FixedTimeProvider(CreatedAtUtc);
        var useCase = new AddTradeScreenshotUseCase(
            existenceReader,
            fileStorage,
            screenshotStore,
            timeProvider);
        using var cancellationSource = new CancellationTokenSource();
        using var content = new MemoryStream([1, 2, 3]);
        var command = new AddTradeScreenshotCommand(
            TradeId,
            TradeScreenshotType.Entry,
            content,
            "NQ-entry.PNG",
            CapturedAtUtc,
            "  5m  ",
            "  Opening context  ");

        Guid result = await useCase.ExecuteAsync(command, cancellationSource.Token);

        TradeScreenshot screenshot = Assert.IsType<TradeScreenshot>(
            screenshotStore.AddedScreenshot);
        Assert.NotEqual(Guid.Empty, result);
        Assert.Equal(screenshot.Id, result);
        Assert.Equal(TradeId, screenshot.TradeId);
        Assert.Equal(TradeScreenshotType.Entry, screenshot.Type);
        Assert.Equal("NQ-entry.PNG", screenshot.FileName);
        Assert.Equal(CapturedAtUtc, screenshot.CapturedAtUtc);
        Assert.Equal("5m", screenshot.Timeframe);
        Assert.Equal("Opening context", screenshot.Description);
        Assert.Equal(CreatedAtUtc, screenshot.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, screenshot.UpdatedAtUtc);
        Assert.Equal(fileStorage.StorageKey, screenshot.StorageKey);
        Assert.Equal(".PNG", fileStorage.RequestedExtension);
        Assert.Same(content, fileStorage.Content);
        Assert.Equal(TradeId, existenceReader.RequestedTradeId);
        Assert.Equal(cancellationSource.Token, existenceReader.CancellationToken);
        Assert.Equal(cancellationSource.Token, fileStorage.StoreCancellationToken);
        Assert.Equal(cancellationSource.Token, screenshotStore.CancellationToken);
        Assert.Equal(["exists", "store-file", "store-metadata"], operations);
        Assert.Equal(1, timeProvider.CallCount);
        Assert.Equal(0, fileStorage.DeleteCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsMissingTradeBeforeStorage()
    {
        var existenceReader = new RecordingTradeExistenceReader
        {
            Exists = false,
        };
        var fileStorage = new RecordingFileStorage();
        var screenshotStore = new RecordingScreenshotStore();
        var useCase = CreateUseCase(existenceReader, fileStorage, screenshotStore);
        AddTradeScreenshotCommand command = CreateCommand();

        KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => useCase.ExecuteAsync(command));

        Assert.Contains(TradeId.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, existenceReader.CallCount);
        Assert.Equal(0, fileStorage.StoreCallCount);
        Assert.Equal(0, fileStorage.DeleteCallCount);
        Assert.Equal(0, screenshotStore.CallCount);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsEmptyTradeIdBeforeDependencies()
    {
        var existenceReader = new RecordingTradeExistenceReader();
        var fileStorage = new RecordingFileStorage();
        var screenshotStore = new RecordingScreenshotStore();
        var useCase = CreateUseCase(existenceReader, fileStorage, screenshotStore);
        AddTradeScreenshotCommand command = CreateCommand() with
        {
            TradeId = Guid.Empty,
        };

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(command));

        AssertNoDependencyCalls(existenceReader, fileStorage, screenshotStore);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" nq.png")]
    [InlineData("nq.png ")]
    [InlineData("C:\\captures\\nq.png")]
    [InlineData("captures/nq.png")]
    [InlineData("..\\nq.png")]
    [InlineData("../nq.png")]
    [InlineData("nested\\nq.png")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("nq")]
    [InlineData("nq.")]
    [InlineData("nq?.png")]
    public async Task ExecuteAsyncRejectsInvalidFileNameBeforeDependencies(string fileName)
    {
        var existenceReader = new RecordingTradeExistenceReader();
        var fileStorage = new RecordingFileStorage();
        var screenshotStore = new RecordingScreenshotStore();
        var useCase = CreateUseCase(existenceReader, fileStorage, screenshotStore);
        AddTradeScreenshotCommand command = CreateCommand() with
        {
            FileName = fileName,
        };

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(command));

        AssertNoDependencyCalls(existenceReader, fileStorage, screenshotStore);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsNullContentBeforeDependencies()
    {
        var existenceReader = new RecordingTradeExistenceReader();
        var fileStorage = new RecordingFileStorage();
        var screenshotStore = new RecordingScreenshotStore();
        var useCase = CreateUseCase(existenceReader, fileStorage, screenshotStore);
        AddTradeScreenshotCommand command = CreateCommand() with
        {
            Content = null!,
        };

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(command));

        AssertNoDependencyCalls(existenceReader, fileStorage, screenshotStore);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsUnreadableContentBeforeDependencies()
    {
        var existenceReader = new RecordingTradeExistenceReader();
        var fileStorage = new RecordingFileStorage();
        var screenshotStore = new RecordingScreenshotStore();
        var useCase = CreateUseCase(existenceReader, fileStorage, screenshotStore);
        using var content = new WriteOnlyStream();
        AddTradeScreenshotCommand command = CreateCommand() with
        {
            Content = content,
        };

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(command));

        AssertNoDependencyCalls(existenceReader, fileStorage, screenshotStore);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesUnsupportedExtensionFromFileStorage()
    {
        var expectedException = new ArgumentException("Unsupported extension.");
        var fileStorage = new RecordingFileStorage
        {
            StoreException = expectedException,
        };
        var screenshotStore = new RecordingScreenshotStore();
        var useCase = CreateUseCase(
            new RecordingTradeExistenceReader(),
            fileStorage,
            screenshotStore);
        AddTradeScreenshotCommand command = CreateCommand() with
        {
            FileName = "chart.gif",
        };

        ArgumentException actualException = await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(command));

        Assert.Same(expectedException, actualException);
        Assert.Equal(".gif", fileStorage.RequestedExtension);
        Assert.Equal(0, fileStorage.DeleteCallCount);
        Assert.Equal(0, screenshotStore.CallCount);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesBinaryStorageFailureWithoutCompensation()
    {
        var expectedException = new IOException("Storage failed.");
        var fileStorage = new RecordingFileStorage
        {
            StoreException = expectedException,
        };
        var screenshotStore = new RecordingScreenshotStore();
        var useCase = CreateUseCase(
            new RecordingTradeExistenceReader(),
            fileStorage,
            screenshotStore);

        IOException actualException = await Assert.ThrowsAsync<IOException>(
            () => useCase.ExecuteAsync(CreateCommand()));

        Assert.Same(expectedException, actualException);
        Assert.Equal(0, fileStorage.DeleteCallCount);
        Assert.Equal(0, screenshotStore.CallCount);
    }

    [Fact]
    public async Task ExecuteAsyncCompensatesDomainValidationFailure()
    {
        var fileStorage = new RecordingFileStorage();
        var screenshotStore = new RecordingScreenshotStore();
        var useCase = CreateUseCase(
            new RecordingTradeExistenceReader(),
            fileStorage,
            screenshotStore);
        AddTradeScreenshotCommand command = CreateCommand() with
        {
            Type = (TradeScreenshotType)999,
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal(1, fileStorage.StoreCallCount);
        Assert.Equal(1, fileStorage.DeleteCallCount);
        Assert.Equal(fileStorage.StorageKey, fileStorage.DeletedStorageKey);
        Assert.Equal(CancellationToken.None, fileStorage.DeleteCancellationToken);
        Assert.Equal(0, screenshotStore.CallCount);
    }

    [Fact]
    public async Task ExecuteAsyncCompensatesMetadataPersistenceFailure()
    {
        var expectedException = new InvalidOperationException("Metadata failed.");
        var fileStorage = new RecordingFileStorage();
        var screenshotStore = new RecordingScreenshotStore
        {
            Exception = expectedException,
        };
        var useCase = CreateUseCase(
            new RecordingTradeExistenceReader(),
            fileStorage,
            screenshotStore);

        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => useCase.ExecuteAsync(CreateCommand()));

        Assert.Same(expectedException, actualException);
        Assert.Equal(1, fileStorage.DeleteCallCount);
        Assert.Equal(fileStorage.StorageKey, fileStorage.DeletedStorageKey);
        Assert.Equal(CancellationToken.None, fileStorage.DeleteCancellationToken);
    }

    [Fact]
    public async Task ExecuteAsyncCompensatesMetadataCancellationWithNonCancelledToken()
    {
        using var cancellationSource = new CancellationTokenSource();
        var expectedException = new OperationCanceledException(cancellationSource.Token);
        var fileStorage = new RecordingFileStorage();
        var screenshotStore = new RecordingScreenshotStore
        {
            Exception = expectedException,
        };
        var useCase = CreateUseCase(
            new RecordingTradeExistenceReader(),
            fileStorage,
            screenshotStore);

        OperationCanceledException actualException =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => useCase.ExecuteAsync(CreateCommand(), cancellationSource.Token));

        Assert.Same(expectedException, actualException);
        Assert.Equal(1, fileStorage.DeleteCallCount);
        Assert.Equal(CancellationToken.None, fileStorage.DeleteCancellationToken);
        Assert.False(fileStorage.DeleteCancellationToken.CanBeCanceled);
    }

    [Fact]
    public async Task ExecuteAsyncPreservesMetadataFailureWhenCompensationFails()
    {
        var expectedException = new InvalidOperationException("Metadata failed.");
        var fileStorage = new RecordingFileStorage
        {
            DeleteException = new IOException("Cleanup failed."),
        };
        var screenshotStore = new RecordingScreenshotStore
        {
            Exception = expectedException,
        };
        var useCase = CreateUseCase(
            new RecordingTradeExistenceReader(),
            fileStorage,
            screenshotStore);

        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => useCase.ExecuteAsync(CreateCommand()));

        Assert.Same(expectedException, actualException);
        Assert.Equal(1, fileStorage.DeleteCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncPreservesDomainFailureWhenCompensationFails()
    {
        var fileStorage = new RecordingFileStorage
        {
            DeleteException = new IOException("Cleanup failed."),
        };
        var useCase = CreateUseCase(
            new RecordingTradeExistenceReader(),
            fileStorage,
            new RecordingScreenshotStore());
        AddTradeScreenshotCommand command = CreateCommand() with
        {
            CapturedAtUtc = CapturedAtUtc.ToOffset(TimeSpan.FromHours(2)),
        };

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal("capturedAtUtc", exception.ParamName);
        Assert.Equal(1, fileStorage.DeleteCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncDoesNotReclassifySuccessfulCommitAsCancelled()
    {
        using var cancellationSource = new CancellationTokenSource();
        var fileStorage = new RecordingFileStorage();
        var screenshotStore = new RecordingScreenshotStore
        {
            OnAdd = cancellationSource.Cancel,
        };
        var useCase = CreateUseCase(
            new RecordingTradeExistenceReader(),
            fileStorage,
            screenshotStore);

        Guid screenshotId = await useCase.ExecuteAsync(
            CreateCommand(),
            cancellationSource.Token);

        Assert.Equal(screenshotStore.AddedScreenshot!.Id, screenshotId);
        Assert.True(cancellationSource.IsCancellationRequested);
        Assert.Equal(0, fileStorage.DeleteCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncDoesNotDisposeSuppliedContentStream()
    {
        var content = new TrackingMemoryStream([1, 2, 3]);
        var useCase = CreateUseCase(
            new RecordingTradeExistenceReader(),
            new RecordingFileStorage(),
            new RecordingScreenshotStore());

        await useCase.ExecuteAsync(CreateCommand(content));

        Assert.False(content.WasDisposed);
        content.Dispose();
    }

    [Fact]
    public async Task ExecuteAsyncAcceptsNonSeekableReadableStream()
    {
        await using var content = new NonSeekableReadStream();
        var fileStorage = new RecordingFileStorage();
        var useCase = CreateUseCase(
            new RecordingTradeExistenceReader(),
            fileStorage,
            new RecordingScreenshotStore());

        await useCase.ExecuteAsync(CreateCommand(content));

        Assert.Same(content, fileStorage.Content);
        Assert.False(content.CanSeek);
    }

    [Fact]
    public async Task ExecuteAsyncAllowsDuplicateFileName()
    {
        var screenshotStore = new RecordingScreenshotStore();
        var useCase = CreateUseCase(
            new RecordingTradeExistenceReader(),
            new RecordingFileStorage(),
            screenshotStore);

        Guid firstId = await useCase.ExecuteAsync(CreateCommand());
        Guid secondId = await useCase.ExecuteAsync(CreateCommand());

        Assert.NotEqual(firstId, secondId);
        Assert.Equal(2, screenshotStore.CallCount);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesExistenceFailureBeforeStorage()
    {
        var expectedException = new IOException("Existence read failed.");
        var existenceReader = new RecordingTradeExistenceReader
        {
            Exception = expectedException,
        };
        var fileStorage = new RecordingFileStorage();
        var screenshotStore = new RecordingScreenshotStore();
        var useCase = CreateUseCase(existenceReader, fileStorage, screenshotStore);

        IOException actualException = await Assert.ThrowsAsync<IOException>(
            () => useCase.ExecuteAsync(CreateCommand()));

        Assert.Same(expectedException, actualException);
        Assert.Equal(0, fileStorage.StoreCallCount);
        Assert.Equal(0, screenshotStore.CallCount);
    }

    private static AddTradeScreenshotUseCase CreateUseCase(
        RecordingTradeExistenceReader existenceReader,
        RecordingFileStorage fileStorage,
        RecordingScreenshotStore screenshotStore)
    {
        return new AddTradeScreenshotUseCase(
            existenceReader,
            fileStorage,
            screenshotStore,
            new FixedTimeProvider(CreatedAtUtc));
    }

    private static AddTradeScreenshotCommand CreateCommand(Stream? content = null)
    {
        return new AddTradeScreenshotCommand(
            TradeId,
            TradeScreenshotType.PreTrade,
            content ?? Stream.Null,
            "chart.png",
            CapturedAtUtc,
            null,
            null);
    }

    private static void AssertNoDependencyCalls(
        RecordingTradeExistenceReader existenceReader,
        RecordingFileStorage fileStorage,
        RecordingScreenshotStore screenshotStore)
    {
        Assert.Equal(0, existenceReader.CallCount);
        Assert.Equal(0, fileStorage.StoreCallCount);
        Assert.Equal(0, fileStorage.DeleteCallCount);
        Assert.Equal(0, screenshotStore.CallCount);
    }

    private sealed class RecordingTradeExistenceReader(List<string>? operations = null)
        : ITradeExistenceReader
    {
        public bool Exists { get; set; } = true;

        public Exception? Exception { get; set; }

        public int CallCount { get; private set; }

        public Guid RequestedTradeId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<bool> ExistsAsync(
            Guid tradeId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            RequestedTradeId = tradeId;
            CancellationToken = cancellationToken;
            operations?.Add("exists");

            return Exception is null
                ? Task.FromResult(Exists)
                : Task.FromException<bool>(Exception);
        }
    }

    private sealed class RecordingFileStorage(List<string>? operations = null)
        : ITradeScreenshotFileStorage
    {
        public string StorageKey { get; set; } = "09f85f6752854af49c9a596e834a8341.png";

        public Exception? StoreException { get; set; }

        public Exception? DeleteException { get; set; }

        public Stream? Content { get; private set; }

        public string? RequestedExtension { get; private set; }

        public string? DeletedStorageKey { get; private set; }

        public int StoreCallCount { get; private set; }

        public int DeleteCallCount { get; private set; }

        public CancellationToken StoreCancellationToken { get; private set; }

        public CancellationToken DeleteCancellationToken { get; private set; }

        public Task<string> StoreAsync(
            Stream content,
            string fileExtension,
            CancellationToken cancellationToken = default)
        {
            StoreCallCount++;
            Content = content;
            RequestedExtension = fileExtension;
            StoreCancellationToken = cancellationToken;
            operations?.Add("store-file");

            return StoreException is null
                ? Task.FromResult(StorageKey)
                : Task.FromException<string>(StoreException);
        }

        public Task<Stream?> OpenReadAsync(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteIfExistsAsync(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            DeleteCallCount++;
            DeletedStorageKey = storageKey;
            DeleteCancellationToken = cancellationToken;

            return DeleteException is null
                ? Task.CompletedTask
                : Task.FromException(DeleteException);
        }
    }

    private sealed class RecordingScreenshotStore(List<string>? operations = null)
        : ITradeScreenshotStore
    {
        public TradeScreenshot? AddedScreenshot { get; private set; }

        public Exception? Exception { get; set; }

        public Action? OnAdd { get; set; }

        public int CallCount { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task AddAsync(
            TradeScreenshot screenshot,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            AddedScreenshot = screenshot;
            CancellationToken = cancellationToken;
            operations?.Add("store-metadata");
            OnAdd?.Invoke();

            return Exception is null
                ? Task.CompletedTask
                : Task.FromException(Exception);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public int CallCount { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            CallCount++;
            return utcNow;
        }
    }

    private sealed class TrackingMemoryStream(byte[] content) : MemoryStream(content)
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class WriteOnlyStream : MemoryStream
    {
        public override bool CanRead => false;
    }

    private sealed class NonSeekableReadStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
