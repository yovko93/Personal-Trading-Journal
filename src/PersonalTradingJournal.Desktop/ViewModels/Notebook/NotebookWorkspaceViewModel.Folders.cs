using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.ViewModels.Notebook;

public sealed partial class NotebookWorkspaceViewModel
{
    private bool _folderCreationOpen, _folderCreationPending;
    private string _folderCreationMessage = "";
    private long _folderCreationGeneration, _panelGeneration;
    public bool IsFolderCreationOpen { get => _folderCreationOpen; private set => SetProperty(ref _folderCreationOpen, value); }
    public bool IsFolderCreationPending { get => _folderCreationPending; private set { SetProperty(ref _folderCreationPending, value); CreateFolderCommand.NotifyCanExecuteChanged(); } }
    public string FolderCreationMessage { get => _folderCreationMessage; private set => SetProperty(ref _folderCreationMessage, value); }

    public bool BeginFolderCreation()
    {
        if (!CanMutate || IsFolderCreationPending) return false;
        ++_folderCreationGeneration; FolderCreationMessage = ""; IsFolderCreationOpen = true;
        return true;
    }
    public void DismissFolderCreation()
    {
        ++_folderCreationGeneration; IsFolderCreationOpen = false;
        // Dismissal is not a database rollback. A service call already in flight may commit.
        if (IsFolderCreationPending) Message = "Folder input closed. Creation may already be saved; check FOLDERS or Refresh.";
    }
    private Task SelectScopeAsync(NotebookScopeOption value, Func<bool>? owns = null) => GuardedAsync(async () =>
    {
        long panel = _panelGeneration;
        SetProperty(ref _scope, value, nameof(Scope)); OnPropertyChanged(nameof(ScopeName));
        if (value.FolderId is Guid id) ManagedFolder = Folders.FirstOrDefault(f => f.Id == id);
        await ReloadAsync(preserveSelection: false);
        if (owns is null) Panel = 1;
        else if (_active && !_disposed && panel == _panelGeneration && Scope?.Scope == value.Scope && Scope.FolderId == value.FolderId)
        {
            // This is the requested transition, not newer user navigation dismissing the input.
            if (SetProperty(ref _panel, 1, nameof(Panel))) ++_panelGeneration;
        }
    }, owns: owns);

    private async Task CreateFolderAsync()
    {
        if (!CanMutate || IsFolderCreationPending) return;
        string name;
        try { name = NotebookLimits.Name(FolderName, NotebookLimits.FolderName); }
        catch (ArgumentException) { FolderCreationMessage = "Enter a folder name of 1–128 characters."; Message = FolderCreationMessage; return; }
        long request = _folderCreationGeneration, selection = _selectionGeneration, read = _generation;
        var activation = _lifetime; var scope = Scope;
        bool Owns() => _active && !_disposed && ReferenceEquals(activation, _lifetime) && activation?.IsCancellationRequested == false &&
            request == _folderCreationGeneration && selection == _selectionGeneration && read == _generation &&
            Scope?.Scope == scope?.Scope && Scope?.FolderId == scope?.FolderId;
        IsFolderCreationPending = true; FolderCreationMessage = "";
        bool entered = false;
        NotebookNamedItem? created = null;
        try
        {
            await GuardedAsync(async () =>
            {
                entered = true;
                await MutateAsync(async ct =>
                {
                    if (!Owns()) return;
                    var result = await Task.Run(() => _service.ExecuteCatalogAsync(new CreateNotebookFolder(name), ct), ct);
                    if (!Current(ct) || !ReferenceEquals(activation, _lifetime)) return;
                    if (!PublishWrite(result.Status, result.Issues.IsDefault ? "" : string.Join(" ", result.Issues.Select(i => i.Message)))) return;
                    created = result.Item;
                    if (created is null) { Message = "Folder save could not be confirmed. Refresh before trying again."; return; }
                    AddFolder(new(created, 0));
                    Message = "Folder created. It is available under FOLDERS.";
                });
                if (created is null || !Owns()) return;
                // Reuse the normal guarded scope transition, revalidating this request after consent.
                LoadTask = SelectScopeAsync(Scopes.Single(s => s.FolderId == created.Id), Owns);
                await LoadTask;
                if (request == _folderCreationGeneration && ReferenceEquals(activation, _lifetime) && _active && !_disposed)
                {
                    FolderName = ""; IsFolderCreationOpen = false;
                    Message = Scope?.FolderId == created.Id ? "Folder created." : "Folder created. Current note retained.";
                }
            }, owns: Owns);
            if (created is null && Owns()) FolderCreationMessage = entered ? Message : "Creation cancelled. Your input is retained.";
        }
        finally { IsFolderCreationPending = false; }
    }
}
