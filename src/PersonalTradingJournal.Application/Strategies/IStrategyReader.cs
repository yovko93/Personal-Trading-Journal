namespace PersonalTradingJournal.Application.Strategies;

public interface IStrategyReader
{
    Task<IReadOnlyList<StrategyListItem>> GetAllAsync(
        CancellationToken cancellationToken = default);
}
