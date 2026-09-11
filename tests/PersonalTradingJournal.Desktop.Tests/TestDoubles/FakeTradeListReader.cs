using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeListReader : ITradeListReader
{
    private readonly Queue<Func<CancellationToken, Task<IReadOnlyList<TradeListItem>>>>
        _behaviors = new();
    private readonly List<int> _requestedLimits = [];

    public int CallCount { get; private set; }

    public IReadOnlyList<int> RequestedLimits => _requestedLimits;

    public CancellationToken CancellationToken { get; private set; }

    public void EnqueueResult(IReadOnlyList<TradeListItem> trades)
    {
        _behaviors.Enqueue(_ => Task.FromResult(trades));
    }

    public void EnqueueException(Exception exception)
    {
        _behaviors.Enqueue(_ =>
            Task.FromException<IReadOnlyList<TradeListItem>>(exception));
    }

    public Task<IReadOnlyList<TradeListItem>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        _requestedLimits.Add(limit);
        CancellationToken = cancellationToken;

        return _behaviors.Count == 0
            ? Task.FromResult<IReadOnlyList<TradeListItem>>([])
            : _behaviors.Dequeue()(cancellationToken);
    }
}
