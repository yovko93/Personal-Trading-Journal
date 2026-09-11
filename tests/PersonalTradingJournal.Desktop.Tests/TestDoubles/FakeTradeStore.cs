using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeStore : ITradeStore
{
    private readonly TaskCompletionSource<bool> _addStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _releaseAdd =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Trade? AddedTrade { get; private set; }

    public int AddCallCount { get; private set; }

    public Exception? AddException { get; set; }

    public CancellationToken CancellationToken { get; private set; }

    public bool HoldAdd { get; set; }

    public Task AddStarted => _addStarted.Task;

    public void ReleaseAdd()
    {
        _releaseAdd.TrySetResult(true);
    }

    public async Task AddAsync(
        Trade trade,
        CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        AddedTrade = trade;
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
