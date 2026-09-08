using PersonalTradingJournal.Domain.Common;

namespace PersonalTradingJournal.Domain.Trades;

/// <summary>
/// Represents one directional, flat-to-flat position lifecycle.
/// </summary>
public sealed class Trade : AuditableEntity
{
    private readonly List<TradeExecution> _executions;
    private readonly IReadOnlyList<TradeExecution> _readOnlyExecutions;

    private Trade(
        Guid id,
        Guid tradingAccountId,
        Guid instrumentId,
        TradePricingSnapshot pricing,
        Guid? strategyId,
        Guid? tradingSetupId,
        IEnumerable<TradeExecution> executions,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
        : base(id, createdAtUtc, updatedAtUtc)
    {
        TradingAccountId = ValidateIdentifier(
            tradingAccountId,
            nameof(tradingAccountId),
            "A trading account identifier cannot be empty.");
        InstrumentId = ValidateIdentifier(
            instrumentId,
            nameof(instrumentId),
            "An instrument identifier cannot be empty.");
        ArgumentNullException.ThrowIfNull(pricing);
        Pricing = pricing;
        StrategyId = ValidateOptionalIdentifier(strategyId, nameof(strategyId));
        TradingSetupId = ValidateOptionalIdentifier(
            tradingSetupId,
            nameof(tradingSetupId));

        _executions = OrderAndValidateExecutions(id, executions);
        _readOnlyExecutions = _executions.AsReadOnly();
    }

    public Guid TradingAccountId { get; }

    public Guid InstrumentId { get; }

    public TradePricingSnapshot Pricing { get; }

    public Guid? StrategyId { get; private set; }

    public Guid? TradingSetupId { get; private set; }

    public TradeDirection Direction =>
        _executions[0].Side == ExecutionSide.Buy
            ? TradeDirection.Long
            : TradeDirection.Short;

    public TradeStatus Status =>
        OpenQuantity > 0m
            ? TradeStatus.Open
            : TradeStatus.Closed;

    public decimal OpenQuantity => CalculateOpenQuantity(_executions, Direction);

    public DateTimeOffset OpenedAtUtc => _executions[0].ExecutedAtUtc;

    public DateTimeOffset? ClosedAtUtc =>
        Status == TradeStatus.Closed
            ? _executions[^1].ExecutedAtUtc
            : null;

    public IReadOnlyList<TradeExecution> Executions => _readOnlyExecutions;

    public decimal TotalCosts => _executions.Sum(execution => execution.TotalCosts);

    public decimal AverageEntryPrice =>
        CalculateAveragePrice(GetOpeningSide(Direction))
        ?? throw new InvalidOperationException(
            "A trade must contain at least one opening-side execution.");

    public decimal? AverageExitPrice =>
        CalculateAveragePrice(GetOppositeSide(GetOpeningSide(Direction)));

    public decimal? GrossPnL =>
        Status == TradeStatus.Closed
            ? CalculateGrossPnL()
            : null;

    public decimal? NetPnL
    {
        get
        {
            decimal? grossPnL = GrossPnL;
            return grossPnL.HasValue
                ? checked(grossPnL.Value - TotalCosts)
                : null;
        }
    }

    public static Trade Start(
        Guid tradingAccountId,
        Guid instrumentId,
        TradePricingSnapshot pricing,
        TradeExecution openingExecution,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(pricing);
        ArgumentNullException.ThrowIfNull(openingExecution);

        return new Trade(
            openingExecution.TradeId,
            tradingAccountId,
            instrumentId,
            pricing,
            null,
            null,
            [openingExecution],
            createdAtUtc,
            createdAtUtc);
    }

    public static Trade Rehydrate(
        Guid id,
        Guid tradingAccountId,
        Guid instrumentId,
        TradePricingSnapshot pricing,
        Guid? strategyId,
        Guid? tradingSetupId,
        IEnumerable<TradeExecution> executions,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(pricing);
        ArgumentNullException.ThrowIfNull(executions);

        return new Trade(
            id,
            tradingAccountId,
            instrumentId,
            pricing,
            strategyId,
            tradingSetupId,
            executions,
            createdAtUtc,
            updatedAtUtc);
    }

    public void SetClassification(
        Guid? strategyId,
        Guid? tradingSetupId,
        DateTimeOffset updatedAtUtc)
    {
        Guid? validatedStrategyId = ValidateOptionalIdentifier(
            strategyId,
            nameof(strategyId));
        Guid? validatedTradingSetupId = ValidateOptionalIdentifier(
            tradingSetupId,
            nameof(tradingSetupId));

        if (StrategyId == validatedStrategyId &&
            TradingSetupId == validatedTradingSetupId)
        {
            return;
        }

        SetUpdatedAtUtc(updatedAtUtc);
        StrategyId = validatedStrategyId;
        TradingSetupId = validatedTradingSetupId;
    }

    public void AddExecution(
        TradeExecution execution,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(execution);

        if (Status == TradeStatus.Closed)
        {
            throw new InvalidOperationException(
                "An execution cannot be added after the trade is closed.");
        }

        if (execution.TradeId != Id)
        {
            throw new ArgumentException(
                "The execution must belong to this trade.",
                nameof(execution));
        }

        if (_executions.Any(existing => existing.Id == execution.Id))
        {
            throw new ArgumentException(
                "The execution identifier is already present in this trade.",
                nameof(execution));
        }

        int expectedSequence = checked(_executions[^1].Sequence + 1);
        if (execution.Sequence != expectedSequence)
        {
            throw new ArgumentException(
                $"The execution sequence must be {expectedSequence}.",
                nameof(execution));
        }

        if (execution.ExecutedAtUtc < _executions[^1].ExecutedAtUtc)
        {
            throw new ArgumentException(
                "The execution timestamp cannot precede the previous execution.",
                nameof(execution));
        }

        decimal resultingOpenQuantity = IsOpeningSide(execution.Side)
            ? checked(OpenQuantity + execution.Quantity)
            : OpenQuantity - execution.Quantity;

        if (resultingOpenQuantity < 0m)
        {
            throw new InvalidOperationException(
                "An execution cannot reduce the position below zero.");
        }

        SetUpdatedAtUtc(updatedAtUtc);
        _executions.Add(execution);
    }

    private bool IsOpeningSide(ExecutionSide side)
    {
        return side == GetOpeningSide(Direction);
    }

    private decimal? CalculateAveragePrice(ExecutionSide side)
    {
        decimal weightedPrice = 0m;
        decimal totalQuantity = 0m;

        foreach (TradeExecution execution in _executions.Where(x => x.Side == side))
        {
            weightedPrice = checked(
                weightedPrice + checked(execution.Price * execution.Quantity));
            totalQuantity = checked(totalQuantity + execution.Quantity);
        }

        return totalQuantity == 0m
            ? null
            : weightedPrice / totalQuantity;
    }

    private decimal CalculateGrossPnL()
    {
        decimal buyQuantity = 0m;
        decimal sellQuantity = 0m;
        decimal buyNotional = 0m;
        decimal sellNotional = 0m;

        foreach (TradeExecution execution in _executions)
        {
            decimal executionNotional = checked(execution.Price * execution.Quantity);

            if (execution.Side == ExecutionSide.Buy)
            {
                buyQuantity = checked(buyQuantity + execution.Quantity);
                buyNotional = checked(buyNotional + executionNotional);
            }
            else
            {
                sellQuantity = checked(sellQuantity + execution.Quantity);
                sellNotional = checked(sellNotional + executionNotional);
            }
        }

        if (buyQuantity != sellQuantity)
        {
            throw new InvalidOperationException(
                "Final trade P&L can only be calculated for a flat position.");
        }

        return checked(checked(sellNotional - buyNotional) * Pricing.PointValue);
    }

    private static ExecutionSide GetOpeningSide(TradeDirection direction)
    {
        return direction switch
        {
            TradeDirection.Long => ExecutionSide.Buy,
            TradeDirection.Short => ExecutionSide.Sell,
            _ => throw new InvalidOperationException("The trade direction is invalid.")
        };
    }

    private static ExecutionSide GetOppositeSide(ExecutionSide side)
    {
        return side switch
        {
            ExecutionSide.Buy => ExecutionSide.Sell,
            ExecutionSide.Sell => ExecutionSide.Buy,
            _ => throw new InvalidOperationException("The execution side is invalid.")
        };
    }

    private static Guid ValidateIdentifier(
        Guid id,
        string parameterName,
        string message)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(message, parameterName);
        }

