using PersonalTradingJournal.Domain.Strategies;

namespace PersonalTradingJournal.Application.Strategies;

public sealed class StrategyLifecycleUseCase
{
    private readonly IStrategyStore _strategyStore;
    private readonly TimeProvider _timeProvider;

    public StrategyLifecycleUseCase(
        IStrategyStore strategyStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(strategyStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _strategyStore = strategyStore;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(
        SetStrategyActiveStateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.StrategyId == Guid.Empty)
        {
            throw new ArgumentException(
                "A strategy identifier is required.",
                nameof(command));
        }

        Strategy? strategy = await _strategyStore.GetByIdAsync(
            command.StrategyId,
            cancellationToken);

        if (strategy is null)
        {
            throw new KeyNotFoundException(
                $"Strategy '{command.StrategyId}' was not found.");
        }

        if (strategy.IsActive == command.IsActive)
        {
            return;
        }

        DateTimeOffset updatedAtUtc = _timeProvider.GetUtcNow();
        if (command.IsActive)
        {
            strategy.Activate(updatedAtUtc);
        }
        else
        {
            strategy.Deactivate(updatedAtUtc);
        }

        await _strategyStore.UpdateAsync(strategy, cancellationToken);
    }
}
