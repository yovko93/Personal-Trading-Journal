using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeDeletionStore : ITradeDeletionStore
{
    private readonly TaskCompletionSource<bool> _deleteStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _releaseDelete =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TradeDeletionInfo? Result { get; set; }
    public Exception? Exception { get; set; }
    public bool HoldDelete { get; set; }
    public int CallCount { get; private set; }
    public Guid RequestedTradeId { get; private set; }
    public CancellationToken CancellationToken { get; private set; }
    public Task DeleteStarted => _deleteStarted.Task;

    public void ReleaseDelete() => _releaseDelete.TrySetResult(true);

    public async Task<TradeDeletionInfo?> DeleteAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        RequestedTradeId = tradeId;
        CancellationToken = cancellationToken;
        _deleteStarted.TrySetResult(true);

        if (Exception is not null)
        {
            throw Exception;
        }

        if (HoldDelete)
        {
            await _releaseDelete.Task.WaitAsync(cancellationToken);
        }

        return Result;
    }
}
