namespace PersonalTradingJournal.Application.Instruments;

public sealed class DeleteInstrumentUseCase
{
    private readonly IInstrumentStore _instrumentStore;
    private readonly IInstrumentDeletionStore _deletionStore;

    public DeleteInstrumentUseCase(
        IInstrumentStore instrumentStore,
        IInstrumentDeletionStore deletionStore)
    {
        ArgumentNullException.ThrowIfNull(instrumentStore);
        ArgumentNullException.ThrowIfNull(deletionStore);
        _instrumentStore = instrumentStore;
        _deletionStore = deletionStore;
    }

    public async Task<DeleteInstrumentResult> ExecuteAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        if (instrumentId == Guid.Empty)
        {
            throw new ArgumentException(
                "An instrument identifier is required.",
                nameof(instrumentId));
        }

        if (await _instrumentStore.GetByIdAsync(instrumentId, cancellationToken) is null)
        {
            throw new KeyNotFoundException(
                $"Instrument '{instrumentId}' was not found.");
        }

        if (await _deletionStore.HasTradesAsync(instrumentId, cancellationToken))
        {
            return DeleteInstrumentResult.Referenced;
        }

        try
        {
            await _deletionStore.DeleteAsync(instrumentId, cancellationToken);
            return DeleteInstrumentResult.Deleted;
        }
        catch (InstrumentDeleteBlockedException)
        {
            return DeleteInstrumentResult.Referenced;
        }
    }
}
