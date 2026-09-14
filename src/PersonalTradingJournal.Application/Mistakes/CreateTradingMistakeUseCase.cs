using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Application.Mistakes;

public sealed class CreateTradingMistakeUseCase
{
    public const string DuplicateNameMessage = "A trading mistake with this name already exists.";
    private readonly ITradingMistakeStore _store;
    private readonly ITradingMistakeNameChecker _nameChecker;
    private readonly TimeProvider _timeProvider;

    public CreateTradingMistakeUseCase(ITradingMistakeStore store,
        ITradingMistakeNameChecker nameChecker, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(nameChecker);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store; _nameChecker = nameChecker; _timeProvider = timeProvider;
    }

    public async Task<Guid> ExecuteAsync(CreateTradingMistakeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var mistake = new TradingMistake(command.Name, command.Description, _timeProvider.GetUtcNow());
        if (await _nameChecker.ExistsAsync(mistake.Name, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException(DuplicateNameMessage);
        }

        await _store.AddAsync(mistake, cancellationToken);
        return mistake.Id;
    }
}
