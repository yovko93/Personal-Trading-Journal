using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Screenshots;

namespace PersonalTradingJournal.Application.Screenshots;

public sealed class AddTradeScreenshotUseCase
{
    private readonly ITradeExistenceReader _tradeExistenceReader;
    private readonly ITradeScreenshotFileStorage _fileStorage;
    private readonly ITradeScreenshotStore _screenshotStore;
    private readonly TimeProvider _timeProvider;

    public AddTradeScreenshotUseCase(
        ITradeExistenceReader tradeExistenceReader,
        ITradeScreenshotFileStorage fileStorage,
        ITradeScreenshotStore screenshotStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(tradeExistenceReader);
        ArgumentNullException.ThrowIfNull(fileStorage);
        ArgumentNullException.ThrowIfNull(screenshotStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _tradeExistenceReader = tradeExistenceReader;
        _fileStorage = fileStorage;
        _screenshotStore = screenshotStore;
        _timeProvider = timeProvider;
    }

    public async Task<Guid> ExecuteAsync(
        AddTradeScreenshotCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        string fileExtension = ValidateCommand(command);
        bool tradeExists = await _tradeExistenceReader.ExistsAsync(
            command.TradeId,
            cancellationToken);
        if (!tradeExists)
        {
            throw new KeyNotFoundException(
                $"Trade '{command.TradeId}' was not found.");
        }

        string? storageKey = null;
        try
        {
            storageKey = await _fileStorage.StoreAsync(
                command.Content,
                fileExtension,
                cancellationToken);
            var screenshot = new TradeScreenshot(
                command.TradeId,
                command.Type,
                storageKey,
                command.FileName,
                command.CapturedAtUtc,
                command.Timeframe,
                command.Description,
                _timeProvider.GetUtcNow());

            await _screenshotStore.AddAsync(screenshot, cancellationToken);

            return screenshot.Id;
        }
        catch
        {
            if (storageKey is not null)
            {
                try
                {
                    await _fileStorage.DeleteIfExistsAsync(
                        storageKey,
                        CancellationToken.None);
                }
                catch
                {
                    // Compensation is best-effort and cannot replace the workflow failure.
                }
            }

            throw;
        }
    }

    private static string ValidateCommand(AddTradeScreenshotCommand command)
    {
        if (command.TradeId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trade identifier cannot be empty.",
                nameof(command));
        }

        if (command.Content is null)
        {
            throw new ArgumentException("Screenshot content is required.", nameof(command));
        }

        if (!command.Content.CanRead)
        {
            throw new ArgumentException(
                "The screenshot content stream must be readable.",
                nameof(command));
        }

        string fileName = command.FileName;
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(fileName, fileName.Trim(), StringComparison.Ordinal) ||
            Path.IsPathRooted(fileName) ||
            fileName is "." or ".." ||
            fileName.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
            fileName.IndexOf(Path.AltDirectorySeparatorChar) >= 0 ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The screenshot filename must be a trimmed single filename.",
                nameof(command));
        }

        string fileExtension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(fileExtension) || fileExtension == ".")
        {
            throw new ArgumentException(
                "The screenshot filename must include a file extension.",
                nameof(command));
        }

        return fileExtension;
    }
}
