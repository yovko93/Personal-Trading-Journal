namespace PersonalTradingJournal.Application.Setups;

public interface ITradingSetupDeletionStore
{
    Task<bool> HasTradesAsync(
        Guid setupId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid setupId,
        CancellationToken cancellationToken = default);
}
