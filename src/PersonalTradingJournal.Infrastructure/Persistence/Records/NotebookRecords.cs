namespace PersonalTradingJournal.Infrastructure.Persistence.Records;

/// <summary>Durable ownership ledger, retained after reference removal for recovery/undo. No source-note cascade.</summary>
public sealed class NotebookOwnedImageRecord
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public string StorageKey { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
}

public sealed class NotebookNoteRecord
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string TitleKey { get; set; } = "";
    public string DocumentJson { get; set; } = "";
    public int ContentVersion { get; set; }
    public string PlainText { get; set; } = "";
    public string SearchText { get; set; } = "";
    public Guid? FolderId { get; set; }
    public int State { get; set; }
    public bool IsPinned { get; set; }
    public int? PreviousState { get; set; }
    public Guid? PreviousFolderId { get; set; }
    public long Version { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
public sealed class NotebookFolderRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string NameKey { get; set; } = "";
    public long Version { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
public sealed class NotebookTagRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string NameKey { get; set; } = "";
    public long Version { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
public sealed class NotebookTagAssignmentRecord
{
    public Guid NoteId { get; set; }
    public Guid TagId { get; set; }
}
public sealed class NotebookLinkRecord
{
    public Guid NoteId { get; set; }
    public int Kind { get; set; }
    public Guid TargetId { get; set; }
    public string LastKnownLabel { get; set; } = "";
}
public sealed class NotebookImageRecord
{
    public Guid NoteId { get; set; }
    public Guid Id { get; set; }
    public int Position { get; set; }
    public int Ownership { get; set; }
    public string? StorageKey { get; set; }
    public long? SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public Guid? TradeId { get; set; }
    public Guid? TradeScreenshotId { get; set; }
    public string Caption { get; set; } = "";
    public string AlternativeText { get; set; } = "";
}
