namespace PersonalTradingJournal.Application.Setups;

public interface ITradingSetupReader
{
    Task<IReadOnlyList<TradingSetupListItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TradingSetupDetails?> GetByIdAsync(
        Guid setupId,
        CancellationToken cancellationToken = default);
}
