using System.Collections.Immutable;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Application.Notebook;

/// <summary>A local checkpoint, never a canonical Saved note. Identity is per editing/recovery session.
/// Empty titles are recoverable; unsupported editor structures cannot be checkpointed.</summary>
public sealed record NotebookRecoveryDraft(Guid Id, Guid NoteId, long ExpectedVersion, long Revision,
    string Title, NotebookDocument Document, DateTimeOffset CheckpointedAtUtc);
public sealed record NotebookRecoveryItem(Guid Id, Guid NoteId, long Revision, string Title, DateTimeOffset CheckpointedAtUtc);
public sealed record NotebookRecoveryPage(ImmutableArray<NotebookRecoveryItem> Items, Guid? Next);
public sealed record NotebookRecoveryLoad(NotebookLoadStatus Status, NotebookRecoveryDraft? Draft = null);

public interface INotebookRecoveryStore
{
    // At most 128 retained checkpoints, 50 per page; deterministic ID order.
    Task<NotebookRecoveryPage> ListAsync(Guid? after = null, CancellationToken cancellationToken = default);
    Task<NotebookRecoveryLoad> LoadAsync(Guid id, CancellationToken cancellationToken = default);
    // ExpectedRevision=0 creates a fresh ID; updates require its exact persisted checkpoint revision.
    // An update after cleanup cannot recreate that ID.
    Task<NotebookWriteStatus> PutAsync(NotebookRecoveryDraft draft, long expectedRevision, CancellationToken cancellationToken = default);
    Task<NotebookWriteStatus> RemoveAsync(Guid id, long expectedRevision, CancellationToken cancellationToken = default);
}
