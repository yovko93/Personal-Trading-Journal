using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Mapping;

public static class InstrumentPersistenceMapper
{
    public static InstrumentRecord ToRecord(Instrument instrument)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        return new InstrumentRecord
        {
            Id = instrument.Id,
            Symbol = instrument.Symbol,
            DisplayName = instrument.DisplayName,
            AssetClass = instrument.AssetClass,
            Exchange = instrument.Exchange,
            Currency = instrument.Currency,
            TickSize = instrument.TickSize,
            TickValue = instrument.TickValue,
            IsActive = instrument.IsActive,
            CreatedAtUtc = instrument.CreatedAtUtc,
            UpdatedAtUtc = instrument.UpdatedAtUtc,
        };
    }

    public static Instrument ToDomain(InstrumentRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return Instrument.Rehydrate(
            record.Id,
            record.Symbol,
            record.DisplayName,
            record.AssetClass,
            record.Exchange,
            record.Currency,
            record.TickSize,
            record.TickValue,
            record.IsActive,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);
    }
}
