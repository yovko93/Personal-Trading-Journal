using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Desktop.Screenshots;
using System.Collections.ObjectModel;

namespace PersonalTradingJournal.Desktop.ViewModels.Setups;

public sealed class PlaybookField(string label) : ObservableObject
{
    private string _text = "";
    private string _error = "";
    private bool _validate;
    public string Label { get; } = label;
    public string Text { get => _text; set { if (SetProperty(ref _text, value) && _validate) Validate(); } }
    public string Error { get => _error; private set { if (SetProperty(ref _error, value)) { OnPropertyChanged(nameof(IsInvalid)); OnPropertyChanged(nameof(HelpText)); } } }
    public bool IsInvalid => Error.Length > 0;
    public string HelpText => IsInvalid ? Error : "Required; up to 4,000 characters. Ctrl+S saves the current Playbook.";
    public void Validate() { _validate = true; Error = string.IsNullOrWhiteSpace(Text) ? "This section is required." : Text.Trim().Length > PlaybookGuidance.MaximumFieldLength ? "Use at most 4,000 characters." : ""; }
    public void ResetValidation() { _validate = false; Error = ""; }
}

/// <summary>Authoring host for the existing Setup catalog; never classifies a Trade.</summary>
public sealed partial class PlaybookViewModel : ObservableObject
{
    private readonly ITradingSetupReader _setups;
    private readonly ITradingSetupPlaybookStore _store;
    private readonly ITradingSetupPlaybookHistoryReader _history;
    private readonly SaveTradingSetupPlaybookUseCase _save;
    private readonly TradingSetupLifecycleUseCase _lifecycle;
    private readonly IDialogService _dialogs;
    private readonly PlaybookSaveDiagnostics? _diagnostics;
    private readonly IPlaybookRevisionDeletion? _deletion;
    private CancellationTokenSource? _read;
    private long _generation;
    private TradingSetupListItem? _selected;
    private TradingSetupPlaybookRevision? _current, _historical;
    private IReadOnlyList<TradingSetupListItem> _catalog = [];
    private IReadOnlyList<TradingSetupPlaybookRevision> _revisions = [];
    private string[] _baseline = [];
    private readonly List<int?> _cursors = [null];
    private int _page;
    private bool _loading, _writing, _editing, _ready, _hasNext;
    private string _message = "Select a Trading Setup. Create Setups in Trading Setups; this page uses the same catalog.";

