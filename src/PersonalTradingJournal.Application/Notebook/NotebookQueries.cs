using System.Collections.Immutable;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Application.Notebook;

public enum NotebookScope { AllNotes = 1, Pinned = 2, Folder = 3, Unfiled = 4, Archived = 5, Trash = 6 }
public enum NotebookSort { RecentlyUpdated = 1, Title = 2 }
public enum NotebookCatalogKind { Folders = 1, Tags = 2 }

public sealed class NotebookCatalogQuery
{
    public NotebookCatalogQuery(NotebookCatalogKind kind, int limit = 50, string? cursor = null)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        if (cursor is not null) NotebookLimits.Text(cursor, 2048, nameof(cursor), true);
        Kind = kind; Limit = limit; Cursor = cursor;
    }
    public NotebookCatalogKind Kind { get; }
    public int Limit { get; }
    public string? Cursor { get; }
}

/// <summary>All selected tags are required. Normal scopes contain only Active notes.
/// RecentlyUpdated is descending; Title is normalized ordinal-ignore-case ascending; both tie-break by ID ascending.</summary>
public sealed class NotebookQuery
{
    public NotebookQuery(NotebookScope scope = NotebookScope.AllNotes, Guid? folderId = null, string search = "",
        IEnumerable<Guid>? tagIds = null, NotebookSort sort = NotebookSort.RecentlyUpdated, int limit = 50, string? cursor = null)
    {
        if (!Enum.IsDefined(scope)) throw new ArgumentOutOfRangeException(nameof(scope));
        if (!Enum.IsDefined(sort)) throw new ArgumentOutOfRangeException(nameof(sort));
        if ((scope == NotebookScope.Folder) != folderId.HasValue) throw new ArgumentException("Only Folder scope requires a folder identity.");
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        FolderId = NotebookLimits.OptionalId(folderId, nameof(folderId));
        Search = NotebookLimits.Text(search, NotebookLimits.Search, nameof(search));
        var tags = (tagIds ?? []).Take(NotebookLimits.Tags + 1).ToImmutableArray();
        if (tags.Length > NotebookLimits.Tags || tags.Contains(Guid.Empty) || tags.Distinct().Count() != tags.Length)
            throw new ArgumentException("Tag filters must be bounded, nonempty and unique.", nameof(tagIds));
        if (cursor is not null) NotebookLimits.Text(cursor, 2048, nameof(cursor), true);
        Scope = scope; TagIds = tags; Sort = sort; Limit = limit; Cursor = cursor;
    }
    public NotebookScope Scope { get; }
    public Guid? FolderId { get; }
    public string Search { get; }
    public ImmutableArray<Guid> TagIds { get; }
    public NotebookSort Sort { get; }
    public int Limit { get; }
    /// <summary>Opaque cursor bound to the exact filter/sort. A mismatched cursor must be rejected, not reused.</summary>
    public string? Cursor { get; }
}

public sealed record NotebookNoteSnapshot(Guid Id, string Title, NotebookDocument Document, Guid? FolderId,
    NotebookNoteState State, bool IsPinned, NotebookRestorePoint? RestorePoint, long Version,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, ImmutableArray<Guid> TagIds,
    ImmutableArray<NotebookLink> Links, ImmutableArray<NotebookImage> Images)
{
    public static NotebookNoteSnapshot From(NotebookNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        return new(note.Id, note.Title, note.Document, note.FolderId, note.State, note.IsPinned, note.RestorePoint,
            note.Version, note.CreatedAtUtc, note.UpdatedAtUtc, [.. note.TagIds], [.. note.Links], [.. note.Images]);
    }
}

public sealed record NotebookListItem(Guid Id, string Title, string Preview, NotebookNoteState State,
    bool IsPinned, Guid? FolderId, long Version, DateTimeOffset UpdatedAtUtc, ImmutableArray<NotebookNamedItem> Tags);
public sealed record NotebookNamedItem(Guid Id, string Name, long Version);
public sealed record NotebookScopeCount(NotebookScope Scope, Guid? FolderId, long Count);
public sealed record NotebookCatalogItem(NotebookNamedItem Item, long ActiveNoteCount);
/// <summary>Names ordered using the catalog comparer, then stable ID. Every omitted entry remains reachable.</summary>
public sealed record NotebookCatalogPage(ImmutableArray<NotebookCatalogItem> Items, long TotalCount, string? NextCursor);
public sealed record NotebookWorkspaceSummary(ImmutableArray<NotebookScopeCount> ScopeCounts,
    NotebookCatalogPage Folders, NotebookCatalogPage Tags);
/// <summary>Rows, filtered total and sidebar counts share one consistent read.
/// Sidebar counts exclude text/tag filters. NextCursor uses the last displayed row, not a lookahead row.
/// Enumerations exceeding declared limits fail explicitly, never truncate a supposedly complete catalog.</summary>
public sealed record NotebookPage(ImmutableArray<NotebookListItem> Items, long FilteredCount,
    string? NextCursor, NotebookWorkspaceSummary Summary);
public enum NotebookTargetAvailability { Available, Unavailable }
public sealed record NotebookResolvedLink(NotebookLink Link, NotebookTargetAvailability Availability, string DisplayLabel);
