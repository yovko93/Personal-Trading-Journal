using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradingSetupStore : ITradingSetupStore
{
    public TradingSetup? Setup { get; set; }
    public Exception? AddException { get; set; }
    public Exception? GetException { get; set; }
    public Exception? UpdateException { get; set; }
    public Guid RequestedId { get; private set; }
    public int AddCallCount { get; private set; }
    public int GetCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public CancellationToken GetCancellationToken { get; private set; }
    public Task AddAsync(TradingSetup setup, CancellationToken cancellationToken = default)
    { AddCallCount++; Setup = setup; return AddException is null ? Task.CompletedTask : Task.FromException(AddException); }
    public Task<TradingSetup?> GetByIdAsync(Guid setupId, CancellationToken cancellationToken = default)
    {
        GetCallCount++;
        RequestedId = setupId;
        GetCancellationToken = cancellationToken;
        return GetException is null
            ? Task.FromResult(Setup)
            : Task.FromException<TradingSetup?>(GetException);
    }
    public Task UpdateAsync(TradingSetup setup, CancellationToken cancellationToken = default)
    { UpdateCallCount++; Setup = setup; return UpdateException is null ? Task.CompletedTask : Task.FromException(UpdateException); }
}
