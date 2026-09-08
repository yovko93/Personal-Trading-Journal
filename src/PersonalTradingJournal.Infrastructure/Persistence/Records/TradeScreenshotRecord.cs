using PersonalTradingJournal.Domain.Screenshots;

namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

public sealed class TradeScreenshotRecord
{
    public Guid Id { get; set; }

    public Guid TradeId { get; set; }

    public TradeScreenshotType Type { get; set; }

    public string StorageKey { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public DateTimeOffset? CapturedAtUtc { get; set; }

    public string? Timeframe { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
