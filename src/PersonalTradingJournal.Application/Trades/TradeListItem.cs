using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Trades;

/// <summary>
/// Represents one authoritative Trade list row.
/// </summary>
/// <remarks>
/// Direction, status, lifecycle timestamps, open quantity, average prices, costs, and
/// profit/loss values come from the reconstructed Domain Trade. Currency comes from the
/// Trade's historical pricing snapshot rather than current Instrument reference data.
/// Open quantity is the current directional exposure and is zero for a closed Trade.
/// An open Trade may have an average exit price after a partial exit, while its gross and
/// net profit/loss values remain null under the current Domain semantics.
/// </remarks>
public sealed record TradeListItem(
    Guid Id,
    Guid TradingAccountId,
    string TradingAccountName,
    Guid InstrumentId,
    string InstrumentSymbol,
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
    string Currency);
