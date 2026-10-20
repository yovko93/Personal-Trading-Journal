using System.Collections.Immutable;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Application.Notebook;

/// <summary>Editor save with reference reconciliation. New owned bytes must first pass ImportAsync;
/// restored references must match the same note's immutable retention ledger.</summary>
public sealed record SaveNotebookImageDraft(Guid NoteId, long ExpectedVersion, string Title, NotebookDocument Document,
    ImmutableArray<NotebookImage> Images) : NotebookCommand;

// Transport commands are validated by the future handler; bad input returns Invalid without writes.
// ExpectedVersion is mandatory for mutations of an existing note, including metadata and deletion.
public abstract record NotebookCommand;
public sealed record CreateNotebookNote(Guid? FolderId = null, string Title = "Untitled") : NotebookCommand;
public sealed record UpdateNotebookTitle(Guid NoteId, long ExpectedVersion, string Title) : NotebookCommand;
/// <summary>One editor save: title, canonical document and its derived projection commit together.
/// Existing image metadata must remain unchanged; this is not a file-management operation.</summary>
public sealed record SaveNotebookDraft(Guid NoteId, long ExpectedVersion, string Title, NotebookDocument Document,
    ImmutableArray<NotebookImage> Images) : NotebookCommand;
public sealed record UpdateNotebookContent(Guid NoteId, long ExpectedVersion, NotebookDocument Document,
    ImmutableArray<NotebookImage> Images) : NotebookCommand;
public sealed record MoveNotebookNote(Guid NoteId, long ExpectedVersion, Guid? FolderId) : NotebookCommand;
public sealed record SetNotebookPin(Guid NoteId, long ExpectedVersion, bool IsPinned) : NotebookCommand;
public sealed record AssignNotebookTag(Guid NoteId, long ExpectedVersion, Guid TagId) : NotebookCommand;
public sealed record RemoveNotebookTag(Guid NoteId, long ExpectedVersion, Guid TagId) : NotebookCommand;
public sealed record AddNotebookLink(Guid NoteId, long ExpectedVersion, NotebookLink Link) : NotebookCommand;
public sealed record RemoveNotebookLink(Guid NoteId, long ExpectedVersion, NotebookTargetKind Kind, Guid TargetId) : NotebookCommand;
public sealed record ArchiveNotebookNote(Guid NoteId, long ExpectedVersion) : NotebookCommand;
public sealed record UnarchiveNotebookNote(Guid NoteId, long ExpectedVersion) : NotebookCommand;
public sealed record TrashNotebookNote(Guid NoteId, long ExpectedVersion) : NotebookCommand;
public sealed record RestoreNotebookNote(Guid NoteId, long ExpectedVersion) : NotebookCommand;
/// <summary>Application must stage independent owned-image copies and remap attachment IDs; never reuse owned storage
/// keys. Copy body/tags/links into a new Active, unpinned note. Source and targets are unchanged on failure.</summary>
public sealed record DuplicateNotebookNote(Guid NoteId, long ExpectedVersion) : NotebookCommand;
/// <summary>Requires confirmed Trash deletion and atomic retention recheck. Never deletes Trade screenshot targets.</summary>
public sealed record DeleteNotebookNote(Guid NoteId, long ExpectedVersion, bool Confirmed) : NotebookCommand;

// Image operations include the whole resulting document and ordered metadata, so changing a caption/reference
// cannot leave the searchable projection or another block out of sync. The handler checks the named operation's delta.
public sealed record AddNotebookImage(Guid NoteId, long ExpectedVersion, Guid AddedImageId,
    NotebookDocument Document, ImmutableArray<NotebookImage> Images) : NotebookCommand;
/// <summary>Admit one staged original and save its final editor placement, including selection replacement.
/// Remaining references must preserve their existing immutable ownership and node identities.</summary>
public sealed record PlaceNotebookImage(Guid NoteId, long ExpectedVersion, Guid AddedImageId,
    NotebookDocument Document, ImmutableArray<NotebookImage> Images) : NotebookCommand;
public sealed record UpdateNotebookImage(Guid NoteId, long ExpectedVersion, Guid ImageId,
    NotebookDocument Document, ImmutableArray<NotebookImage> Images) : NotebookCommand;
public sealed record RemoveNotebookImage(Guid NoteId, long ExpectedVersion, Guid RemovedImageId,
    NotebookDocument Document, ImmutableArray<NotebookImage> Images) : NotebookCommand;

public abstract record NotebookCatalogCommand;
public sealed record CreateNotebookFolder(string Name) : NotebookCatalogCommand;
public sealed record RenameNotebookFolder(Guid FolderId, long ExpectedVersion, string Name) : NotebookCatalogCommand;
/// <summary>Moves every affected lifecycle/restoration reference to Unfiled atomically, advancing affected note versions.</summary>
public sealed record DeleteNotebookFolder(Guid FolderId, long ExpectedVersion, bool Confirmed) : NotebookCatalogCommand;
public sealed record CreateNotebookTag(string Name) : NotebookCatalogCommand;
public sealed record RenameNotebookTag(Guid TagId, long ExpectedVersion, string Name) : NotebookCatalogCommand;
/// <summary>Removes assignments in every lifecycle state, not notes; advances affected note versions.</summary>
public sealed record DeleteNotebookTag(Guid TagId, long ExpectedVersion, bool Confirmed) : NotebookCatalogCommand;
