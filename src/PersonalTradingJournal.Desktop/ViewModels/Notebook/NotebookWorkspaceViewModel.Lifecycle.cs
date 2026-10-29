using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.ViewModels.Notebook;

public sealed partial class NotebookWorkspaceViewModel
{
    private bool _noteOperation, _committingNote;
    private NotebookUnavailableMetadata? _unavailable;
    private NotebookTemplateDefinition _selectedTemplate = NotebookTemplates.All[0];
    public IReadOnlyList<NotebookTemplateDefinition> Templates => NotebookTemplates.All;
    public NotebookTemplateDefinition SelectedTemplate { get => _selectedTemplate; set { if (value is not null) SetProperty(ref _selectedTemplate, value); } }
    public AsyncRelayCommand CreateTemplateCommand { get; private set; } = null!;
    public AsyncRelayCommand ArchiveCommand { get; private set; } = null!;
    public AsyncRelayCommand UnarchiveCommand { get; private set; } = null!;
    public AsyncRelayCommand TrashCommand { get; private set; } = null!;
    public AsyncRelayCommand RestoreCommand { get; private set; } = null!;
    public AsyncRelayCommand DuplicateCommand { get; private set; } = null!;
    public AsyncRelayCommand DeletePermanentlyCommand { get; private set; } = null!;
    private NotebookNoteState? LifecycleState => Note?.State ?? _unavailable?.State;
    public bool ShowArchive => LifecycleState == NotebookNoteState.Active;
    public bool ShowUnarchive => LifecycleState == NotebookNoteState.Archived;
    public bool ShowTrash => LifecycleState is NotebookNoteState.Active or NotebookNoteState.Archived;
    public bool ShowRestore => LifecycleState == NotebookNoteState.Trashed;
    public bool ShowDuplicate => LifecycleState is NotebookNoteState.Active or NotebookNoteState.Archived;
    public bool ShowDeletePermanently => LifecycleState == NotebookNoteState.Trashed;
    private bool CanStartNoteOperation => _active && !_disposed && !_noteOperation && !_guarding &&
        !_linkOperation && !_imageOperation && !_preparingImage && !_tagOperation && (!IsBusy || IsSaving) && (!IsWriting || IsSaving);
    public bool CanCreateNote => CanStartNoteOperation;
    private void InitializeLifecycle()
    {
        CreateTemplateCommand = new(() => CreateFromTemplateAsync(SelectedTemplate.Id), () => CanStartNoteOperation);
        ArchiveCommand = new(() => ChangeLifecycleAsync(NotebookNoteState.Archived), () => CanStartNoteOperation && ShowArchive);
        UnarchiveCommand = new(() => ChangeLifecycleAsync(NotebookNoteState.Active), () => CanStartNoteOperation && ShowUnarchive);
        TrashCommand = new(() => ChangeLifecycleAsync(NotebookNoteState.Trashed), () => CanStartNoteOperation && ShowTrash);
        RestoreCommand = new(() => ChangeLifecycleAsync(null), () => CanStartNoteOperation && ShowRestore);
        DuplicateCommand = new(DuplicateNoteAsync, () => CanStartNoteOperation && ShowDuplicate && Note is not null);
        DeletePermanentlyCommand = new(DeleteNoteAsync, () => CanStartNoteOperation && ShowDeletePermanently);
    }
    private void NotifyLifecycleCommands()
    {
        OnPropertyChanged(nameof(CanCreateNote));
        foreach (var name in new[] { nameof(LegacyReadOnlyMessage), nameof(ShowArchive), nameof(ShowUnarchive), nameof(ShowTrash), nameof(ShowRestore), nameof(ShowDuplicate), nameof(ShowDeletePermanently), nameof(CanInteract), nameof(CanSelectNote) }) OnPropertyChanged(name);
        foreach (var command in new[] { CreateTemplateCommand, ArchiveCommand, UnarchiveCommand, TrashCommand, RestoreCommand, DuplicateCommand, DeletePermanentlyCommand }) command?.NotifyCanExecuteChanged();
    }
    private Task CreateFromTemplateAsync(NotebookTemplate template)
    {
        var activation = _lifetime; long panel = _panelGeneration;
        var definition = NotebookTemplates.All.Single(x => x.Id == template);
        var folder = Scope?.Scope == NotebookScope.Folder ? Scope.FolderId : null;
        return RunNoteOperationAsync(async ct =>
        {
            var title = template == NotebookTemplate.BlankNote ? "Untitled" : definition.Name;
            var result = await Task.Run(() => _service.ExecuteAsync(new CreateNotebookNote(folder, title, template), ct), ct);
            if (!_active || _disposed || !ReferenceEquals(activation, _lifetime) || !PublishWrite(result.Status, "")) return;
            if (result.Note is not { } saved || saved.State != NotebookNoteState.Active || saved.Version != 1 || saved.Title != title ||
                saved.FolderId != folder || saved.IsPinned || saved.Document.PlainText != NotebookTemplates.Create(template).PlainText ||
                saved.Images.Length != 0 || saved.Links.Length != 0 || saved.TagIds.Length != 0)
            { ReportMessage("Creation could not be verified. Keep your draft and refresh before retrying.", NotebookNotificationKind.Error); return; }
            AcceptNoteDeparture(); ResetLinkPresentation(); _unavailable = null; Note = saved; InstallDraft(); SetEditing(true);
            ContentMessage = saved.Document.Blocks.Count == 0 ? "This note has no content." : "";
            _publishing = true;
            try
            {
                _filterIds.Clear(); PublishTags(); SearchText = ""; Sort = SortOptions[0];
                Scope = Scopes.FirstOrDefault(s => folder is null ? s.Scope == NotebookScope.AllNotes : s.FolderId == folder);
            }
            finally { _publishing = false; }
            bool ownsPanel = panel == _panelGeneration && !ct.IsCancellationRequested;
            if (ownsPanel) Panel = 2;
            long completionPanel = _panelGeneration;
            if (!ct.IsCancellationRequested) await ReadPageAsync(false, saved.Id, ct, keepEditor: true);
            if (!_active || _disposed || !ReferenceEquals(activation, _lifetime) || Note?.Id != saved.Id) return;
            ReportMessage("Note created.", NotebookNotificationKind.Success);
            if (ownsPanel && completionPanel == _panelGeneration && !ct.IsCancellationRequested) NoteCreated?.Invoke(this, EventArgs.Empty);
        });
    }
    private Task ChangeLifecycleAsync(NotebookNoteState? target)
    {
        var id = Note?.Id ?? _unavailable?.Id;
        if (id is null) return Task.CompletedTask;
        var owns = CaptureNoteActionOwnership();
        if (target == NotebookNoteState.Trashed && !_dialogs.Confirm(new("Move note to Trash?",
            "The note becomes read-only. Its content and images are retained; Restore returns it to its previous state and available folder.",
            "Move to Trash", isDestructive: true))) return Task.CompletedTask;
        if (!owns()) return Task.CompletedTask;
        return RunNoteOperationAsync(async ct =>
        {
            if (!owns()) return;
            var before = Note; var metadata = _unavailable;
            long version = before?.Version ?? metadata!.Version;
            NotebookCommand command = target switch
            {
                NotebookNoteState.Archived => new ArchiveNotebookNote(id.Value, version),
                NotebookNoteState.Active => new UnarchiveNotebookNote(id.Value, version),
                NotebookNoteState.Trashed => new TrashNotebookNote(id.Value, version),
                _ => new RestoreNotebookNote(id.Value, version)
            };
            var result = await Task.Run(() => _service.ExecuteAsync(command, ct), ct);
            if (!_active || _disposed || (Note?.Id ?? _unavailable?.Id) != id) return;
            if (!PublishWrite(result.Status, "")) { _autosavePaused = true; return; }
            var saved = result.Note; var unavailable = result.UnavailableMetadata;
            var expectedState = target ?? before?.RestorePoint?.PreviousState;
            if ((saved?.Id ?? unavailable?.Id) != id || (saved?.Version ?? unavailable?.Version) != version + 1 ||
                expectedState is { } expected && (saved?.State ?? unavailable?.State) != expected ||
                before is not null && (saved is null || Signature(saved.Title, saved.Document) != _savedSignature ||
                    !saved.Images.SequenceEqual(before.Images) || !saved.Links.SequenceEqual(before.Links) || !saved.TagIds.SequenceEqual(before.TagIds)))
            { _autosavePaused = true; ReportMessage("The stored lifecycle change could not be verified. Keep your draft and refresh.", NotebookNotificationKind.Error); return; }
            AcceptNoteDeparture(); _unavailable = unavailable; Note = saved;
            // No newer edit is accepted during the canonical transition; explicit discard completes only now.
            DraftChanged();
            if (!ct.IsCancellationRequested) await ReadPageAsync(false, id, ct);
            if (Current(ct)) ReportMessage("Note state updated. Find retained notes in All Notes, or deleted notes in Trash.", NotebookNotificationKind.Success);
        });
    }
    private Task RunNoteOperationAsync(Func<CancellationToken, Task> action)
    {
        if (!CanStartNoteOperation) return Task.CompletedTask;
        _noteOperation = true; NotifyCommands(); StopSearch(); StopLinkTargetRead();
        var activation = _lifetime; var id = Note?.Id ?? _unavailable?.Id;
        return Track(RunAsync());
        async Task RunAsync()
        {
            try
            {
                if (!await PrepareLeaveAsync(noteOperation: true) || !ReferenceEquals(activation, _lifetime) ||
                    !Current(activation?.Token ?? CancellationToken.None) || (Note?.Id ?? _unavailable?.Id) != id) return;
                _committingNote = true; StopDebounce(); NotifyCommands();
                await MutateAsync(action);
            }
            finally
            {
                _committingNote = false; _noteOperation = false; ClearLeaveConsent(); NotifyCommands(); DraftChanged();
                if (IsDirty) ScheduleAutosave();
            }
        }
    }
    private void AcceptNoteDeparture()
    {
        // Unlike shell departure, this acknowledgement occurs while IsWriting is still true.
        // Consume only the exact accepted draft revision, and only after a verified commit.
        if (_discardConsent && _acceptedRevision == _draftRevision) RestoreDraft();
        ClearLeaveConsent();
    }
}
