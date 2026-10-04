namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

public sealed class DailyJournalRecord
{
    public Guid Id { get; set; }
    public DateOnly TradingDate { get; set; }
    public Guid? TradingAccountId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
