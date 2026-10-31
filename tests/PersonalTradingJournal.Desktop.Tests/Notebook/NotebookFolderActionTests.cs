using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookFolderActionTests
{
    private static async Task<NotebookWorkspaceViewModel> Open(NotebookFake data, IDialogService? dialogs = null, NotebookChoices? choices = null)
    {
        data.Folders.Add(new(Guid.NewGuid(), "Research", 1)); data.Folders.Add(new(Guid.NewGuid(), "Other", 1));
        data.Notes.Add(NotebookFake.Note() with { FolderId = data.Folders[0].Id, Document = NotebookEditingTests.Text("Exact retained content") });
        var vm = new NotebookWorkspaceViewModel(data, dialogs ?? new FakeDialogService(), choices);
        await vm.ActivateAsync(); vm.Scope = vm.Scopes.Single(s => s.FolderId == data.Folders[0].Id); await vm.LoadTask;
        vm.SelectedNote = vm.Notes[0]; await vm.LoadTask; return vm;
    }
    [Fact]
    public async Task RenameRetainsIdentityNoteFiltersAndCancelDoesNotWrite()
    {
        var data = new NotebookFake(); using var vm = await Open(data);
        var note = vm.Note!; var id = vm.Scope!.FolderId;
        vm.SearchText = note.Title; await vm.SearchCommand.ExecuteAsync(null);
        Assert.True(vm.BeginFolderRename()); Assert.Equal("Research", vm.FolderRenameName);
        vm.FolderRenameName = "Not saved"; vm.DismissFolderRename(); Assert.Equal(0, data.Writes);
        Assert.True(vm.BeginFolderRename()); vm.FolderRenameName = "  Renamed research  "; await vm.RenameFolderCommand.ExecuteAsync(null);
        Assert.Equal(id, vm.Scope.FolderId); Assert.Equal("Renamed research", vm.ScopeName);
        Assert.Equal("Renamed research", vm.Folders.Single(f => f.Id == id).Name);
        Assert.Equal(note.Id, vm.Note!.Id); Assert.Equal(note.Document, vm.Note.Document); Assert.Equal(note.Title, vm.SearchText);
        Assert.False(vm.IsFolderRenameOpen); Assert.Equal("", vm.FolderRenameName);
        foreach (var scope in vm.Scopes.Where(s => !s.FolderId.HasValue).ToArray())
        { vm.Scope = scope; await vm.LoadTask; Assert.False(vm.HasFolderActions); Assert.False(vm.BeginFolderRename()); Assert.False(vm.DeleteFolderCommand.CanExecute(null)); }
    }
    [Theory]
    [InlineData(NotebookWriteStatus.Invalid)]
    [InlineData(NotebookWriteStatus.StorageFailure)]
    [InlineData(NotebookWriteStatus.Conflict)]
    [InlineData(NotebookWriteStatus.NotFound)]
    public async Task RenameFailuresRetainDraftAndDoNotCreate(NotebookWriteStatus status)
    {
        var data = new NotebookFake(); using var vm = await Open(data); vm.BeginFolderRename();
        vm.FolderRenameName = " "; await vm.RenameFolderCommand.ExecuteAsync(null);
        Assert.Equal(0, data.Writes); Assert.NotEmpty(vm.FolderActionMessage);
        data.NextWrite = status; vm.FolderRenameName = "Retained draft"; await vm.RenameFolderCommand.ExecuteAsync(null);
        Assert.Equal("Retained draft", vm.FolderRenameName); Assert.True(vm.IsFolderRenameOpen); Assert.NotEmpty(vm.FolderActionMessage);
        Assert.Equal("Research", data.Folders[0].Name); Assert.Equal(2, data.Folders.Count);
    }
    [Fact]
    public async Task DeleteConfirmsBeforeDraftConsentAndRetainsExactContentWithAllNotesFallback()
    {
        var data = new NotebookFake(); var dialogs = new FakeDialogService(); var choices = new NotebookChoices { Choice = NotebookLeaveChoice.Save };
        using var vm = await Open(data, dialogs, choices); var note = vm.Note!; var folder = vm.Scope!.FolderId;
        vm.DraftTitle = "Unsaved title";
        await vm.DeleteFolderCommand.ExecuteAsync(null);
        Assert.Equal(0, data.Writes); Assert.Equal(0, choices.Prompts); Assert.True(vm.IsDirty); Assert.Equal(folder, vm.Scope.FolderId);
        Assert.Contains("Research", dialogs.ConfirmationRequest!.Message); Assert.Contains("no folder", dialogs.ConfirmationRequest.Message);
        dialogs.ConfirmationResult = true; await vm.DeleteFolderCommand.ExecuteAsync(null);
        Assert.Equal(1, choices.Prompts); Assert.Equal(NotebookScope.AllNotes, vm.Scope.Scope); Assert.Equal("All Notes", vm.ScopeName);
        Assert.Equal(note.Id, vm.Note!.Id); Assert.Equal("Unsaved title", vm.Note.Title); Assert.Equal("Exact retained content\n", vm.Note.Document.PlainText);
        Assert.Null(vm.Note.FolderId); Assert.DoesNotContain(vm.Folders, f => f.Id == folder); Assert.False(vm.HasFolderActions);
        Assert.Equal(1, vm.Scope.Count);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingOperationCannotRedirectOrPublishAfterCancellationAndSelectionChange(bool delete)
    {
        var data = new NotebookFake(); using var vm = await Open(data, new FakeDialogService { ConfirmationResult = true });
        var target = data.Folders[0]; var other = data.Folders[1];
        var entered = new TaskCompletionSource<NotebookCatalogCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        data.OnCatalog = async (command, _) =>
        {
            entered.SetResult(command); await release.Task;
            if (delete) { data.Folders.RemoveAt(0); return new(NotebookWriteStatus.Success, DeletedItemId: target.Id); }
            data.Folders[0] = target with { Name = "Saved rename", Version = 2 }; return new(NotebookWriteStatus.Success, data.Folders[0]);
        };
        vm.BeginFolderRename(); vm.FolderRenameName = "Saved rename";
        var command = delete ? vm.DeleteFolderCommand : vm.RenameFolderCommand;
        var pending = command.ExecuteAsync(null); var submitted = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task cancellation = Task.CompletedTask;
        try
        {
            Assert.False(command.CanExecute(null)); await command.ExecuteAsync(null); Assert.Equal(1, data.Writes);
            Assert.True(delete ? submitted is DeleteNotebookFolder { ExpectedVersion: 1 } d && d.FolderId == target.Id : submitted is RenameNotebookFolder { ExpectedVersion: 1 } r && r.FolderId == target.Id);
            vm.Scope = vm.Scopes.Single(s => s.FolderId == other.Id); await vm.LoadTask;
            Assert.Equal(target.Id, vm.Scope!.FolderId); // The write owns the selection until cancellation drains it.
            cancellation = vm.CancelCommand.ExecuteAsync(null);
        }
        finally { release.SetResult(); await pending; }
        await cancellation;
        vm.Scope = vm.Scopes.Single(s => s.FolderId == other.Id); await vm.LoadTask;
        Assert.Equal(other.Id, vm.Scope!.FolderId); Assert.Equal("Other", vm.ScopeName); Assert.Equal("Other", data.Folders.Single(f => f.Id == other.Id).Name);
        Assert.False(vm.IsFolderRenameOpen);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DraftConsentCancellationPreservesFolderAndDraft(bool delete)
    {
        var data = new NotebookFake(); var choices = new NotebookChoices { Choice = NotebookLeaveChoice.Cancel };
        using var vm = await Open(data, new FakeDialogService { ConfirmationResult = true }, choices);
        vm.DraftTitle = "Keep exact draft"; vm.BeginFolderRename(); vm.FolderRenameName = "No rename";
        await (delete ? vm.DeleteFolderCommand : vm.RenameFolderCommand).ExecuteAsync(null);
        Assert.Equal(0, data.Writes); Assert.Equal("Keep exact draft", vm.DraftTitle); Assert.True(vm.IsDirty);
        Assert.Equal("Research", vm.ScopeName); Assert.NotEmpty(vm.FolderActionMessage);
    }
    [Fact]
    public async Task SelectionChangedByConfirmationCancelsCapturedDeletion()
    {
        var data = new NotebookFake(); var dialogs = new CallbackDialogs(); using var vm = await Open(data, dialogs);
        dialogs.OnConfirm = () => { vm.Scope = vm.Scopes.Single(s => s.FolderId == data.Folders[1].Id); return true; };
        await vm.DeleteFolderCommand.ExecuteAsync(null); await vm.LoadTask;
        Assert.Equal(0, data.Writes); Assert.Equal(2, data.Folders.Count); Assert.Equal("Other", vm.ScopeName);
    }
    [Theory]
    [InlineData(NotebookWriteStatus.StorageFailure)]
    [InlineData(NotebookWriteStatus.Conflict)]
    public async Task FailedDeletionRetainsFolderNoteAndActionableFeedback(NotebookWriteStatus status)
    {
        var data = new NotebookFake(); using var vm = await Open(data, new FakeDialogService { ConfirmationResult = true });
        var note = vm.Note; var scope = vm.Scope; data.NextWrite = status;
        await vm.DeleteFolderCommand.ExecuteAsync(null);
        Assert.Same(scope, vm.Scope); Assert.Same(note, vm.Note); Assert.Equal(2, data.Folders.Count);
        Assert.NotEmpty(vm.FolderActionMessage); Assert.True(vm.DeleteFolderCommand.CanExecute(null));
    }
    [Fact]
    public async Task SelectionChangeDuringRenameModeCannotRetargetDraft()
    {
        var data = new NotebookFake(); using var vm = await Open(data);
        vm.BeginFolderRename(); vm.FolderRenameName = "Wrong target";
        vm.Scope = vm.Scopes.Single(s => s.FolderId == data.Folders[1].Id); await vm.LoadTask;
        await vm.RenameFolderCommand.ExecuteAsync(null);
        Assert.Equal(0, data.Writes); Assert.Equal("Other", vm.ScopeName); Assert.False(vm.IsFolderRenameOpen);
    }
    private sealed class CallbackDialogs : IDialogService
    {
        public Func<bool>? OnConfirm { get; set; }
        public bool Confirm(ConfirmationDialogRequest request) => OnConfirm!();
        public void ShowInformation(InformationDialogRequest request) { }
    }
}
