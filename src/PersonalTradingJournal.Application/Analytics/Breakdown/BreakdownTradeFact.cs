using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Analytics.Breakdown;

/// <summary>
/// One authoritative Trade projection plus grouping/reference context. Supply the complete candidate
/// population once per Trade ID, never a browse page or execution join. Facts retain historical currency,
/// final economics and current Setup classification; display metadata is optional, including inactive data.
/// </summary>
public sealed record BreakdownTradeFact(TradeAnalyticsFact Facts, Guid TradingAccountId,
    Guid InstrumentId, TradeDirection Direction)
{
    public string? AccountName { get; init; }
    public bool? AccountIsActive { get; init; }
    public string? InstrumentName { get; init; }
    public bool? InstrumentIsActive { get; init; }
    public string? SetupName { get; init; }
    public bool? SetupIsActive { get; init; }
}
