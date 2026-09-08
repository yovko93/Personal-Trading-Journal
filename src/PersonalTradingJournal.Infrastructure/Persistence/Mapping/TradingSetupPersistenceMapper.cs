using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Mapping;

public static class TradingSetupPersistenceMapper
{
    public static TradingSetupRecord ToRecord(TradingSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);

        return new TradingSetupRecord
        {
            Id = setup.Id,
            Name = setup.Name,
            Description = setup.Description,
            IsActive = setup.IsActive,
            CreatedAtUtc = setup.CreatedAtUtc,
            UpdatedAtUtc = setup.UpdatedAtUtc,
        };
    }

    public static TradingSetup ToDomain(TradingSetupRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return TradingSetup.Rehydrate(
            record.Id,
            record.Name,
            record.Description,
            record.IsActive,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);
    }
}
