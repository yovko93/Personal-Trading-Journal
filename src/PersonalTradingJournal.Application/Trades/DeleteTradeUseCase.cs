using PersonalTradingJournal.Application.Screenshots;

namespace PersonalTradingJournal.Application.Trades;

public sealed class DeleteTradeUseCase
{
    private readonly ITradeDeletionStore _deletionStore;
    private readonly ITradeScreenshotFileStorage _screenshotFileStorage;

    public DeleteTradeUseCase(
        ITradeDeletionStore deletionStore,
        ITradeScreenshotFileStorage screenshotFileStorage)
    {
        ArgumentNullException.ThrowIfNull(deletionStore);
        ArgumentNullException.ThrowIfNull(screenshotFileStorage);
        _deletionStore = deletionStore;
        _screenshotFileStorage = screenshotFileStorage;
    }

    public async Task<DeleteTradeResult> ExecuteAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default)
    {
        if (tradeId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trade identifier is required.",
                nameof(tradeId));
        }

        TradeDeletionInfo? deletion = await _deletionStore.DeleteAsync(
            tradeId,
            cancellationToken);
        if (deletion is null)
        {
            throw new KeyNotFoundException(
                $"Trade '{tradeId}' could not be found.");
        }

        bool cleanupSucceeded = true;
        foreach (string storageKey in deletion.ScreenshotStorageKeys.Distinct(
                     StringComparer.Ordinal))
        {
            try
            {
                // The database delete has committed. Cleanup is compensating work and
                // should not be abandoned because the caller cancelled afterward.
                await _screenshotFileStorage.DeleteIfExistsAsync(
                    storageKey,
                    CancellationToken.None);
            }
            catch
            {
                cleanupSucceeded = false;
            }
        }

        return cleanupSucceeded
            ? DeleteTradeResult.Deleted
            : DeleteTradeResult.DeletedWithFileCleanupWarning;
    }
}
