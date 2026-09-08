using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Instruments;

public sealed record InstrumentListItem(
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
