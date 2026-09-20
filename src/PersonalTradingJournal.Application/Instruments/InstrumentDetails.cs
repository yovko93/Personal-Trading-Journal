using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Application.Instruments;

public sealed record InstrumentDetails(
    Guid Id,
    string Symbol,
    string DisplayName,
    AssetClass AssetClass,
    string? Exchange,
    string Currency,
    decimal TickSize,
    decimal TickValue,
    decimal PointValue,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    internal static InstrumentDetails FromDomain(Instrument instrument)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        return new InstrumentDetails(
            instrument.Id,
            instrument.Symbol,
            instrument.DisplayName,
            instrument.AssetClass,
            instrument.Exchange,
            instrument.Currency,
            instrument.TickSize,
            instrument.TickValue,
            instrument.PointValue,
            instrument.IsActive,
            instrument.CreatedAtUtc,
            instrument.UpdatedAtUtc);
    }
}
