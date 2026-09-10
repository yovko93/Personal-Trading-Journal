using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeManualTradeReferenceDataReader :
    IManualTradeReferenceDataReader
{
    private readonly Queue<Func<CancellationToken, Task<ManualTradeReferenceData>>>
        _behaviors = new();
    private readonly List<bool> _includeInactiveRequests = [];

    public int CallCount { get; private set; }

    public IReadOnlyList<bool> IncludeInactiveRequests => _includeInactiveRequests;

    public CancellationToken CancellationToken { get; private set; }

    public void EnqueueResult(ManualTradeReferenceData referenceData)
    {
        _behaviors.Enqueue(_ => Task.FromResult(referenceData));
    }

    public void EnqueueException(Exception exception)
    {
        _behaviors.Enqueue(_ =>
            Task.FromException<ManualTradeReferenceData>(exception));
    }

    public Task<ManualTradeReferenceData> GetAsync(
        bool includeInactiveReferences = false,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        _includeInactiveRequests.Add(includeInactiveReferences);
        CancellationToken = cancellationToken;

        return _behaviors.Count == 0
            ? Task.FromResult(new ManualTradeReferenceData([], []))
            : _behaviors.Dequeue()(cancellationToken);
    }
}
