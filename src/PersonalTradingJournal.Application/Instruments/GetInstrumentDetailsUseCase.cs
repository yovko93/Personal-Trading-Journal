namespace PersonalTradingJournal.Application.Instruments;

public sealed class GetInstrumentDetailsUseCase
{
    private readonly IInstrumentReader _instrumentReader;

    public GetInstrumentDetailsUseCase(IInstrumentReader instrumentReader)
    {
        ArgumentNullException.ThrowIfNull(instrumentReader);
        _instrumentReader = instrumentReader;
    }

    public Task<InstrumentDetails?> ExecuteAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        if (instrumentId == Guid.Empty)
        {
            throw new ArgumentException(
                "An instrument identifier is required.",
                nameof(instrumentId));
        }

        return _instrumentReader.GetByIdAsync(instrumentId, cancellationToken);
    }
}
