using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.ViewModels.Notebook;

public enum NotebookLeaveChoice { Cancel, Save, Discard }
public interface INotebookInteraction { NotebookLeaveChoice ConfirmLeave(); }

public sealed partial class NotebookWorkspaceViewModel
{
    private readonly INotebookInteraction? _interaction;
    private readonly INotebookContentCodec? _codec;
    private string _draftTitle = "", _savedSignature = "", _draftError = "", _saveState = "";
    private NotebookDocument _draftDocument = NotebookDocument.Empty;
    private long _draftRevision, _acceptedRevision = -1, _editorRevision;
    private bool _guarding, _discardConsent;
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand DiscardCommand { get; }
    public string DraftTitle
    {
        get => _draftTitle;
        set { if (CanEditDraft && SetProperty(ref _draftTitle, value)) Edited(); }
    }
    public NotebookDocument DraftDocument => _draftDocument;
    public long DraftRevision => _draftRevision;
    public long EditorRevision => _editorRevision;
    public string DraftError => _draftError;
    public string SaveState => IsSaving ? "Saving…" : _saveState;
    public bool IsSaving { get; private set; }
    public bool IsDirty => Note is not null && (_draftError.Length != 0 || Signature(_draftTitle, _draftDocument) != _savedSignature);
    public bool CanEditDraft => _active && !_disposed && Note?.State == NotebookNoteState.Active && (!IsBusy || IsSaving);
    // Existing image nodes are displayed intact in a protected body. Title edits remain available.
    // File/image editing is deliberately not approximated by editable placeholder text.
    public bool CanEditBody => CanEditDraft && Note!.Document.ImageNodes.Count == 0;
    public bool BodyReadOnly => !CanEditBody;
    public bool NeedsClosePreparation => IsDirty || IsWriting;
    public bool HasLeaveConsent => !IsWriting && (!IsDirty || _acceptedRevision == _draftRevision);
    private static string Signature(string title, NotebookDocument doc) =>
        title + "\0" + JsonSerializer.Serialize(doc.Blocks.Select(Value));
    private static object Value(NotebookNode n) => new
    { n.Kind, n.Text, n.Marks, n.HeadingLevel, n.IsChecked, n.Url, n.ImageId, n.Caption, n.AlternativeText, Children = n.Children.Select(Value).ToArray() };
    private void InstallDraft()
    {
        _draftTitle = Note?.Title ?? "";
        _draftDocument = Note?.Document ?? NotebookDocument.Empty;
        _savedSignature = Signature(_draftTitle, _draftDocument); _draftError = ""; _saveState = "";
        ++_draftRevision; ++_editorRevision; ClearLeaveConsent();
        OnPropertyChanged(nameof(DraftTitle)); OnPropertyChanged(nameof(EditorRevision)); DraftChanged();
    }
    public void SetDraftDocument(NotebookDocument document)
    {
        if (!CanEditBody) return;
        _draftDocument = document; _draftError = ""; Edited();
    }
    public void SetDraftError(string error)
    {
        if (!CanEditBody) return;
        _draftError = error; Edited();
    }
    public void ValidateDocument(NotebookDocument document) => _codec?.Encode(document);
    private void Edited()
    {
        ++_draftRevision; ClearLeaveConsent();
        _saveState = IsDirty ? "Unsaved changes" : "";
        DraftChanged();
    }
    private void DraftChanged()
    {
        foreach (var name in new[] { nameof(IsDirty), nameof(SaveState), nameof(DraftError), nameof(DraftRevision), nameof(CanEditDraft), nameof(CanEditBody), nameof(BodyReadOnly) })
            OnPropertyChanged(name);
        SaveCommand?.NotifyCanExecuteChanged(); DiscardCommand?.NotifyCanExecuteChanged();
    }
    private void RestoreDraft() => InstallDraft();
    public void ClearLeaveConsent() { _acceptedRevision = -1; _discardConsent = false; }
    public void CompleteLeave()
    {
        if (_discardConsent && HasLeaveConsent) RestoreDraft();
        ClearLeaveConsent();
    }
    public async Task<bool> PrepareLeaveAsync()
    {
        if (_disposed || IsWriting) { Message = "Wait for the Notebook change, or cancel it before leaving."; return false; }
        ClearLeaveConsent();
        if (!IsDirty) return true;
        var choice = _interaction?.ConfirmLeave() ?? NotebookLeaveChoice.Cancel;
        if (choice == NotebookLeaveChoice.Cancel) return false;
        if (choice == NotebookLeaveChoice.Save && !await SaveDraftAsync()) return false;
        _acceptedRevision = _draftRevision; _discardConsent = choice == NotebookLeaveChoice.Discard;
        return !_disposed;
    }
    private async Task GuardedAsync(Func<Task> action)
    {
        if (_guarding) return;
        _guarding = true;
        try
        {
            if (!await PrepareLeaveAsync()) return;
            CompleteLeave();
        }
        finally { _guarding = false; ClearLeaveConsent(); }
        if (_active && !_disposed) await action();
    }
    public async Task<bool> SaveDraftAsync()
    {
        if (!CanMutate || !CanEditDraft) return false;
        if (!IsDirty) return true;
        if (_draftError.Length != 0) { _saveState = "Could not save"; DraftChanged(); return false; }
        try { NotebookLimits.NoteTitle(_draftTitle); ValidateDocument(_draftDocument); }
        catch (ArgumentException) { _saveState = "Could not save"; Message = "Use a nonblank title of at most 200 characters and content within the document limits."; DraftChanged(); return false; }
        var captured = Note!; var revision = _draftRevision; var title = _draftTitle; var document = _draftDocument;
        bool saved = false, committed = false;
        IsSaving = true; DraftChanged();
        try
        {
            await MutateAsync(async ct =>
            {
                var result = await Task.Run(() => _service.ExecuteAsync(new SaveNotebookDraft(captured.Id, captured.Version, title, document, captured.Images), ct), ct);
                // A successful commit is authoritative even if cancellation arrived just after commit.
                if (_disposed || !_active || Note?.Id != captured.Id) return;
                if (result.Status != NotebookWriteStatus.Success || result.Note is not { } persisted || persisted.Id != captured.Id ||
                    persisted.Version <= captured.Version || persisted.Title != title.Trim() ||
                    Signature(persisted.Title, persisted.Document) != Signature(title.Trim(), document))
                {
                    PublishWrite(result.Status == NotebookWriteStatus.Success ? NotebookWriteStatus.StorageFailure : result.Status, "");
                    _saveState = "Could not save"; return;
                }
                committed = true; Note = persisted; _savedSignature = Signature(persisted.Title, persisted.Document);
                if (_draftRevision == revision) { _draftTitle = persisted.Title; OnPropertyChanged(nameof(DraftTitle)); }
                _saveState = IsDirty ? "Unsaved changes" : "Saved"; saved = !IsDirty;
                Message = ""; // Do not reload/rebuild the document or clear undo/caret on save.
                int index = Notes.ToList().FindIndex(n => n.Id == persisted.Id);
                if (index >= 0)
                {
                    var old = Notes[index].Item;
                    var row = new NotebookRow(old with { Title = persisted.Title, Preview = persisted.Document.PlainText[..Math.Min(200, persisted.Document.PlainText.Length)], Version = persisted.Version, UpdatedAtUtc = persisted.UpdatedAtUtc });
                    _publishing = true;
                    try { Notes[index] = row; SelectedNote = row; } finally { _publishing = false; }
                }
                // Refresh saved search membership/counts without re-reading/replacing the selected editor.
                if (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var page = await Task.Run(() => _service.ListAsync(_query, ct), ct);
                        if (!Current(ct) || Note?.Id != persisted.Id) return;
                        _publishing = true;
                        try
                        {
                            Notes.Clear(); foreach (var item in page.Items) Notes.Add(new(item));
                            SelectedNote = Notes.FirstOrDefault(n => n.Id == persisted.Id);
                            _total = page.FilteredCount; _cursor = page.NextCursor; PublishCatalog(page.Summary);
                        }
                        finally { _publishing = false; }
                        OnPropertyChanged(nameof(ListSummary));
                        if (SelectedNote is null) Message = "Saved note is outside this list page/filter. Its editor remains open.";
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception) { Message = "Saved. The list could not refresh; select Refresh to retry."; }
                }
            });
        }
        finally { IsSaving = false; if (!_disposed) { if (!committed) _saveState = "Could not save"; DraftChanged(); } }
        return saved && !IsDirty;
    }
}
