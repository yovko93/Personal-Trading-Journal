using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Trades;

public interface ITradeStore
{
    Task AddAsync(
        Trade trade,
        CancellationToken cancellationToken = default);
}
