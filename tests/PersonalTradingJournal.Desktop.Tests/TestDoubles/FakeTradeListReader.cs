using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeListReader : ITradeListReader
{
    private readonly Queue<Func<TradeListQuery, CancellationToken, Task<TradeListPage>>>
        _behaviors = new();
    private readonly List<TradeListQuery> _requestedQueries = [];
    private readonly TaskCompletionSource<bool> _readStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _releaseRead =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int CallCount { get; private set; }

    public IReadOnlyList<TradeListQuery> RequestedQueries => _requestedQueries;

    public IReadOnlyList<int> RequestedLimits => _requestedQueries
        .Select(query => query.PageSize)
        .ToList();

    public CancellationToken CancellationToken { get; private set; }

    public bool HoldRead { get; set; }

    public Task ReadStarted => _readStarted.Task;

    public void ReleaseRead()
    {
        _releaseRead.TrySetResult(true);
    }

    public void EnqueueResult(IReadOnlyList<TradeListItem> trades)
    {
        EnqueuePage(trades, trades.Count);
    }

    public void EnqueuePage(
        IReadOnlyList<TradeListItem> trades,
        int totalCount,
        int? pageNumber = null)
    {
        _behaviors.Enqueue((query, _) => Task.FromResult(new TradeListPage(
            trades,
            pageNumber ?? query.PageNumber,
            query.PageSize,
            totalCount)));
    }

    public void EnqueueException(Exception exception)
    {
        _behaviors.Enqueue((_, _) =>
            Task.FromException<TradeListPage>(exception));
    }

    public void EnqueueBehavior(
        Func<CancellationToken, Task<IReadOnlyList<TradeListItem>>> behavior)
    {
        _behaviors.Enqueue(async (query, cancellationToken) =>
        {
            IReadOnlyList<TradeListItem> items = await behavior(cancellationToken);
            return new TradeListPage(
                items,
                query.PageNumber,
                query.PageSize,
                items.Count);
        });
    }

    public void EnqueuePageBehavior(
        Func<TradeListQuery, CancellationToken, Task<TradeListPage>> behavior)
    {
        _behaviors.Enqueue(behavior);
    }

    public async Task<TradeListPage> GetPageAsync(
        TradeListQuery query,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        _requestedQueries.Add(query);
        CancellationToken = cancellationToken;
        _readStarted.TrySetResult(true);

        if (HoldRead)
        {
            _ = await _releaseRead.Task.WaitAsync(cancellationToken);
        }

        return _behaviors.Count == 0
            ? new TradeListPage([], query.PageNumber, query.PageSize, 0)
            : await _behaviors.Dequeue()(query, cancellationToken);
    }
}
