using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Imports.Tradovate;

/// <summary>
/// Read-only economics preserved from the existing PTJ Instrument selected by resolution.
/// </summary>
public sealed record TradovateExistingInstrumentSnapshot(
    string DisplayName,
    AssetClass AssetClass,
    string? Exchange,
    string Currency,
    decimal TickSize,
    decimal TickValue);
