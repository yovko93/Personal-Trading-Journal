using PersonalTradingJournal.Application.Screenshots;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeScreenshotFileStorage : ITradeScreenshotFileStorage
{
    public int StoreCallCount { get; private set; }

    public Stream? StoredContent { get; private set; }

    public string? StoredExtension { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public int DeleteCallCount { get; private set; }

    public string? DeletedStorageKey { get; private set; }

    public CancellationToken DeleteCancellationToken { get; private set; }

    public Exception? StoreException { get; set; }

    public Exception? DeleteException { get; set; }

    public string StorageKey { get; set; } = "screenshots/test.png";

    public Task<string> StoreAsync(
        Stream content,
        string fileExtension,
        CancellationToken cancellationToken = default)
    {
        StoreCallCount++;
        StoredContent = content;
        StoredExtension = fileExtension;
        CancellationToken = cancellationToken;

        return StoreException is null
            ? Task.FromResult(StorageKey)
            : Task.FromException<string>(StoreException);
    }

    public Task<Stream?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(null);

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
