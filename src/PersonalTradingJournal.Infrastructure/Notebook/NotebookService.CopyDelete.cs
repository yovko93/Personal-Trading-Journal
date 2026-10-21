using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Notebook;

public sealed partial class NotebookService
{
    private async Task<NotebookWriteResult> DuplicateAsync(DuplicateNotebookNote request, CancellationToken ct)
    {
        NotebookLimits.Id(request.NoteId, nameof(request.NoteId));
        if (request.ExpectedVersion < 1) return new(NotebookWriteStatus.Invalid);
        var source = await LoadAsync(request.NoteId, ct);
        if (source.Status == NotebookLoadStatus.NotFound) return new(NotebookWriteStatus.NotFound);
        if ((source.Note?.Version ?? source.UnavailableMetadata?.Version) != request.ExpectedVersion) return new(NotebookWriteStatus.Conflict);
        if (source.Note is not { } note) return new(NotebookWriteStatus.UnsupportedContent);
        if (note.State == NotebookNoteState.Trashed) return new(NotebookWriteStatus.Invalid);
        if (imageFiles is null && note.Images.Any(x => x.Ownership == NotebookImageOwnership.NoteOwned)) return new(NotebookWriteStatus.StorageFailure);
        var id = Guid.NewGuid(); var ids = note.Images.ToDictionary(x => x.Id, _ => Guid.NewGuid());
        var document = NotebookDuplication.Document(note.Document, ids); var encoded = codec.Encode(document);
        var staged = new List<(Guid Id, NotebookStoredImage File)>(); var images = new List<NotebookImage>();
        NotebookWriteResult result;
        try
        {
            foreach (var image in note.Images)
            {
                if (image.Ownership == NotebookImageOwnership.NoteOwned)
                {
                    var bytes = await imageFiles!.ReadAsync(new(image.StorageKey!, image.SizeBytes!.Value, image.Sha256!), ct);
                    using var stream = new MemoryStream(bytes, writable: false);
                    var copy = await imageFiles.StageAsync(id, ids[image.Id], stream, ct);
                    staged.Add((ids[image.Id], copy));
                    images.Add(NotebookImage.Owned(ids[image.Id], image.Position, copy.StorageKey, copy.SizeBytes, copy.Sha256, image.Caption, image.AlternativeText));
                }
                else images.Add(NotebookImage.TradeReference(ids[image.Id], image.Position, image.TradeId!.Value, image.TradeScreenshotId!.Value, image.Caption, image.AlternativeText));
            }
            result = await CommitAsync();
        }
        catch (OperationCanceledException) { result = new(NotebookWriteStatus.Cancelled); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or DbUpdateException or InvalidOperationException)
        { result = new(NotebookWriteStatus.StorageFailure, CleanupPending: ex is NotebookImageFileException { CleanupPending: true }); }
        if (result.Status == NotebookWriteStatus.Success) return result;
        // Resolve ambiguous acknowledgement before compensating any published file.
        try
        {
            var resolved = await LoadAsync(id, CancellationToken.None);
            if (resolved.Note is { } committed && codec.Encode(committed.Document).Json == encoded.Json && committed.Version == 1)
                return new(NotebookWriteStatus.Success, committed);
            if (resolved.Status != NotebookLoadStatus.NotFound) return result with { CleanupPending = true };
        }
        catch (Exception) { return result with { CleanupPending = staged.Count > 0 || result.CleanupPending }; }
        foreach (var item in staged)
        {
            bool safe = false;
            try { safe = await imageFiles!.DiscardUncommittedAsync(item.File, CancellationToken.None); } catch (Exception) { }
            if (!safe)
            {
                result = result with { CleanupPending = true };
                // Existing retention ledger records ambiguous prepared originals; no directory scanning.
                try
                {
                    await using var retained = await factory.CreateDbContextAsync(CancellationToken.None);
                    if (!await retained.NotebookOwnedImageFiles.AnyAsync(x => x.Id == item.Id))
                    { retained.NotebookOwnedImageFiles.Add(Owned(id, item.Id, item.File)); await retained.SaveChangesAsync(); }
                }
                catch (Exception) { /* Outcome remains explicitly pending; never delete uncertain bytes. */ }
            }
        }
        return result;

        async Task<NotebookWriteResult> CommitAsync()
        {
            await using var db = await factory.CreateDbContextAsync(ct); await db.Database.OpenConnectionAsync(ct);
            await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
            await db.Database.UseTransactionAsync(tx, ct);
            var current = await db.NotebookNotes.SingleOrDefaultAsync(x => x.Id == note.Id, ct);
            if (current is null) return new(NotebookWriteStatus.NotFound);
            if (current.Version != request.ExpectedVersion) return new(NotebookWriteStatus.Conflict);
            if (current.State is not (1 or 2)) return new(NotebookWriteStatus.Invalid);
            var now = Now;
            var duplicate = NotebookNote.Rehydrate(id, NotebookDuplication.Title(note.Title), document, note.FolderId,
                NotebookNoteState.Active, false, null, note.TagIds, note.Links, images, 1, now, now);
            var row = new NotebookNoteRecord(); NotebookMapper.Apply(row, duplicate, encoded); db.NotebookNotes.Add(row);
            foreach (var tag in note.TagIds) db.NotebookTagAssignments.Add(new() { NoteId = id, TagId = tag });
            foreach (var link in note.Links) db.NotebookLinks.Add(new() { NoteId = id, Kind = (int)link.Kind, TargetId = link.TargetId, LastKnownLabel = link.LastKnownLabel });
            foreach (var image in images) db.NotebookImages.Add(NotebookMapper.Image(id, image));
            foreach (var item in staged) db.NotebookOwnedImageFiles.Add(Owned(id, item.Id, item.File));
            await db.SaveChangesAsync(ct);
            var read = await Read(db, await db.NotebookNotes.AsNoTracking().SingleAsync(x => x.Id == id, ct), ct);
            if (read.Result.Note is null) return new(NotebookWriteStatus.StorageFailure);
            ct.ThrowIfCancellationRequested(); await db.Database.CommitTransactionAsync(CancellationToken.None);
            return new(NotebookWriteStatus.Success, read.Result.Note);
        }
    }
    private static NotebookOwnedImageRecord Owned(Guid note, Guid image, NotebookStoredImage file) =>
        new() { Id = image, NoteId = note, StorageKey = file.StorageKey, SizeBytes = file.SizeBytes, Sha256 = file.Sha256 };

