using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Analytics;

/// <summary>
/// One authoritative, unrounded Trade projection, not a displayed price or execution row.
/// Currency belongs to the historical pricing snapshot; Setup is the current classification.
/// A future reader must supply the complete selected population, not a browse page.
/// </summary>
public sealed record TradeAnalyticsFact(
    Guid TradeId,
    TradeStatus Status,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    string Currency,
    Guid? TradingSetupId,
    decimal? GrossPnL,
    decimal? TotalCosts,
    decimal? NetPnL)
{
    public string? InstrumentSymbol { get; init; }

    public static TradeAnalyticsFact FromTrade(Trade trade)
    {
        ArgumentNullException.ThrowIfNull(trade);
        return new(trade.Id, trade.Status, trade.OpenedAtUtc, trade.ClosedAtUtc,
            trade.Pricing.Currency, trade.TradingSetupId, trade.GrossPnL,
            trade.TotalCosts, trade.NetPnL);
    }
}
