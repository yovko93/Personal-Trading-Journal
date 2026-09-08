using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Mapping;

public static class TradeScreenshotPersistenceMapper
{
    public static TradeScreenshotRecord ToRecord(TradeScreenshot screenshot)
    {
        ArgumentNullException.ThrowIfNull(screenshot);

        return new TradeScreenshotRecord
        {
            Id = screenshot.Id,
            TradeId = screenshot.TradeId,
            Type = screenshot.Type,
            StorageKey = screenshot.StorageKey,
            FileName = screenshot.FileName,
            CapturedAtUtc = screenshot.CapturedAtUtc,
            Timeframe = screenshot.Timeframe,
            Description = screenshot.Description,
            CreatedAtUtc = screenshot.CreatedAtUtc,
            UpdatedAtUtc = screenshot.UpdatedAtUtc,
        };
    }

    public static TradeScreenshot ToDomain(TradeScreenshotRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return TradeScreenshot.Rehydrate(
            record.Id,
            record.TradeId,
            record.Type,
            record.StorageKey,
            record.FileName,
            record.CapturedAtUtc,
            record.Timeframe,
            record.Description,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);
    }
}
