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
    private LinkedNavigationRequest? _linkedLeaveConsent;
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand DiscardCommand { get; }
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
    public bool IsDirty => _detachedRecovery || Note is not null && (_draftError.Length != 0 || Signature(_draftTitle, _draftDocument) != _savedSignature);
    public bool HasDraft => Note is not null || _detachedRecovery;
    public bool HasEditorContext => HasDraft || _unavailable is not null || SelectedNote is not null;
    public string CompactSaveState => IsSaving ? "Saving…" : _recoveryConflict ? "Conflict" :
        _detachedRecovery ? "Recovered draft" : _autosavePaused ? "Save paused" :
        IsDirty ? "Unsaved changes" : Note is null ? "Content unavailable" : "Saved locally";
    // Includes leave-and-return to the same note; saving that note may advance its version.
    public Func<bool> CaptureNoteActionOwnership()
    {
        var activation = _lifetime; var id = Note?.Id ?? _unavailable?.Id;
        long selection = _selectionGeneration;
        return () => _active && !_disposed && ReferenceEquals(activation, _lifetime) &&
            activation?.IsCancellationRequested == false && selection == _selectionGeneration &&
            (Note?.Id ?? _unavailable?.Id) == id && HasEditorContext;
    }
    public bool CanEditDraft => _active && !_disposed && !_detachedRecovery && !_committingNote && Note?.State == NotebookNoteState.Active && (!IsBusy || IsSaving);
    public bool CanEditBody => CanEditDraft;
    public bool BodyReadOnly => !CanEditBody;
    public bool NeedsClosePreparation => IsDirty || IsWriting || _linkOperation || _noteOperation;
    public bool HasLeaveConsent => !IsWriting && (!IsDirty || _acceptedRevision == _draftRevision);
    private static string Signature(string title, NotebookDocument doc) =>
        title + "\0" + JsonSerializer.Serialize(doc.Blocks.Select(Value));
    private static object Value(NotebookNode n) => new
    { n.Kind, n.Text, n.Marks, n.HeadingLevel, n.IsChecked, n.Url, n.ImageId, n.Caption, n.AlternativeText, Children = n.Children.Select(Value).ToArray() };
    private void InstallDraft()
    {
        _knownImages.Clear();
        if (Note is not null) RememberImages(Note);
        StopDebounce(); _recoveryId = null; _checkpointRevision = 0;
        _autosavePaused = false; _recoveryConflict = false; _detachedRecovery = false;
        _draftTitle = Note?.Title ?? _unavailable?.Title ?? "";
        _draftDocument = Note?.Document ?? NotebookDocument.Empty;
        _savedSignature = Signature(_draftTitle, _draftDocument); _draftError = ""; _saveState = "";
        ++_draftRevision; ++_editorRevision; ClearLeaveConsent();
        OnPropertyChanged(nameof(HasDraft)); OnPropertyChanged(nameof(DraftTitle)); OnPropertyChanged(nameof(EditorRevision)); DraftChanged();
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
        _saveState = !IsDirty ? "" : _recoveryConflict ? "Conflict — preserve your draft or discard before refreshing." :
            _autosavePaused && _recovery is not null ? "Unsaved changes — autosave paused; select Save to retry." : "Unsaved changes";
        DraftChanged();
        ScheduleAutosave();
    }
    private void DraftChanged()
    {
        foreach (var name in new[] { nameof(HasEditorContext), nameof(CompactSaveState), nameof(IsDirty), nameof(SaveState), nameof(DraftError), nameof(DraftRevision), nameof(CanEditDraft), nameof(CanEditBody), nameof(BodyReadOnly) })
            OnPropertyChanged(name);
        SaveCommand?.NotifyCanExecuteChanged(); DiscardCommand?.NotifyCanExecuteChanged();
    }
    private void RestoreDraft() => InstallDraft();
    public void ClearLeaveConsent()
    {
        bool resume = _discardConsent && IsDirty;
        _acceptedRevision = -1; _discardConsent = false; _linkedLeaveConsent = null;
        if (resume) ScheduleAutosave(); // A later ancestor veto retained the editor; checkpoint it again.
    }
    public void CompleteLeave()
    {
        if (_discardConsent && HasLeaveConsent) RestoreDraft();
        ClearLeaveConsent();
    }
    public Task<bool> PrepareLeaveAsync(bool linkOperation = false, bool noteOperation = false) =>
        PrepareLeaveCoreAsync(linkOperation, noteOperation, null);
    internal Task<bool> PrepareLinkedLeaveAsync(LinkedNavigationRequest request) => PrepareLeaveCoreAsync(false, false, request);
    internal void ClearLinkedLeaveConsent(LinkedNavigationRequest request)
    {
        if (ReferenceEquals(_linkedLeaveConsent, request)) ClearLeaveConsent();
    }
    private async Task<bool> PrepareLeaveCoreAsync(bool linkOperation, bool noteOperation, LinkedNavigationRequest? request)
    {
        bool Owns() => request is null || request.IsCurrent;
        if (!Owns()) return false;
        if (_noteOperation && !noteOperation) { Message = "Wait for the note operation before leaving."; return false; }
        if (_linkOperation && !linkOperation) { Message = "Wait for the linked-record operation before leaving."; return false; }
        if (_imageOperation || _preparingImage) { Message = "Wait for the image operation before leaving."; return false; }
        if (_recovery is not null)
        {
            StopDebounce();
            await _flushTask;
            if (!Owns()) return false;
            if (_active && !_disposed && IsDirty && !_autosavePaused && !_recoveryConflict && await SaveDraftAsync()) return Owns();
            if (!Owns()) return false;
        }
        if (_disposed || IsWriting) { Message = "Wait for the Notebook change, or cancel it before leaving."; return false; }
        ClearLeaveConsent();
        if (!IsDirty) return true;
        var choice = _interaction?.ConfirmLeave() ?? NotebookLeaveChoice.Cancel;
        if (!Owns()) return false; // Modal consent can pump selection/activation changes.
        if (choice == NotebookLeaveChoice.Cancel) return false;
        if (choice == NotebookLeaveChoice.Save && !await SaveDraftAsync()) return false;
        if (!Owns()) return false;
        if (choice == NotebookLeaveChoice.Discard && !await DiscardCheckpointAsync()) return false;
        if (!Owns()) return false;
        _acceptedRevision = _draftRevision; _discardConsent = choice == NotebookLeaveChoice.Discard; _linkedLeaveConsent = request;
        return !_disposed;
    }
    private async Task GuardedAsync(Func<Task> action, bool linkOperation = false, Func<bool>? owns = null)
    {
        if (_noteOperation) return;
        if (_guarding) return;
        _guarding = true;
        try
        {
            if (owns?.Invoke() == false || !await PrepareLeaveAsync(linkOperation) || owns?.Invoke() == false) return;
            CompleteLeave();
        }
        finally { _guarding = false; ClearLeaveConsent(); }
        if (_active && !_disposed && owns?.Invoke() != false) await action();
    }
    private async Task<bool> SaveCoreAsync(bool checkpointOnly, CancellationToken owner)
    {
        if (!CanMutate || !CanEditDraft) return false;
        if (!IsDirty) return true;
        owner.ThrowIfCancellationRequested();
        var captured = Note!; var revision = _draftRevision; var title = _draftTitle; var document = _draftDocument;
        var activation = _lifetime;
        bool saved = false, committed = false;
        IsSaving = true; DraftChanged();
        try
        {
            await MutateAsync(async ct =>
            {
                if (_draftError.Length != 0) { Message = "Correct or undo the invalid editor content before saving."; return; }
                if (_recovery is not null && !await CheckpointAsync(captured, revision, title, document, ct)) return;
                if (checkpointOnly || _recoveryConflict) return;
                try { NotebookLimits.NoteTitle(title); ValidateDocument(document); }
                catch (ArgumentException) { Message = "Use a nonblank title of at most 200 characters and content within the document limits."; return; }
                ct.ThrowIfCancellationRequested();
                var references = await DraftImagesAsync(captured.Id, document, ct);
                if (references is null) { Message = "An image reference could not be resolved. The draft is retained."; return; }
                NotebookCommand command = references.Value.SequenceEqual(captured.Images)
                    ? new SaveNotebookDraft(captured.Id, captured.Version, title, document, references.Value)
                    : new SaveNotebookImageDraft(captured.Id, captured.Version, title, document, references.Value);
                var result = await Task.Run(() => _service.ExecuteAsync(command, ct), ct);
                // A successful commit is authoritative even if cancellation arrived just after commit.
                if (_disposed || !_active || !ReferenceEquals(activation, _lifetime) || Note?.Id != captured.Id) return;
                if (result.Status != NotebookWriteStatus.Success || result.Note is not { } persisted || persisted.Id != captured.Id ||
                    persisted.Version <= captured.Version || persisted.Title != title.Trim() ||
                    Signature(persisted.Title, persisted.Document) != Signature(title.Trim(), document) || !persisted.Images.SequenceEqual(references.Value))
                {
                    PublishWrite(result.Status == NotebookWriteStatus.Success ? NotebookWriteStatus.StorageFailure : result.Status, "");
                    _saveState = _recovery is null ? "Could not save" : result.Status == NotebookWriteStatus.Conflict
                        ? "Conflict — keep or discard your draft before refreshing." : "Save failed — use Save to retry.";
                    _autosavePaused = true; return;
                }
                committed = true; _autosavePaused = false; RememberImages(persisted); Note = persisted; _savedSignature = Signature(persisted.Title, persisted.Document);
                if (_draftRevision == revision) { _draftTitle = persisted.Title; OnPropertyChanged(nameof(DraftTitle)); }
                _saveState = IsDirty ? "Unsaved changes" : "Saved"; saved = !IsDirty;
                Message = ""; // Do not reload/rebuild the document or clear undo/caret on save.
                await CleanupCheckpointAsync(ct);
                if (!Current(owner) || Note?.Id != captured.Id) return;
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
        finally
        {
            IsSaving = false;
            if (!committed) _autosavePaused = true;
            if (!_disposed)
            {
                if (!committed && !checkpointOnly && !_recoveryConflict && (_recovery is null || !_saveState.StartsWith("Conflict")))
                    _saveState = _recovery is null ? "Could not save" : "Save failed — use Save to retry.";
                DraftChanged();
            }
        }
        return saved && !IsDirty;
    }
}
