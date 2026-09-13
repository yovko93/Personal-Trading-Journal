using PersonalTradingJournal.Domain.Screenshots;

namespace PersonalTradingJournal.Application.Screenshots;

/// <summary>
/// Carries the user-supplied facts required to add a screenshot to a Trade.
/// </summary>
/// <remarks>
/// <see cref="FileName"/> is the original user-facing filename, not a source path or
/// storage identity. The caller owns <see cref="Content"/>; executing the use case does
/// not dispose the supplied stream.
/// </remarks>
public sealed record AddTradeScreenshotCommand(
    Guid TradeId,
    TradeScreenshotType Type,
    Stream Content,
    string FileName,
    DateTimeOffset? CapturedAtUtc,
    string? Timeframe,
    string? Description);
