using PersonalTradingJournal.Application.Instruments;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeInstrumentReader : IInstrumentReader
{
    private readonly Queue<Func<CancellationToken, Task<IReadOnlyList<InstrumentListItem>>>>
        _behaviors = new();

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
}
