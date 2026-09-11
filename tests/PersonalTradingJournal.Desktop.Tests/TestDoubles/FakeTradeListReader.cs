using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeListReader : ITradeListReader
{
    private readonly Queue<Func<CancellationToken, Task<IReadOnlyList<TradeListItem>>>>
        _behaviors = new();
    private readonly List<int> _requestedLimits = [];
    private readonly TaskCompletionSource<bool> _readStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _releaseRead =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int CallCount { get; private set; }

    public IReadOnlyList<int> RequestedLimits => _requestedLimits;

    public CancellationToken CancellationToken { get; private set; }

    public bool HoldRead { get; set; }

    public Task ReadStarted => _readStarted.Task;

    public void ReleaseRead()
    {
        _releaseRead.TrySetResult(true);
    }

    public void EnqueueResult(IReadOnlyList<TradeListItem> trades)
    {
        _behaviors.Enqueue(_ => Task.FromResult(trades));
    }

    public void EnqueueException(Exception exception)
    {
        _behaviors.Enqueue(_ =>
            Task.FromException<IReadOnlyList<TradeListItem>>(exception));
    }

    public async Task<IReadOnlyList<TradeListItem>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        _requestedLimits.Add(limit);
        CancellationToken = cancellationToken;
        _readStarted.TrySetResult(true);

        if (HoldRead)
        {
            _ = await _releaseRead.Task.WaitAsync(cancellationToken);
        }

        return _behaviors.Count == 0
            ? []
            : await _behaviors.Dequeue()(cancellationToken);
    }
}
