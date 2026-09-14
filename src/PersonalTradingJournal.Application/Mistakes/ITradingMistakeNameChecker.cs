namespace PersonalTradingJournal.Application.Mistakes;

public interface ITradingMistakeNameChecker
{
    Task<bool> ExistsAsync(string normalizedName, Guid? excludingMistakeId = null,
        CancellationToken cancellationToken = default);
}
