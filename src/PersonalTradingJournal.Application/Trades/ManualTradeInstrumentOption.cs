using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Trades;

public sealed record ManualTradeInstrumentOption(
    Guid Id,
    string Symbol,
    string DisplayName,
    AssetClass AssetClass,
    string? Exchange,
    string Currency,
    decimal TickSize,
    decimal TickValue,
    decimal PointValue,
    bool IsActive);
