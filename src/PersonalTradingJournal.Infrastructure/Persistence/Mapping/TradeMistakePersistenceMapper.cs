using PersonalTradingJournal.Domain.Mistakes;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Mapping;

public static class TradeMistakePersistenceMapper
{
    public static TradeMistakeRecord ToRecord(TradeMistake tradeMistake)
    {
        ArgumentNullException.ThrowIfNull(tradeMistake);

        return new TradeMistakeRecord
        {
            Id = tradeMistake.Id,
            TradeId = tradeMistake.TradeId,
            TradingMistakeId = tradeMistake.TradingMistakeId,
            Note = tradeMistake.Note,
            CreatedAtUtc = tradeMistake.CreatedAtUtc,
            UpdatedAtUtc = tradeMistake.UpdatedAtUtc,
        };
    }

    public static TradeMistake ToDomain(TradeMistakeRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return TradeMistake.Rehydrate(
            record.Id,
            record.TradeId,
            record.TradingMistakeId,
            record.Note,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);
    }
}
