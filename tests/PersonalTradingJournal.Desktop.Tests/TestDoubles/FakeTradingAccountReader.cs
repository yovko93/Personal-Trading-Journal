using PersonalTradingJournal.Application.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradingAccountReader : ITradingAccountReader
{
    private readonly Queue<Func<CancellationToken, Task<IReadOnlyList<AccountListItem>>>>
        _behaviors = new();

    public int CallCount { get; private set; }

    public void EnqueueResult(IReadOnlyList<AccountListItem> accounts)
    {
        _behaviors.Enqueue(_ => Task.FromResult(accounts));
    }

    public void EnqueueException(Exception exception)
    {
        _behaviors.Enqueue(_ =>
            Task.FromException<IReadOnlyList<AccountListItem>>(exception));
    }

    public Task<IReadOnlyList<AccountListItem>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        CallCount++;

        return _behaviors.Count == 0
            ? Task.FromResult<IReadOnlyList<AccountListItem>>([])
            : _behaviors.Dequeue()(cancellationToken);
    }
}
