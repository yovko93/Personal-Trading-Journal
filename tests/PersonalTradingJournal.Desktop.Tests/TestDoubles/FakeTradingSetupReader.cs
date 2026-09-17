using PersonalTradingJournal.Application.Setups;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradingSetupReader : ITradingSetupReader
{
    private readonly Queue<object> _outcomes = new();
    public TradingSetupDetails? DetailsToReturn { get; set; }
    public Exception? DetailsException { get; set; }
    public int CallCount { get; private set; }
    public int DetailsCallCount { get; private set; }
    public Guid RequestedId { get; private set; }
    public CancellationToken CancellationToken { get; private set; }
    public CancellationToken DetailsCancellationToken { get; private set; }
    public void EnqueueResult(IReadOnlyList<TradingSetupListItem> result) => _outcomes.Enqueue(result);
    public void EnqueueException(Exception exception) => _outcomes.Enqueue(exception);
    public Task<IReadOnlyList<TradingSetupListItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        CallCount++; CancellationToken = cancellationToken;
        object outcome = _outcomes.Count == 0 ? Array.Empty<TradingSetupListItem>() : _outcomes.Dequeue();
        return outcome is Exception ex ? Task.FromException<IReadOnlyList<TradingSetupListItem>>(ex)
            : Task.FromResult((IReadOnlyList<TradingSetupListItem>)outcome);
    }

    public Task<TradingSetupDetails?> GetByIdAsync(
        Guid setupId,
        CancellationToken cancellationToken = default)
    {
        DetailsCallCount++;
        RequestedId = setupId;
        DetailsCancellationToken = cancellationToken;
        return DetailsException is null
            ? Task.FromResult(DetailsToReturn?.Id == setupId ? DetailsToReturn : null)
            : Task.FromException<TradingSetupDetails?>(DetailsException);
    }
}
