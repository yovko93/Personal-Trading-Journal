using PersonalTradingJournal.Application.Mistakes;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradeMistakeReader : ITradeMistakeReader
{
    private readonly Queue<object> _outcomes = new();

    public int CallCount { get; private set; }

    public Guid RequestedTradeId { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public void EnqueueResult(IReadOnlyList<TradeMistakeListItem> result) =>
        _outcomes.Enqueue(result);

    public void EnqueueException(Exception exception) =>
        _outcomes.Enqueue(exception);

    public Task<IReadOnlyList<TradeMistakeListItem>> GetByTradeIdAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        RequestedTradeId = tradeId;
        CancellationToken = cancellationToken;
        object outcome = _outcomes.Count == 0
            ? Array.Empty<TradeMistakeListItem>()
            : _outcomes.Dequeue();
        return outcome is Exception exception
            ? Task.FromException<IReadOnlyList<TradeMistakeListItem>>(exception)
            : Task.FromResult((IReadOnlyList<TradeMistakeListItem>)outcome);
    }
}
