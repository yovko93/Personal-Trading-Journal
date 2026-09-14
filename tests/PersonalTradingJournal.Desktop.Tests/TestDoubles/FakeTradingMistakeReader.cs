using PersonalTradingJournal.Application.Mistakes;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradingMistakeReader : ITradingMistakeReader
{
    private readonly Queue<object> _outcomes = new();
    public int CallCount { get; private set; }
    public CancellationToken Token { get; private set; }
    public void EnqueueResult(IReadOnlyList<TradingMistakeListItem> value) => _outcomes.Enqueue(value);
    public void EnqueueException(Exception value) => _outcomes.Enqueue(value);
    public Task<IReadOnlyList<TradingMistakeListItem>> GetAllAsync(CancellationToken token = default)
    { CallCount++; Token = token; object value = _outcomes.Count == 0 ? Array.Empty<TradingMistakeListItem>() : _outcomes.Dequeue();
      return value is Exception ex ? Task.FromException<IReadOnlyList<TradingMistakeListItem>>(ex) : Task.FromResult((IReadOnlyList<TradingMistakeListItem>)value); }
}
