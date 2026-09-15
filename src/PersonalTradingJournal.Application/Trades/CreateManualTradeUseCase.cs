using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Trades;

public sealed class CreateManualTradeUseCase
{
    private readonly ITradingAccountStore _tradingAccountStore;
    private readonly IInstrumentStore _instrumentStore;
    private readonly ITradeStore _tradeStore;
    private readonly ITradingSetupStore _tradingSetupStore;
    private readonly TimeProvider _timeProvider;

    public CreateManualTradeUseCase(
        ITradingAccountStore tradingAccountStore,
        IInstrumentStore instrumentStore,
        ITradingSetupStore tradingSetupStore,
        ITradeStore tradeStore,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(tradingAccountStore);
        ArgumentNullException.ThrowIfNull(instrumentStore);
        ArgumentNullException.ThrowIfNull(tradingSetupStore);
        ArgumentNullException.ThrowIfNull(tradeStore);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _tradingAccountStore = tradingAccountStore;
        _instrumentStore = instrumentStore;
        _tradingSetupStore = tradingSetupStore;
        _tradeStore = tradeStore;
        _timeProvider = timeProvider;
    }

    public async Task<Guid> ExecuteAsync(
        CreateManualTradeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Entry);

        ValidateIdentifier(
            command.TradingAccountId,
            nameof(command.TradingAccountId),
            "A trading account identifier is required.");
        ValidateIdentifier(
            command.InstrumentId,
            nameof(command.InstrumentId),
            "An instrument identifier is required.");
        if (command.TradingSetupId == Guid.Empty)
        {
            throw new ArgumentException(
                "A trading setup identifier cannot be empty.",
                nameof(command.TradingSetupId));
        }

        TradingAccount? tradingAccount = await _tradingAccountStore.GetByIdAsync(
            command.TradingAccountId,
            cancellationToken);
        if (tradingAccount is null)
        {
            throw new KeyNotFoundException(
                $"Trading Account '{command.TradingAccountId}' could not be found.");
        }

        Instrument? instrument = await _instrumentStore.GetByIdAsync(
            command.InstrumentId,
            cancellationToken);
        if (instrument is null)
        {
            throw new KeyNotFoundException(
                $"Instrument '{command.InstrumentId}' could not be found.");
        }

        if (command.TradingSetupId is Guid tradingSetupId)
        {
            Domain.Setups.TradingSetup? tradingSetup =
                await _tradingSetupStore.GetByIdAsync(
                    tradingSetupId,
                    cancellationToken);
            if (tradingSetup is null)
            {
                throw new KeyNotFoundException(
                    "The selected trading setup was not found.");
            }

            if (!tradingSetup.IsActive)
            {
                throw new InvalidOperationException(
                    "The selected trading setup is inactive.");
            }
        }

        TradeQuantityPolicy.Validate(
            instrument.AssetClass,
            command.Quantity,
            nameof(command.Quantity));

        var pricing = new TradePricingSnapshot(
            instrument.PointValue,
            instrument.Currency);
        (ExecutionSide openingSide, ExecutionSide closingSide) =
            GetExecutionSides(command.Direction);
        Guid tradeId = Guid.NewGuid();
        DateTimeOffset createdAtUtc = _timeProvider.GetUtcNow();
        var openingExecution = CreateExecution(
            tradeId,
            sequence: 1,
            openingSide,
            command.Quantity,
            command.Entry);
        Trade trade = Trade.Start(
            command.TradingAccountId,
            command.InstrumentId,
            pricing,
            openingExecution,
            createdAtUtc);

        if (command.TradingSetupId.HasValue)
        {
            trade.SetTradingSetup(command.TradingSetupId, createdAtUtc);
        }

        if (command.Exit is not null)
        {
            var closingExecution = CreateExecution(
                tradeId,
                sequence: 2,
                closingSide,
                command.Quantity,
                command.Exit);
            trade.AddExecution(closingExecution, createdAtUtc);
        }

        await _tradeStore.AddAsync(trade, cancellationToken);

        return trade.Id;
    }

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

    private static (ExecutionSide Opening, ExecutionSide Closing) GetExecutionSides(
        TradeDirection direction)
    {
        return direction switch
        {
            TradeDirection.Long => (ExecutionSide.Buy, ExecutionSide.Sell),
            TradeDirection.Short => (ExecutionSide.Sell, ExecutionSide.Buy),
            _ => throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                "The trade direction is not defined.")
        };
    }

    private static TradeExecution CreateExecution(
        Guid tradeId,
        int sequence,
        ExecutionSide side,
        decimal quantity,
        ManualTradeExecutionInput input)
    {
        return new TradeExecution(
            tradeId,
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
}
