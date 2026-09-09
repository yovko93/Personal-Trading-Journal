using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Application.Accounts;

public interface ITradingAccountStore
{
    Task AddAsync(
        TradingAccount account,
        CancellationToken cancellationToken = default);

    Task<TradingAccount?> GetByIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        TradingAccount account,
        CancellationToken cancellationToken = default);
}