        return id;
    }

    private static Guid? ValidateOptionalIdentifier(Guid? id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "A classification identifier cannot be empty.",
                parameterName);
        }

        return id;
    }

    private static List<TradeExecution> OrderAndValidateExecutions(
        Guid tradeId,
        IEnumerable<TradeExecution> executions)
    {
        ArgumentNullException.ThrowIfNull(executions);

        List<TradeExecution> materialized = executions.ToList();
        if (materialized.Count == 0)
        {
            throw new ArgumentException(
                "A trade must contain at least one execution.",
                nameof(executions));
        }

        if (materialized.Any(execution => execution is null))
        {
            throw new ArgumentException(
                "The execution collection cannot contain null values.",
                nameof(executions));
        }

        List<TradeExecution> ordered = materialized
            .OrderBy(execution => execution.Sequence)
            .ToList();

        var executionIds = new HashSet<Guid>();
        var sequences = new HashSet<int>();
        ExecutionSide openingSide = ordered[0].Side;
        decimal openQuantity = 0m;
        bool positionWasFlattened = false;

        for (int index = 0; index < ordered.Count; index++)
        {
            TradeExecution execution = ordered[index];

            if (execution.TradeId != tradeId)
            {
                throw new ArgumentException(
                    "Every execution must belong to the rehydrated trade.",
                    nameof(executions));
            }

            if (!executionIds.Add(execution.Id))
            {
                throw new ArgumentException(
                    "Execution identifiers must be unique within a trade.",
                    nameof(executions));
            }

            if (!sequences.Add(execution.Sequence))
            {
                throw new ArgumentException(
                    "Execution sequences must be unique within a trade.",
                    nameof(executions));
            }

            int expectedSequence = index + 1;
            if (execution.Sequence != expectedSequence)
            {
                throw new ArgumentException(
                    "Execution sequences must be contiguous and start at one.",
                    nameof(executions));
            }

            if (index > 0 && execution.ExecutedAtUtc < ordered[index - 1].ExecutedAtUtc)
            {
                throw new ArgumentException(
                    "Execution timestamps cannot move backwards.",
                    nameof(executions));
            }

            if (positionWasFlattened)
            {
                throw new ArgumentException(
                    "A trade cannot contain executions after becoming flat.",
                    nameof(executions));
            }

            bool isOpeningSide = execution.Side == openingSide;
            openQuantity = isOpeningSide
                ? checked(openQuantity + execution.Quantity)
                : openQuantity - execution.Quantity;

            if (openQuantity < 0m)
            {
                throw new ArgumentException(
                    "The execution lifecycle cannot cross through zero.",
                    nameof(executions));
            }

            positionWasFlattened = openQuantity == 0m;
        }

        return ordered;
    }

    private static decimal CalculateOpenQuantity(
        IEnumerable<TradeExecution> executions,
        TradeDirection direction)
    {
        ExecutionSide openingSide = direction == TradeDirection.Long
            ? ExecutionSide.Buy
            : ExecutionSide.Sell;

        decimal openQuantity = 0m;

        foreach (TradeExecution execution in executions)
        {
            openQuantity = execution.Side == openingSide
                ? checked(openQuantity + execution.Quantity)
                : openQuantity - execution.Quantity;
        }

        return openQuantity;
    }
}
