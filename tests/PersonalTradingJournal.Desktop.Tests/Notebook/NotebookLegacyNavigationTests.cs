using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookLegacyNavigationTests
{
    [Theory]
    [InlineData(NotebookScope.Archived)]
    [InlineData(NotebookScope.Unfiled)]
    public async Task RemovedContextNormalizesThroughDraftGuardAndSurvivesReactivation(NotebookScope legacy)
    {
        var data = new NotebookFake(); var folder = new NotebookNamedItem(Guid.NewGuid(), "Research", 1); data.Folders.Add(folder);
        var active = NotebookFake.Note("Research active", folder.Id); data.Notes.Add(active);
        var archived = NotebookFake.Note("Research legacy", state: NotebookNoteState.Archived) with { IsPinned = true }; data.Notes.Add(archived);
        var choices = new NotebookChoices { Choice = NotebookLeaveChoice.Cancel };
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), choices);
        await vm.ActivateAsync(); vm.Scope = vm.Scopes.Single(x => x.FolderId == folder.Id); await vm.LoadTask;
        vm.SelectedNote = Assert.Single(vm.Notes); await vm.LoadTask; vm.DraftTitle = "Retain this exact draft";
        vm.Scope = new(legacy, null, legacy.ToString(), 0); await vm.LoadTask;
        Assert.Equal(folder.Id, vm.Scope!.FolderId); Assert.Equal(active.Id, vm.Note!.Id);
        Assert.Equal("Retain this exact draft", vm.DraftTitle); Assert.True(vm.IsDirty); Assert.Equal(0, data.Writes);
        choices.Choice = NotebookLeaveChoice.Save;
        vm.Scope = new(legacy, null, legacy.ToString(), 0); await vm.LoadTask;
        Assert.Equal(NotebookScope.AllNotes, vm.Scope!.Scope); Assert.Equal("All Notes", vm.ScopeName);
        Assert.Same(vm.Scope, vm.Scopes.Single(x => x.Scope == NotebookScope.AllNotes)); Assert.Equal(2, vm.Scope.Count);
        Assert.Equal("Retain this exact draft", data.Notes.Single(n => n.Id == active.Id).Title);
        Assert.DoesNotContain(vm.Scopes, x => x.Scope is NotebookScope.Unfiled or NotebookScope.Archived);
        vm.SelectedNote = vm.Notes.Single(n => n.Id == archived.Id); await vm.LoadTask;
        Assert.False(vm.CanEditDraft); Assert.False(vm.CanEditBody); Assert.False(vm.PinCommand.CanExecute(null));
        Assert.False(vm.MoveCommand.CanExecute(null)); Assert.True(vm.DuplicateCommand.CanExecute(null)); Assert.True(vm.TrashCommand.CanExecute(null));
        Assert.Contains("read-only", vm.LegacyReadOnlyMessage); Assert.Equal("No folder", vm.Breadcrumb);
        vm.Deactivate(); await vm.DeactivationTask; await vm.ActivateAsync();
        Assert.Equal(NotebookScope.AllNotes, vm.Scope!.Scope); Assert.Equal(archived.Id, vm.Note!.Id);
    }
}
