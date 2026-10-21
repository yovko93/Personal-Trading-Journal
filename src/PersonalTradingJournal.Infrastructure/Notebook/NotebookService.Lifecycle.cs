using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Notebook;

public sealed partial class NotebookService
{
    private async Task<NotebookWriteResult> ChangeLifecycleAsync(JournalDbContext db, NotebookNoteRecord row,
        NotebookCommand command, CancellationToken ct)
    {
        var lifecycle = new NotebookLifecycle(row.Id, (NotebookNoteState)row.State, row.FolderId,
            row.PreviousState is { } state ? new((NotebookNoteState)state, row.PreviousFolderId) : null,
            row.Version, row.CreatedAtUtc, row.UpdatedAtUtc);
        var now = Now;
        switch (command)
        {
            case ArchiveNotebookNote: lifecycle.Archive(now); break;
            case UnarchiveNotebookNote: lifecycle.Unarchive(now); break;
            case TrashNotebookNote: lifecycle.Trash(now); break;
            case RestoreNotebookNote:
                lifecycle.Restore(lifecycle.RestorePoint?.FolderId is { } folder &&
                    await db.NotebookFolders.AnyAsync(x => x.Id == folder, ct), now); break;
        }
        // Only lifecycle/audit columns change. Do not decode/re-encode the body or touch children/checkpoints/files.
        row.State = (int)lifecycle.State; row.FolderId = lifecycle.FolderId;
        row.PreviousState = (int?)lifecycle.RestorePoint?.PreviousState; row.PreviousFolderId = lifecycle.RestorePoint?.FolderId;
        row.Version = lifecycle.Version; row.UpdatedAtUtc = lifecycle.UpdatedAtUtc;
        await db.SaveChangesAsync(ct);
        var stored = await db.NotebookNotes.AsNoTracking().SingleAsync(x => x.Id == row.Id, ct);
        if (stored.State != row.State || stored.Version != row.Version || stored.FolderId != row.FolderId ||
            stored.PreviousState != row.PreviousState || stored.PreviousFolderId != row.PreviousFolderId ||
            stored.DocumentJson != row.DocumentJson || stored.UpdatedAtUtc != row.UpdatedAtUtc)
            return new(NotebookWriteStatus.StorageFailure);
        var read = await Read(db, stored, ct);
        ct.ThrowIfCancellationRequested();
        await db.Database.CommitTransactionAsync(CancellationToken.None);
        return new(NotebookWriteStatus.Success, read.Result.Note, UnavailableMetadata: read.Result.UnavailableMetadata);
    }
}
