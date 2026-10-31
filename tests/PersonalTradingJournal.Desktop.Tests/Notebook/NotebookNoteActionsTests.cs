using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookNoteActionsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscardConfirmationCannotConsumeNewerDraftOrSelection(bool changeSelection)
    {
        var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note("First")); data.Notes.Add(NotebookFake.Note("Second"));
        var dialogs = new ReentrantDialogs(); using var vm = new NotebookWorkspaceViewModel(data, dialogs);
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes.Single(n => n.Title == "First"); await vm.LoadTask;
        vm.DraftTitle = "Draft at confirmation";
        dialogs.DuringConfirmation = () =>
        {
            if (changeSelection) { vm.DraftTitle = "First"; vm.SelectedNote = vm.Notes.Single(n => n.Title == "Second"); }
            else vm.DraftTitle = "Newer input during confirmation";
        };
        await vm.DiscardCommand.ExecuteAsync(null); await vm.LoadTask;
        Assert.Equal(changeSelection ? "Second" : "Newer input during confirmation", vm.DraftTitle);
        Assert.Equal(!changeSelection, vm.IsDirty); Assert.Equal(0, data.Writes);
    }
    [Fact]
    public async Task CheckpointAndFailedCanonicalSaveNeverReportSaved()
    {
        var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note());
        var recovery = new NotebookRecoveryFake(); var clock = new NotebookClock();
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), new NotebookChoices(), new NotebookJsonCodec(), recovery, clock);
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        Assert.Equal("Saved locally", vm.CompactSaveState);
        vm.DraftTitle = "Retained unsaved title"; Assert.Equal("Unsaved changes", vm.CompactSaveState);
        data.NextWrite = NotebookWriteStatus.StorageFailure;
        clock.Advance(750); await vm.AutosaveTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("Save paused", vm.CompactSaveState); Assert.True(vm.IsDirty);
        Assert.Equal("Retained unsaved title", Assert.Single(recovery.Drafts).Value.Title);
        Assert.Contains("not yet saved", vm.RecoveryMessage); Assert.Equal("Liquidity sweep", data.Notes[0].Title);
        data.NextWrite = NotebookWriteStatus.Success; await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Saved locally", vm.CompactSaveState); Assert.False(vm.IsDirty); Assert.Empty(recovery.Drafts);
    }
    [Fact]
    public async Task CapturedContextSurvivesSaveButNotLeaveAndReturnOrReactivation()
    {
        var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note("First")); data.Notes.Add(NotebookFake.Note("Second"));
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService());
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes.Single(n => n.Title == "First"); await vm.LoadTask;
        var owns = vm.CaptureNoteActionOwnership(); vm.DraftTitle = "Saved first"; await vm.SaveCommand.ExecuteAsync(null); Assert.True(owns());
        var first = vm.Note!.Id; vm.SelectedNote = vm.Notes.Single(n => n.Id != first); await vm.LoadTask; Assert.False(owns());
        vm.SelectedNote = vm.Notes.Single(n => n.Id == first); await vm.LoadTask; Assert.False(owns());
        var newer = vm.CaptureNoteActionOwnership(); vm.Deactivate(); await vm.DeactivationTask; await vm.ActivateAsync(); Assert.False(newer());
    }
    [Fact]
    public async Task TrashConfirmationCannotRetargetAfterModalSelectionChange()
    {
        var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note("First")); data.Notes.Add(NotebookFake.Note("Second"));
        var dialogs = new ReentrantDialogs(); using var vm = new NotebookWorkspaceViewModel(data, dialogs);
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes.Single(n => n.Title == "First"); await vm.LoadTask;
        dialogs.DuringConfirmation = () => { vm.SelectedNote = vm.Notes.Single(n => n.Title == "Second"); };
        await vm.TrashCommand.ExecuteAsync(null); await vm.LoadTask;
        Assert.Equal("Second", vm.Note!.Title); Assert.Equal(0, data.Writes);
        Assert.All(data.Notes, n => Assert.Equal(PersonalTradingJournal.Domain.Notebook.NotebookNoteState.Active, n.State));
    }
    [Theory]
    [InlineData(NotebookLoadStatus.UnsupportedContent)]
    [InlineData(NotebookLoadStatus.InvalidContent)]
    public async Task UnavailableSelectionKeepsFeedbackWithoutEditableManagement(NotebookLoadStatus status)
    {
        var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note());
        data.OnLoad = (_, _) => Task.FromResult(new NotebookLoadResult(status, OriginalContent: "opaque"));
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService()); await vm.ActivateAsync();
        Assert.False(vm.HasEditorContext); vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        Assert.True(vm.HasEditorContext); Assert.Equal("Content unavailable", vm.CompactSaveState);
        Assert.NotEmpty(vm.ContentMessage); Assert.False(vm.CanEditDraft); Assert.False(vm.PinCommand.CanExecute(null)); Assert.False(vm.MoveCommand.CanExecute(null));
    }
    private sealed class ReentrantDialogs : IDialogService
    {
        internal Action? DuringConfirmation;
        public bool Confirm(ConfirmationDialogRequest request) { DuringConfirmation?.Invoke(); return true; }
        public void ShowInformation(InformationDialogRequest request) { }
    }
}
