using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Application.Mistakes;

public sealed class UpdateTradingMistakeUseCase
{
    public const string DuplicateNameMessage =
        CreateTradingMistakeUseCase.DuplicateNameMessage;

    private readonly ITradingMistakeStore _store;
    private readonly ITradingMistakeNameChecker _nameChecker;
    private readonly TimeProvider _timeProvider;

    public UpdateTradingMistakeUseCase(
        ITradingMistakeStore store,
        ITradingMistakeNameChecker nameChecker,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(nameChecker);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store;
        _nameChecker = nameChecker;
        _timeProvider = timeProvider;
    }

    public async Task<UpdateTradingMistakeResult> ExecuteAsync(
        UpdateTradingMistakeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TradingMistakeId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trading mistake identifier is required.",
                nameof(command));
        }

        TradingMistake? mistake = await _store.GetByIdAsync(
            command.TradingMistakeId,
            cancellationToken);
        if (mistake is null)
        {
            throw new KeyNotFoundException(
                $"Trading mistake '{command.TradingMistakeId}' was not found.");
        }

        string originalName = mistake.Name;
        bool wasChanged = mistake.UpdateDetails(
            command.Name,
            command.Description,
            _timeProvider.GetUtcNow());
        if (!wasChanged)
        {
            return new UpdateTradingMistakeResult(
                TradingMistakeDetails.FromDomain(mistake),
                false);
        }

        if (!string.Equals(originalName, mistake.Name, StringComparison.Ordinal) &&
            await _nameChecker.ExistsAsync(
                mistake.Name,
                mistake.Id,
                cancellationToken))
        {
            throw new InvalidOperationException(DuplicateNameMessage);
        }

        await _store.UpdateAsync(mistake, cancellationToken);
        return new UpdateTradingMistakeResult(
            TradingMistakeDetails.FromDomain(mistake),
            true);
    }
}
