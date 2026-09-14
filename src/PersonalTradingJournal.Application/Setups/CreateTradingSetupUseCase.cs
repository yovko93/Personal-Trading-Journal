using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Setups;

public sealed class CreateTradingSetupUseCase
{
    public const string DuplicateNameMessage = "A trading setup with this name already exists.";
    private readonly ITradingSetupStore _store;
    private readonly ITradingSetupNameChecker _nameChecker;
    private readonly TimeProvider _timeProvider;

    public CreateTradingSetupUseCase(ITradingSetupStore store,
        ITradingSetupNameChecker nameChecker, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(nameChecker);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store;
        _nameChecker = nameChecker;
        _timeProvider = timeProvider;
    }

    public async Task<Guid> ExecuteAsync(CreateTradingSetupCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var setup = new TradingSetup(command.Name, command.Description, _timeProvider.GetUtcNow());
        if (await _nameChecker.ExistsAsync(setup.Name, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException(DuplicateNameMessage);
        }

        await _store.AddAsync(setup, cancellationToken);
        return setup.Id;
    }
}
