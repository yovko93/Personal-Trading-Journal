using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Setups;

public sealed class UpdateTradingSetupUseCase
{
    public const string DuplicateNameMessage =
        CreateTradingSetupUseCase.DuplicateNameMessage;

    private readonly ITradingSetupStore _store;
    private readonly ITradingSetupNameChecker _nameChecker;
    private readonly TimeProvider _timeProvider;

    private readonly ISetupExampleStore? _examples;

    public UpdateTradingSetupUseCase(
        ITradingSetupStore store,
        ITradingSetupNameChecker nameChecker,
        TimeProvider timeProvider, ISetupExampleStore? examples = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(nameChecker);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store;
        _examples = examples;
        _nameChecker = nameChecker;
        _timeProvider = timeProvider;
    }

    public async Task<UpdateTradingSetupResult> ExecuteAsync(
        UpdateTradingSetupCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TradingSetupId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trading setup identifier is required.",
                nameof(command));
        }

        TradingSetup? setup = await _store.GetByIdAsync(
            command.TradingSetupId,
            cancellationToken);
        if (setup is null)
        {
            throw new KeyNotFoundException(
                $"Trading setup '{command.TradingSetupId}' was not found.");
        }

        string originalName = setup.Name;
        bool wasChanged = setup.UpdateDetails(
            command.Name,
            command.Description,
            _timeProvider.GetUtcNow());
        if (!wasChanged && command.Images is null)
        {
            return new UpdateTradingSetupResult(
                TradingSetupDetails.FromDomain(setup),
                false);
        }

        if (!string.Equals(originalName, setup.Name, StringComparison.Ordinal) &&
            await _nameChecker.ExistsAsync(
                setup.Name,
                setup.Id,
                cancellationToken))
        {
            throw new InvalidOperationException(DuplicateNameMessage);
        }

        if (command.Images is { } changes)
        {
            var store = _examples ?? throw new InvalidOperationException("Setup image storage is unavailable.");
            bool cleanupComplete = await store.UpdateWithExamplesAsync(setup, wasChanged, changes, cancellationToken);
            return new(TradingSetupDetails.FromDomain(setup), true, cleanupComplete);
        }

        await _store.UpdateAsync(setup, cancellationToken);
        return new UpdateTradingSetupResult(
            TradingSetupDetails.FromDomain(setup),
            true);
    }
}
