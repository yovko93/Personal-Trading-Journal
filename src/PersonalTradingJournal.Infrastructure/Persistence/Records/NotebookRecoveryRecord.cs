namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

public sealed class NotebookRecoveryRecord
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public int EnvelopeVersion { get; set; } = 1;
    public long ExpectedVersion { get; set; }
    public long Revision { get; set; }
    public string Title { get; set; } = "";
    public string DocumentJson { get; set; } = "";
    public DateTimeOffset CheckpointedAtUtc { get; set; }
}
