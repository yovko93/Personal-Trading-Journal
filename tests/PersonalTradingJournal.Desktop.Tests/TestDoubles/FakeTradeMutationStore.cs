using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeMutationStore : ITradeMutationStore
{
    private readonly TaskCompletionSource<bool> _saveStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _releaseSave =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Trade? TradeToReturn { get; set; }

    public Trade? SavedTrade { get; private set; }

    public Exception? GetException { get; set; }

    public Exception? SaveException { get; set; }

    public int GetCallCount { get; private set; }

    public int SaveCallCount { get; private set; }

    public Guid? RequestedTradeId { get; private set; }

    public CancellationToken GetCancellationToken { get; private set; }

    public CancellationToken SaveCancellationToken { get; private set; }

    public bool HoldSave { get; set; }

    public Task SaveStarted => _saveStarted.Task;

    public void ReleaseSave() => _releaseSave.TrySetResult(true);

    public Task<Trade?> GetByIdAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default)
    {
        GetCallCount++;
        RequestedTradeId = tradeId;
        GetCancellationToken = cancellationToken;

        return GetException is not null
            ? Task.FromException<Trade?>(GetException)
            : Task.FromResult(TradeToReturn);
    }

    public async Task SaveAsync(
        Trade trade,
        CancellationToken cancellationToken = default)
    {
        SaveCallCount++;
        SavedTrade = trade;
        SaveCancellationToken = cancellationToken;
        _saveStarted.TrySetResult(true);

        if (SaveException is not null)
        {
            throw SaveException;
        }

        if (HoldSave)
        {
            await _releaseSave.Task.WaitAsync(cancellationToken);
        }
    }
}