    private async Task<NotebookWriteResult> DeleteAsync(DeleteNotebookNote request, CancellationToken ct)
    {
        NotebookLimits.Id(request.NoteId, nameof(request.NoteId));
        if (!request.Confirmed || request.ExpectedVersion < 1) return new(NotebookWriteStatus.Invalid);
        bool commitEntered = false;
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct); await db.Database.OpenConnectionAsync(ct);
            await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
            await db.Database.UseTransactionAsync(tx, ct);
            var row = await db.NotebookNotes.SingleOrDefaultAsync(x => x.Id == request.NoteId, ct);
            if (row is null) return new(NotebookWriteStatus.NotFound);
            if (row.Version != request.ExpectedVersion) return new(NotebookWriteStatus.Conflict);
            if (row.State != (int)NotebookNoteState.Trashed) return new(NotebookWriteStatus.Invalid);
            // Adopt legacy current originals before cascade, including unreadable documents.
            foreach (var image in await db.NotebookImages.AsNoTracking().Where(x => x.NoteId == row.Id && x.Ownership == 1).ToArrayAsync(ct))
                if (!await db.NotebookOwnedImageFiles.AnyAsync(x => x.Id == image.Id, ct))
                    db.NotebookOwnedImageFiles.Add(Owned(row.Id, image.Id, new(image.StorageKey!, image.SizeBytes!.Value, image.Sha256!)));
            await db.NotebookRecoveryDrafts.Where(x => x.NoteId == row.Id).ExecuteDeleteAsync(ct);
            await db.NotebookTagAssignments.Where(x => x.NoteId == row.Id).ExecuteDeleteAsync(ct);
            await db.NotebookLinks.Where(x => x.NoteId == row.Id).ExecuteDeleteAsync(ct);
            await db.NotebookImages.Where(x => x.NoteId == row.Id).ExecuteDeleteAsync(ct);
            db.NotebookNotes.Remove(row); // Restrict FKs remain; only this note's relationships are removed.
            await db.SaveChangesAsync(ct); ct.ThrowIfCancellationRequested(); commitEntered = true;
            await db.Database.CommitTransactionAsync(CancellationToken.None);
        }
        catch (Exception) when (commitEntered)
        {
            // Never claim failure/compensate on an ambiguous acknowledgement without a fresh read.
            try
            {
                await using var check = await factory.CreateDbContextAsync(CancellationToken.None);
                if (await check.NotebookNotes.AnyAsync(x => x.Id == request.NoteId)) return new(NotebookWriteStatus.StorageFailure);
            }
            catch (Exception) { return new(NotebookWriteStatus.StorageFailure, CleanupPending: true,
                Issues: [new("Deletion", "Commit outcome is uncertain. Refresh before retrying; files were retained.")]); }
        }
        bool cleaned = false;
        try
        {
            await using var check = await factory.CreateDbContextAsync(CancellationToken.None);
            cleaned = !await check.NotebookOwnedImageFiles.AnyAsync(x => x.NoteId == request.NoteId);
            if (!cleaned && imageFiles is not null) cleaned = await imageFiles.CleanupDeletedNoteAsync(request.NoteId, CancellationToken.None);
        }
        catch (Exception) { /* Canonical deletion is already committed; ledger retains pending work. */ }
        return new(NotebookWriteStatus.Success, DeletedNoteId: request.NoteId, CleanupPending: !cleaned,
            Issues: cleaned ? [] : [new("Cleanup", "Note deleted. Some original files are retained pending safe cleanup.")]);
    }
}
