namespace PersonalTradingJournal.Application.Mistakes;

public sealed class GetTradingMistakeDetailsUseCase(ITradingMistakeReader reader)
{
    private readonly ITradingMistakeReader _reader =
        reader ?? throw new ArgumentNullException(nameof(reader));

    public Task<TradingMistakeDetails?> ExecuteAsync(
        Guid mistakeId,
        CancellationToken cancellationToken = default)
    {
        if (mistakeId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trading mistake identifier is required.",
                nameof(mistakeId));
        }

        return _reader.GetByIdAsync(mistakeId, cancellationToken);
    }
}
