using System.Collections.Concurrent;
using PersonalTradingJournal.Application.Calendar;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

public sealed class FakeTradingCalendarDayReader : ITradingCalendarDayReader
{
    public ConcurrentQueue<(TradingCalendarDayQuery Query, CancellationToken Token)> Calls { get; } = new();
    public Func<TradingCalendarDayQuery, CancellationToken, Task<TradingCalendarDayDetails>> Handler { get; set; } =
        (query, token) => Task.FromResult(TradingCalendarDayDetails.Create(query.Date, [], token));
    public Task<TradingCalendarDayDetails> GetAsync(TradingCalendarDayQuery query, CancellationToken cancellationToken = default)
    {
        Calls.Enqueue((query, cancellationToken));
        return Handler(query, cancellationToken);
    }
}
