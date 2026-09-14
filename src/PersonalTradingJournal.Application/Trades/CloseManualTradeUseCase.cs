using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Trades;

public sealed class CloseManualTradeUseCase
{
    private readonly ITradeMutationStore _tradeMutationStore;
    private readonly TimeProvider _timeProvider;

    public CloseManualTradeUseCase(
        ITradeMutationStore tradeMutationStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(tradeMutationStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _tradeMutationStore = tradeMutationStore;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(
        CloseManualTradeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommand(command);

        Trade? trade = await _tradeMutationStore.GetByIdAsync(
            command.TradeId,
            cancellationToken);
        if (trade is null)
        {
            throw new KeyNotFoundException(
                $"Trade '{command.TradeId}' could not be found.");
        }

        if (trade.Status == TradeStatus.Closed)
        {
            throw new InvalidOperationException(
                $"Trade '{command.TradeId}' is already closed.");
        }

        ExecutionSide closingSide = trade.Direction switch
        {
            TradeDirection.Long => ExecutionSide.Sell,
            TradeDirection.Short => ExecutionSide.Buy,
            _ => throw new InvalidOperationException(
                "The Trade direction is invalid."),
        };
        int nextSequence = checked(trade.Executions[^1].Sequence + 1);
        var closingExecution = new TradeExecution(
            trade.Id,
            nextSequence,
            command.ExecutedAtUtc,
            closingSide,
            trade.OpenQuantity,
            command.Price,
            command.Commission,
            command.Fees,
            externalExecutionId: null,
            externalOrderId: null,
            brokerSymbol: null);

        trade.AddExecution(closingExecution, _timeProvider.GetUtcNow());

        await _tradeMutationStore.SaveAsync(trade, cancellationToken);
    }

    private static void ValidateCommand(CloseManualTradeCommand command)
    {
        if (command.TradeId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trade identifier cannot be empty.",
                nameof(command.TradeId));
        }

        if (command.ExecutedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The execution timestamp must use a UTC offset of zero.",
                nameof(command.ExecutedAtUtc));
        }

        if (command.Commission < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command.Commission),
                command.Commission,
                "Commission cannot be negative.");
        }

        if (command.Fees < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command.Fees),
                command.Fees,
                "Fees cannot be negative.");
        }
    }
}
