namespace PersonalTradingJournal.Application.Screenshots;

public sealed class DeleteTradeScreenshotUseCase
{
    private readonly ITradeScreenshotDeletionStore _deletionStore;
    private readonly ITradeScreenshotFileStorage _fileStorage;

    public DeleteTradeScreenshotUseCase(
        ITradeScreenshotDeletionStore deletionStore,
        ITradeScreenshotFileStorage fileStorage)
    {
        ArgumentNullException.ThrowIfNull(deletionStore);
        ArgumentNullException.ThrowIfNull(fileStorage);

        _deletionStore = deletionStore;
        _fileStorage = fileStorage;
    }

    public async Task<DeleteTradeScreenshotResult> ExecuteAsync(
        DeleteTradeScreenshotCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.TradeId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trade identifier cannot be empty.",
                nameof(command));
        }

        if (command.ScreenshotId == Guid.Empty)
        {
            throw new ArgumentException(
                "A screenshot identifier cannot be empty.",
                nameof(command));
        }

        TradeScreenshotDeletionInfo? deletionInfo =
            await _deletionStore.DeleteAsync(
                command.TradeId,
                command.ScreenshotId,
                cancellationToken);

        if (deletionInfo is null)
        {
            throw new KeyNotFoundException(
                $"Screenshot '{command.ScreenshotId}' no longer exists for " +
                $"Trade '{command.TradeId}'.");
        }

        try
        {
            await _fileStorage.DeleteIfExistsAsync(
                deletionInfo.StorageKey,
                CancellationToken.None);
            return new DeleteTradeScreenshotResult(FileCleanupSucceeded: true);
        }
        catch
        {
            return new DeleteTradeScreenshotResult(FileCleanupSucceeded: false);
        }
    }
}
