namespace PersonalTradingJournal.Application.Accounts;

public interface ITradingAccountReader
{
    Task<IReadOnlyList<AccountListItem>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<TradingAccountDetails?> GetByIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);
}
