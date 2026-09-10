using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Instruments;

public sealed class InstrumentLifecycleUseCase
{
    private readonly IInstrumentStore _instrumentStore;
    private readonly TimeProvider _timeProvider;

    public InstrumentLifecycleUseCase(
        IInstrumentStore instrumentStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(instrumentStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _instrumentStore = instrumentStore;
        _timeProvider = timeProvider;
    }

    public async Task ActivateAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        Instrument instrument = await GetRequiredInstrumentAsync(
            instrumentId,
            cancellationToken);

        if (instrument.IsActive)
        {
            return;
        }

        instrument.Activate(_timeProvider.GetUtcNow());
        await _instrumentStore.UpdateAsync(instrument, cancellationToken);
    }

    public async Task DeactivateAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        Instrument instrument = await GetRequiredInstrumentAsync(
            instrumentId,
            cancellationToken);

        if (!instrument.IsActive)
        {
            return;
        }

        instrument.Deactivate(_timeProvider.GetUtcNow());
        await _instrumentStore.UpdateAsync(instrument, cancellationToken);
    }

    private async Task<Instrument> GetRequiredInstrumentAsync(
        Guid instrumentId,
        CancellationToken cancellationToken)
    {
        if (instrumentId == Guid.Empty)
        {
            throw new ArgumentException(
                "An instrument identifier is required.",
                nameof(instrumentId));
        }

        Instrument? instrument = await _instrumentStore.GetByIdAsync(
            instrumentId,
            cancellationToken);

        return instrument ?? throw new KeyNotFoundException(
            $"Instrument '{instrumentId}' was not found.");
    }
}
