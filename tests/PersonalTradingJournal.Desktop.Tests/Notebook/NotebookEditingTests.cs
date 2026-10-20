using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

internal sealed class NotebookChoices : INotebookInteraction
{
    public NotebookLeaveChoice Choice { get; set; }
    public int Prompts { get; private set; }
    public NotebookLeaveChoice ConfirmLeave() { Prompts++; return Choice; }
}
public sealed class NotebookEditingTests
{
    internal static NotebookDocument Text(string text) => new([NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), text)])]);
    private static async Task<NotebookWorkspaceViewModel> Open(NotebookFake data, NotebookChoices? choice = null)
    {
        data.Notes.Add(NotebookFake.Note() with { Document = Text("Original") });
        var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService { ConfirmationResult = true }, choice, new NotebookJsonCodec());
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        return vm;
    }
    [Fact]
    public async Task ManualSaveCommitsTitleAndBodyOnceAndDoesNotResetEditor()
    {
        var data = new NotebookFake(); using var vm = await Open(data);
        long editor = vm.EditorRevision;
        vm.DraftTitle = " Research "; vm.SetDraftDocument(Text("Revised"));
        Assert.True(vm.IsDirty); Assert.Equal("Unsaved changes", vm.SaveState);
        Assert.True(await vm.SaveDraftAsync());
        Assert.Equal(1, data.Writes); Assert.Equal("Research", vm.Note!.Title);
        Assert.Contains("Revised", vm.Notes.Single().Preview); Assert.Equal(editor, vm.EditorRevision);
        Assert.False(vm.IsDirty); Assert.Equal("Saved", vm.SaveState);
        await vm.SaveDraftAsync(); Assert.Equal(1, data.Writes);
    }
    [Theory]
    [InlineData(NotebookWriteStatus.Conflict)]
    [InlineData(NotebookWriteStatus.StorageFailure)]
    [InlineData(NotebookWriteStatus.Cancelled)]
    [InlineData(NotebookWriteStatus.Invalid)]
    public async Task FailedSaveKeepsEveryDraftFieldAndVersion(NotebookWriteStatus status)
    {
        var data = new NotebookFake(); using var vm = await Open(data);
        var original = vm.Note; vm.DraftTitle = "Keep title"; vm.SetDraftDocument(Text("Keep content")); data.NextWrite = status;
        Assert.False(await vm.SaveDraftAsync()); Assert.Same(original, vm.Note);
        Assert.Equal("Keep title", vm.DraftTitle); Assert.Contains("Keep content", vm.DraftDocument.PlainText);
        Assert.True(vm.IsDirty); Assert.Equal("Could not save", vm.SaveState); Assert.False(vm.IsBusy);
        data.NextWrite = NotebookWriteStatus.Success; Assert.True(await vm.SaveDraftAsync());
    }
    [Fact]
    public async Task TitleValidationCanBeCorrectedWithoutDiscardingBody()
    {
        var data = new NotebookFake(); using var vm = await Open(data);
        vm.DraftTitle = " "; vm.SetDraftDocument(Text("Keep")); Assert.False(await vm.SaveDraftAsync());
        Assert.Equal(0, data.Writes); vm.DraftTitle = "Valid"; Assert.True(await vm.SaveDraftAsync());
        Assert.Contains("Keep", vm.Note!.Document.PlainText);
    }
    [Theory]
    [InlineData(NotebookLeaveChoice.Cancel)]
    [InlineData(NotebookLeaveChoice.Discard)]
    [InlineData(NotebookLeaveChoice.Save)]
    public async Task SelectionAndRefreshUseSaveDiscardCancel(NotebookLeaveChoice choice)
    {
        var data = new NotebookFake(); var dialog = new NotebookChoices { Choice = choice };
        using var vm = await Open(data, dialog); var original = vm.Note!;
        vm.DraftTitle = "Changed";
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(1, dialog.Prompts);
        Assert.Equal(choice == NotebookLeaveChoice.Cancel, vm.IsDirty);
        Assert.Equal(choice == NotebookLeaveChoice.Save ? "Changed" : original.Title, vm.Note!.Title);
        if (choice == NotebookLeaveChoice.Cancel) Assert.Equal("Changed", vm.DraftTitle);
        Assert.Equal(choice == NotebookLeaveChoice.Save ? 1 : 0, data.Writes);
    }
    [Fact]
    public async Task DiscardConsentIsNotDestructiveUntilFinalOwnerAcceptance()
    {
        var data = new NotebookFake(); using var vm = await Open(data, new() { Choice = NotebookLeaveChoice.Discard });
        vm.DraftTitle = "Retained after later veto"; Assert.True(await vm.PrepareLeaveAsync());
        Assert.True(vm.TryLeave()); Assert.True(vm.IsDirty);
        vm.ClearLeaveConsent(); Assert.Equal("Retained after later veto", vm.DraftTitle); Assert.False(vm.TryLeave());
        Assert.True(await vm.PrepareLeaveAsync()); vm.CompleteLeave(); Assert.False(vm.IsDirty); Assert.Equal(0, data.Writes);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapturedSaveCannotAcknowledgeNewerEditsOrPublishAfterDisposal(bool dispose)
    {
        var data = new NotebookFake(); using var vm = await Open(data);
        var entered = new TaskCompletionSource<SaveNotebookDraft>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        data.OnWrite = (c, _) => { entered.TrySetResult((SaveNotebookDraft)c); return release.Task; };
        vm.DraftTitle = "Captured"; var save = vm.SaveDraftAsync();
        try
        {
            var captured = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(vm.TryLeave()); Assert.False(vm.SaveCommand.CanExecute(null));
            vm.DraftTitle = "Newer"; vm.SetDraftDocument(Text("Newer body"));
            var old = vm.Note!; var persisted = old with { Title = captured.Title, Document = captured.Document, Version = old.Version + 1 };
            if (dispose) vm.Dispose();
            release.SetResult(new(NotebookWriteStatus.Success, persisted)); await save.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("Newer", vm.DraftTitle); Assert.Contains("Newer body", vm.DraftDocument.PlainText);
            Assert.True(vm.IsDirty); Assert.False(vm.IsBusy); Assert.True(vm.CompletionTask.IsCompleted);
            Assert.Equal(dispose ? old.Version : persisted.Version, vm.Note!.Version);
        }
        finally { release.TrySetResult(new(NotebookWriteStatus.Cancelled)); await save.WaitAsync(TimeSpan.FromSeconds(10)); }
    }
    [Fact]
    public async Task PinCoordinatesSavedDraftVersionAndImagesRemainProtected()
    {
        var data = new NotebookFake(); using var vm = await Open(data, new() { Choice = NotebookLeaveChoice.Save });
        vm.DraftTitle = "Before pin"; await vm.PinCommand.ExecuteAsync(null);
        Assert.Equal(2, data.Writes); Assert.True(vm.Note!.IsPinned); Assert.Equal("Before pin", vm.Note.Title); Assert.False(vm.IsDirty);
        var image = NotebookNode.Image(Guid.NewGuid(), Guid.NewGuid(), "caption", "alt");
        data.Notes[0] = data.Notes[0] with { Document = new([image]) }; await vm.RefreshCommand.ExecuteAsync(null);
        Assert.False(vm.CanEditBody); Assert.True(vm.CanEditDraft);
        vm.DraftTitle = "Image title"; Assert.True(await vm.SaveDraftAsync()); Assert.Same(image, vm.Note!.Document.Blocks[0]);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavingBeforeMoveRetainsExactUnfiledOrLaterPageDestination(bool laterPage)
    {
        var data = new NotebookFake();
        data.Folders.AddRange(Enumerable.Range(0, 55).Select(i => new NotebookNamedItem(Guid.NewGuid(), $"Folder {i:000}", 1)));
        data.Notes.Add(NotebookFake.Note(folder: data.Folders[0].Id));
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), new NotebookChoices { Choice = NotebookLeaveChoice.Save }, new NotebookJsonCodec());
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        await vm.MoreFoldersCommand.ExecuteAsync(null);
        Guid? target = laterPage ? data.Folders[^1].Id : null;
        vm.MoveFolder = vm.Folders.Single(f => f.Id == target); vm.DraftTitle = "Saved before move";
        await vm.MoveCommand.ExecuteAsync(null);
        Assert.Equal(2, data.Writes); Assert.Equal(target, data.Notes.Single().FolderId);
        Assert.Equal("Saved before move", data.Notes.Single().Title); Assert.False(vm.IsDirty);
    }
}
