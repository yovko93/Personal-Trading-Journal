using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Trades;

/// <summary>
/// Represents one complete, authoritative Trade detail projection.
/// </summary>
/// <remarks>
/// Direction, status, market-event timestamps, exposure, average prices, costs,
/// profit/loss values, pricing point value, currency, and executions come from the
/// reconstructed Domain Trade. Account and instrument values provide current reference
/// display identity only; point value and currency preserve the Trade's historical
/// pricing snapshot. Execution facts are projected from the Domain Trade's executions,
/// and <see cref="Executions"/> is ordered by sequence ascending.
/// An open Trade may have an average exit price after a partial exit, while gross and
/// net profit/loss remain null under the current Domain semantics.
/// </remarks>
public sealed record TradeDetail(
    Guid Id,
    Guid TradingAccountId,
    string TradingAccountName,
    Guid InstrumentId,
    string InstrumentSymbol,
    string InstrumentDisplayName,
    TradeDirection Direction,
    TradeStatus Status,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    decimal OpenQuantity,
    decimal AverageEntryPrice,
    decimal? AverageExitPrice,
    decimal TotalCosts,
    decimal? GrossPnL,
    decimal? NetPnL,
    decimal PricingPointValue,
    string Currency,
    IReadOnlyList<TradeExecutionDetailItem> Executions);
