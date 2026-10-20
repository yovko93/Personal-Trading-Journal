using PersonalTradingJournal.Domain.Common;

namespace PersonalTradingJournal.Domain.Notebook;

public enum NotebookNoteState { Active = 1, Archived = 2, Trashed = 3 }

public sealed record NotebookRestorePoint
{
    public NotebookRestorePoint(NotebookNoteState previousState, Guid? folderId)
    {
        if (previousState is not (NotebookNoteState.Active or NotebookNoteState.Archived))
            throw new ArgumentOutOfRangeException(nameof(previousState));
        PreviousState = previousState; FolderId = NotebookLimits.OptionalId(folderId, nameof(folderId));
    }
    public NotebookNoteState PreviousState { get; }
    public Guid? FolderId { get; }
}

/// <summary>One note owns its document and assignments. Catalog existence/uniqueness, concurrent writes and files
/// require transactional application checks. No operation here modifies a linked record or a physical image.</summary>
public sealed class NotebookNote : AuditableEntity
{
    public NotebookNote(DateTimeOffset now, Guid? folderId = null, string title = "Untitled")
        : this(Guid.NewGuid(), title, NotebookDocument.Empty, folderId, NotebookNoteState.Active, false,
            null, [], [], [], 1, now, now) { }

    private NotebookNote(Guid id, string title, NotebookDocument document, Guid? folderId,
        NotebookNoteState state, bool pinned, NotebookRestorePoint? restorePoint,
        IEnumerable<Guid> tagIds, IEnumerable<NotebookLink> links, IEnumerable<NotebookImage> images,
        long version, DateTimeOffset created, DateTimeOffset updated) : base(id, created, updated)
    {
        Title = NotebookLimits.NoteTitle(title);
        Document = document ?? throw new ArgumentNullException(nameof(document));
        FolderId = NotebookLimits.OptionalId(folderId, nameof(folderId));
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        if ((state == NotebookNoteState.Trashed) != (restorePoint is not null) ||
            restorePoint is not null && restorePoint.FolderId != folderId)
            throw new ArgumentException("Only Trash requires a matching restoration point.", nameof(restorePoint));
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        State = state; IsPinned = pinned; RestorePoint = restorePoint; Version = version;
        TagIds = ValidateTags(tagIds); Links = ValidateLinks(links); Images = ValidateImages(document, images);
    }

    public static NotebookNote Rehydrate(Guid id, string title, NotebookDocument document, Guid? folderId,
        NotebookNoteState state, bool pinned, NotebookRestorePoint? restorePoint, IEnumerable<Guid> tagIds,
        IEnumerable<NotebookLink> links, IEnumerable<NotebookImage> images, long version,
        DateTimeOffset created, DateTimeOffset updated) =>
        new(id, title, document, folderId, state, pinned, restorePoint, tagIds, links, images, version, created, updated);

    public string Title { get; private set; }
    public NotebookDocument Document { get; private set; }
    public Guid? FolderId { get; private set; }
    public NotebookNoteState State { get; private set; }
    public bool IsPinned { get; private set; }
    public long Version { get; private set; }
    public NotebookRestorePoint? RestorePoint { get; private set; }
    public IReadOnlyList<Guid> TagIds { get; private set; }
    public IReadOnlyList<NotebookLink> Links { get; private set; }
    public IReadOnlyList<NotebookImage> Images { get; private set; }
    public bool CanPermanentlyDelete => State == NotebookNoteState.Trashed;

    public void UpdateTitle(string title, DateTimeOffset now)
    {
        RequireActive(); string value = NotebookLimits.NoteTitle(title);
        if (Title == value) return;
        Advance(now); Title = value;
    }
    public void SaveDraft(string title, NotebookDocument document, IEnumerable<NotebookImage> images, DateTimeOffset now)
    {
        RequireActive();
        var value = NotebookLimits.NoteTitle(title);
        ArgumentNullException.ThrowIfNull(document);
        var copy = ValidateImages(document, images);
        Advance(now); Title = value; Document = document; Images = copy;
    }

