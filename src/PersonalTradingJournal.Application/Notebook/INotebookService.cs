using System.Collections.Immutable;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Application.Notebook;

public enum NotebookWriteStatus { Success, Invalid, Conflict, NotFound, UnavailableTarget, UnsupportedContent, TooLarge, Cancelled, StorageFailure, Deferred }
public sealed record NotebookValidationIssue(string Field, string Message);
public sealed record NotebookWriteResult(NotebookWriteStatus Status, NotebookNoteSnapshot? Note = null,
    Guid? DeletedNoteId = null, ImmutableArray<NotebookValidationIssue> Issues = default);
public sealed record NotebookCatalogResult(NotebookWriteStatus Status, NotebookNamedItem? Item = null,
    Guid? DeletedItemId = null, ImmutableArray<NotebookValidationIssue> Issues = default);
public enum NotebookLoadStatus { Success, NotFound, UnsupportedContent, InvalidContent, TooLarge }
/// <summary>Unsupported/invalid content is never substituted with an empty editable note. Preserve the stored original;
/// bounded raw content may be returned for recovery, never executed. TooLarge leaves the original in storage.</summary>
public sealed record NotebookUnavailableMetadata(Guid Id, string Title, long Version, NotebookNoteState State);
public sealed record NotebookLoadResult(NotebookLoadStatus Status, NotebookNoteSnapshot? Note = null,
    string? OriginalContent = null, ImmutableArray<NotebookValidationIssue> Issues = default,
    NotebookUnavailableMetadata? UnavailableMetadata = null);

/// <summary>
/// Database-backed implementation in M22.3; cross-resource operations explicitly return Deferred.
/// Reads throw cancellation/storage errors. Mutations validate commands, versions, catalog existence and target
/// identity transactionally, return sanitized outcomes, and publish only verified committed snapshots.
/// A post-commit cancellation must not report an uncommitted Cancelled result.
/// Folder/tag name uniqueness uses normalized names and the domain ordinal-ignore-case comparers.
/// Duplicate/image/delete orchestration stages and compensates owned files, and rechecks all retention references;
/// neither links nor Trade screenshot references authorize a target mutation/deletion.
/// </summary>
public interface INotebookService
{
    Task<NotebookLoadResult> LoadAsync(Guid noteId, CancellationToken cancellationToken = default);
    Task<NotebookPage> ListAsync(NotebookQuery query, CancellationToken cancellationToken = default);
    /// <summary>Bounded continuation of the summary's initial folder/tag pages; no unbounded catalog read.</summary>
    Task<NotebookCatalogPage> ListCatalogAsync(NotebookCatalogQuery query, CancellationToken cancellationToken = default);
    Task<NotebookWriteResult> ExecuteAsync(NotebookCommand command, CancellationToken cancellationToken = default);
    Task<NotebookCatalogResult> ExecuteCatalogAsync(NotebookCatalogCommand command, CancellationToken cancellationToken = default);
    /// <summary>At most 100 distinct links; batched lookup. No same-name substitution or unbounded per-row queries.</summary>
    Task<ImmutableArray<NotebookResolvedLink>> ResolveLinksAsync(ImmutableArray<NotebookLink> links, CancellationToken cancellationToken = default);
}

public enum NotebookContentStatus { Success, UnsupportedVersion, Invalid, TooLarge }
public sealed record NotebookDecodedContent(NotebookContentStatus Status, NotebookDocument? Document = null);
public sealed record NotebookEncodedContent(string Json, string PlainText, int Version);
/// <summary>
/// Codec contract for M22.3, not an editor serializer. Reads must check the exact format/version and bounded
/// UTF-8 input (1 MiB) before parsing allowlisted nodes; unknown properties/types must not execute or be discarded
/// into a rewritten document. Writes enforce that same byte limit and derive PlainText from Document.
/// Persistence must save JSON, projection and metadata in one transaction, not accept an unrelated caller projection.
/// </summary>
public interface INotebookContentCodec
{
    NotebookDecodedContent Decode(string originalJson);
    NotebookEncodedContent Encode(NotebookDocument document);
}
