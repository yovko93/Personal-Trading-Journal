using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Instruments;

public sealed class UpdateInstrumentUseCase
{
    private readonly IInstrumentStore _instrumentStore;
    private readonly IInstrumentDeletionStore _deletionStore;
    private readonly TimeProvider _timeProvider;

    public UpdateInstrumentUseCase(
        IInstrumentStore instrumentStore,
        IInstrumentDeletionStore deletionStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(instrumentStore);
        ArgumentNullException.ThrowIfNull(deletionStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _instrumentStore = instrumentStore;
        _deletionStore = deletionStore;
        _timeProvider = timeProvider;
    }

    public async Task<UpdateInstrumentResult> ExecuteAsync(
        UpdateInstrumentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.InstrumentId == Guid.Empty)
        {
            throw new ArgumentException(
                "An instrument identifier is required.",
                nameof(command));
        }

        Instrument? instrument = await _instrumentStore.GetByIdAsync(
            command.InstrumentId,
            cancellationToken);
        if (instrument is null)
        {
            throw new KeyNotFoundException(
                $"Instrument '{command.InstrumentId}' was not found.");
        }

        if (instrument.AssetClass != command.AssetClass &&
            await _deletionStore.HasTradesAsync(command.InstrumentId, cancellationToken))
        {
            throw new InstrumentAssetClassChangeBlockedException(
                "The asset class cannot be changed because the instrument is used by existing trades.");
        }

        bool wasChanged = instrument.UpdateDetails(
            command.Symbol,
            command.DisplayName,
            command.AssetClass,
            command.Exchange,
            command.Currency,
            command.TickSize,
            command.TickValue,
            _timeProvider.GetUtcNow());

        if (wasChanged)
        {
            await _instrumentStore.UpdateAsync(instrument, cancellationToken);
        }

        return new UpdateInstrumentResult(
            InstrumentDetails.FromDomain(instrument),
            wasChanged);
    }
}
