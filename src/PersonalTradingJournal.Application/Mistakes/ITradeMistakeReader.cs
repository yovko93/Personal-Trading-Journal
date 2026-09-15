namespace PersonalTradingJournal.Application.Mistakes;

public interface ITradeMistakeReader
{
    Task<IReadOnlyList<TradeMistakeListItem>> GetByTradeIdAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default);
}
