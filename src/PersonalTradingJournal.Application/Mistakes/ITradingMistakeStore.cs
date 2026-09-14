using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Application.Mistakes;

public interface ITradingMistakeStore
{
    Task AddAsync(TradingMistake mistake, CancellationToken cancellationToken = default);
    Task<TradingMistake?> GetByIdAsync(Guid mistakeId, CancellationToken cancellationToken = default);
    Task UpdateAsync(TradingMistake mistake, CancellationToken cancellationToken = default);
}
