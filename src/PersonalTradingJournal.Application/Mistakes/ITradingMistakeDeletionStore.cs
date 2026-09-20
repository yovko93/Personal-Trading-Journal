namespace PersonalTradingJournal.Application.Mistakes;

public interface ITradingMistakeDeletionStore
{
    Task<bool> HasTradeMistakesAsync(
        Guid mistakeId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid mistakeId,
        CancellationToken cancellationToken = default);
}
