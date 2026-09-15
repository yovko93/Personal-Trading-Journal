using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Setups;

public interface ITradingSetupStore
{
    Task AddAsync(TradingSetup setup, CancellationToken cancellationToken = default);
    Task<TradingSetup?> GetByIdAsync(Guid setupId, CancellationToken cancellationToken = default);
    Task UpdateAsync(TradingSetup setup, CancellationToken cancellationToken = default);
}
