namespace PersonalTradingJournal.Application.Strategies;

public interface IStrategyNameChecker
{
    Task<bool> ExistsAsync(
        string normalizedName,
        Guid? excludingStrategyId = null,
        CancellationToken cancellationToken = default);
}
