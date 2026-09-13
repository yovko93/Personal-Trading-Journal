using PersonalTradingJournal.Application.Screenshots;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeScreenshotDeletionStore :
    ITradeScreenshotDeletionStore
{
    private readonly TaskCompletionSource<bool> _deleteStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _releaseDelete =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int CallCount { get; private set; }

    public Guid TradeId { get; private set; }

    public Guid ScreenshotId { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public TradeScreenshotDeletionInfo? Result { get; set; }

    public Exception? Exception { get; set; }

    public bool HoldDelete { get; set; }

    public Task DeleteStarted => _deleteStarted.Task;

    public void ReleaseDelete() => _releaseDelete.TrySetResult(true);

    public async Task<TradeScreenshotDeletionInfo?> DeleteAsync(
        Guid tradeId,
        Guid screenshotId,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        TradeId = tradeId;
        ScreenshotId = screenshotId;
        CancellationToken = cancellationToken;
        _deleteStarted.TrySetResult(true);

        if (HoldDelete)
        {
            _ = await _releaseDelete.Task.WaitAsync(cancellationToken);
        }

        if (Exception is not null)
        {
            throw Exception;
        }

        return Result;
    }
}
