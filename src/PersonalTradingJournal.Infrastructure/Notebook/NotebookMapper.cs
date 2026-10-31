using System.Text;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Notebook;

internal static class NotebookMapper
{
    // Binary UTF-16 sort keys retain ordinal ordering, including supplementary characters.
    // SQLite NOCASE alone is ASCII-only and cannot implement the domain name contract.
    internal static string Key(string name) => Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(name.Normalize(NormalizationForm.FormC).ToUpperInvariant()));
    internal static string Search(string title, string plainText) => (title + "\n" + plainText).Normalize(NormalizationForm.FormC).ToUpperInvariant();
    internal static NotebookNote Read(NotebookNoteRecord r, NotebookDocument document, IEnumerable<NotebookTagAssignmentRecord> tags,
        IEnumerable<NotebookLinkRecord> links, IEnumerable<NotebookImageRecord> images)
    {
        if (r.TitleKey != Key(r.Title) || r.ContentVersion != document.Version || r.PlainText != document.PlainText ||
            r.SearchText != Search(r.Title, document.PlainText)) throw new ArgumentException("Notebook projection mismatch.");
        return NotebookNote.Rehydrate(r.Id, r.Title, document, r.FolderId, (NotebookNoteState)r.State, r.IsPinned,
            r.PreviousState is { } state ? new((NotebookNoteState)state, r.PreviousFolderId) : null,
            tags.Select(x => x.TagId), links.Select(x => new NotebookLink((NotebookTargetKind)x.Kind, x.TargetId, x.LastKnownLabel)),
            images.Select(Image), r.Version, r.CreatedAtUtc, r.UpdatedAtUtc);
    }
    internal static NotebookImage Image(NotebookImageRecord r) => r.Ownership switch
    {
        1 when r.TradeId is null && r.TradeScreenshotId is null => NotebookImage.Owned(r.Id, r.Position, r.StorageKey!, r.SizeBytes ?? 0, r.Sha256!, r.Caption, r.AlternativeText),
        2 when r.StorageKey is null && r.SizeBytes is null && r.Sha256 is null => NotebookImage.TradeReference(r.Id, r.Position,
            r.TradeId ?? Guid.Empty, r.TradeScreenshotId ?? Guid.Empty, r.Caption, r.AlternativeText),
        _ => throw new ArgumentException("Invalid ownership.")
    };
    internal static NotebookImageRecord Image(Guid noteId, NotebookImage i) => new()
    {
        NoteId = noteId, Id = i.Id, Position = i.Position, Ownership = (int)i.Ownership, StorageKey = i.StorageKey,
        SizeBytes = i.SizeBytes, Sha256 = i.Sha256, TradeId = i.TradeId, TradeScreenshotId = i.TradeScreenshotId,
        Caption = i.Caption, AlternativeText = i.AlternativeText
    };
    internal static void Apply(NotebookNoteRecord r, NotebookNote note, NotebookEncodedContent? content = null)
    {
        r.Id = note.Id; r.Title = note.Title; r.TitleKey = Key(note.Title); r.FolderId = note.FolderId;
        r.State = (int)note.State; r.IsPinned = note.IsPinned; r.PreviousState = (int?)note.RestorePoint?.PreviousState;
        r.PreviousFolderId = note.RestorePoint?.FolderId; r.Version = note.Version;
        r.CreatedAtUtc = note.CreatedAtUtc; r.UpdatedAtUtc = note.UpdatedAtUtc;
        if (content is not null) { r.DocumentJson = content.Json; r.ContentVersion = content.Version; r.PlainText = content.PlainText; }
        r.SearchText = Search(note.Title, note.Document.PlainText);
    }
}
