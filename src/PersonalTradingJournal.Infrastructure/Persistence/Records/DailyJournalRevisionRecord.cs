namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

/// <summary>A full immutable-by-repository snapshot, including the initial revision.</summary>
public sealed class DailyJournalRevisionRecord
{
    public Guid JournalId { get; set; }
    public long Revision { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public DateTimeOffset SavedAtUtc { get; set; }
}
