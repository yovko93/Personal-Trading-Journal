namespace PersonalTradingJournal.Domain.Journals;

/// <summary>An explicit target scope. A null ID means the distinct All accounts journal.</summary>
public sealed record DailyJournalAccountScope
{
    public DailyJournalAccountScope(Guid? tradingAccountId)
    {
        if (tradingAccountId == Guid.Empty) throw new ArgumentException("Use null or a non-empty Account ID.", nameof(tradingAccountId));
        TradingAccountId = tradingAccountId;
    }

    public Guid? TradingAccountId { get; }
}
