using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Domain.TradingPlans;

public enum TradingPlanTimeframe { M1 = 1, M5 = 2, M15 = 3, H1 = 4, H4 = 5, D = 6, W = 7 }

/// <summary>Plan-owned immutable image reference. No Trade is fabricated and image bytes are never evidence text.</summary>
public sealed record TradingPlanScreenshot
{
    public TradingPlanScreenshot(Guid id, Guid instrumentId, TradingPlanTimeframe timeframe, PlaybookExampleImage image)
    {
        Id = TradingPlanContent.Id(id, nameof(id)); InstrumentId = TradingPlanContent.Id(instrumentId, nameof(instrumentId));
        if (!Enum.IsDefined(timeframe)) throw new ArgumentOutOfRangeException(nameof(timeframe));
        Timeframe = timeframe; Image = image ?? throw new ArgumentNullException(nameof(image));
    }
    public Guid Id { get; }
    public Guid InstrumentId { get; }
    public TradingPlanTimeframe Timeframe { get; }
    public PlaybookExampleImage Image { get; }
}
