using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Notebook;

public sealed partial class NotebookService
{
    public async Task<NotebookCatalogResult> ExecuteCatalogAsync(NotebookCatalogCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var ct = cancellationToken; ct.ThrowIfCancellationRequested();
            var (folder, id, expected, name, delete, confirmed) = command switch
            {
                CreateNotebookFolder x => (true, (Guid?)null, 0L, x.Name, false, false),
                RenameNotebookFolder x => (true, (Guid?)x.FolderId, x.ExpectedVersion, x.Name, false, false),
                DeleteNotebookFolder x => (true, (Guid?)x.FolderId, x.ExpectedVersion, (string?)null, true, x.Confirmed),
                CreateNotebookTag x => (false, (Guid?)null, 0L, x.Name, false, false),
                RenameNotebookTag x => (false, (Guid?)x.TagId, x.ExpectedVersion, x.Name, false, false),
                DeleteNotebookTag x => (false, (Guid?)x.TagId, x.ExpectedVersion, (string?)null, true, x.Confirmed),
                _ => throw new ArgumentException("Unknown catalog operation.")
            };
            if (id.HasValue && (id == Guid.Empty || expected < 1)) return new(NotebookWriteStatus.Invalid);
            if (delete && !confirmed) return new(NotebookWriteStatus.Invalid);
            string? normalized = delete ? null : NotebookLimits.Name(name!, folder ? NotebookLimits.FolderName : NotebookLimits.TagName);
            string? key = normalized is null ? null : NotebookMapper.Key(normalized);
            await using var db = await factory.CreateDbContextAsync(ct);
            await db.Database.OpenConnectionAsync(ct);
            await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
            await db.Database.UseTransactionAsync(tx, ct);
            NotebookFolderRecord? f = folder && id.HasValue ? await db.NotebookFolders.SingleOrDefaultAsync(x => x.Id == id, ct) : null;
            NotebookTagRecord? t = !folder && id.HasValue ? await db.NotebookTags.SingleOrDefaultAsync(x => x.Id == id, ct) : null;
            if (id.HasValue && f is null && t is null) return new(NotebookWriteStatus.NotFound);
            if (id.HasValue && (f?.Version ?? t!.Version) != expected) return new(NotebookWriteStatus.Conflict);
            if (!delete && (folder
                ? await db.NotebookFolders.AnyAsync(x => x.NameKey == key && x.Id != id, ct)
                : await db.NotebookTags.AnyAsync(x => x.NameKey == key && x.Id != id, ct)))
                return new(NotebookWriteStatus.Invalid, Issues: [new("Name", "A name with the same normalized spelling already exists.")]);
            NotebookNamedItem? result = null; var now = Now;
            if (delete)
            {
                // System catalog maintenance is independent of document readability: never decode/re-encode content.
                var affected = folder
                    ? db.NotebookNotes.Where(x => x.FolderId == id || x.PreviousFolderId == id)
                    : db.NotebookNotes.Where(x => db.NotebookTagAssignments.Any(a => a.NoteId == x.Id && a.TagId == id));
                if (now.Offset != TimeSpan.Zero || await affected.AnyAsync(x => x.UpdatedAtUtc > now || x.Version == long.MaxValue, ct))
                    return new(NotebookWriteStatus.Invalid);
                if (folder)
                {
                    await affected.ExecuteUpdateAsync(s => s.SetProperty(x => x.FolderId, (Guid?)null)
                        .SetProperty(x => x.PreviousFolderId, (Guid?)null).SetProperty(x => x.Version, x => x.Version + 1)
                        .SetProperty(x => x.UpdatedAtUtc, now), ct);
                    db.NotebookFolders.Remove(f!);
                }
                else
                {
                    await affected.ExecuteUpdateAsync(s => s.SetProperty(x => x.Version, x => x.Version + 1).SetProperty(x => x.UpdatedAtUtc, now), ct);
                    await db.NotebookTagAssignments.Where(x => x.TagId == id).ExecuteDeleteAsync(ct);
                    db.NotebookTags.Remove(t!);
                }
            }
            else if (folder)
            {
                var entity = f is null ? new NotebookFolder(normalized!, now) : NotebookFolder.Rehydrate(f.Id, f.Name, f.Version, f.CreatedAtUtc, f.UpdatedAtUtc);
                if (f is not null) entity.Rename(normalized!, now);
                if (f is null) { f = new(); db.NotebookFolders.Add(f); }
                f.Id = entity.Id; f.Name = entity.Name; f.NameKey = key!; f.Version = entity.Version;
                f.CreatedAtUtc = entity.CreatedAtUtc; f.UpdatedAtUtc = entity.UpdatedAtUtc;
                result = new(f.Id, f.Name, f.Version);
            }
            else
            {
                var entity = t is null ? new NotebookTag(normalized!, now) : NotebookTag.Rehydrate(t.Id, t.Name, t.Version, t.CreatedAtUtc, t.UpdatedAtUtc);
                if (t is not null) entity.Rename(normalized!, now);
                if (t is null) { t = new(); db.NotebookTags.Add(t); }
                t.Id = entity.Id; t.Name = entity.Name; t.NameKey = key!; t.Version = entity.Version;
                t.CreatedAtUtc = entity.CreatedAtUtc; t.UpdatedAtUtc = entity.UpdatedAtUtc;
                result = new(t.Id, t.Name, t.Version);
            }
            await db.SaveChangesAsync(ct);
            if (!delete)
            {
                result = folder
                    ? await db.NotebookFolders.AsNoTracking().Where(x => x.Id == result!.Id).Select(x => new NotebookNamedItem(x.Id, x.Name, x.Version)).SingleAsync(ct)
                    : await db.NotebookTags.AsNoTracking().Where(x => x.Id == result!.Id).Select(x => new NotebookNamedItem(x.Id, x.Name, x.Version)).SingleAsync(ct);
            }
            ct.ThrowIfCancellationRequested(); await db.Database.CommitTransactionAsync(CancellationToken.None);
            return new(NotebookWriteStatus.Success, result, delete ? id : null);
        }
        catch (OperationCanceledException) { return new(NotebookWriteStatus.Cancelled); }
        catch (DbUpdateConcurrencyException) { return new(NotebookWriteStatus.Conflict); }
        catch (ArgumentException) { return new(NotebookWriteStatus.Invalid); }
        catch (Exception ex) when (ex is SqliteException or DbUpdateException or IOException or InvalidOperationException or OverflowException)
        { return new(NotebookWriteStatus.StorageFailure); }
    }
}
