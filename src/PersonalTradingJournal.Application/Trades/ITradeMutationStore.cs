using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Trades;

/// <summary>
/// Loads and atomically persists authoritative Trade aggregate mutations.
/// </summary>
public interface ITradeMutationStore
{
    Task<Trade?> GetByIdAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        Trade trade,
        CancellationToken cancellationToken = default);
}
