using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.ViewModels.Notebook;

public sealed partial class NotebookWorkspaceViewModel
{
    private readonly INotebookRecoveryStore? _recovery;
    private readonly TimeProvider _time;
    private CancellationTokenSource? _lifetime, _debounce;
    private Task<bool> _flushTask = Task.FromResult(true);
    private bool _autosavePaused, _recoveryConflict, _detachedRecovery;
    private Guid? _recoveryId, _recoveryCursor;
    private long _checkpointRevision;
    private string _recoveryMessage = "";
    public string RecoveryMessage { get => _recoveryMessage; private set => SetProperty(ref _recoveryMessage, value); }
    public ObservableCollection<NotebookRecoveryItem> RecoveryDrafts { get; } = [];
    public bool HasRecovery => RecoveryDrafts.Count > 0;
    public bool HasMoreRecovery => _recoveryCursor is not null;
    public AsyncRelayCommand<NotebookRecoveryItem> RecoverCommand { get; }
    public AsyncRelayCommand<NotebookRecoveryItem> DeleteRecoveryCommand { get; }
    public AsyncRelayCommand MoreRecoveryCommand { get; }
    public Task AutosaveTask { get; private set; } = Task.CompletedTask;

    private void StopDebounce() => _debounce?.Cancel();
    private void ScheduleAutosave()
    {
        StopDebounce();
        if (_imageOperation || _noteOperation) return;
        if (_recovery is null || !_active || _disposed || (!IsDirty && _recoveryId is null) || _lifetime?.IsCancellationRequested != false) return;
        var owner = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _debounce = owner;
        AutosaveTask = Track(RunAsync());
        async Task RunAsync()
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(750), _time, owner.Token);
                await FlushAsync(manual: false, owner.Token);
            }
            catch (OperationCanceledException) { }
            finally { if (ReferenceEquals(_debounce, owner)) _debounce = null; owner.Dispose(); }
        }
    }
    public Task<bool> SaveDraftAsync()
    {
        if (_imageOperation) return Task.FromResult(false);
        StopDebounce();
        return FlushAsync(manual: true, _lifetime?.Token ?? CancellationToken.None);
    }
    private async Task<bool> FlushAsync(bool manual, CancellationToken owner)
    {
        while (!_flushTask.IsCompleted) await _flushTask;
        if (!Current(owner)) return false;
        // Only one pipeline owns checkpoint + canonical write + acknowledgement/cleanup.
        // Debounce cancellation never cancels an already entered canonical write.
        var lifetime = _lifetime?.Token ?? CancellationToken.None;
        var operation = _flushTask = RunAsync();
        await Track(operation);
        return await operation;
        async Task<bool> RunAsync()
        {
            await Task.Yield();
            bool result = false;
            try
            {
                lifetime.ThrowIfCancellationRequested();
                if (!IsDirty) return await CleanupCheckpointAsync(lifetime);
                result = await SaveCoreAsync(!manual && _autosavePaused, lifetime);
                if (result) _autosavePaused = false;
                return result;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception)
            {
                _autosavePaused = true;
                if (Current(lifetime)) { _saveState = "Save failed — use Save to retry."; DraftChanged(); }
                return false;
            }
            finally
            {
                // Newer edits have their own timer, or need a fresh quiet interval if that
                // timer elapsed while the older write owned the pipeline.
                if (Current(lifetime) && IsDirty && !_autosavePaused && !_recoveryConflict && _draftError.Length == 0)
                    ScheduleAutosave();
            }
        }
    }
    private async Task<bool> CheckpointAsync(NotebookNoteSnapshot note, long revision, string title, NotebookDocument document, CancellationToken ct)
    {
        if (_recovery is null) return true;
        if (_draftError.Length != 0) { _autosavePaused = true; RecoveryMessage = "This editor content cannot be checkpointed. Correct or undo the invalid change."; return false; }
        _recoveryId ??= Guid.NewGuid();
        var captured = new NotebookRecoveryDraft(_recoveryId.Value, note.Id,
            _recoveryConflict ? _recoveredVersion : note.Version, Math.Max(revision, _checkpointRevision + 1),
            title, document, _time.GetUtcNow());
        var status = await Task.Run(() => _recovery.PutAsync(captured, _checkpointRevision, ct), ct);
        if (status == NotebookWriteStatus.Success)
        {
            _checkpointRevision = captured.Revision;
            if (Current(ct)) RecoveryMessage = "Draft checkpointed locally; not yet saved as a note.";
            return true;
        }
        _autosavePaused = true;
        if (Current(ct)) RecoveryMessage = "Recovery checkpoint failed. Input is retained; use Save to retry or keep this editor open.";
        return false;
    }
    private async Task<bool> CleanupCheckpointAsync(CancellationToken ct)
    {
        if (_recovery is null || _recoveryId is not Guid id) return true;
        var revision = _checkpointRevision;
        var status = await Task.Run(() => _recovery.RemoveAsync(id, revision, ct), ct);
        if (status != NotebookWriteStatus.Success)
        {
            if (Current(ct)) RecoveryMessage = "Recovery checkpoint could not be removed. Its stored copy is retained; recovery will compare it with the saved note.";
            return false;
        }
        if (_recoveryId == id && _checkpointRevision == revision) { _recoveryId = null; _checkpointRevision = 0; }
        if (Current(ct))
        {
            var row = RecoveryDrafts.FirstOrDefault(x => x.Id == id);
            if (row is not null) RecoveryDrafts.Remove(row);
            RecoveryMessage = ""; OnPropertyChanged(nameof(HasRecovery));
        }
        return true;
    }
    private async Task<bool> DiscardCheckpointAsync(Func<bool>? owns = null)
    {
        if (owns?.Invoke() == false) return false;
        StopDebounce();
        await _flushTask;
        if (_disposed || !_active || owns?.Invoke() == false) return false;
        // Drain first, then CAS delete. No old callback can resurrect the explicitly
        // discarded checkpoint. A later ancestor veto retains the in-memory editor.
        try
        {
            bool removed = false;
            await MutateAsync(async ct => { if (owns?.Invoke() != false) removed = await CleanupCheckpointAsync(ct); });
            return removed && _active && !_disposed && owns?.Invoke() != false;
        }
        catch (Exception) { RecoveryMessage = "Could not discard the recovery checkpoint. Your draft is retained."; return false; }
    }
    private async Task DiscoverRecoveryAsync(bool more, CancellationToken ct)
    {
        if (_recovery is null) return;
        try
        {
            var page = await Task.Run(() => _recovery.ListAsync(more ? _recoveryCursor : null, ct), ct);
            if (!Current(ct)) return;
            if (!more) RecoveryDrafts.Clear();
            foreach (var row in page.Items) if (RecoveryDrafts.All(x => x.Id != row.Id)) RecoveryDrafts.Add(row);
            _recoveryCursor = page.Next;
            RecoveryMessage = HasRecovery ? "Local recovery drafts are available. Review before restoring; no note has been overwritten." : "";
            OnPropertyChanged(nameof(HasRecovery)); OnPropertyChanged(nameof(HasMoreRecovery)); NotifyCommands();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { if (Current(ct)) RecoveryMessage = "Recovery drafts could not be read. Stored originals are retained; Refresh to retry."; }
    }
    private long _recoveredVersion;
    private Task RecoverAsync(NotebookRecoveryItem? item)
    {
        if (_recovery is null || item is null) return Task.CompletedTask;
        return ReadAsync(async ct =>
        {
            var recovered = await Task.Run(() => _recovery.LoadAsync(item.Id, ct), ct);
            if (!Current(ct)) return;
            if (recovered.Status != NotebookLoadStatus.Success || recovered.Draft is not { } draft)
            { RecoveryMessage = "This recovery draft is unavailable, unreadable, too large or from a newer application. Its stored original has not been changed."; return; }
            var loaded = await Task.Run(() => _service.LoadAsync(draft.NoteId, ct), ct);
            if (!Current(ct)) return;
            ResetLinkPresentation(); Note = loaded.Note; InstallDraft();
            _recoveryId = draft.Id; _checkpointRevision = draft.Revision; _recoveredVersion = draft.ExpectedVersion;
            _publishing = true;
            try { SelectedNote = Notes.FirstOrDefault(x => x.Id == draft.NoteId); } finally { _publishing = false; }
            if (Note is { } saved && Signature(saved.Title, saved.Document) == Signature(draft.Title.Trim(), draft.Document))
            {
                await CleanupCheckpointAsync(ct);
                if (Current(ct)) { _saveState = "Saved"; RecoveryMessage = "This checkpoint is already present in the saved note."; DraftChanged(); }
                return;
            }
            _draftTitle = draft.Title; _draftDocument = draft.Document;
            _draftRevision = Math.Max(_draftRevision + 1, draft.Revision + 1);
            _detachedRecovery = Note is null || Note.State != NotebookNoteState.Active;
            _recoveryConflict = _detachedRecovery || Note!.Version != draft.ExpectedVersion;
            SetEditing(true);
            _autosavePaused = true; // Recovery is explicit; first canonical save requires Save.
            _saveState = _recoveryConflict ? "Recovered draft conflicts with the current note — preserve its text or discard before refreshing."
                : "Recovered unsaved draft — select Save to persist.";
            ContentMessage = _detachedRecovery ? "The original note is unavailable or read-only. Recovery is protected; no note will be recreated." : "";
            LinkText = ""; OnPropertyChanged(nameof(LinkText));
            Panel = 2; ++_editorRevision;
            OnPropertyChanged(nameof(HasDraft)); OnPropertyChanged(nameof(DraftTitle)); OnPropertyChanged(nameof(EditorRevision)); DraftChanged();
            await ResolveCurrentLinksAsync(ct);
        });
    }
    private async Task DeleteRecoveryAsync(NotebookRecoveryItem? item)
    {
        if (_recovery is null || item is null || !_dialogs.Confirm(new("Discard recovery draft?",
            "Permanently discard this local recovery copy? The saved note will not change.", "Discard", isDestructive: true))) return;
        if (_recoveryId == item.Id)
        {
            if (await DiscardCheckpointAsync()) RestoreDraft();
            return;
        }
        await MutateAsync(async ct =>
        {
            var status = await Task.Run(() => _recovery.RemoveAsync(item.Id, item.Revision, ct), ct);
            if (!Current(ct)) return;
            if (status == NotebookWriteStatus.Success) await DiscoverRecoveryAsync(false, ct);
            else RecoveryMessage = "Recovery draft changed or could not be discarded. Refresh to inspect the retained copy.";
        });
    }
}
