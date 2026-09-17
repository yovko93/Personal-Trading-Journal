namespace PersonalTradingJournal.Application.Setups;

public sealed class GetTradingSetupDetailsUseCase(ITradingSetupReader reader)
{
    private readonly ITradingSetupReader _reader =
        reader ?? throw new ArgumentNullException(nameof(reader));

    public Task<TradingSetupDetails?> ExecuteAsync(
        Guid setupId,
        CancellationToken cancellationToken = default)
    {
        if (setupId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trading setup identifier is required.",
                nameof(setupId));
        }

        return _reader.GetByIdAsync(setupId, cancellationToken);
    }
}
