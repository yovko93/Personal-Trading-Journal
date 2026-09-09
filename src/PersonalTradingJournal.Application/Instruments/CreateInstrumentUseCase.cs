using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Instruments;

public sealed class CreateInstrumentUseCase
{
    private readonly IInstrumentStore _instrumentStore;
    private readonly TimeProvider _timeProvider;

    public CreateInstrumentUseCase(
        IInstrumentStore instrumentStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(instrumentStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _instrumentStore = instrumentStore;
        _timeProvider = timeProvider;
    }

    public async Task<Guid> ExecuteAsync(
        CreateInstrumentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        DateTimeOffset createdAtUtc = _timeProvider.GetUtcNow();
        var instrument = new Instrument(
            command.Symbol,
            command.DisplayName,
            command.AssetClass,
            command.Exchange,
            command.Currency,
            command.TickSize,
            command.TickValue,
            createdAtUtc);

        await _instrumentStore.AddAsync(instrument, cancellationToken);

        return instrument.Id;
    }
}
