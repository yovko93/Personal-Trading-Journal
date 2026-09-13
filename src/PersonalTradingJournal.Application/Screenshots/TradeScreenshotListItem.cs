using PersonalTradingJournal.Domain.Screenshots;

namespace PersonalTradingJournal.Application.Screenshots;

/// <summary>
/// Represents user-facing metadata for one Trade screenshot list item.
/// </summary>
/// <remarks>
/// <see cref="Type"/> describes screenshot purpose or context and does not imply Trade
/// quality or profitability. <see cref="FileName"/> is the original user-facing name,
/// not a unique storage identity; duplicate original names are valid.
/// <see cref="CapturedAtUtc"/> records market-context capture time, while
/// <see cref="CreatedAtUtc"/> records when PTJ created the metadata. A historical capture
/// may therefore substantially predate its metadata. <see cref="Timeframe"/> remains optional
/// free-form metadata, and <see cref="Description"/> is screenshot-specific context. Storage
/// addressing and last-modified audit data are intentionally not exposed by this projection.
/// </remarks>
public sealed record TradeScreenshotListItem(
    Guid Id,
    Guid TradeId,
    TradeScreenshotType Type,
    string FileName,
    DateTimeOffset? CapturedAtUtc,
    string? Timeframe,
    string? Description,
    DateTimeOffset CreatedAtUtc);
