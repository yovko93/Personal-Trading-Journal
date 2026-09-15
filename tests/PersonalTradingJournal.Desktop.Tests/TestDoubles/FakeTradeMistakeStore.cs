using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeMistakeStore : ITradeMistakeStore
{
    private readonly TaskCompletionSource<bool> _addStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _releaseAdd =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _removeStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _releaseRemove =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool Exists { get; set; }
    public TradeMistake? TradeMistakeToReturn { get; set; }
    public TradeMistake? AddedTradeMistake { get; private set; }
    public Exception? ExistsException { get; set; }
    public Exception? GetException { get; set; }
    public Exception? AddException { get; set; }
    public Exception? RemoveException { get; set; }
    public bool HoldAdd { get; set; }
    public bool HoldRemove { get; set; }
    public int ExistsCallCount { get; private set; }
    public int GetCallCount { get; private set; }
    public int AddCallCount { get; private set; }
    public int RemoveCallCount { get; private set; }
    public Guid RequestedTradeId { get; private set; }
    public Guid RequestedTradingMistakeId { get; private set; }
    public Guid RequestedTradeMistakeId { get; private set; }
    public CancellationToken CancellationToken { get; private set; }
    public Task AddStarted => _addStarted.Task;
    public Task RemoveStarted => _removeStarted.Task;

    public void ReleaseAdd() => _releaseAdd.TrySetResult(true);
    public void ReleaseRemove() => _releaseRemove.TrySetResult(true);

    public Task<bool> ExistsAsync(
        Guid tradeId,
        Guid tradingMistakeId,
        CancellationToken cancellationToken = default)
    {
        ExistsCallCount++;
        RequestedTradeId = tradeId;
        RequestedTradingMistakeId = tradingMistakeId;
        CancellationToken = cancellationToken;
        return ExistsException is null
            ? Task.FromResult(Exists)
            : Task.FromException<bool>(ExistsException);
    }

    public Task<TradeMistake?> GetByIdAsync(
        Guid tradeMistakeId,
        CancellationToken cancellationToken = default)
    {
        GetCallCount++;
        RequestedTradeMistakeId = tradeMistakeId;
        CancellationToken = cancellationToken;
        return GetException is null
            ? Task.FromResult(TradeMistakeToReturn)
            : Task.FromException<TradeMistake?>(GetException);
    }

    public async Task AddAsync(
        TradeMistake tradeMistake,
        CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        AddedTradeMistake = tradeMistake;
        CancellationToken = cancellationToken;
        _addStarted.TrySetResult(true);
        if (AddException is not null)
        {
            throw AddException;
        }

        if (HoldAdd)
        {
            await _releaseAdd.Task.WaitAsync(cancellationToken);
        }
    }

    public async Task RemoveAsync(
        Guid tradeMistakeId,
        CancellationToken cancellationToken = default)
    {
        RemoveCallCount++;
        RequestedTradeMistakeId = tradeMistakeId;
        CancellationToken = cancellationToken;
        _removeStarted.TrySetResult(true);
        if (RemoveException is not null)
        {
            throw RemoveException;
        }

        if (HoldRemove)
        {
            await _releaseRemove.Task.WaitAsync(cancellationToken);
        }
    }
}
