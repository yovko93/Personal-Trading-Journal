using PersonalTradingJournal.Application.Screenshots;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeScreenshotContentReader :
    ITradeScreenshotContentReader
{
    private readonly Queue<Func<Task<TradeScreenshotContent?>>> _behaviors = new();
    private readonly List<Guid> _requestedScreenshotIds = [];
    private readonly TaskCompletionSource<bool> _readStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _releaseRead =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int CallCount { get; private set; }

    public IReadOnlyList<Guid> RequestedScreenshotIds => _requestedScreenshotIds;

    public CancellationToken CancellationToken { get; private set; }

    public bool HoldRead { get; set; }

    public Task ReadStarted => _readStarted.Task;

    public void ReleaseRead() => _releaseRead.TrySetResult(true);

    public void EnqueueResult(TradeScreenshotContent? content)
    {
        _behaviors.Enqueue(() => Task.FromResult(content));
    }

    public void EnqueueException(Exception exception)
    {
        _behaviors.Enqueue(() =>
            Task.FromException<TradeScreenshotContent?>(exception));
    }

    public async Task<TradeScreenshotContent?> OpenAsync(
        Guid screenshotId,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        _requestedScreenshotIds.Add(screenshotId);
        CancellationToken = cancellationToken;
        _readStarted.TrySetResult(true);

        if (HoldRead)
        {
            _ = await _releaseRead.Task.WaitAsync(cancellationToken);
        }

        return _behaviors.Count == 0 ? null : await _behaviors.Dequeue()();
    }
}
