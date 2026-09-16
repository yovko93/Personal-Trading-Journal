using PersonalTradingJournal.Application.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradingAccountDeletionStore : ITradingAccountDeletionStore
{
    public bool HasTrades { get; set; }

    public Exception? ReferenceCheckException { get; set; }

    public Exception? DeleteException { get; set; }

    public TaskCompletionSource<bool>? HasTradesCompletion { get; set; }

    public int ReferenceCheckCallCount { get; private set; }

    public int DeleteCallCount { get; private set; }

    public Guid LastAccountId { get; private set; }

    public Task<bool> HasTradesAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        ReferenceCheckCallCount++;
        LastAccountId = accountId;

        if (ReferenceCheckException is not null)
        {
            return Task.FromException<bool>(ReferenceCheckException);
        }

        return HasTradesCompletion?.Task ?? Task.FromResult(HasTrades);
    }

    public Task DeleteAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        DeleteCallCount++;
        LastAccountId = accountId;

        return DeleteException is null
            ? Task.CompletedTask
            : Task.FromException(DeleteException);
    }
}
