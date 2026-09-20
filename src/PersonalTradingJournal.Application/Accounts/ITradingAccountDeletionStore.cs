namespace PersonalTradingJournal.Application.Accounts;

public interface ITradingAccountDeletionStore
{
    Task<bool> HasTradesAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);
}
