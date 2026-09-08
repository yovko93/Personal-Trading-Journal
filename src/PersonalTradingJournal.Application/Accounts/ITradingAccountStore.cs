using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Application.Accounts;

public interface ITradingAccountStore
{
    Task AddAsync(
        TradingAccount account,
        CancellationToken cancellationToken = default);
}
