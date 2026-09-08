using PersonalTradingJournal.Domain.Strategies;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Mapping;

public static class StrategyPersistenceMapper
{
    public static StrategyRecord ToRecord(Strategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        return new StrategyRecord
        {
            Id = strategy.Id,
            Name = strategy.Name,
            Description = strategy.Description,
            IsActive = strategy.IsActive,
            CreatedAtUtc = strategy.CreatedAtUtc,
            UpdatedAtUtc = strategy.UpdatedAtUtc,
        };
    }

    public static Strategy ToDomain(StrategyRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return Strategy.Rehydrate(
            record.Id,
            record.Name,
            record.Description,
            record.IsActive,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);
    }
}
