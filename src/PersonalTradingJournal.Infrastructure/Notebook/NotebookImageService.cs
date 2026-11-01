using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Notebook;

public sealed class NotebookImageService(IDbContextFactory<JournalDbContext> factory, INotebookService notes,
    INotebookContentCodec codec, INotebookImageFiles files) : INotebookImages
{
    public async Task<NotebookWriteResult> ImportAsync(ImportNotebookImage request, Stream content, CancellationToken token = default)
    {
        NotebookStoredImage? staged = null;
        NotebookWriteResult result;
        try
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(request);
            if (request.ExpectedVersion < 1) return new(NotebookWriteStatus.Invalid);
            NotebookLimits.Id(request.NoteId, nameof(request.NoteId)); NotebookLimits.Id(request.ImageId, nameof(request.ImageId));
            ArgumentNullException.ThrowIfNull(content); ArgumentNullException.ThrowIfNull(request.Document);
            var loaded = await notes.LoadAsync(request.NoteId, token);
            if (loaded.Status == NotebookLoadStatus.NotFound) return new(NotebookWriteStatus.NotFound);
            if (loaded.Note is not { } note) return new(NotebookWriteStatus.UnsupportedContent);
            if (note.Version != request.ExpectedVersion) return new(NotebookWriteStatus.Conflict);
            if (!request.EditorPlacement && note.Images.Length >= NotebookLimits.Images) return new(NotebookWriteStatus.TooLarge);
            await using (var ownership = await factory.CreateDbContextAsync(token))
                if (await ownership.NotebookOwnedImageFiles.AnyAsync(x => x.Id == request.ImageId, token)) return new(NotebookWriteStatus.Invalid);
            if (note.State != NotebookNoteState.Active || note.Images.Any(x => x.Id == request.ImageId)) return new(NotebookWriteStatus.Invalid);
            if (request.Document.ImageNodes.Count > NotebookLimits.Images ||
                (!request.EditorPlacement && request.Document.ImageNodes.Count != note.Images.Length + 1)) return new(NotebookWriteStatus.Invalid);
            var node = request.Document.ImageNodes.Single(n => n.ImageId == request.ImageId);
            if (!request.EditorPlacement && codec.Encode(NotebookService.WithoutImage(request.Document, request.ImageId)).Json != codec.Encode(note.Document).Json)
                return new(NotebookWriteStatus.Invalid);
            codec.Encode(request.Document); // Enforce the unchanged canonical document bound before staging bytes.
            staged = await files.StageAsync(note.Id, request.ImageId, content, token);
            var added = NotebookImage.Owned(request.ImageId, 0, staged.StorageKey, staged.SizeBytes, staged.Sha256, node.Caption, node.AlternativeText);
            var images = request.Document.ImageNodes.Select((n, i) => NotebookService.At(n.ImageId == request.ImageId ? added : note.Images.Single(x => x.Id == n.ImageId), i)).ToImmutableArray();
            NotebookCommand command = request.EditorPlacement
                ? new PlaceNotebookImage(note.Id, note.Version, added.Id, request.Document, images)
                : new AddNotebookImage(note.Id, note.Version, added.Id, request.Document, images);
            result = await notes.ExecuteAsync(command, token);
            if (result.Status == NotebookWriteStatus.Success) return result;
        }
        catch (OperationCanceledException) { result = new(NotebookWriteStatus.Cancelled); }
        catch (NotebookImageFileException e) { result = new(e.Status switch
            { NotebookImageStatus.TooLarge => NotebookWriteStatus.TooLarge, NotebookImageStatus.Unsupported => NotebookWriteStatus.UnsupportedContent,
              NotebookImageStatus.Cancelled => NotebookWriteStatus.Cancelled, NotebookImageStatus.Unreadable => NotebookWriteStatus.StorageFailure, _ => NotebookWriteStatus.Invalid },
            CleanupPending: e.CleanupPending, Issues: [new("Image", e.Status.ToString())]); }
        catch (ArgumentOutOfRangeException) { result = new(NotebookWriteStatus.TooLarge); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { result = new(NotebookWriteStatus.Invalid); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        { result = new(NotebookWriteStatus.StorageFailure); }
        if (staged is null) return result;
        // Resolve a possible commit with a fresh, non-cancelled connection. Never compensate an uncertain commit.
        try
        {
            await using var db = await factory.CreateDbContextAsync(CancellationToken.None);
            var ledger = await db.NotebookOwnedImageFiles.AsNoTracking().SingleOrDefaultAsync(x => x.StorageKey == staged.StorageKey);
            if (ledger is not null)
            {
                var committed = await notes.LoadAsync(request.NoteId, CancellationToken.None);
                if (ledger.NoteId == request.NoteId && ledger.StorageKey == staged.StorageKey &&
                    committed.Note is { } n && n.Version == request.ExpectedVersion + 1 &&
                    n.Images.Any(x => x.Id == request.ImageId && x.StorageKey == staged.StorageKey) && codec.Encode(n.Document).Json == codec.Encode(request.Document).Json)
                    return new(NotebookWriteStatus.Success, n);
                return new(NotebookWriteStatus.StorageFailure, CleanupPending: true,
                    Issues: [new("Image", "Commit outcome could not be verified. Image bytes were retained; refresh before retrying.")]);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
        { return new(NotebookWriteStatus.StorageFailure, CleanupPending: true, Issues: [new("Image", "Commit outcome is uncertain; image bytes were retained.")]); }
        try
        {
            bool cleaned = await files.DiscardUncommittedAsync(staged, CancellationToken.None);
            return cleaned ? result : result with { CleanupPending = true, Issues = [new("Image", "Staging cleanup could not be verified; unused bytes were retained.")] };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        { return result with { CleanupPending = true, Issues = [new("Image", "Staging cleanup failed; unused bytes were retained.")] }; }
    }

    public async Task<NotebookImageContent> ReadOriginalAsync(Guid noteId, Guid imageId, CancellationToken token = default)
    {
        NotebookStoredImage? metadata = null;
        try
        {
            NotebookLimits.Id(noteId, nameof(noteId)); NotebookLimits.Id(imageId, nameof(imageId));
            await using var db = await factory.CreateDbContextAsync(token);
            var ledger = await db.NotebookOwnedImageFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == imageId && x.NoteId == noteId, token);
            metadata = ledger is null ? null : new(ledger.StorageKey, ledger.SizeBytes, ledger.Sha256);
            if (metadata is null)
            {
                var row = await db.NotebookImages.AsNoTracking().SingleOrDefaultAsync(x => x.NoteId == noteId && x.Id == imageId, token);
                if (row is null) return new(NotebookImageStatus.Missing);
                if (row.Ownership != (int)NotebookImageOwnership.NoteOwned) return new(NotebookImageStatus.Unsupported);
                metadata = new(row.StorageKey!, row.SizeBytes!.Value, row.Sha256!);
            }
            return new(NotebookImageStatus.Success, metadata, await files.ReadAsync(metadata, token));
        }
        catch (OperationCanceledException) { return new(NotebookImageStatus.Cancelled); }
        catch (NotebookImageFileException e) { return new(e.Status, metadata); }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException) { return new(NotebookImageStatus.Missing, metadata); }
        catch (ArgumentException) { return new(NotebookImageStatus.Invalid); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        { return new(NotebookImageStatus.Unreadable, metadata); }
    }
}
