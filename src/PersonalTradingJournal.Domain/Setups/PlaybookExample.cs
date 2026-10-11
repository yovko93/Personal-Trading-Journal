using PersonalTradingJournal.Domain.Screenshots;

namespace PersonalTradingJournal.Domain.Setups;

/// <summary>
/// An immutable annotated example. Optional screenshot IDs are provenance only, not file ownership.
/// No storage key, local path, image bytes or implied continued availability is captured here.
/// </summary>
public sealed class PlaybookExample
{
    public PlaybookExample(Guid id, string annotation, Guid? sourceTradeId = null,
        Guid? sourceScreenshotId = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("An example identity is required.", nameof(id));
        if (sourceTradeId == Guid.Empty || sourceScreenshotId == Guid.Empty ||
            (sourceScreenshotId.HasValue && !sourceTradeId.HasValue))
            throw new ArgumentException("Screenshot provenance requires non-empty Trade and screenshot identities.");
        Id = id;
        Annotation = PlaybookGuidance.RequiredText(annotation, nameof(annotation));
        SourceTradeId = sourceTradeId;
        SourceScreenshotId = sourceScreenshotId;
    }

    public Guid Id { get; }
    public string Annotation { get; }
    public Guid? SourceTradeId { get; }
    public Guid? SourceScreenshotId { get; }

    public static PlaybookExample FromScreenshot(Guid id, string annotation, TradeScreenshot screenshot)
    {
        ArgumentNullException.ThrowIfNull(screenshot);
        return new(id, annotation, screenshot.TradeId, screenshot.Id);
    }
}
