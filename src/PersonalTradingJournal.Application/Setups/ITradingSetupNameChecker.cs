namespace PersonalTradingJournal.Application.Setups;

public interface ITradingSetupNameChecker
{
    Task<bool> ExistsAsync(string normalizedName, Guid? excludingSetupId = null,
        CancellationToken cancellationToken = default);
}
