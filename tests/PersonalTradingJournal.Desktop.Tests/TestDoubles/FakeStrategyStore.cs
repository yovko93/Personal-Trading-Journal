using PersonalTradingJournal.Application.Strategies;
using PersonalTradingJournal.Domain.Strategies;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeStrategyStore : IStrategyStore
{
    public Strategy? Strategy { get; set; }

    public Exception? AddException { get; set; }

    public Exception? LoadException { get; set; }

    public Exception? UpdateException { get; set; }

    public int AddCallCount { get; private set; }

    public int GetCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    public CancellationToken AddCancellationToken { get; private set; }

    public CancellationToken GetCancellationToken { get; private set; }

    public Guid RequestedStrategyId { get; private set; }

    public CancellationToken UpdateCancellationToken { get; private set; }

    public Task AddAsync(Strategy strategy, CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        AddCancellationToken = cancellationToken;
        Strategy = strategy;
        return AddException is null ? Task.CompletedTask : Task.FromException(AddException);
    }

    public Task<Strategy?> GetByIdAsync(Guid strategyId, CancellationToken cancellationToken = default)
    {
        GetCallCount++;
        RequestedStrategyId = strategyId;
        GetCancellationToken = cancellationToken;
        return LoadException is null
            ? Task.FromResult(Strategy)
            : Task.FromException<Strategy?>(LoadException);
    }

    public Task UpdateAsync(Strategy strategy, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        UpdateCancellationToken = cancellationToken;
        Strategy = strategy;
        return UpdateException is null ? Task.CompletedTask : Task.FromException(UpdateException);
    }
}
