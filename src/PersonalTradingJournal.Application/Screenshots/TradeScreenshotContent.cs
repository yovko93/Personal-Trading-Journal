namespace PersonalTradingJournal.Application.Screenshots;

/// <summary>
/// Represents the readable binary content and presentation identity of one Trade screenshot.
/// </summary>
/// <remarks>
/// The caller owns <see cref="Content"/> and must dispose it. Storage keys, filesystem paths,
/// and other storage-topology details are intentionally not exposed.
/// </remarks>
public sealed record TradeScreenshotContent(
    Guid Id,
    Guid TradeId,
    string FileName,
    Stream Content);
