using PersonalTradingJournal.Domain.Strategies;

namespace PersonalTradingJournal.Application.Strategies;

public sealed class CreateStrategyUseCase
{
    public const string DuplicateNameMessage =
        "A strategy with this name already exists.";

    private readonly IStrategyStore _strategyStore;
    private readonly IStrategyNameChecker _strategyNameChecker;
    private readonly TimeProvider _timeProvider;

    public CreateStrategyUseCase(
        IStrategyStore strategyStore,
        IStrategyNameChecker strategyNameChecker,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(strategyStore);
        ArgumentNullException.ThrowIfNull(strategyNameChecker);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _strategyStore = strategyStore;
        _strategyNameChecker = strategyNameChecker;
        _timeProvider = timeProvider;
    }

    public async Task<Guid> ExecuteAsync(
        CreateStrategyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var strategy = new Strategy(
            command.Name,
            command.Description,
            _timeProvider.GetUtcNow());

        bool alreadyExists = await _strategyNameChecker.ExistsAsync(
            strategy.Name,
            cancellationToken: cancellationToken);

        if (alreadyExists)
        {
            throw new InvalidOperationException(DuplicateNameMessage);
        }

        await _strategyStore.AddAsync(strategy, cancellationToken);
        return strategy.Id;
    }
}
