using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.ViewModels.Notebook;

public sealed partial class NotebookWorkspaceViewModel
{
    private Task DuplicateNoteAsync() => RunNoteOperationAsync(async ct =>
    {
        if (Note is not { State: NotebookNoteState.Active or NotebookNoteState.Archived } source) return;
        var result = await Task.Run(() => _service.ExecuteAsync(new DuplicateNotebookNote(source.Id, source.Version), ct), ct);
        if (!_active || _disposed || Note?.Id != source.Id) return;
        if (!PublishWrite(result.Status, ""))
        { if (result.CleanupPending) ReportMessage(Message + " Prepared originals were retained pending safe cleanup.", Notification?.Kind ?? NotebookNotificationKind.Error); return; }
        if (result.Note is not { } copy || copy.Id == source.Id || copy.Version != 1 || copy.IsPinned || copy.State != NotebookNoteState.Active ||
            copy.Title != NotebookDuplication.Title(source.Title) || copy.Document.PlainText != source.Document.PlainText ||
            !copy.TagIds.SequenceEqual(source.TagIds) || !copy.Links.SequenceEqual(source.Links))
        { ReportMessage("Duplication could not be verified. Keep your draft and refresh before retrying.", NotebookNotificationKind.Error); return; }
        AcceptNoteDeparture(); ResetLinkPresentation(); _unavailable = null; Note = copy; InstallDraft();
        ContentMessage = copy.Document.Blocks.Count == 0 ? "This note has no content." : "";
        _publishing = true;
        try { if (Scope?.Scope is NotebookScope.Archived or NotebookScope.Pinned) Scope = Scopes.First(x => x.Scope == NotebookScope.AllNotes); }
        finally { _publishing = false; }
        Panel = 2;
        if (!ct.IsCancellationRequested) await ReadPageAsync(false, copy.Id, ct, keepEditor: true);
        if (Current(ct) && Note?.Id == copy.Id) ReportMessage(SelectedNote is null ? "Note duplicated. Its editor is open outside the current filter/page." : "Note duplicated.", NotebookNotificationKind.Success);
        NoteCreated?.Invoke(this, EventArgs.Empty);
    });
    private Task DeleteNoteAsync() => RunNoteOperationAsync(async ct =>
    {
        var id = Note?.Id ?? _unavailable?.Id; var version = Note?.Version ?? _unavailable?.Version;
        if (id is null || version is null || LifecycleState != NotebookNoteState.Trashed) return;
        var title = Note?.Title ?? _unavailable!.Title;
        if (!_dialogs.Confirm(new("Delete note permanently?",
            $"Permanently delete “{title}”? This removes this note and all its recovery drafts. It cannot be undone. Linked Trades, Trading Setups and Trade screenshots are preserved.",
            "Delete permanently", isDestructive: true))) return;
        ct.ThrowIfCancellationRequested();
        var result = await Task.Run(() => _service.ExecuteAsync(new DeleteNotebookNote(id.Value, version.Value, true), ct), ct);
        if (!_active || _disposed || (Note?.Id ?? _unavailable?.Id) != id) return;
        if (!PublishWrite(result.Status, "")) return;
        if (result.DeletedNoteId != id) { ReportMessage("Deletion could not be verified. Refresh before retrying.", NotebookNotificationKind.Error); return; }
        AcceptNoteDeparture(); ResetLinkPresentation(); _unavailable = null; Note = null; InstallDraft();
        _publishing = true;
        try { SelectedNote = null; foreach (var row in Notes.Where(x => x.Id == id).ToArray()) Notes.Remove(row); }
        finally { _publishing = false; }
        foreach (var draft in RecoveryDrafts.Where(x => x.NoteId == id).ToArray()) RecoveryDrafts.Remove(draft);
        OnPropertyChanged(nameof(HasRecovery)); ContentMessage = "Choose a note to read its content.";
        if (!ct.IsCancellationRequested) await ReadPageAsync(false, null, ct);
        if (Current(ct) && Note is null) ReportMessage(result.CleanupPending ? "Note permanently deleted. Some original files are retained pending safe cleanup." : "Note permanently deleted.", result.CleanupPending ? NotebookNotificationKind.Warning : NotebookNotificationKind.Success);
    });
}
