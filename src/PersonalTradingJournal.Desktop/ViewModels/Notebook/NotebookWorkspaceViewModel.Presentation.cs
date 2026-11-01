using CommunityToolkit.Mvvm.Input;

namespace PersonalTradingJournal.Desktop.ViewModels.Notebook;

public sealed partial class NotebookWorkspaceViewModel
{
    private bool _isEditing;
    public bool IsEditing => _isEditing;
    // Draft capabilities remain available to save/recovery coordination. UI input additionally
    // requires explicit edit intent; changing that intent never installs a new document.
    public bool CanUseEditor => IsEditing && CanEditDraft;
    public bool EditorReadOnly => !CanUseEditor;
    public string EditModeLabel => IsEditing ? "Done" : "Edit";
    public AsyncRelayCommand EditModeCommand { get; private set; } = null!;

    private void InitializePresentation() => EditModeCommand = new(ToggleEditModeAsync,
        () => CanEditDraft && !_imageOperation && !_preparingImage && !_tagOperation && !_linkOperation && !_noteOperation);

    private void SetEditing(bool editing)
    {
        if (!SetProperty(ref _isEditing, editing, nameof(IsEditing))) return;
        NotifyPresentation();
    }

    private void NotifyPresentation()
    {
        OnPropertyChanged(nameof(CanUseEditor));
        OnPropertyChanged(nameof(EditorReadOnly));
        OnPropertyChanged(nameof(EditModeLabel));
        EditModeCommand?.NotifyCanExecuteChanged();
    }

    private async Task ToggleEditModeAsync()
    {
        if (!EditModeCommand.CanExecute(null)) return;
        if (!IsEditing) { SetEditing(true); return; }
        var owns = CaptureNoteActionOwnership();
        long revision = DraftRevision;
        // Flush uses the existing serialized pipeline. A checkpoint alone, newer input,
        // a conflict, or departure must never acknowledge Done for another revision/note.
        if (await SaveDraftAsync() && owns() && revision == DraftRevision && !IsDirty &&
            !IsSaving && !_autosavePaused && !_recoveryConflict && !_detachedRecovery)
            SetEditing(false);
    }
}