    /// <summary>Image additions/removals/caption edits and their document references form one atomic domain edit.</summary>
    public void UpdateContent(NotebookDocument document, IEnumerable<NotebookImage> images, DateTimeOffset now)
    {
        RequireActive(); ArgumentNullException.ThrowIfNull(document);
        var copy = ValidateImages(document, images);
        Advance(now); Document = document; Images = copy;
    }
    public void Move(Guid? folderId, DateTimeOffset now)
    {
        RequireActive(); var value = NotebookLimits.OptionalId(folderId, nameof(folderId));
        if (FolderId == value) return;
        Advance(now); FolderId = value;
    }
    public void SetPinned(bool value, DateTimeOffset now)
    {
        RequireActive(); if (IsPinned == value) return;
        Advance(now); IsPinned = value;
    }
    public void AssignTag(Guid tagId, DateTimeOffset now)
    {
        RequireActive();
        var tags = ValidateTags(TagIds.Append(tagId));
        Advance(now); TagIds = tags;
    }
    public void RemoveTag(Guid tagId, DateTimeOffset now)
    {
        RequireActive(); NotebookLimits.Id(tagId, nameof(tagId));
        if (!TagIds.Contains(tagId)) return;
        var tags = ValidateTags(TagIds.Where(x => x != tagId));
        Advance(now); TagIds = tags;
    }
    public void AddLink(NotebookLink link, DateTimeOffset now)
    {
        RequireActive(); ArgumentNullException.ThrowIfNull(link);
        var links = ValidateLinks(Links.Append(link));
        Advance(now); Links = links;
    }
    public void RemoveLink(NotebookTargetKind kind, Guid targetId, DateTimeOffset now)
    {
        RequireActive();
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        NotebookLimits.Id(targetId, nameof(targetId));
        if (!Links.Any(x => x.Kind == kind && x.TargetId == targetId)) return;
        var links = ValidateLinks(Links.Where(x => x.Kind != kind || x.TargetId != targetId));
        Advance(now); Links = links;
    }
    public void Archive(DateTimeOffset now)
    {
        RequireActive(); Advance(now); State = NotebookNoteState.Archived;
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
    /// <param name="previousFolderExists">Supplied by the application from the same transaction as restoration.</param>
    public void Restore(bool previousFolderExists, DateTimeOffset now)
    {
        if (State != NotebookNoteState.Trashed) throw new InvalidOperationException("Only Trashed notes can be restored.");
        var point = RestorePoint!;
        Advance(now); State = point.PreviousState; FolderId = previousFolderExists ? point.FolderId : null; RestorePoint = null;
    }

    // Catalog maintenance is distinct from editing archived/trashed content; it must cover all lifecycle states.
    public void ApplyFolderDeletion(Guid deletedFolderId, DateTimeOffset now)
    {
        NotebookLimits.Id(deletedFolderId, nameof(deletedFolderId));
        if (FolderId != deletedFolderId) return;
        var point = RestorePoint is null ? null : new NotebookRestorePoint(RestorePoint.PreviousState, null);
        Advance(now); FolderId = null; RestorePoint = point;
    }
    public void ApplyTagDeletion(Guid deletedTagId, DateTimeOffset now)
    {
        NotebookLimits.Id(deletedTagId, nameof(deletedTagId));
        if (!TagIds.Contains(deletedTagId)) return;
        var tags = ValidateTags(TagIds.Where(x => x != deletedTagId));
        Advance(now); TagIds = tags;
    }
    private void RequireActive()
    {
        if (State != NotebookNoteState.Active) throw new InvalidOperationException("Unarchive or restore the note before editing.");
    }
    private void Advance(DateTimeOffset now)
    {
        var next = checked(Version + 1); SetUpdatedAtUtc(now); Version = next;
    }
    private static IReadOnlyList<Guid> ValidateTags(IEnumerable<Guid> tags)
    {
        var copy = NotebookLimits.Copy(tags, NotebookLimits.Tags, nameof(tags));
        if (copy.Contains(Guid.Empty) || copy.Distinct().Count() != copy.Count) throw new ArgumentException("Tag identities must be nonempty and unique.");
        return copy;
    }
    private static IReadOnlyList<NotebookLink> ValidateLinks(IEnumerable<NotebookLink> links)
    {
        var copy = NotebookLimits.Copy(links, NotebookLimits.Links, nameof(links));
        if (copy.Select(x => (x.Kind, x.TargetId)).Distinct().Count() != copy.Count) throw new ArgumentException("Duplicate target link.");
        return copy;
    }
    private static IReadOnlyList<NotebookImage> ValidateImages(NotebookDocument document, IEnumerable<NotebookImage> images)
    {
        var copy = NotebookLimits.Copy(images, NotebookLimits.Images, nameof(images));
        if (copy.Count != document.ImageNodes.Count || copy.Select(x => x.Id).Distinct().Count() != copy.Count)
            throw new ArgumentException("Images must match the document references exactly.");
        var ownedKeys = copy.Where(x => x.Ownership == NotebookImageOwnership.NoteOwned).Select(x => x.StorageKey!).ToArray();
        if (ownedKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ownedKeys.Length)
            throw new ArgumentException("Each note-owned attachment needs its own managed file.");
        for (int i = 0; i < copy.Count; i++)
        {
            var node = document.ImageNodes[i]; var image = copy[i];
            if (image.Position != i || image.Id != node.ImageId || image.Caption != node.Caption || image.AlternativeText != node.AlternativeText)
                throw new ArgumentException("Image order, caption and alternative text must match the document.");
        }
        return copy;
    }
}
