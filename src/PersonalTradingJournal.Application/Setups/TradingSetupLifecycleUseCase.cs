using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Setups;

public sealed class TradingSetupLifecycleUseCase
{
    private readonly ITradingSetupStore _store;
    private readonly TimeProvider _timeProvider;

    public TradingSetupLifecycleUseCase(ITradingSetupStore store, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(SetTradingSetupActiveStateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TradingSetupId == Guid.Empty)
        {
            throw new ArgumentException("A trading setup identifier is required.", nameof(command));
        }

        TradingSetup? setup = await _store.GetByIdAsync(command.TradingSetupId, cancellationToken);
        if (setup is null)
        {
            throw new KeyNotFoundException($"Trading setup '{command.TradingSetupId}' was not found.");
        }

        if (setup.IsActive == command.IsActive)
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (command.IsActive) setup.Activate(now); else setup.Deactivate(now);
        await _store.UpdateAsync(setup, cancellationToken);
    }
}
