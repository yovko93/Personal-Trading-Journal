using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Trades;

/// <summary>
/// Represents one immutable execution fact in a Trade detail lifecycle.
/// </summary>
/// <remarks>
/// Items are exposed by their parent <see cref="TradeDetail"/> in sequence order.
/// <see cref="TotalCosts"/> uses the Domain execution semantic of commission plus fees.
/// Broker provenance values may be null for manually entered Trades.
/// </remarks>
public sealed record TradeExecutionDetailItem(
    Guid Id,
    int Sequence,
    DateTimeOffset ExecutedAtUtc,
    ExecutionSide Side,
    decimal Quantity,
    decimal Price,
    decimal Commission,
    decimal Fees,
    decimal TotalCosts,
    string? BrokerSymbol,
    string? ExternalExecutionId,
    string? ExternalOrderId);
