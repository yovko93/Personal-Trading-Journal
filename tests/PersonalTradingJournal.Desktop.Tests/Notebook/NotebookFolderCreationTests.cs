using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookFolderCreationTests
{
    [Fact]
    public async Task OpenDismissAndSuccessPreserveFiltersAndSelectNewFolderBeyondFirstCatalogPage()
    {
        var data = new NotebookFake();
        for (int i = 0; i < 55; i++) data.Folders.Add(new(Guid.NewGuid(), $"Folder {i}", 1));
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService());
        await vm.ActivateAsync(); vm.SearchText = "retained search"; await vm.SearchCommand.ExecuteAsync(null);
        Assert.True(vm.BeginFolderCreation()); vm.FolderName = "  New research  ";
        vm.DismissFolderCreation(); Assert.False(vm.IsFolderCreationOpen); Assert.Equal(0, data.Writes);
        Assert.Equal("  New research  ", vm.FolderName); Assert.True(vm.BeginFolderCreation());
        await vm.CreateFolderCommand.ExecuteAsync(null);
        Assert.Equal(1, data.Writes); Assert.Equal("New research", vm.ScopeName);
        Assert.Equal(data.Folders.Last().Id, vm.Scope!.FolderId);
        Assert.Equal(0, vm.Scope.Count); Assert.Equal("retained search", vm.SearchText);
        Assert.False(vm.IsFolderCreationOpen); Assert.Equal("", vm.FolderName);
        await vm.RefreshCommand.ExecuteAsync(null); Assert.Equal(data.Folders.Last().Id, vm.Scope.FolderId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid\0name")]
    public async Task InvalidNamesRetainInputWithoutPersistence(string name)
    {
        var data = new NotebookFake(); using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService());
        await vm.ActivateAsync(); vm.BeginFolderCreation(); vm.FolderName = name;
        await vm.CreateFolderCommand.ExecuteAsync(null);
        Assert.Equal(0, data.Writes); Assert.Equal(name, vm.FolderName);
        Assert.True(vm.IsFolderCreationOpen); Assert.NotEmpty(vm.FolderCreationMessage);
    }

    [Theory]
    [InlineData(NotebookWriteStatus.Invalid)]
    [InlineData(NotebookWriteStatus.StorageFailure)]
    [InlineData(NotebookWriteStatus.Cancelled)]
    public async Task FailedPersistenceRetainsNameAndInlineError(NotebookWriteStatus status)
    {
        var data = new NotebookFake { NextWrite = status }; using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService());
        await vm.ActivateAsync(); var scope = vm.Scope; vm.BeginFolderCreation(); vm.FolderName = "Keep name";
        await vm.CreateFolderCommand.ExecuteAsync(null);
        Assert.Same(scope, vm.Scope); Assert.Empty(data.Folders); Assert.Equal("Keep name", vm.FolderName);
        Assert.True(vm.IsFolderCreationOpen); Assert.NotEmpty(vm.FolderCreationMessage); Assert.False(vm.IsFolderCreationPending);
    }

    [Theory]
    [InlineData(NotebookLeaveChoice.Cancel)]
    [InlineData(NotebookLeaveChoice.Save)]
    [InlineData(NotebookLeaveChoice.Discard)]
    public async Task CreationUsesExistingDirtyDraftConsent(NotebookLeaveChoice choice)
    {
        var data = new NotebookFake(); var note = NotebookFake.Note(); data.Notes.Add(note);
        var choices = new NotebookChoices { Choice = choice };
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), choices);
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        vm.DraftTitle = "Exact retained draft"; vm.BeginFolderCreation(); vm.FolderName = "Research";
        await vm.CreateFolderCommand.ExecuteAsync(null);
        Assert.Equal(1, choices.Prompts);
        if (choice == NotebookLeaveChoice.Cancel)
        {
            Assert.Equal(0, data.Writes); Assert.Equal(note.Id, vm.Note!.Id);
            Assert.Equal("Exact retained draft", vm.DraftTitle); Assert.True(vm.IsDirty);
            Assert.True(vm.IsFolderCreationOpen); Assert.Equal("Research", vm.FolderName); Assert.NotEmpty(vm.FolderCreationMessage);
        }
        else
        {
            Assert.Single(data.Folders); Assert.Equal(data.Folders[0].Id, vm.Scope!.FolderId);
            Assert.Equal(choice == NotebookLeaveChoice.Save ? "Exact retained draft" : note.Title, data.Notes[0].Title);
        }
    }

    [Theory]
    [InlineData("stay")]
    [InlineData("dismiss")]
    [InlineData("panel")]
    [InlineData("deactivate")]
    public async Task PendingCreationIsSingleSubmissionAndLateCommitCannotStealContext(string action)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var data = new NotebookFake(); var item = new NotebookNamedItem(Guid.NewGuid(), "Committed folder", 1);
        data.OnCatalog = async (_, _) => { entered.SetResult(); await release.Task; data.Folders.Add(item); return new(NotebookWriteStatus.Success, item); };
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService());
        await vm.ActivateAsync(); vm.Panel = 0; var origin = vm.Scope;
        vm.BeginFolderCreation(); vm.FolderName = item.Name;
        var pending = vm.CreateFolderCommand.ExecuteAsync(null); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Assert.True(vm.IsFolderCreationPending); Assert.False(vm.CreateFolderCommand.CanExecute(null));
            await vm.CreateFolderCommand.ExecuteAsync(null); Assert.Equal(1, data.Writes);
            if (action == "dismiss") vm.DismissFolderCreation();
            if (action == "panel") { vm.Panel = 2; vm.Panel = 0; }
            if (action == "deactivate") vm.Deactivate();
        }
        finally { release.SetResult(); await pending; }
        Assert.Single(data.Folders); Assert.False(vm.IsFolderCreationPending);
        if (action == "stay") { Assert.Equal(item.Id, vm.Scope!.FolderId); Assert.False(vm.IsFolderCreationOpen); }
        else { Assert.Same(origin, vm.Scope); Assert.Equal(item.Name, vm.FolderName); }
        if (action is "dismiss" or "panel") Assert.Contains("Folder created", vm.Message);
    }

    [Fact]
    public async Task DismissBeforePersistenceAndStaleConsentDoNotCreate()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var data = new NotebookFake(); var note = NotebookFake.Note(); data.Notes.Add(note);
        data.OnWrite = (_, _) => { entered.SetResult(); return release.Task; };
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), new NotebookChoices { Choice = NotebookLeaveChoice.Save });
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        vm.DraftTitle = "Saved draft"; vm.BeginFolderCreation(); vm.FolderName = "Must not create";
        var pending = vm.CreateFolderCommand.ExecuteAsync(null); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        vm.DismissFolderCreation(); release.SetResult(new(NotebookWriteStatus.Success, note with { Title = "Saved draft", Version = 2 }));
        await pending;
        Assert.Empty(data.Folders); Assert.Equal(1, data.Writes); Assert.Equal(note.Id, vm.Note!.Id);
        Assert.False(vm.IsFolderCreationOpen); Assert.Equal("Must not create", vm.FolderName);
    }
}
