using PersonalTradingJournal.Domain.Common;

namespace PersonalTradingJournal.Domain.Notebook;

/// <summary>Body-independent lifecycle rules, including for retained future/unreadable documents.</summary>
public sealed class NotebookLifecycle : AuditableEntity
{
    public NotebookLifecycle(Guid id, NotebookNoteState state, Guid? folderId, NotebookRestorePoint? restorePoint,
        long version, DateTimeOffset created, DateTimeOffset updated) : base(id, created, updated)
    {
        if (!Enum.IsDefined(state) || version < 1) throw new ArgumentException("Invalid lifecycle metadata.");
        NotebookLimits.OptionalId(folderId, nameof(folderId));
        if ((state == NotebookNoteState.Trashed) != (restorePoint is not null) ||
            restorePoint is not null && restorePoint.FolderId != folderId) throw new ArgumentException("Invalid restoration metadata.");
        State = state; FolderId = folderId; RestorePoint = restorePoint; Version = version;
    }
    public NotebookNoteState State { get; private set; }
    public Guid? FolderId { get; private set; }
    public NotebookRestorePoint? RestorePoint { get; private set; }
    public long Version { get; private set; }
    private void Advance(DateTimeOffset now) { var next = checked(Version + 1); SetUpdatedAtUtc(now); Version = next; }
    public void Archive(DateTimeOffset now)
    {
        if (State != NotebookNoteState.Active) throw new InvalidOperationException("Only Active notes can be archived.");
        Advance(now); State = NotebookNoteState.Archived;
    }
    public void Unarchive(DateTimeOffset now)
    {
        if (State != NotebookNoteState.Archived) throw new InvalidOperationException("Only Archived notes can be unarchived.");
        Advance(now); State = NotebookNoteState.Active;
    }
    public void Trash(DateTimeOffset now)
    {
        if (State == NotebookNoteState.Trashed) throw new InvalidOperationException("The note is already in Trash.");
        var point = new NotebookRestorePoint(State, FolderId);
        Advance(now); RestorePoint = point; State = NotebookNoteState.Trashed;
    }
    public void Restore(bool previousFolderExists, DateTimeOffset now)
    {
        if (State != NotebookNoteState.Trashed) throw new InvalidOperationException("Only Trashed notes can be restored.");
        var point = RestorePoint!;
        Advance(now); State = point.PreviousState; FolderId = previousFolderExists ? point.FolderId : null; RestorePoint = null;
    }
}
