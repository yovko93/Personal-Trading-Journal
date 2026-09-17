using PersonalTradingJournal.Application.Setups;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradingSetupDeletionStore : ITradingSetupDeletionStore
{
    public bool HasTrades { get; set; }
    public Exception? DeleteException { get; set; }
    public int HasTradesCallCount { get; private set; }
    public int DeleteCallCount { get; private set; }

    public Task<bool> HasTradesAsync(
        Guid setupId,
        CancellationToken cancellationToken = default)
    {
        HasTradesCallCount++;
        return Task.FromResult(HasTrades);
    }

    public Task DeleteAsync(
        Guid setupId,
        CancellationToken cancellationToken = default)
    {
        DeleteCallCount++;
        return DeleteException is null
            ? Task.CompletedTask
            : Task.FromException(DeleteException);
    }
}
