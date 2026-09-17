namespace PersonalTradingJournal.Application.Trades;

/// <summary>
/// Supplies corrected authoritative values for one immutable Trade execution.
/// </summary>
public sealed record UpdateTradeExecutionInput(
    Guid ExecutionId,
    DateTimeOffset ExecutedAtUtc,
    decimal Price,
    decimal Commission,
    decimal Fees);
