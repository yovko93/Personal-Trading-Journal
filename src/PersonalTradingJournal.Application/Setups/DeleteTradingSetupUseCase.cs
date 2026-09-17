namespace PersonalTradingJournal.Application.Setups;

public sealed class DeleteTradingSetupUseCase
{
    private readonly ITradingSetupStore _store;
    private readonly ITradingSetupDeletionStore _deletionStore;

    public DeleteTradingSetupUseCase(
        ITradingSetupStore store,
        ITradingSetupDeletionStore deletionStore)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(deletionStore);
        _store = store;
        _deletionStore = deletionStore;
    }

    public async Task<DeleteTradingSetupResult> ExecuteAsync(
        Guid setupId,
        CancellationToken cancellationToken = default)
    {
        if (setupId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trading setup identifier is required.",
                nameof(setupId));
        }

        if (await _store.GetByIdAsync(setupId, cancellationToken) is null)
        {
            throw new KeyNotFoundException(
                $"Trading setup '{setupId}' was not found.");
        }

        if (await _deletionStore.HasTradesAsync(setupId, cancellationToken))
        {
            return DeleteTradingSetupResult.Referenced;
        }

        try
        {
            await _deletionStore.DeleteAsync(setupId, cancellationToken);
            return DeleteTradingSetupResult.Deleted;
        }
        catch (TradingSetupDeleteBlockedException)
        {
            return DeleteTradingSetupResult.Referenced;
        }
    }
}
