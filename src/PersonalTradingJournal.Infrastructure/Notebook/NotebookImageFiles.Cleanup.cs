using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Screenshots;

namespace PersonalTradingJournal.Infrastructure.Notebook;

public sealed partial class NotebookImageFiles
{
    public Task<bool> CleanupDeletedNoteAsync(Guid noteId, CancellationToken token) => Task.Run(() =>
    {
        try
        {
            using var lease = ScreenshotCaptureLease.Acquire(paths.ScreenshotsDirectory, token);
            // The cross-process mutex is thread-affine. Keep its owner here until all async work completes.
            return CleanupUnderLeaseAsync(noteId, token).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or DbUpdateException or InvalidOperationException or OperationCanceledException or ArgumentException)
        { return false; }
    }, CancellationToken.None);

    private async Task<bool> CleanupUnderLeaseAsync(Guid noteId, CancellationToken token)
    {
            await using var db = await factory.CreateDbContextAsync(token);
            // Bound one request; excess retained originals stay in the ledger for explicit later cleanup.
            var ids = await db.NotebookOwnedImageFiles.Where(x => x.NoteId == noteId).Select(x => x.Id).Take(129).ToArrayAsync(token);
            if (ids.Length > 128) return false;
            bool complete = true;
            foreach (var id in ids)
            {
                db.ChangeTracker.Clear(); await db.Database.OpenConnectionAsync(token);
                using (var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false))
                {
                    await db.Database.UseTransactionAsync(tx, token);
                    var row = await db.NotebookOwnedImageFiles.SingleOrDefaultAsync(x => x.Id == id, token);
                    if (row is null) continue;
                    if (!await SafeAsync(db, row, token))
                    { row.CleanupPending = false; await db.SaveChangesAsync(token); await db.Database.CommitTransactionAsync(CancellationToken.None); complete = false; continue; }
                    // Durable intent first. Archive v1 need not carry optional cleanup bytes after this boundary.
                    row.CleanupPending = true; await db.SaveChangesAsync(token); token.ThrowIfCancellationRequested();
                    await db.Database.CommitTransactionAsync(CancellationToken.None);
                }
                db.ChangeTracker.Clear();
                using (var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false))
                {
                    await db.Database.UseTransactionAsync(tx, token);
                    var row = await db.NotebookOwnedImageFiles.SingleAsync(x => x.Id == id, token);
                    if (!await SafeAsync(db, row, token))
                    { row.CleanupPending = false; await db.SaveChangesAsync(token); await db.Database.CommitTransactionAsync(CancellationToken.None); complete = false; continue; }
                    string path = Resolve(row.StorageKey);
                    bool exists;
                    try { File.GetAttributes(path); exists = true; }
                    catch (FileNotFoundException) { exists = false; }
                    catch (DirectoryNotFoundException) { exists = false; }
                    if (exists)
                    {
                        // Resolve rejects links; verified read rejects aliases/tampered bytes. Never delete a caller path.
                        await ReadAsync(new(row.StorageKey, row.SizeBytes, row.Sha256), token);
                        token.ThrowIfCancellationRequested(); File.Delete(path);
                    }
                    db.NotebookOwnedImageFiles.Remove(row); await db.SaveChangesAsync(token);
                    await db.Database.CommitTransactionAsync(CancellationToken.None);
                    // A crash before this commit leaves optional CleanupPending metadata, safe to retry.
                }
            }
            return complete;
    }

    private static async Task<bool> SafeAsync(JournalDbContext db, NotebookOwnedImageRecord row, CancellationToken ct)
    {
        if (!Owns(row.StorageKey, row.NoteId, row.Id) || await db.NotebookNotes.AnyAsync(x => x.Id == row.NoteId, ct)) return false;
        var keys = db.NotebookImages.Where(x => x.StorageKey != null).Select(x => x.StorageKey!)
            .Concat(db.NotebookOwnedImageFiles.Where(x => x.Id != row.Id).Select(x => x.StorageKey))
            .Concat(db.TradeScreenshots.Select(x => x.StorageKey))
            .Concat(db.PlaybookExamples.Where(x => x.ImageStorageKey != null).Select(x => x.ImageStorageKey!))
            .Concat(db.CurrentPlaybookExamples.Where(x => x.ImageStorageKey != null).Select(x => x.ImageStorageKey!))
            .Concat(db.TradingPlanAssets.Select(x => x.StorageKey));
        if (await keys.AnyAsync(x => x.ToUpper() == row.StorageKey.ToUpper(), ct)) return false;
        // Unknown/over-capacity content is retention, never evidence of absence.
        var notes = await db.NotebookNotes.Select(x => new { x.Id, Length = x.DocumentJson.Length, Version = x.ContentVersion }).Take(129).ToArrayAsync(ct);
        var drafts = await db.NotebookRecoveryDrafts.Select(x => new { x.Id, Length = x.DocumentJson.Length, Version = x.EnvelopeVersion }).Take(129).ToArrayAsync(ct);
        if (notes.Length > 128 || drafts.Length > 128) return false;
        var codec = new NotebookJsonCodec();
        foreach (var item in notes.Select(x => (Row:x, Draft:false)).Concat(drafts.Select(x => (Row:x, Draft:true))))
        {
            if (item.Row.Version != 1 || item.Row.Length > 1024 * 1024) return false;
            string json = item.Draft ? await db.NotebookRecoveryDrafts.Where(x => x.Id == item.Row.Id).Select(x => x.DocumentJson).SingleAsync(ct)
                : await db.NotebookNotes.Where(x => x.Id == item.Row.Id).Select(x => x.DocumentJson).SingleAsync(ct);
            var decoded = codec.Decode(json);
            if (decoded.Status != NotebookContentStatus.Success || decoded.Document!.ImageNodes.Any(x => x.ImageId == row.Id)) return false;
        }
        return true;
    }
}
