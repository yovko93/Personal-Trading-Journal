using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Notebook;

public sealed partial class NotebookService
{
    internal static NotebookImage At(NotebookImage image, int position, string? caption = null, string? alternative = null) =>
        image.Ownership == NotebookImageOwnership.NoteOwned
            ? NotebookImage.Owned(image.Id, position, image.StorageKey!, image.SizeBytes!.Value, image.Sha256!, caption ?? image.Caption, alternative ?? image.AlternativeText)
            : NotebookImage.TradeReference(image.Id, position, image.TradeId!.Value, image.TradeScreenshotId!.Value, caption ?? image.Caption, alternative ?? image.AlternativeText);

    internal static NotebookDocument WithoutImage(NotebookDocument doc, Guid id) => new(RewriteNodes(doc.Blocks, id));
    private static IEnumerable<NotebookNode> RewriteNodes(IEnumerable<NotebookNode> nodes, Guid id)
    {
        foreach (var n in nodes)
        {
            if (n.Kind == NotebookNodeKind.Image && n.ImageId == id) continue;
            yield return n.Kind switch
            {
                NotebookNodeKind.BulletedList or NotebookNodeKind.NumberedList or NotebookNodeKind.Checklist => NotebookNode.List(n.Id, n.Kind, RewriteNodes(n.Children, id)),
                NotebookNodeKind.ListItem or NotebookNodeKind.ChecklistItem => NotebookNode.Item(n.Id, RewriteNodes(n.Children, id), n.IsChecked),
                _ => n
            };
        }
    }
    private async Task<NotebookWriteStatus> ApplyImageCommand(JournalDbContext db, NotebookNote note, NotebookCommand command,
        DateTimeOffset now, CancellationToken ct)
    {
        var (id, document, images, adding, removing) = command switch
        {
            AddNotebookImage c => (c.AddedImageId, c.Document, c.Images, true, false),
            UpdateNotebookImage c => (c.ImageId, c.Document, c.Images, false, false),
            RemoveNotebookImage c => (c.RemovedImageId, c.Document, c.Images, false, true),
            _ => throw new ArgumentException()
        };
        NotebookLimits.Id(id, nameof(id));
        if (images.IsDefault || document is null) return NotebookWriteStatus.Invalid;
        var old = note.Images.SingleOrDefault(x => x.Id == id);
        var changed = images.SingleOrDefault(x => x.Id == id);
        if (adding ? old is not null || changed is null : old is null || (removing ? changed is not null : changed is null)) return NotebookWriteStatus.Invalid;
        if (images.Length != note.Images.Count + (adding ? 1 : removing ? -1 : 0)) return NotebookWriteStatus.Invalid;
        foreach (var image in images.Where(x => x.Id != id))
            if (note.Images.SingleOrDefault(x => x.Id == image.Id) is not { } original || At(original, 0) != At(image, 0)) return NotebookWriteStatus.Invalid;
        if (!adding && !removing && (At(old!, 0, changed!.Caption, changed.AlternativeText) != At(changed, 0) ||
            note.Document.ImageNodes.Single(n => n.ImageId == id).Id != document.ImageNodes.Single(n => n.ImageId == id).Id)) return NotebookWriteStatus.Invalid;
        try
        {
            if (codec.Encode(WithoutImage(note.Document, id)).Json != codec.Encode(WithoutImage(document, id)).Json) return NotebookWriteStatus.Invalid;
        }
        catch (ArgumentOutOfRangeException) { return NotebookWriteStatus.TooLarge; }
        // Adopt existing owned metadata into retention before an edit can remove its last current reference.
        // No source-note FK/cascade: later lifecycle work must resolve recovery/undo before reclaiming these rows.
        foreach (var owned in note.Images.Where(x => x.Ownership == NotebookImageOwnership.NoteOwned))
        {
            var retained = await db.NotebookOwnedImageFiles.SingleOrDefaultAsync(x => x.Id == owned.Id, ct);
            if (retained is null) db.NotebookOwnedImageFiles.Add(new() { Id = owned.Id, NoteId = note.Id, StorageKey = owned.StorageKey!, SizeBytes = owned.SizeBytes!.Value, Sha256 = owned.Sha256! });
            else if (retained.NoteId != note.Id || retained.StorageKey != owned.StorageKey || retained.SizeBytes != owned.SizeBytes || retained.Sha256 != owned.Sha256)
                return NotebookWriteStatus.Invalid;
        }
        if (adding)
        {
            if (changed!.Ownership != NotebookImageOwnership.NoteOwned || !NotebookImageFiles.Owns(changed.StorageKey!, note.Id, id)) return NotebookWriteStatus.Invalid;
            var ledger = await db.NotebookOwnedImageFiles.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (ledger is not null)
            {
                if (ledger.NoteId != note.Id || ledger.StorageKey != changed.StorageKey || ledger.SizeBytes != changed.SizeBytes || ledger.Sha256 != changed.Sha256)
                    return NotebookWriteStatus.Invalid;
            }
            else
            {
                db.NotebookOwnedImageFiles.Add(new() { Id = id, NoteId = note.Id, StorageKey = changed.StorageKey!, SizeBytes = changed.SizeBytes!.Value, Sha256 = changed.Sha256! });
            }
            if (imageFiles is null) return NotebookWriteStatus.Deferred;
            await imageFiles.ReadAsync(new(changed.StorageKey!, changed.SizeBytes!.Value, changed.Sha256!), ct, requireAdmission: true);
        }
        note.UpdateContent(document, images, now);
        // Replace the small bounded assignment set after validating the whole delta. Ledger rows never disappear here.
        await db.NotebookImages.Where(x => x.NoteId == note.Id).ExecuteDeleteAsync(ct);
        foreach (var image in images) db.NotebookImages.Add(NotebookMapper.Image(note.Id, image));
        return NotebookWriteStatus.Success;
    }
}
