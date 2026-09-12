namespace PersonalTradingJournal.Application.Screenshots;

/// <summary>
/// Stores and retrieves Trade screenshot binary content through opaque storage keys.
/// </summary>
/// <remarks>
/// This boundary is independent from screenshot metadata persistence. The returned storage
/// key has no path semantics for Application or Desktop callers.
/// </remarks>
public interface ITradeScreenshotFileStorage
{
    Task<string> StoreAsync(
        Stream content,
        string fileExtension,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
