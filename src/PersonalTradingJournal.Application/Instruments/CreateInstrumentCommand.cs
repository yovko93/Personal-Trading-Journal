using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Instruments;

public sealed record CreateInstrumentCommand(
    string Symbol,
    string DisplayName,
    AssetClass AssetClass,
    string? Exchange,
    string Currency,
    decimal TickSize,
    decimal TickValue);
