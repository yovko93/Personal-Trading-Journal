using PersonalTradingJournal.Application.Mistakes;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeTradingMistakeDeletionStore : ITradingMistakeDeletionStore
{
    public bool HasTradeMistakes { get; set; }
    public Exception? DeleteException { get; set; }
    public int HasTradeMistakesCallCount { get; private set; }
    public int DeleteCallCount { get; private set; }

    public Task<bool> HasTradeMistakesAsync(
        Guid mistakeId,
        CancellationToken cancellationToken = default)
    {
        HasTradeMistakesCallCount++;
        return Task.FromResult(HasTradeMistakes);
    }

    public Task DeleteAsync(
        Guid mistakeId,
        CancellationToken cancellationToken = default)
    {
        DeleteCallCount++;
        return DeleteException is null
            ? Task.CompletedTask
            : Task.FromException(DeleteException);
    }
}
