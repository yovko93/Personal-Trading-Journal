using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradingMistakeStore : ITradingMistakeStore
{
    public TradingMistake? Mistake { get; set; } public Exception? AddException { get; set; } public Exception? UpdateException { get; set; }
    public int AddCalls { get; private set; } public int UpdateCalls { get; private set; } public Guid RequestedId { get; private set; }
    public Task AddAsync(TradingMistake mistake, CancellationToken token = default)
    { AddCalls++; Mistake = mistake; return AddException is null ? Task.CompletedTask : Task.FromException(AddException); }
    public Task<TradingMistake?> GetByIdAsync(Guid id, CancellationToken token = default)
    { RequestedId = id; return Task.FromResult(Mistake); }
    public Task UpdateAsync(TradingMistake mistake, CancellationToken token = default)
    { UpdateCalls++; Mistake = mistake; return UpdateException is null ? Task.CompletedTask : Task.FromException(UpdateException); }
}
