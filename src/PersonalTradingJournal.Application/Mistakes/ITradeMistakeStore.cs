using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Application.Mistakes;

public interface ITradeMistakeStore
{
    Task<bool> ExistsAsync(
        Guid tradeId,
        Guid tradingMistakeId,
        CancellationToken cancellationToken = default);

    Task<TradeMistake?> GetByIdAsync(
        Guid tradeMistakeId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        TradeMistake tradeMistake,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        Guid tradeMistakeId,
        CancellationToken cancellationToken = default);
}
