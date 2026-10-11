using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Domain.Trades;

/// <summary>
/// Frozen, user-selected context. Neither this snapshot nor Trade.TradingSetupId proves adherence.
/// M17.1 defines the value only; existing Trade classification does not capture or persist it.
/// </summary>
public sealed class TradePlaybookContext
{
    private TradePlaybookContext(Trade trade, TradingSetupPlaybookRevision revision, DateTimeOffset capturedAtUtc)
    {
        TradeId = trade.Id;
        TradeOpenedAtUtc = trade.OpenedAtUtc;
        Definition = revision;
        CapturedAtUtc = capturedAtUtc;
    }

    public const int ContractVersion = 1;
    public Guid TradeId { get; }
    public DateTimeOffset TradeOpenedAtUtc { get; }
    public TradingSetupPlaybookRevision Definition { get; }
    public DateTimeOffset CapturedAtUtc { get; }
    public bool IsRetrospective => CapturedAtUtc > TradeOpenedAtUtc;

    public static TradePlaybookContext Capture(Trade trade, TradingSetup setup,
        TradingSetupPlaybookRevision revision, DateTimeOffset capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(trade);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(revision);
        if (trade.TradingSetupId != setup.Id || revision.TradingSetupId != setup.Id)
            throw new ArgumentException("Trade classification, Setup and Playbook identities must match.");
        if (!setup.IsActive) throw new InvalidOperationException("Archived Setups cannot supply new Trade context.");
        if (capturedAtUtc.Offset != TimeSpan.Zero || capturedAtUtc < trade.OpenedAtUtc ||
            capturedAtUtc < trade.UpdatedAtUtc || capturedAtUtc < setup.UpdatedAtUtc || capturedAtUtc < revision.RecordedAtUtc)
            throw new ArgumentException("Capture must use UTC and cannot predate the supplied records.", nameof(capturedAtUtc));
        return new(trade, revision, capturedAtUtc);
    }
}
