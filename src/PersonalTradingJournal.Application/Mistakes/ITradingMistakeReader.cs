namespace PersonalTradingJournal.Application.Mistakes;

public interface ITradingMistakeReader
{
    Task<IReadOnlyList<TradingMistakeListItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TradingMistakeDetails?> GetByIdAsync(
        Guid mistakeId,
        CancellationToken cancellationToken = default);
}
