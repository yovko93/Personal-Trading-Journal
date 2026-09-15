using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Application.Mistakes;

public sealed class RemoveTradeMistakeUseCase
{
    public const string MissingAssociationMessage =
        "The selected trade mistake was not found.";

    private readonly ITradeMistakeStore _tradeMistakeStore;

    public RemoveTradeMistakeUseCase(ITradeMistakeStore tradeMistakeStore)
    {
        ArgumentNullException.ThrowIfNull(tradeMistakeStore);
        _tradeMistakeStore = tradeMistakeStore;
    }

    public async Task ExecuteAsync(
        RemoveTradeMistakeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateIdentifier(
            command.TradeId,
            nameof(command.TradeId),
            "A trade identifier is required.");
        ValidateIdentifier(
            command.TradeMistakeId,
            nameof(command.TradeMistakeId),
            "A trade mistake identifier is required.");

        TradeMistake? tradeMistake = await _tradeMistakeStore.GetByIdAsync(
            command.TradeMistakeId,
            cancellationToken);
        if (tradeMistake is null || tradeMistake.TradeId != command.TradeId)
        {
            throw new KeyNotFoundException(MissingAssociationMessage);
        }

        await _tradeMistakeStore.RemoveAsync(
            command.TradeMistakeId,
            cancellationToken);
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
