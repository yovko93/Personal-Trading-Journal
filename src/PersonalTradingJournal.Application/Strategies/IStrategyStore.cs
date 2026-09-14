using PersonalTradingJournal.Domain.Strategies;

namespace PersonalTradingJournal.Application.Strategies;

public interface IStrategyStore
{
    Task AddAsync(
        Strategy strategy,
        CancellationToken cancellationToken = default);

    Task<Strategy?> GetByIdAsync(
        Guid strategyId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Strategy strategy,
        CancellationToken cancellationToken = default);
}
