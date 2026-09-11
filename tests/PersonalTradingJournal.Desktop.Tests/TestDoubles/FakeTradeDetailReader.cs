using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeDetailReader : ITradeDetailReader
{
    private readonly Queue<Func<CancellationToken, Task<TradeDetail?>>> _behaviors =
        new();
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

    public void ReleaseRead()
    {
        _releaseRead.TrySetResult(true);
    }

    public void EnqueueResult(TradeDetail? detail)
    {
        _behaviors.Enqueue(_ => Task.FromResult(detail));
    }

    public void EnqueueException(Exception exception)
    {
        _behaviors.Enqueue(_ => Task.FromException<TradeDetail?>(exception));
    }

    public async Task<TradeDetail?> GetByIdAsync(
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

        return _behaviors.Count == 0
            ? null
            : await _behaviors.Dequeue()(cancellationToken);
    }
}
