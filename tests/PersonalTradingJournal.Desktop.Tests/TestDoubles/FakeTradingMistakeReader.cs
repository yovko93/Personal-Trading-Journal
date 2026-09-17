using PersonalTradingJournal.Application.Mistakes;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradingMistakeReader : ITradingMistakeReader
{
    private readonly Queue<object> _outcomes = new();
    public TradingMistakeDetails? DetailsToReturn { get; set; }
    public Exception? DetailsException { get; set; }
    public int CallCount { get; private set; }
    public int DetailsCallCount { get; private set; }
    public Guid RequestedId { get; private set; }
    public CancellationToken Token { get; private set; }
    public CancellationToken DetailsToken { get; private set; }
    public void EnqueueResult(IReadOnlyList<TradingMistakeListItem> value) => _outcomes.Enqueue(value);
    public void EnqueueException(Exception value) => _outcomes.Enqueue(value);
    public Task<IReadOnlyList<TradingMistakeListItem>> GetAllAsync(CancellationToken token = default)
    { CallCount++; Token = token; object value = _outcomes.Count == 0 ? Array.Empty<TradingMistakeListItem>() : _outcomes.Dequeue();
      return value is Exception ex ? Task.FromException<IReadOnlyList<TradingMistakeListItem>>(ex) : Task.FromResult((IReadOnlyList<TradingMistakeListItem>)value); }

    public Task<TradingMistakeDetails?> GetByIdAsync(
        Guid mistakeId,
        CancellationToken cancellationToken = default)
    {
        DetailsCallCount++;
        RequestedId = mistakeId;
        DetailsToken = cancellationToken;
        return DetailsException is null
            ? Task.FromResult(DetailsToReturn?.Id == mistakeId ? DetailsToReturn : null)
            : Task.FromException<TradingMistakeDetails?>(DetailsException);
    }
}
