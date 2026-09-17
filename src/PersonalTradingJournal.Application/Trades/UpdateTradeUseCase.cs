using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Trades;

/// <summary>
/// Corrects the authoritative facts of a manually recorded Trade.
/// </summary>
public sealed class UpdateTradeUseCase
{
    public const string InactiveAccountMessage =
        "The selected trading account is inactive.";
    public const string InactiveInstrumentMessage =
        "The selected instrument is inactive.";
    public const string InactiveSetupMessage =
        "The selected trading setup is inactive.";

    private readonly ITradeMutationStore _tradeStore;
    private readonly ITradingAccountStore _accountStore;
    private readonly IInstrumentStore _instrumentStore;
    private readonly ITradingSetupStore _setupStore;
    private readonly TimeProvider _timeProvider;

    public UpdateTradeUseCase(
        ITradeMutationStore tradeStore,
        ITradingAccountStore accountStore,
        IInstrumentStore instrumentStore,
        ITradingSetupStore setupStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(tradeStore);
        ArgumentNullException.ThrowIfNull(accountStore);
        ArgumentNullException.ThrowIfNull(instrumentStore);
        ArgumentNullException.ThrowIfNull(setupStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _tradeStore = tradeStore;
        _accountStore = accountStore;
        _instrumentStore = instrumentStore;
        _setupStore = setupStore;
        _timeProvider = timeProvider;
    }

    public async Task<UpdateTradeResult> ExecuteAsync(
        UpdateTradeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Entry);
        ValidateIdentifier(command.TradeId, nameof(command.TradeId), "A trade identifier is required.");
        ValidateIdentifier(command.TradingAccountId, nameof(command.TradingAccountId), "A trading account identifier is required.");
        ValidateIdentifier(command.InstrumentId, nameof(command.InstrumentId), "An instrument identifier is required.");
        if (command.TradingSetupId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trading setup identifier cannot be empty.",
                nameof(command.TradingSetupId));
        }

        Trade? trade = await _tradeStore.GetByIdAsync(
            command.TradeId,
            cancellationToken);
        if (trade is null)
        {
            throw new KeyNotFoundException(
                $"Trade '{command.TradeId}' could not be found.");
        }

        if (trade.Executions.Count > 2)
        {
            throw new InvalidOperationException(
                "Only trades represented by the current manual entry workflow can be edited.");
        }

        TradingAccount? account = await _accountStore.GetByIdAsync(
            command.TradingAccountId,
            cancellationToken);
        if (account is null)
        {
            throw new KeyNotFoundException(
                $"Trading Account '{command.TradingAccountId}' could not be found.");
        }

        if (trade.TradingAccountId != account.Id && !account.IsActive)
        {
            throw new InvalidOperationException(InactiveAccountMessage);
        }

        Instrument? instrument = await _instrumentStore.GetByIdAsync(
            command.InstrumentId,
            cancellationToken);
        if (instrument is null)
        {
            throw new KeyNotFoundException(
                $"Instrument '{command.InstrumentId}' could not be found.");
        }

        if (trade.InstrumentId != instrument.Id && !instrument.IsActive)
        {
            throw new InvalidOperationException(InactiveInstrumentMessage);
        }

        if (command.TradingSetupId is Guid setupId)
        {
            Domain.Setups.TradingSetup? setup = await _setupStore.GetByIdAsync(
                setupId,
                cancellationToken);
            if (setup is null)
            {
                throw new KeyNotFoundException(
                    $"Trading setup '{setupId}' could not be found.");
            }

            if (trade.TradingSetupId != setup.Id && !setup.IsActive)
            {
                throw new InvalidOperationException(InactiveSetupMessage);
            }
        }

        TradeQuantityPolicy.Validate(
            instrument.AssetClass,
            command.Quantity,
            nameof(command.Quantity));

        (ExecutionSide openingSide, ExecutionSide closingSide) =
            GetExecutionSides(command.Direction);
        TradeExecution entry = CreateCorrectedExecution(
            trade,
            command.Entry,
            sequence: 1,
            openingSide,
            command.Quantity);
        var executions = new List<TradeExecution> { entry };
        if (command.Exit is not null)
        {
            executions.Add(CreateCorrectedExecution(
                trade,
                command.Exit,
                sequence: 2,
                closingSide,
                command.Quantity));
        }

        TradePricingSnapshot pricing = trade.InstrumentId == instrument.Id
            ? trade.Pricing
            : new TradePricingSnapshot(instrument.PointValue, instrument.Currency);

        bool wasChanged = trade.CorrectDetails(
            account.Id,
            instrument.Id,
            pricing,
            command.TradingSetupId,
            executions,
            _timeProvider.GetUtcNow());

        if (wasChanged)
        {
            await _tradeStore.SaveAsync(trade, cancellationToken);
        }

        return new UpdateTradeResult(wasChanged);
    }

    private static TradeExecution CreateCorrectedExecution(
        Trade trade,
        UpdateTradeExecutionInput input,
        int sequence,
        ExecutionSide side,
        decimal quantity)
    {
        TradeExecution? current = input.ExecutionId == Guid.Empty
            ? trade.Executions.SingleOrDefault(execution => execution.Sequence == sequence)
            : trade.Executions.SingleOrDefault(execution => execution.Id == input.ExecutionId);

        if (input.ExecutionId != Guid.Empty && current is null)
        {
            throw new ArgumentException(
                $"Execution '{input.ExecutionId}' does not belong to Trade '{trade.Id}'.",
                nameof(input));
        }

        if (current is null)
        {
            return new TradeExecution(
                trade.Id,
                sequence,
                input.ExecutedAtUtc,
                side,
                quantity,
                input.Price,
                input.Commission,
                input.Fees,
                externalExecutionId: null,
                externalOrderId: null,
                brokerSymbol: null);
        }

        return TradeExecution.Rehydrate(
            current.Id,
            trade.Id,
            sequence,
            input.ExecutedAtUtc,
            side,
            quantity,
            input.Price,
            input.Commission,
            input.Fees,
            current.ExternalExecutionId,
            current.ExternalOrderId,
            current.BrokerSymbol);
    }

    private static (ExecutionSide Opening, ExecutionSide Closing) GetExecutionSides(
        TradeDirection direction) => direction switch
        {
            TradeDirection.Long => (ExecutionSide.Buy, ExecutionSide.Sell),
            TradeDirection.Short => (ExecutionSide.Sell, ExecutionSide.Buy),
            _ => throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                "The trade direction is not defined."),
        };

    private static void ValidateIdentifier(
        Guid identifier,
        string parameterName,
        string message)
    {
        if (identifier == Guid.Empty)
        {
            throw new ArgumentException(message, parameterName);
        }
    }
}
