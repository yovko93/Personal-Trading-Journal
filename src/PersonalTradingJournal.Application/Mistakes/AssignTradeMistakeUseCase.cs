using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Application.Mistakes;

public sealed class AssignTradeMistakeUseCase
{
    public const string MissingTradeMessage = "The selected trade was not found.";
    public const string MissingMistakeMessage =
        "The selected trading mistake was not found.";
    public const string InactiveMistakeMessage =
        "The selected trading mistake is inactive.";
    public const string DuplicateMistakeMessage =
        "This trading mistake is already assigned to the trade.";

    private readonly ITradeExistenceReader _tradeExistenceReader;
    private readonly ITradingMistakeStore _tradingMistakeStore;
    private readonly ITradeMistakeStore _tradeMistakeStore;
    private readonly TimeProvider _timeProvider;

    public AssignTradeMistakeUseCase(
        ITradeExistenceReader tradeExistenceReader,
        ITradingMistakeStore tradingMistakeStore,
        ITradeMistakeStore tradeMistakeStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(tradeExistenceReader);
        ArgumentNullException.ThrowIfNull(tradingMistakeStore);
        ArgumentNullException.ThrowIfNull(tradeMistakeStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _tradeExistenceReader = tradeExistenceReader;
        _tradingMistakeStore = tradingMistakeStore;
        _tradeMistakeStore = tradeMistakeStore;
        _timeProvider = timeProvider;
    }

    public async Task<Guid> ExecuteAsync(
        AssignTradeMistakeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateIdentifier(
            command.TradeId,
            nameof(command.TradeId),
            "A trade identifier is required.");
        ValidateIdentifier(
            command.TradingMistakeId,
            nameof(command.TradingMistakeId),
            "A trading mistake identifier is required.");

        if (!await _tradeExistenceReader.ExistsAsync(
                command.TradeId,
                cancellationToken))
        {
            throw new KeyNotFoundException(MissingTradeMessage);
        }

        TradingMistake? mistake = await _tradingMistakeStore.GetByIdAsync(
            command.TradingMistakeId,
            cancellationToken);
        if (mistake is null)
        {
            throw new KeyNotFoundException(MissingMistakeMessage);
        }

        if (!mistake.IsActive)
        {
            throw new InvalidOperationException(InactiveMistakeMessage);
        }

        if (await _tradeMistakeStore.ExistsAsync(
                command.TradeId,
                command.TradingMistakeId,
                cancellationToken))
        {
            throw new InvalidOperationException(DuplicateMistakeMessage);
        }

        var tradeMistake = new TradeMistake(
            command.TradeId,
            command.TradingMistakeId,
            command.Note,
            _timeProvider.GetUtcNow());
        await _tradeMistakeStore.AddAsync(tradeMistake, cancellationToken);

        return tradeMistake.Id;
    }

    private static void ValidateIdentifier(
        Guid id,
        string parameterName,
        string message)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(message, parameterName);
        }
    }
}
