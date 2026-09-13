using PersonalTradingJournal.Application.Screenshots;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeScreenshotReader : ITradeScreenshotReader
{
    private readonly Queue<Func<Task<IReadOnlyList<TradeScreenshotListItem>>>>
        _behaviors = new();
    private readonly List<Guid> _requestedTradeIds = [];
    private readonly TaskCompletionSource<bool> _readStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _releaseRead =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int CallCount { get; private set; }

    public IReadOnlyList<Guid> RequestedTradeIds => _requestedTradeIds;

    public CancellationToken CancellationToken { get; private set; }

    public bool HoldRead { get; set; }

    public Task ReadStarted => _readStarted.Task;

    public void ReleaseRead() => _releaseRead.TrySetResult(true);

    public void EnqueueResult(IReadOnlyList<TradeScreenshotListItem> screenshots)
    {
        _behaviors.Enqueue(() => Task.FromResult(screenshots));
    }

    public void EnqueueException(Exception exception)
    {
        _behaviors.Enqueue(() =>
            Task.FromException<IReadOnlyList<TradeScreenshotListItem>>(exception));
    }

    public async Task<IReadOnlyList<TradeScreenshotListItem>> GetForTradeAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        _requestedTradeIds.Add(tradeId);
        CancellationToken = cancellationToken;
        _readStarted.TrySetResult(true);

        if (HoldRead)
        {
            _ = await _releaseRead.Task.WaitAsync(cancellationToken);
        }

        return _behaviors.Count == 0 ? [] : await _behaviors.Dequeue()();
    }
}
