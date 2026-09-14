using PersonalTradingJournal.Application.Strategies;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeStrategyReader : IStrategyReader
{
    private readonly Queue<object> _outcomes = new();

    public int CallCount { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public void EnqueueResult(IReadOnlyList<StrategyListItem> result) =>
        _outcomes.Enqueue(result);

    public void EnqueueException(Exception exception) => _outcomes.Enqueue(exception);

    public Task<IReadOnlyList<StrategyListItem>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        CancellationToken = cancellationToken;
        object outcome = _outcomes.Count == 0
            ? Array.Empty<StrategyListItem>()
            : _outcomes.Dequeue();

        return outcome is Exception exception
            ? Task.FromException<IReadOnlyList<StrategyListItem>>(exception)
            : Task.FromResult((IReadOnlyList<StrategyListItem>)outcome);
    }
}
