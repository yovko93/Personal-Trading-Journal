using System.Collections.Immutable;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Notebook;

public sealed partial class NotebookService(IDbContextFactory<JournalDbContext> factory, INotebookContentCodec codec, TimeProvider? clock = null) : INotebookService
{
    private DateTimeOffset Now => (clock ?? TimeProvider.System).GetUtcNow();
    public async Task<NotebookLoadResult> LoadAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        NotebookLimits.Id(noteId, nameof(noteId));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await db.Database.UseTransactionAsync(tx, cancellationToken);
        var r = await db.NotebookNotes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == noteId, cancellationToken);
        return r is null ? new(NotebookLoadStatus.NotFound) : (await Read(db, r, cancellationToken)).Result;
    }
    private async Task<(NotebookLoadResult Result, NotebookNote? Note)> Read(JournalDbContext db, NotebookNoteRecord r, CancellationToken ct)
    {
        var decoded = codec.Decode(r.DocumentJson);
        NotebookLoadStatus status = decoded.Status switch
        {
            NotebookContentStatus.UnsupportedVersion => NotebookLoadStatus.UnsupportedContent,
            NotebookContentStatus.TooLarge => NotebookLoadStatus.TooLarge,
            _ => NotebookLoadStatus.InvalidContent
        };
        if (decoded.Status == NotebookContentStatus.Success)
        {
            try
            {
                var tags = await db.NotebookTagAssignments.AsNoTracking().Where(x => x.NoteId == r.Id).OrderBy(x => x.TagId).Take(33).ToArrayAsync(ct);
                var links = await db.NotebookLinks.AsNoTracking().Where(x => x.NoteId == r.Id).OrderBy(x => x.Kind).ThenBy(x => x.TargetId).Take(101).ToArrayAsync(ct);
                var images = await db.NotebookImages.AsNoTracking().Where(x => x.NoteId == r.Id).OrderBy(x => x.Position).Take(33).ToArrayAsync(ct);
                var note = NotebookMapper.Read(r, decoded.Document!, tags, links, images);
                return (new(NotebookLoadStatus.Success, NotebookNoteSnapshot.From(note)), note);
            }
            catch (ArgumentException) { status = NotebookLoadStatus.InvalidContent; }
        }
        return (new(status, OriginalContent: status == NotebookLoadStatus.TooLarge ? null : r.DocumentJson,
            UnavailableMetadata: new(r.Id, r.Title, r.Version, (NotebookNoteState)r.State)), null);
    }

    public async Task<NotebookWriteResult> ExecuteAsync(NotebookCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(command);
            if (command is DuplicateNotebookNote or DeleteNotebookNote or AddNotebookImage or UpdateNotebookImage or RemoveNotebookImage)
                return new(NotebookWriteStatus.Deferred, Issues: [new("Operation", "Image/file ownership orchestration is not available in M22.3.")]);
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            await db.Database.OpenConnectionAsync(cancellationToken);
            await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
            await db.Database.UseTransactionAsync(tx, cancellationToken);
            var ct = cancellationToken;
            NotebookNote note; NotebookNoteRecord row; NotebookEncodedContent? encoded = null;
            if (command is CreateNotebookNote create)
            {
                if (create.FolderId is { } folder && !await db.NotebookFolders.AnyAsync(x => x.Id == folder, ct)) return new(NotebookWriteStatus.NotFound);
                note = new(Now, create.FolderId, create.Title);
                row = new(); encoded = codec.Encode(note.Document);
                db.NotebookNotes.Add(row);
            }
            else
            {
                var (id, expected) = Identity(command);
                NotebookLimits.Id(id, nameof(command));
                if (expected < 1) return new(NotebookWriteStatus.Invalid);
                row = await db.NotebookNotes.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new MissingNoteException();
                if (row.Version != expected) return new(NotebookWriteStatus.Conflict);
                var read = await Read(db, row, ct);
                if (read.Note is null) return new(NotebookWriteStatus.UnsupportedContent,
                    Issues: [new("Content", "Original content is unavailable and has not been changed.")]);
                note = read.Note;
                var now = Now;
                switch (command)
                {
                    case UpdateNotebookTitle title: note.UpdateTitle(title.Title, now); break;
                    case UpdateNotebookContent content:
                        if (content.Images.IsDefault) return new(NotebookWriteStatus.Invalid);
                        // Text-only editing cannot smuggle in file imports/deletes via the content command.
                        if (!content.Images.SequenceEqual(note.Images)) return new(NotebookWriteStatus.Deferred);
                        note.UpdateContent(content.Document, content.Images, now);
                        try { encoded = codec.Encode(note.Document); }
                        catch (ArgumentOutOfRangeException) { return new(NotebookWriteStatus.TooLarge); }
                        break;
                    case MoveNotebookNote move:
                        if (move.FolderId is { } folder && !await db.NotebookFolders.AnyAsync(x => x.Id == folder, ct)) return new(NotebookWriteStatus.NotFound);
                        note.Move(move.FolderId, now); break;
                    case SetNotebookPin pin: note.SetPinned(pin.IsPinned, now); break;
                    case AssignNotebookTag tag:
                        if (!await db.NotebookTags.AnyAsync(x => x.Id == tag.TagId, ct)) return new(NotebookWriteStatus.NotFound);
                        note.AssignTag(tag.TagId, now);
                        db.NotebookTagAssignments.Add(new() { NoteId = id, TagId = tag.TagId }); break;
                    case RemoveNotebookTag tag:
                        note.RemoveTag(tag.TagId, now);
                        await db.NotebookTagAssignments.Where(x => x.NoteId == id && x.TagId == tag.TagId).ExecuteDeleteAsync(ct); break;
                    case AddNotebookLink link:
                        ArgumentNullException.ThrowIfNull(link.Link);
                        if (!await TargetExists(db, link.Link, ct)) return new(NotebookWriteStatus.UnavailableTarget);
                        note.AddLink(link.Link, now);
                        db.NotebookLinks.Add(new() { NoteId = id, Kind = (int)link.Link.Kind, TargetId = link.Link.TargetId, LastKnownLabel = link.Link.LastKnownLabel }); break;
                    case RemoveNotebookLink link:
                        note.RemoveLink(link.Kind, link.TargetId, now);
                        await db.NotebookLinks.Where(x => x.NoteId == id && x.Kind == (int)link.Kind && x.TargetId == link.TargetId).ExecuteDeleteAsync(ct); break;
                    case ArchiveNotebookNote: note.Archive(now); break;
                    case UnarchiveNotebookNote: note.Unarchive(now); break;
                    case TrashNotebookNote: note.Trash(now); break;
                    case RestoreNotebookNote:
                        note.Restore(note.RestorePoint?.FolderId is { } previous && await db.NotebookFolders.AnyAsync(x => x.Id == previous, ct), now); break;
                    default: return new(NotebookWriteStatus.Invalid);
                }
            }
            NotebookMapper.Apply(row, note, encoded);
            await db.SaveChangesAsync(ct);
            var readback = await db.NotebookNotes.AsNoTracking().SingleAsync(x => x.Id == row.Id, ct);
            var verified = await Read(db, readback, ct);
            if (verified.Result.Status != NotebookLoadStatus.Success) return new(NotebookWriteStatus.StorageFailure);
            ct.ThrowIfCancellationRequested();
            // Final cancellation boundary: commit is deliberately non-cancellable. Once it succeeds return Success.
            await db.Database.CommitTransactionAsync(CancellationToken.None);
            return new(NotebookWriteStatus.Success, verified.Result.Note);
        }
        catch (MissingNoteException) { return new(NotebookWriteStatus.NotFound); }
        catch (OperationCanceledException) { return new(NotebookWriteStatus.Cancelled); }
        catch (DbUpdateConcurrencyException) { return new(NotebookWriteStatus.Conflict); }
        catch (ArgumentException) { return new(NotebookWriteStatus.Invalid); }
        catch (InvalidOperationException) { return new(NotebookWriteStatus.Invalid); }
        catch (Exception ex) when (ex is SqliteException or DbUpdateException or IOException or OverflowException)
        { return new(NotebookWriteStatus.StorageFailure); }
    }
    private sealed class MissingNoteException : Exception;
    private static Task<bool> TargetExists(JournalDbContext db, NotebookLink link, CancellationToken ct) =>
        link.Kind == NotebookTargetKind.Trade ? db.Trades.AnyAsync(x => x.Id == link.TargetId, ct) : db.TradingSetups.AnyAsync(x => x.Id == link.TargetId, ct);

    private static (Guid, long) Identity(NotebookCommand c) => c switch
    {
        UpdateNotebookTitle x => (x.NoteId, x.ExpectedVersion), UpdateNotebookContent x => (x.NoteId, x.ExpectedVersion),
        MoveNotebookNote x => (x.NoteId, x.ExpectedVersion), SetNotebookPin x => (x.NoteId, x.ExpectedVersion),
        AssignNotebookTag x => (x.NoteId, x.ExpectedVersion), RemoveNotebookTag x => (x.NoteId, x.ExpectedVersion),
        AddNotebookLink x => (x.NoteId, x.ExpectedVersion), RemoveNotebookLink x => (x.NoteId, x.ExpectedVersion),
        ArchiveNotebookNote x => (x.NoteId, x.ExpectedVersion), UnarchiveNotebookNote x => (x.NoteId, x.ExpectedVersion),
        TrashNotebookNote x => (x.NoteId, x.ExpectedVersion), RestoreNotebookNote x => (x.NoteId, x.ExpectedVersion),
        _ => throw new ArgumentException("Unknown command.")
    };
}
