using PersonalTradingJournal.Application.Instruments;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeInstrumentDeletionStore : IInstrumentDeletionStore
{
    public bool HasTrades { get; set; }

    public Exception? DeleteException { get; set; }

    public int HasTradesCallCount { get; private set; }

    public int DeleteCallCount { get; private set; }

    public Task<bool> HasTradesAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        HasTradesCallCount++;
        return Task.FromResult(HasTrades);
    }

    public Task DeleteAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        DeleteCallCount++;
        return DeleteException is null
            ? Task.CompletedTask
            : Task.FromException(DeleteException);
    }
}
