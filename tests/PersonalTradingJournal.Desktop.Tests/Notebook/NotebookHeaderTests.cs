using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookHeaderTests
{
    [Fact]
    public async Task TemplateIdentityIsCapturedBeforeAwaitedSaveAndBothCreationEntriesAreLocked()
    {
        var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note());
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), new NotebookChoices { Choice = NotebookLeaveChoice.Save });
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        vm.DraftTitle = "Save exact draft";
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        data.OnWrite = (command, _) => { Assert.IsType<SaveNotebookDraft>(command); entered.SetResult(); return release.Task; };
        vm.SelectedTemplate = vm.Templates.Single(t => t.Id == NotebookTemplate.Psychology);
        var create = vm.CreateTemplateCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Assert.False(vm.CanCreateNote); Assert.False(vm.NewNoteCommand.CanExecute(null)); Assert.False(vm.CreateTemplateCommand.CanExecute(null));
            vm.SelectedTemplate = vm.Templates.Single(t => t.Id == NotebookTemplate.MarketStudy);
            await vm.NewNoteCommand.ExecuteAsync(null); await vm.CreateTemplateCommand.ExecuteAsync(null); Assert.Equal(1, data.Writes);
        }
        finally
        {
            var saved = data.Notes[0] with { Title = "Save exact draft", Version = 2 }; data.Notes[0] = saved; data.OnWrite = null;
            release.SetResult(new(NotebookWriteStatus.Success, saved)); await create;
        }
        Assert.Equal(2, data.Writes); Assert.Equal(2, data.Notes.Count);
        Assert.Equal(NotebookTemplates.Create(NotebookTemplate.Psychology).PlainText, vm.Note!.Document.PlainText);
        Assert.Equal("Save exact draft", data.Notes[0].Title); Assert.True(vm.CanCreateNote);
        await vm.NewNoteCommand.ExecuteAsync(null); Assert.Equal("Untitled", vm.Note!.Title); Assert.Empty(vm.Note.Document.Blocks);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateCreationDoesNotReplaceNewerPanelOrRequestFocus(bool cancel)
    {
        var data = new NotebookFake(); using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService());
        await vm.ActivateAsync(); vm.Panel = 1; int createdEvents = 0; vm.NoteCreated += (_, _) => createdEvents++;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        data.OnWrite = (command, _) => { Assert.IsType<CreateNotebookNote>(command); entered.SetResult(); return release.Task; };
        var create = vm.NewNoteCommand.ExecuteAsync(null); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        vm.Panel = 0; Task drain = cancel ? vm.CancelAsync() : Task.CompletedTask;
        var note = NotebookFake.Note("Untitled") with { Document = NotebookDocument.Empty }; data.Notes.Add(note);
        release.SetResult(new(NotebookWriteStatus.Success, note));
        await Task.WhenAll(create, drain).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, vm.Panel); Assert.Equal(0, createdEvents); Assert.Equal(note.Id, vm.Note!.Id);
        Assert.False(vm.IsBusy); Assert.False(vm.IsWriting); Assert.True(vm.CanCreateNote); Assert.Single(data.Notes);
        if (cancel) Assert.Contains("completed change may already be saved", vm.Message);
    }
}
