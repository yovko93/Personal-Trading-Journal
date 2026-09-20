using PersonalTradingJournal.Application.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradingAccountReader : ITradingAccountReader
{
    private readonly Queue<Func<CancellationToken, Task<IReadOnlyList<AccountListItem>>>>
        _behaviors = new();
    private readonly Queue<Func<CancellationToken, Task<TradingAccountDetails?>>>
        _detailBehaviors = new();

    public int CallCount { get; private set; }

    public int DetailCallCount { get; private set; }

    public void EnqueueResult(IReadOnlyList<AccountListItem> accounts)
    {
        _behaviors.Enqueue(_ => Task.FromResult(accounts));
    }

    public void EnqueueException(Exception exception)
    {
        _behaviors.Enqueue(_ =>
            Task.FromException<IReadOnlyList<AccountListItem>>(exception));
    }

    public void EnqueueDetailResult(TradingAccountDetails? account)
    {
        _detailBehaviors.Enqueue(_ => Task.FromResult(account));
    }

    public void EnqueueDetailException(Exception exception)
    {
        _detailBehaviors.Enqueue(_ =>
            Task.FromException<TradingAccountDetails?>(exception));
    }

    public Task<IReadOnlyList<AccountListItem>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        CallCount++;

        return _behaviors.Count == 0
            ? Task.FromResult<IReadOnlyList<AccountListItem>>([])
            : _behaviors.Dequeue()(cancellationToken);
    }

    public Task<TradingAccountDetails?> GetByIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        DetailCallCount++;

        return _detailBehaviors.Count == 0
            ? Task.FromResult<TradingAccountDetails?>(null)
            : _detailBehaviors.Dequeue()(cancellationToken);
    }
}
