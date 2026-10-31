using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookReadingModeTests
{
    private static async Task<NotebookWorkspaceViewModel> Open(NotebookFake data, NotebookRecoveryFake? recovery = null)
    {
        data.Notes.Add(NotebookFake.Note("First") with { Document = NotebookEditingTests.Text("Original") });
        data.Notes.Add(NotebookFake.Note("Second") with { Document = NotebookEditingTests.Text("Other") });
        var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService { ConfirmationResult = true }, new NotebookChoices(), new NotebookJsonCodec(), recovery, new NotebookClock());
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes.Single(n => n.Id == data.Notes[0].Id); await vm.LoadTask;
        return vm;
    }

    [Fact]
    public async Task ReadingEditAndCleanDoneDoNotWriteOrReinstallDocument()
    {
        var data = new NotebookFake(); using var vm = await Open(data);
        var document = vm.DraftDocument; long revision = vm.EditorRevision, version = vm.Note!.Version;
        Assert.False(vm.IsEditing); Assert.True(vm.EditorReadOnly);
        await vm.EditModeCommand.ExecuteAsync(null);
        Assert.True(vm.IsEditing); Assert.True(vm.CanUseEditor); Assert.Equal("Done", vm.EditModeLabel);
        await vm.EditModeCommand.ExecuteAsync(null);
        Assert.False(vm.IsEditing); Assert.Equal("Edit", vm.EditModeLabel);
        Assert.Equal(0, data.Writes); Assert.Equal(version, vm.Note.Version);
        Assert.Equal(revision, vm.EditorRevision); Assert.Same(document, vm.DraftDocument);
    }

    [Fact]
    public async Task DoneSavesCurrentDraftThroughExistingPipeline()
    {
        var data = new NotebookFake(); using var vm = await Open(data, new());
        await vm.EditModeCommand.ExecuteAsync(null); long editor = vm.EditorRevision;
        vm.DraftTitle = "Edited"; vm.SetDraftDocument(NotebookEditingTests.Text("Saved body"));
        await vm.EditModeCommand.ExecuteAsync(null);
        Assert.False(vm.IsEditing); Assert.False(vm.IsDirty); Assert.Equal("Saved locally", vm.CompactSaveState);
        Assert.Equal(1, data.Writes); Assert.Equal(2, vm.Note!.Version); Assert.Equal("Saved body\n", vm.Note.Document.PlainText);
        Assert.Equal(editor, vm.EditorRevision);
    }

    [Theory]
    [InlineData(NotebookWriteStatus.StorageFailure)]
    [InlineData(NotebookWriteStatus.Conflict)]
    [InlineData(NotebookWriteStatus.Cancelled)]
    public async Task FailedDoneRetainsEditingDraftAndRecovery(NotebookWriteStatus status)
    {
        var data = new NotebookFake(); var recovery = new NotebookRecoveryFake(); using var vm = await Open(data, recovery);
        await vm.EditModeCommand.ExecuteAsync(null); vm.DraftTitle = "Retain me"; data.NextWrite = status;
        await vm.EditModeCommand.ExecuteAsync(null);
        Assert.True(vm.IsEditing); Assert.True(vm.IsDirty); Assert.Equal("Retain me", vm.DraftTitle);
        Assert.Equal(1, vm.Note!.Version); Assert.NotEmpty(recovery.Drafts); Assert.NotEqual("Saved locally", vm.CompactSaveState);
        vm.DismissNotificationCommand.Execute(null); Assert.True(vm.IsEditing); Assert.NotEmpty(vm.SaveFeedback);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DoneCannotAcknowledgeNewerInputOrAnotherActivation(bool reactivate)
    {
        var data = new NotebookFake(); using var vm = await Open(data);
        await vm.EditModeCommand.ExecuteAsync(null); vm.DraftTitle = "Captured";
        var entered = new TaskCompletionSource<SaveNotebookDraft>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        data.OnWrite = (c, _) => { entered.TrySetResult((SaveNotebookDraft)c); return release.Task; };
        var note = vm.Note!; var pending = vm.EditModeCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.False(vm.EditModeCommand.CanExecute(null));
            if (reactivate) vm.Deactivate(); else vm.DraftTitle = "Newer";
        }
        finally { release.TrySetResult(new(NotebookWriteStatus.Success, note with { Title = "Captured", Version = 2 })); }
        await pending.WaitAsync(TimeSpan.FromSeconds(10)); data.OnWrite = null;
        if (reactivate)
        {
            await vm.DeactivationTask; await vm.ActivateAsync();
            Assert.True(vm.IsEditing); Assert.Equal("Captured", vm.DraftTitle);
            await vm.DiscardCommand.ExecuteAsync(null);
            vm.SelectedNote = vm.Notes.Single(n => n.Id == data.Notes[1].Id); await vm.LoadTask;
            Assert.False(vm.IsEditing); Assert.Equal("Second", vm.DraftTitle);
        }
        else { Assert.True(vm.IsEditing); Assert.Equal("Newer", vm.DraftTitle); Assert.True(vm.IsDirty); }
    }

    [Fact]
    public async Task CancelledDepartureKeepsEditIntentAcceptedDepartureReturnsToReading()
    {
        var data = new NotebookFake(); using var vm = await Open(data);
        await vm.EditModeCommand.ExecuteAsync(null); vm.DraftTitle = "Draft"; var id = vm.Note!.Id;
        vm.SelectedNote = vm.Notes.Single(n => n.Id != id); await vm.LoadTask;
        Assert.Equal(id, vm.Note.Id); Assert.True(vm.IsEditing); Assert.Equal("Draft", vm.DraftTitle);
        await vm.EditModeCommand.ExecuteAsync(null); Assert.False(vm.IsEditing);
        await vm.EditModeCommand.ExecuteAsync(null);
        vm.SelectedNote = vm.Notes.Single(n => n.Id != id); await vm.LoadTask;
        Assert.False(vm.IsEditing); Assert.True(vm.EditorReadOnly); Assert.Equal("Second", vm.DraftTitle);
    }

    [Theory]
    [InlineData(NotebookTemplate.BlankNote)]
    [InlineData(NotebookTemplate.MarketStudy)]
    [InlineData(NotebookTemplate.TradingIdea)]
    [InlineData(NotebookTemplate.LessonLearned)]
    [InlineData(NotebookTemplate.Psychology)]
    public async Task NewNotesEnterEditWithOneAtomicWrite(NotebookTemplate template)
    {
        var data = new NotebookFake(); using var vm = await Open(data);
        vm.SelectedTemplate = vm.Templates.Single(t => t.Id == template);
        await vm.CreateTemplateCommand.ExecuteAsync(null);
        Assert.True(vm.IsEditing); Assert.True(vm.CanUseEditor); Assert.Equal(1, data.Writes);
        Assert.Equal(1, vm.Note!.Version); Assert.Equal(NotebookTemplates.Create(template).PlainText, vm.DraftDocument.PlainText);
    }

    [Theory]
    [InlineData(NotebookNoteState.Archived)]
    [InlineData(NotebookNoteState.Trashed)]
    public async Task ReadOnlyLifecycleCannotEnterEdit(NotebookNoteState state)
    {
        var data = new NotebookFake(); using var vm = await Open(data);
        data.Notes[0] = data.Notes[0] with { State = state };
        if (state == NotebookNoteState.Trashed) { vm.Scope = vm.Scopes.Single(s => s.Scope == NotebookScope.Trash); await vm.LoadTask; }
        else await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedNote = vm.Notes.Single(n => n.Id == data.Notes[0].Id); await vm.LoadTask;
        Assert.False(vm.IsEditing); Assert.False(vm.EditModeCommand.CanExecute(null)); Assert.False(vm.CanUseEditor); Assert.Equal(0, data.Writes);
    }

    [Fact]
    public async Task ConflictingRecoveryIsVisibleInEditAndDoneCannotDismissIt()
    {
        var data = new NotebookFake(); var recovery = new NotebookRecoveryFake(); using var vm = await Open(data, recovery);
        var draft = new NotebookRecoveryDraft(Guid.NewGuid(), vm.Note!.Id, 99, 10, "Recovery", NotebookEditingTests.Text("Recovered body"), DateTimeOffset.UtcNow);
        recovery.Drafts[draft.Id] = draft; await vm.RefreshCommand.ExecuteAsync(null);
        await vm.RecoverCommand.ExecuteAsync(Assert.Single(vm.RecoveryDrafts));
        Assert.True(vm.IsEditing); Assert.Equal("Conflict", vm.CompactSaveState);
        await vm.EditModeCommand.ExecuteAsync(null);
        Assert.True(vm.IsEditing); Assert.Equal("Recovery", vm.DraftTitle); Assert.True(vm.IsDirty); Assert.Equal(0, data.Writes);
    }
}
