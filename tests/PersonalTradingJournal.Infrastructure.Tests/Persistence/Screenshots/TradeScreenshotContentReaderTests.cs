using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Screenshots;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Screenshots;

[Collection(ScreenshotPersistenceCollection.Name)]
public sealed class TradeScreenshotContentReaderTests
{
    [Fact]
    public async Task OpenAsyncRejectsEmptyScreenshotId()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => GetReader(database).OpenAsync(Guid.Empty));

        Assert.Equal("screenshotId", exception.ParamName);
    }

    [Fact]
    public async Task OpenAsyncReturnsNullForMissingMetadataWithoutCallingFileStorage()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var fileStorage = new RecordingFileStorage();
        var reader = new TradeScreenshotContentReader(
            database.ContextFactory,
            fileStorage);

        TradeScreenshotContent? result = await reader.OpenAsync(Guid.NewGuid());

        Assert.Null(result);
        Assert.Equal(0, fileStorage.OpenCallCount);
    }

    [Fact]
    public async Task OpenAsyncReturnsExactIdentityFileNameAndBytesWithCallerOwnedStream()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        byte[] expectedBytes = [0, 1, 2, 127, 128, 254, 255];
        TradeScreenshot screenshot = await PersistScreenshotAsync(
            database,
            expectedBytes,
            fileName: "Original NQ Capture.PNG");

        TradeScreenshotContent content = Assert.IsType<TradeScreenshotContent>(
            await GetReader(database).OpenAsync(screenshot.Id));

        Assert.Equal(screenshot.Id, content.Id);
        Assert.Equal(screenshot.TradeId, content.TradeId);
        Assert.Equal("Original NQ Capture.PNG", content.FileName);
        Assert.True(content.Content.CanRead);
        Assert.Equal(expectedBytes, await ReadAllAsync(content.Content));
        await content.Content.DisposeAsync();
        Assert.False(content.Content.CanRead);
        Assert.Null(typeof(TradeScreenshotContent).GetProperty("StorageKey"));
        Assert.Null(typeof(TradeScreenshotContent).GetProperty("Path"));
        Assert.Null(typeof(TradeScreenshotContent).GetProperty("UpdatedAtUtc"));
        Assert.Equal(
            typeof(Stream),
            typeof(TradeScreenshotContent).GetProperty("Content")!.PropertyType);
    }

    [Fact]
    public async Task OpenAsyncThrowsFileNotFoundWithoutExposingPathOrDeletingMetadata()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(database);
        string storageKey = $"{Guid.NewGuid():N}.png";
        TradeScreenshot screenshot = ScreenshotPersistenceTestData.CreateScreenshot(
            tradeId,
            storageKey: storageKey);
        await GetStore(database).AddAsync(screenshot);

        FileNotFoundException exception = await Assert.ThrowsAsync<FileNotFoundException>(
            () => GetReader(database).OpenAsync(screenshot.Id));

        Assert.Contains(screenshot.Id.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(
            Path.GetTempPath(),
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(storageKey, exception.Message, StringComparison.Ordinal);
        await using JournalDbContext context =
            await database.ContextFactory.CreateDbContextAsync();
        TradeScreenshotRecord persisted = await context.TradeScreenshots
            .AsNoTracking()
            .SingleAsync(record => record.Id == screenshot.Id);
        Assert.Equal(storageKey, persisted.StorageKey);
    }

    [Fact]
    public async Task OpenAsyncIgnoresInactiveAccountAndInstrumentState()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(
            database,
            referencesAreActive: false);
        TradeScreenshot screenshot = await PersistScreenshotAsync(
            database,
            [4, 5, 6],
            tradeId);

        TradeScreenshotContent content = Assert.IsType<TradeScreenshotContent>(
            await GetReader(database).OpenAsync(screenshot.Id));
        await using (content.Content)
        {
            Assert.Equal([4, 5, 6], await ReadAllAsync(content.Content));
        }
    }

    [Fact]
    public async Task OpenAsyncUsesFreshContextAndSeesLaterMetadataCommit()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        ITradeScreenshotContentReader reader = GetReader(database);
        Guid screenshotId = Guid.NewGuid();
        Assert.Null(await reader.OpenAsync(screenshotId));
        TradeScreenshot screenshot = await PersistScreenshotAsync(
            database,
            [9, 8, 7],
            screenshotId: screenshotId);

        TradeScreenshotContent content = Assert.IsType<TradeScreenshotContent>(
            await reader.OpenAsync(screenshot.Id));
        await using (content.Content)
        {
            Assert.Equal([9, 8, 7], await ReadAllAsync(content.Content));
        }
    }

    [Fact]
    public async Task OpenAsyncPropagatesPreCancelledToken()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => GetReader(database).OpenAsync(
                Guid.NewGuid(),
                cancellationSource.Token));
    }

    [Fact]
    public async Task OpenAsyncPropagatesFileStorageCancellation()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(database);
        TradeScreenshot screenshot = ScreenshotPersistenceTestData.CreateScreenshot(tradeId);
        await GetStore(database).AddAsync(screenshot);
        using var cancellationSource = new CancellationTokenSource();
        var expectedException = new OperationCanceledException(cancellationSource.Token);
        var fileStorage = new RecordingFileStorage
        {
            Exception = expectedException,
        };
        var reader = new TradeScreenshotContentReader(
            database.ContextFactory,
            fileStorage);

        OperationCanceledException actualException =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => reader.OpenAsync(screenshot.Id, cancellationSource.Token));

        Assert.Same(expectedException, actualException);
        Assert.Equal(1, fileStorage.OpenCallCount);
        Assert.Equal(screenshot.StorageKey, fileStorage.RequestedStorageKey);
        Assert.Equal(cancellationSource.Token, fileStorage.CancellationToken);
    }

    [Fact]
    public async Task OpenAsyncRetrievesDuplicateOriginalFileNamesIndependentlyById()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid tradeId = await ScreenshotPersistenceTestData.PersistTradeAsync(database);
        TradeScreenshot first = await PersistScreenshotAsync(
            database,
            [1, 1, 1],
            tradeId,
            fileName: "duplicate.png");
        TradeScreenshot second = await PersistScreenshotAsync(
            database,
            [2, 2, 2],
            tradeId,
            fileName: "duplicate.png");
        ITradeScreenshotContentReader reader = GetReader(database);

        TradeScreenshotContent firstContent = Assert.IsType<TradeScreenshotContent>(
            await reader.OpenAsync(first.Id));
        await using (firstContent.Content)
        {
            Assert.Equal([1, 1, 1], await ReadAllAsync(firstContent.Content));
        }
        TradeScreenshotContent secondContent = Assert.IsType<TradeScreenshotContent>(
            await reader.OpenAsync(second.Id));
        await using (secondContent.Content)
        {
            Assert.Equal([2, 2, 2], await ReadAllAsync(secondContent.Content));
        }

        Assert.Equal("duplicate.png", firstContent.FileName);
        Assert.Equal("duplicate.png", secondContent.FileName);
        Assert.NotEqual(first.StorageKey, second.StorageKey);
    }

    private static async Task<TradeScreenshot> PersistScreenshotAsync(
        ReaderTestDatabase database,
        byte[] bytes,
        Guid? tradeId = null,
        string fileName = "capture.png",
        Guid? screenshotId = null)
    {
        Guid parentTradeId = tradeId ??
            await ScreenshotPersistenceTestData.PersistTradeAsync(database);
        ITradeScreenshotFileStorage fileStorage =
            database.ServiceProvider.GetRequiredService<ITradeScreenshotFileStorage>();
        await using var source = new MemoryStream(bytes);
        string storageKey = await fileStorage.StoreAsync(source, ".png");
        TradeScreenshot screenshot = ScreenshotPersistenceTestData.CreateScreenshot(
            parentTradeId,
            screenshotId,
            storageKey: storageKey,
            fileName: fileName);
        await GetStore(database).AddAsync(screenshot);

        return screenshot;
    }

    private static async Task<byte[]> ReadAllAsync(Stream content)
    {
        using var destination = new MemoryStream();
        await content.CopyToAsync(destination);
        return destination.ToArray();
    }

    private static ITradeScreenshotContentReader GetReader(
        ReaderTestDatabase database) =>
        database.ServiceProvider.GetRequiredService<ITradeScreenshotContentReader>();

    private static ITradeScreenshotStore GetStore(ReaderTestDatabase database) =>
        database.ServiceProvider.GetRequiredService<ITradeScreenshotStore>();

    private sealed class RecordingFileStorage : ITradeScreenshotFileStorage
    {
        public Exception? Exception { get; set; }

        public int OpenCallCount { get; private set; }

        public string? RequestedStorageKey { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<string> StoreAsync(
            Stream content,
            string fileExtension,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Stream?> OpenReadAsync(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            OpenCallCount++;
            RequestedStorageKey = storageKey;
            CancellationToken = cancellationToken;

            return Exception is null
                ? Task.FromResult<Stream?>(null)
                : Task.FromException<Stream?>(Exception);
        }

        public Task DeleteIfExistsAsync(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
