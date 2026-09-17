namespace PersonalTradingJournal.Application.Mistakes;

public sealed class DeleteTradingMistakeUseCase
{
    private readonly ITradingMistakeStore _store;
    private readonly ITradingMistakeDeletionStore _deletionStore;

    public DeleteTradingMistakeUseCase(
        ITradingMistakeStore store,
        ITradingMistakeDeletionStore deletionStore)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(deletionStore);
        _store = store;
        _deletionStore = deletionStore;
    }

    public async Task<DeleteTradingMistakeResult> ExecuteAsync(
        Guid mistakeId,
        CancellationToken cancellationToken = default)
    {
        if (mistakeId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trading mistake identifier is required.",
                nameof(mistakeId));
        }

        if (await _store.GetByIdAsync(mistakeId, cancellationToken) is null)
        {
            throw new KeyNotFoundException(
                $"Trading mistake '{mistakeId}' was not found.");
        }

        if (await _deletionStore.HasTradeMistakesAsync(
                mistakeId,
                cancellationToken))
        {
            return DeleteTradingMistakeResult.Referenced;
        }

        try
        {
            await _deletionStore.DeleteAsync(mistakeId, cancellationToken);
            return DeleteTradingMistakeResult.Deleted;
        }
        catch (TradingMistakeDeleteBlockedException)
        {
            return DeleteTradingMistakeResult.Referenced;
        }
    }
}
