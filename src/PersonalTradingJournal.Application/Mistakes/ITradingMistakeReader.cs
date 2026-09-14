namespace PersonalTradingJournal.Application.Mistakes;

public interface ITradingMistakeReader
{
    Task<IReadOnlyList<TradingMistakeListItem>> GetAllAsync(CancellationToken cancellationToken = default);
}
