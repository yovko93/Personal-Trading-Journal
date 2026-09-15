namespace PersonalTradingJournal.Application.Setups;

public interface ITradingSetupReader
{
    Task<IReadOnlyList<TradingSetupListItem>> GetAllAsync(CancellationToken cancellationToken = default);
}
