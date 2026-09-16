using PersonalTradingJournal.Application.Instruments;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeInstrumentReader : IInstrumentReader
{
    private readonly Queue<Func<CancellationToken, Task<IReadOnlyList<InstrumentListItem>>>>
        _behaviors = new();

    public InstrumentDetails? DetailsToReturn { get; set; }

    public Exception? DetailsException { get; set; }

    public int DetailsCallCount { get; private set; }

    public int CallCount { get; private set; }

    public void EnqueueResult(IReadOnlyList<InstrumentListItem> instruments)
    {
        _behaviors.Enqueue(_ => Task.FromResult(instruments));
    }

    public void EnqueueException(Exception exception)
    {
        _behaviors.Enqueue(_ =>
            Task.FromException<IReadOnlyList<InstrumentListItem>>(exception));
    }

    public Task<IReadOnlyList<InstrumentListItem>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        CallCount++;

        return _behaviors.Count == 0
            ? Task.FromResult<IReadOnlyList<InstrumentListItem>>([])
            : _behaviors.Dequeue()(cancellationToken);
    }

    public Task<InstrumentDetails?> GetByIdAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        DetailsCallCount++;
        if (DetailsException is not null)
        {
            return Task.FromException<InstrumentDetails?>(DetailsException);
        }

        return Task.FromResult(
            DetailsToReturn?.Id == instrumentId ? DetailsToReturn : null);
    }
}
