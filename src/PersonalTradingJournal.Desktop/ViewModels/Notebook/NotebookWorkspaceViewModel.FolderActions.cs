using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.ViewModels.Notebook;

public sealed partial class NotebookWorkspaceViewModel
{
    private NotebookFolderOption? _renameFolder;
    private bool _folderRenameOpen, _folderActionPending;
    private string _folderRenameName = "", _folderActionMessage = "";
    private long _folderActionGeneration;
    private NotebookFolderOption? SelectedFolder => Scope is { Scope: NotebookScope.Folder, FolderId: Guid id }
        ? Folders.FirstOrDefault(f => f.Id == id) : null;
    public bool HasFolderActions => SelectedFolder is not null && ScopeMatchesList;
    public bool CanChangeFolder => HasFolderActions && CanMutate && !IsFolderActionPending;
    public bool IsFolderRenameOpen { get => _folderRenameOpen; private set { SetProperty(ref _folderRenameOpen, value); RenameFolderCommand.NotifyCanExecuteChanged(); } }
    public bool IsFolderActionPending { get => _folderActionPending; private set { SetProperty(ref _folderActionPending, value); NotifyCommands(); } }
    public string FolderRenameName { get => _folderRenameName; set => SetProperty(ref _folderRenameName, value); }
    public string FolderActionMessage { get => _folderActionMessage; private set => SetProperty(ref _folderActionMessage, value); }

    public bool BeginFolderRename()
    {
        if (!CanChangeFolder) return false;
        ++_folderActionGeneration; _renameFolder = SelectedFolder;
        FolderRenameName = _renameFolder!.Name; FolderActionMessage = ""; IsFolderRenameOpen = true;
        return true;
    }
    public void DismissFolderRename()
    {
        ++_folderActionGeneration; IsFolderRenameOpen = false; _renameFolder = null;
        if (IsFolderActionPending) Message = "Folder input closed. A submitted change may already be saved; Refresh to check.";
    }
    private Task RenameFolderAsync()
    {
        if (!CanChangeFolder || !IsFolderRenameOpen || _renameFolder is not { } folder) return Task.CompletedTask;
        string name;
        try { name = NotebookLimits.Name(FolderRenameName, NotebookLimits.FolderName); }
        catch (ArgumentException) { FolderActionMessage = "Enter a folder name of 1–128 characters."; return Task.CompletedTask; }
        return ChangeFolderAsync(folder, new RenameNotebookFolder(folder.Id!.Value, folder.Version, name));
    }
    private Task DeleteFolderAsync()
    {
        if (!CanChangeFolder || SelectedFolder is not { } folder) return Task.CompletedTask;
        return ChangeFolderAsync(folder, new DeleteNotebookFolder(folder.Id!.Value, folder.Version, true));
    }
    private async Task ChangeFolderAsync(NotebookFolderOption folder, NotebookCatalogCommand command)
    {
        long request = _folderActionGeneration, selection = _selectionGeneration, read = _generation, panel = _panelGeneration;
        var activation = _lifetime;
        bool Owns() => _active && !_disposed && ReferenceEquals(activation, _lifetime) && activation?.IsCancellationRequested == false &&
            request == _folderActionGeneration && selection == _selectionGeneration && read == _generation && panel == _panelGeneration && Scope?.FolderId == folder.Id;
        IsFolderActionPending = true; FolderActionMessage = "";
        bool completed = false;
        try
        {
            // Confirm before draft consent: declining deletion must not save/discard the note.
            if (command is DeleteNotebookFolder && (!_dialogs.Confirm(new("Delete folder?",
                $"Delete “{folder.Name}”? Its notes will move to Unfiled. No notes or their content will be deleted.", "Delete folder", isDestructive: true)) || !Owns())) return;
            await GuardedAsync(() => MutateAsync(async ct =>
            {
                if (!Owns()) return;
                var result = await Task.Run(() => _service.ExecuteCatalogAsync(command, ct), ct);
                if (!Current(ct) || !Owns()) return;
                if (!PublishWrite(result.Status, result.Issues.IsDefault ? "" : string.Join(" ", result.Issues.Select(i => i.Message))))
                { FolderActionMessage = Message; return; }
                _publishing = true;
                try
                {
                    if (command is RenameNotebookFolder && result.Item is { } item)
                    { AddFolder(new(item, Scope!.Count)); Scope = Scopes.Single(s => s.FolderId == item.Id); }
                    else if (command is DeleteNotebookFolder && result.DeletedItemId == folder.Id)
                    { Scope = Scopes.First(s => s.Scope == NotebookScope.Unfiled); }
                    else { FolderActionMessage = "Folder change could not be confirmed. Refresh before trying again."; return; }
                }
                finally { _publishing = false; }
                await ReadPageAsync(false, SelectedNote?.Id, ct);
                if (!Current(ct) || !ReferenceEquals(activation, _lifetime)) return;
                completed = true; IsFolderRenameOpen = false; _renameFolder = null; FolderRenameName = "";
                Message = command is DeleteNotebookFolder ? "Folder deleted. Notes retained in Unfiled." : "Folder renamed.";
            }), owns: Owns);
            if (!completed && Owns() && string.IsNullOrEmpty(FolderActionMessage))
                FolderActionMessage = string.IsNullOrEmpty(Message) ? "Folder change cancelled. Your input is retained." : Message;
        }
        finally { IsFolderActionPending = false; }
    }
}
