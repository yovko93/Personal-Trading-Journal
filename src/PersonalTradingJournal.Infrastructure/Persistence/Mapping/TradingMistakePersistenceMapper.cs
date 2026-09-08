using PersonalTradingJournal.Domain.Mistakes;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Mapping;

public static class TradingMistakePersistenceMapper
{
    public static TradingMistakeRecord ToRecord(TradingMistake mistake)
    {
        ArgumentNullException.ThrowIfNull(mistake);

        return new TradingMistakeRecord
        {
            Id = mistake.Id,
            Name = mistake.Name,
            Description = mistake.Description,
            IsActive = mistake.IsActive,
            CreatedAtUtc = mistake.CreatedAtUtc,
            UpdatedAtUtc = mistake.UpdatedAtUtc,
        };
    }

    public static TradingMistake ToDomain(TradingMistakeRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return TradingMistake.Rehydrate(
            record.Id,
            record.Name,
            record.Description,
            record.IsActive,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);
    }
}
