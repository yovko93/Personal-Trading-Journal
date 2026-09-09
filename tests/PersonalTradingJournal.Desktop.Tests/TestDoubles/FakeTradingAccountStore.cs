using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradingAccountStore : ITradingAccountStore
{
    public TradingAccount? AccountToReturn { get; set; }

    public Exception? AddException { get; set; }

    public Exception? GetException { get; set; }

    public Exception? UpdateException { get; set; }

    public int AddCallCount { get; private set; }

    public int GetCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    public TradingAccount? AddedAccount { get; private set; }

    public TradingAccount? UpdatedAccount { get; private set; }

    public Task AddAsync(
        TradingAccount account,
        CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        AddedAccount = account;

        if (AddException is not null)
        {
            return Task.FromException(AddException);
        }

        AccountToReturn = account;
        return Task.CompletedTask;
    }

    public Task<TradingAccount?> GetByIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        GetCallCount++;

        if (GetException is not null)
        {
            return Task.FromException<TradingAccount?>(GetException);
        }

        TradingAccount? account = AccountToReturn?.Id == accountId
            ? AccountToReturn
            : null;

        return Task.FromResult(account);
    }

    public Task UpdateAsync(
        TradingAccount account,
        CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        UpdatedAccount = account;

        if (UpdateException is not null)
        {
            return Task.FromException(UpdateException);
        }

        AccountToReturn = account;
        return Task.CompletedTask;
    }
}
