using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Domain.Screenshots;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeScreenshotStore : ITradeScreenshotStore
{
    private readonly TaskCompletionSource<bool> _addStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _releaseAdd =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int AddCallCount { get; private set; }

    public TradeScreenshot? AddedScreenshot { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public Exception? AddException { get; set; }

    public bool HoldAdd { get; set; }

    public Task AddStarted => _addStarted.Task;

    public void ReleaseAdd() => _releaseAdd.TrySetResult(true);

    public async Task AddAsync(
        TradeScreenshot screenshot,
        CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        AddedScreenshot = screenshot;
        CancellationToken = cancellationToken;
        _addStarted.TrySetResult(true);

        if (AddException is not null)
        {
            throw AddException;
        }

        if (HoldAdd)
        {
            _ = await _releaseAdd.Task.WaitAsync(cancellationToken);
        }
    }
}