    public PlaybookViewModel(ITradingSetupReader setups, ITradingSetupPlaybookStore store,
        ITradingSetupPlaybookHistoryReader history, SaveTradingSetupPlaybookUseCase save,
        TradingSetupLifecycleUseCase lifecycle, IDialogService dialogs,
        IPlaybookExampleImages? exampleImages = null, SavePlaybookExamplesUseCase? saveExamples = null,
        PlaybookSaveDiagnostics? diagnostics = null, IPlaybookRevisionDeletion? deletion = null)
    {
        _setups = setups; _store = store; _history = history; _save = save; _lifecycle = lifecycle; _dialogs = dialogs;
        _exampleImages = exampleImages; _saveExamples = saveExamples;
        _diagnostics = diagnostics;
        _deletion = deletion;
        AttachExampleCommand = new AsyncRelayCommand(AttachExampleAsync, () => IsEditing && !IsBusy && EditorExamples.Count < PlaybookGuidance.MaximumExamples && _exampleImages is not null);
        RemoveExampleCommand = new RelayCommand<PlaybookExampleItem>(e => { if (e is not null) EditorExamples.Remove(e); AttachExampleCommand.NotifyCanExecuteChanged(); }, e => IsEditing && !IsBusy && e is not null);
        ViewImageCommand = new AsyncRelayCommand<PlaybookExampleItem>(ViewImageAsync, e => !IsBusy && e?.HasImage == true && _exampleImages is not null);
        Fields = new[] { "Market context", "Entry conditions", "Confirmations", "Invalidation", "Execution guidance" }
            .Select(label => new PlaybookField(label)).ToArray();
        EditorExamples.CollectionChanged += OnExamplesChanged;
        foreach (var field in Fields) field.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(PlaybookField.Error)) return;
            OnPropertyChanged(nameof(ValidationSummary)); OnPropertyChanged(nameof(HasValidationErrors));
            if (!HasValidationErrors && Message == "Save blocked by field validation. Your edits are retained.")
                Message = "Required sections corrected. Save Playbook to update the current guidance.";
        };
        DeleteRevisionCommand = new AsyncRelayCommand<TradingSetupPlaybookRevision>(DeleteRevisionAsync, r => r is not null && !IsBusy && !IsEditing && _deletion is not null);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsWriting);
        EditCommand = new RelayCommand(BeginEdit, () => _ready && !IsBusy && !IsEditing && SelectedSetup?.IsActive == true && !IsHistorical);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsEditing && !IsBusy);
        CancelEditCommand = new RelayCommand(() => { if (TryLeave()) { IsEditing = false; Message = "Editing closed. No changes were saved."; } }, () => IsEditing && !IsWriting);
        CancelOperationCommand = new RelayCommand(() => { _read?.Cancel(); SaveCommand.Cancel(); DeleteRevisionCommand.Cancel(); AttachExampleCommand.Cancel(); ViewImageCommand.Cancel(); ToggleArchiveCommand?.Cancel(); }, () => IsBusy);
        ToggleArchiveCommand = new AsyncRelayCommand(ToggleArchiveAsync, () => SelectedSetup is not null && _ready && !IsBusy && !IsEditing);
        ViewRevisionCommand = new RelayCommand<TradingSetupPlaybookRevision>(ShowRevision, r => r is not null && !IsBusy);
        ShowCurrentCommand = new RelayCommand(() => { _historical = null; Changed(); }, () => IsHistorical && !IsBusy);
        NextPageCommand = new AsyncRelayCommand(() => ChangePageAsync(1), () => _hasNext && !IsBusy);
        PreviousPageCommand = new AsyncRelayCommand(() => ChangePageAsync(-1), () => _page > 0 && !IsBusy);
    }

    public IReadOnlyList<PlaybookField> Fields { get; }
    public bool HasValidationErrors => Fields.Any(f => f.IsInvalid) || EditorExamples.Any(e => e.HasErrors);
    public string ValidationSummary => HasValidationErrors ? "Cannot save. Correct: " + string.Join(", ", Fields.Where(f => f.IsInvalid).Select(f => f.Label)
        .Concat(EditorExamples.Select((e, i) => (e, i)).Where(x => x.e.HasErrors).Select(x => $"Chart example {x.i + 1}"))) + ". Your text and examples are kept." : "";
    public int ValidationAttempt { get; private set; }
    public int HistoricalPreviewRequest { get; private set; }
    public IAsyncRelayCommand<TradingSetupPlaybookRevision> DeleteRevisionCommand { get; }
    public IReadOnlyList<TradingSetupListItem> Setups { get => _catalog; private set => SetProperty(ref _catalog, value); }
    public IReadOnlyList<TradingSetupPlaybookRevision> Revisions { get => _revisions; private set => SetProperty(ref _revisions, value); }
    public TradingSetupListItem? SelectedSetup => _selected;
    public TradingSetupPlaybookRevision? DisplayedRevision => _historical ?? _current;
    public bool IsHistorical => _historical is not null;
    public bool HasGuidance => DisplayedRevision is not null;
    public bool ShowReading => HasGuidance && (!IsEditing || IsHistorical);
    public bool IsEditing { get => _editing; private set { SetProperty(ref _editing, value); if (!value) { EditorExamples.Clear(); foreach (var input in Fields) input.ResetValidation(); } Changed(); } }
    public bool IsLoading { get => _loading; private set { SetProperty(ref _loading, value); Changed(); } }
    public bool IsWriting { get => _writing; private set { SetProperty(ref _writing, value); Changed(); } }
    public bool IsBusy => IsLoading || IsWriting;
    public bool CanSelectSetup => !IsBusy;
    public bool CanEditFields => !IsWriting;
    public bool HasUnsavedChanges => IsEditing && (!Fields.Select(f => f.Text).SequenceEqual(_baseline) ||
        !EditorExamples.Select(e => e.ExampleId).SequenceEqual(_exampleBaseline) || EditorExamples.Any(e => !e.IsUnchanged));
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string SetupState => SelectedSetup is null ? "No Setup selected" : SelectedSetup.IsActive ? "Active Setup" : "Archived Setup — reopen to edit guidance";
    public string ArchiveLabel => SelectedSetup?.IsActive == true ? "Archive Setup" : "Reopen Setup";
    public string EditLabel => _current is null ? "Create guidance" : "Edit guidance";
    public string VersionLabel => IsHistorical ? $"Historical revision {DisplayedRevision!.Revision} · read-only" :
        _current is null ? "No Playbook guidance yet" : $"Current revision {_current.Revision}";
    public string PageLabel => $"Revision page {_page + 1}";
    public Task LoadTask { get; private set; } = Task.CompletedTask;
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand EditCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public IRelayCommand CancelOperationCommand { get; }
    public IAsyncRelayCommand ToggleArchiveCommand { get; }
    public IRelayCommand<TradingSetupPlaybookRevision> ViewRevisionCommand { get; }
    public IRelayCommand ShowCurrentCommand { get; }
    public IAsyncRelayCommand NextPageCommand { get; }
    public IAsyncRelayCommand PreviousPageCommand { get; }

    public Task ActivateAsync() => LoadTask = LoadAsync(refreshCatalog: true);

    // The host restores the ComboBox's selection if this guard vetoes the change.
    public bool TrySelectSetup(TradingSetupListItem? setup)
    {
        if (setup?.Id == _selected?.Id) return true;
        if (!TryLeave()) return false;
        IsEditing = false;
        _selected = setup;
        ResetHistory();
        LoadTask = LoadAsync(refreshCatalog: false);
        return true;
    }

    public bool TryLeave()
    {
        if (IsWriting) { Message = "Wait for the Playbook operation to finish, or cancel it before leaving."; return false; }
        return !HasUnsavedChanges || _dialogs.Confirm(new ConfirmationDialogRequest(
            "Discard Playbook changes?", "The entered guidance has not been saved. Discard it and continue?", "Discard changes", isDestructive: true));
    }

    public void Deactivate()
    {
        _thumbnails?.Cancel();
        ++_generation; _read?.Cancel(); _read = null; IsLoading = false; IsEditing = false;
    }

    private async Task RefreshAsync()
    {
        if (!TryLeave()) return;
        IsEditing = false;
        ResetHistory();
        await (LoadTask = LoadAsync(refreshCatalog: true));
    }

    private void ResetHistory() { _cursors.Clear(); _cursors.Add(null); _page = 0; _historical = null; }

    private async Task LoadAsync(bool refreshCatalog)
    {
        _read?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _read = cancellation;
        long generation = ++_generation;
        var token = cancellation.Token;
        _ready = false; _current = null; _historical = null; Revisions = []; _hasNext = false;
        IsLoading = true; Message = "Loading Playbook…";
        try
        {
            if (refreshCatalog)
            {
                var catalog = await _setups.GetAllAsync(token);
                token.ThrowIfCancellationRequested();
                if (generation != _generation) return;
                Setups = catalog;
                _selected = catalog.FirstOrDefault(s => s.Id == _selected?.Id);
            }
            if (_selected is null) { Message = "Select a Trading Setup. Create Setups in Trading Setups; this page uses the same catalog."; return; }
            Guid setupId = _selected.Id;
            int? cursor = _cursors[_page];
            var current = await _store.GetLatestAsync(setupId, token);
            token.ThrowIfCancellationRequested();
            if (generation != _generation) return;
            var rows = await _history.GetHistoryAsync(setupId, cursor, 11, token);
            token.ThrowIfCancellationRequested();
            if (generation != _generation) return;
            _current = current; Revisions = rows.Take(10).ToArray(); _hasNext = rows.Count > 10; _ready = true;
            Message = current is null ? "This Setup remains usable without guidance. Create its Playbook guidance when ready." : "Guidance is user-authored. A link or snapshot is not proof that a plan was followed.";
        }
        catch (OperationCanceledException) { if (generation == _generation) Message = "Loading cancelled. Refresh to try again."; }
        catch (Exception) { if (generation == _generation) Message = "Playbook could not be loaded. Refresh to try again."; }
        finally
        {
            if (generation == _generation) { _read = null; IsLoading = false; Changed(); }
        }
    }

    private void BeginEdit()
    {
        var g = _current?.Guidance;
        string[] values = g is null ? ["", "", "", "", ""] : [g.MarketContext, g.EntryConditions, g.Confirmations, g.Invalidation, g.ExecutionGuidance];
        for (int i = 0; i < Fields.Count; i++) { Fields[i].ResetValidation(); Fields[i].Text = values[i]; }
        EditorExamples.Clear();
        foreach (var example in g?.Examples ?? []) EditorExamples.Add(new(example));
        _ = LoadReadingThumbnailsAsync(EditorExamples.ToArray());
        _exampleBaseline = EditorExamples.Select(e => e.ExampleId).ToArray();
        _baseline = values; IsEditing = true;
        Message = "Editing current guidance. No changes are saved until Save Playbook.";
    }

    private async Task SaveAsync(CancellationToken token)
    {
        Guid attempt = Guid.NewGuid();
        Guid? setupId = _selected?.Id;
        int expected = _current?.Revision ?? 0;
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        TradingSetupPlaybookRevision? committed = null;
        PlaybookSaveResult outcome = PlaybookSaveResult.Pending;
        void Trace(PlaybookSavePhase phase, PlaybookSaveResult result) => _diagnostics?.Record(new(
            attempt, phase, result, setupId, expected, committed?.Revision, _selected?.Id,
            IsEditing, _ready, System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds));
        Trace(PlaybookSavePhase.Invoked, PlaybookSaveResult.Pending);
        // Snapshot the five fields and owning identity before the first await; editing is disabled during the write.
        foreach (var field in Fields) field.Validate();
        foreach (var example in EditorExamples) example.Validate();
        if (HasValidationErrors) { Message = "Save blocked by field validation. Your edits are retained."; ValidationAttempt++; OnPropertyChanged(nameof(ValidationAttempt)); Trace(PlaybookSavePhase.ValidationRejected, Fields.Any(f => f.IsInvalid) ? PlaybookSaveResult.InvalidFields : PlaybookSaveResult.InvalidExamples); return; }
        IsWriting = true; Message = "Saving current Playbook…";
        try
        {
            var guidance = new PlaybookGuidance(Fields[0].Text, Fields[1].Text, Fields[2].Text, Fields[3].Text, Fields[4].Text, EditorExamples.Select(e => e.ToDomain()));
            var command = new SaveTradingSetupPlaybookCommand(setupId!.Value, expected, guidance);
            var pending = EditorExamples.Where(e => e.PendingBytes is not null).Select(e => new NewPlaybookImage(e.ExampleId, e.FileName, e.PendingBytes!)).ToArray();
            if (pending.Length > 0 && _saveExamples is null) throw new InvalidOperationException("Image saving is unavailable.");
            Trace(PlaybookSavePhase.WriteStarted, PlaybookSaveResult.Pending);
            var saved = committed = _saveExamples is null ? await _save.ExecuteAsync(command, token) : await _saveExamples.ExecuteAsync(command, pending, token);
            Trace(PlaybookSavePhase.Committed, PlaybookSaveResult.Pending);
            // A write result is not readback. Verify the current definition and token independently;
            // cancellation after commit must not be described as a rolled-back save.
            var readback = await _store.GetLatestAsync(saved.TradingSetupId, CancellationToken.None);
            if (readback is null || !SameRevision(saved, readback))
            {
                Trace(PlaybookSavePhase.Readback, PlaybookSaveResult.ReadbackMissing);
                outcome = PlaybookSaveResult.ReadbackMissing;
                Message = "The write reported a commit, but its current definition could not be verified. Your edits are retained. Copy them if needed, then Refresh before saving again.";
                return;
            }
            Trace(PlaybookSavePhase.Readback, PlaybookSaveResult.Verified);
            ResetHistory();
            await LoadAsync(refreshCatalog: true);
            bool loaded = _ready && _selected?.Id == saved.TradingSetupId && _current?.Revision >= saved.Revision;
            outcome = loaded ? PlaybookSaveResult.Verified : PlaybookSaveResult.ReloadIncomplete;
            Trace(PlaybookSavePhase.Reloaded, loaded ? PlaybookSaveResult.Verified : PlaybookSaveResult.ReloadIncomplete);
            if (loaded) { IsEditing = false; Message = "Playbook saved and verified."; }
            else
            {
                _ready = false; _current = readback; Changed();
                Message = "Playbook verified in storage, but the refreshed view could not be loaded. Your edits are retained; Refresh before editing again.";
            }
        }
        catch (Exception) when (committed is not null)
        {
            outcome = PlaybookSaveResult.ReadbackFailed;
            Trace(PlaybookSavePhase.Readback, PlaybookSaveResult.ReadbackFailed);
            Message = "The write reported a commit, but verification or display failed. Your edits are retained. Copy them if needed, then Refresh before saving again.";
        }
        catch (OperationCanceledException) { outcome = PlaybookSaveResult.Cancelled; Message = "Save cancelled. Your entered text is retained."; }
        catch (ArgumentException) { outcome = PlaybookSaveResult.Invalid; Message = "Check all five sections: meaningful text, at most 4,000 characters each. Your text is retained."; }
        catch (InvalidOperationException) { outcome = PlaybookSaveResult.Conflict; Message = "The Setup or Playbook changed or was archived. Your text is retained. Copy it if needed, then Refresh to load current guidance."; }
        catch (KeyNotFoundException) { outcome = PlaybookSaveResult.Unavailable; Message = "The Setup is unavailable. Your text is retained; copy it before refreshing."; }
        catch (PlaybookImageCleanupException) { Message = "Save failed and unused image cleanup could not finish. Your edits are retained; an unused file may remain. Try Save again."; }
        catch (PersonalTradingJournal.Application.Screenshots.ScreenshotStagingCleanupException) { Message = "Image saving and partial-file cleanup failed. Your edits are retained; an unused partial file may remain. Check available storage and try Save again."; }
        catch (Exception e) when (e is System.IO.IOException or System.IO.InvalidDataException) { Message = "An example image is missing, changed, unreadable, or could not be stored. Your edits are retained. Restore the image or remove that example from the current guidance and try again."; }
        catch (Exception) { Message = "Playbook could not be saved. Your text is retained. Try Save again or Refresh."; }
        finally { IsWriting = false; Trace(PlaybookSavePhase.Finished, outcome == PlaybookSaveResult.Pending ? PlaybookSaveResult.StorageFailure : outcome); }
    }

    private static bool SameRevision(TradingSetupPlaybookRevision expected, TradingSetupPlaybookRevision actual) =>
        System.Text.Json.JsonSerializer.Serialize(expected) == System.Text.Json.JsonSerializer.Serialize(actual);

    private async Task DeleteRevisionAsync(TradingSetupPlaybookRevision? revision, CancellationToken token)
    {
        if (revision is null || _deletion is null || IsBusy || IsEditing || revision.TradingSetupId != _selected?.Id || !Revisions.Contains(revision)) return;
        IsWriting = true;
        try
        {
            var check = await _deletion.CheckAsync(revision.TradingSetupId, revision.Revision, token);
            if (check.Status != PlaybookRevisionDeletionStatus.Eligible) { Message = DeletionMessage(check.Status); return; }
            if (!_dialogs.Confirm(new ConfirmationDialogRequest("Delete Playbook revision?",
                $"Permanently delete revision {revision.Revision} for “{revision.SetupName}”? Current guidance, Trades, frozen contexts, other revisions, saved analyses and image files are retained. This cannot be undone.", "Delete revision", isDestructive: true)))
            { Message = "Deletion cancelled. No revision was changed."; return; }
            var result = await _deletion.DeleteAsync(revision.TradingSetupId, revision.Revision, check.CurrentRevision, token);
            if (result != PlaybookRevisionDeletionStatus.Deleted) { Message = DeletionMessage(result); return; }
            ResetHistory(); await LoadAsync(false);
            Message = _ready ? $"Revision {revision.Revision} deleted. Current guidance and images retained." : "Revision deleted, but the list could not be refreshed. Refresh to reload.";
        }
        catch (OperationCanceledException) { Message = "Revision deletion cancelled. Refresh to check the list."; }
        catch (Exception) { Message = "Revision could not be deleted. Nothing was reported as deleted. Refresh and try again."; }
        finally { IsWriting = false; }
    }
    private static string DeletionMessage(PlaybookRevisionDeletionStatus status) => status switch
    {
        PlaybookRevisionDeletionStatus.CurrentRevision => "The current revision cannot be deleted. Save new guidance first.",
        PlaybookRevisionDeletionStatus.Referenced => "This revision is retained by a Trade capture or association history and cannot be deleted.",
        PlaybookRevisionDeletionStatus.Missing => "This revision is no longer available. Refresh the list.",
        _ => "The Playbook changed since confirmation. Refresh the list before deleting."
    };

    private async Task ToggleArchiveAsync(CancellationToken token)
    {
        if (!_dialogs.Confirm(new ConfirmationDialogRequest(ArchiveLabel + "?",
            $"{ArchiveLabel} “{_selected!.Name}”? Existing classifications, revisions and frozen Trade context are retained.", ArchiveLabel))) return;
        IsWriting = true;
        try
        {
            await _lifecycle.ExecuteAsync(new(_selected.Id, !_selected.IsActive), token);
            ResetHistory(); await LoadAsync(refreshCatalog: true);
        }
        catch (OperationCanceledException) { Message = "Setup operation cancelled. Refresh to check its current state."; }
        catch (Exception) { Message = "Setup state could not be changed. Refresh to check its current state."; }
        finally { IsWriting = false; }
    }

    private void ShowRevision(TradingSetupPlaybookRevision? revision)
    {
        if (IsBusy || revision is null || revision.TradingSetupId != _selected?.Id || !Revisions.Contains(revision)) return;
        // Read-only preview is independent of the pending editor. Never reload its fields or baseline.
        _historical = revision; Changed();
        HistoricalPreviewRequest++; OnPropertyChanged(nameof(HistoricalPreviewRequest));
    }

    private async Task ChangePageAsync(int delta)
    {
        if (!TryLeave()) return;
        IsEditing = false;
        if (delta > 0 && _cursors.Count == _page + 1) _cursors.Add(Revisions[^1].Revision);
        _page += delta;
        await (LoadTask = LoadAsync(refreshCatalog: false));
    }

    private void Changed()
    {
        UpdateReadingExamples();
        foreach (string name in new[] { nameof(SelectedSetup), nameof(DisplayedRevision), nameof(IsHistorical), nameof(HasGuidance), nameof(ShowReading), nameof(IsBusy), nameof(CanSelectSetup), nameof(CanEditFields), nameof(SetupState), nameof(ArchiveLabel), nameof(EditLabel), nameof(VersionLabel), nameof(PageLabel) }) OnPropertyChanged(name);
        RefreshCommand?.NotifyCanExecuteChanged(); EditCommand?.NotifyCanExecuteChanged(); SaveCommand?.NotifyCanExecuteChanged();
        CancelEditCommand?.NotifyCanExecuteChanged(); CancelOperationCommand?.NotifyCanExecuteChanged(); ToggleArchiveCommand?.NotifyCanExecuteChanged();
        ViewRevisionCommand?.NotifyCanExecuteChanged(); ShowCurrentCommand?.NotifyCanExecuteChanged(); NextPageCommand?.NotifyCanExecuteChanged(); PreviousPageCommand?.NotifyCanExecuteChanged();
        DeleteRevisionCommand?.NotifyCanExecuteChanged();
        AttachExampleCommand?.NotifyCanExecuteChanged(); RemoveExampleCommand?.NotifyCanExecuteChanged(); ViewImageCommand?.NotifyCanExecuteChanged();
    }
}
