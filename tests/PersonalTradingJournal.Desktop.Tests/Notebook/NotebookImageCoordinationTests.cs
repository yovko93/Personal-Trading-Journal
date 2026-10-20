using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookImageCoordinationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportRetainsNewerTypingAndDoesNotPublishAfterDisposal(bool dispose)
    {
        var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note() with { Document = NotebookEditingTests.Text("Original"), Images = [] });
        var images = new NotebookImageEditorTests.Imports(data);
        var entered = new TaskCompletionSource<ImportNotebookImage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        images.Before = request => { entered.TrySetResult(request); return release.Task; };
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), new NotebookChoices(), new NotebookJsonCodec(), images: images);
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        int applied = 0; var before = vm.Note!;
        vm.DraftTitle = "Flush first";
        var operation = vm.ImportImageAsync(before.Id, [1, 2], node =>
        { applied++; vm.SetDraftDocument(new(vm.DraftDocument.Blocks.Append(node))); },
            node => new([NotebookEditingTests.Text("Orig").Blocks.Single(), node, NotebookEditingTests.Text("inal").Blocks.Single()]));
        try
        {
            var request = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(request.EditorPlacement); Assert.Equal("Orig\n\ninal\n", request.Document.PlainText);
            Assert.Equal(before.Version + 1, request.ExpectedVersion); Assert.Equal("Flush first", vm.Note!.Title);
            Assert.False(await vm.PrepareLeaveAsync()); Assert.False(vm.TryLeave());
            vm.SelectedNote = null; Assert.Equal(before.Id, vm.Note.Id);
            vm.DraftTitle = "Newer title"; vm.SetDraftDocument(NotebookEditingTests.Text("Newer body"));
            if (dispose) vm.Dispose();
            release.TrySetResult(); await operation.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(dispose ? 0 : 1, applied); Assert.Equal("Newer title", vm.DraftTitle);
            Assert.Equal(dispose ? "Newer body\n" : "Newer body\n\n", vm.DraftDocument.PlainText);
            Assert.True(vm.IsDirty); Assert.NotEqual("Saved", vm.SaveState);
            Assert.Equal(before.Version + (dispose ? 1 : 2), vm.Note!.Version);
            Assert.False(vm.IsBusy); Assert.True(vm.CompletionTask.IsCompleted);
            if (!dispose)
            { Assert.True(await vm.SaveDraftAsync()); Assert.Equal("Newer body\n\n", vm.Note!.Document.PlainText); Assert.Equal("Newer title", vm.Note.Title); }
        }
        finally { release.TrySetResult(); await operation.WaitAsync(TimeSpan.FromSeconds(10)); }
    }
    [Theory]
    [InlineData(NotebookWriteStatus.Conflict)]
    [InlineData(NotebookWriteStatus.Cancelled)]
    [InlineData(NotebookWriteStatus.UnsupportedContent)]
    public async Task FailedImportNeverCallsPlacementOrReplacesDraft(NotebookWriteStatus status)
    {
        var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note() with { Document = NotebookEditingTests.Text("Original"), Images = [] });
        var images = new NotebookImageEditorTests.Imports(data) { Status = status };
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), codec: new NotebookJsonCodec(), images: images);
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        var doc = vm.DraftDocument; var note = vm.Note;
        Assert.False(await vm.ImportImageAsync(note!.Id, [1, 2], _ => throw new Xunit.Sdk.XunitException("Must not place a rejected image")));
        Assert.Same(doc, vm.DraftDocument); Assert.Same(note, vm.Note); Assert.False(vm.IsDirty); Assert.False(vm.IsBusy);
    }
    [Fact]
    public async Task ImageImportDrainsInflightAutosaveAndUsesAcknowledgedVersion()
    {
        var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note() with { Document = NotebookEditingTests.Text("Original"), Images = [] });
        var images = new NotebookImageEditorTests.Imports(data); var clock = new NotebookClock(); var recovery = new NotebookRecoveryFake();
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), codec: new NotebookJsonCodec(), recovery: recovery, time: clock, images: images);
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        var entered = new TaskCompletionSource<SaveNotebookDraft>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        data.OnWrite = (command, _) => { entered.TrySetResult((SaveNotebookDraft)command); return release.Task; };
        vm.DraftTitle = "Autosaved"; clock.Advance(750);
        var draft = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var import = vm.ImportImageAsync(vm.Note!.Id, [1, 2], node => vm.SetDraftDocument(new(vm.DraftDocument.Blocks.Append(node))));
        Assert.Null(images.LastBytes);
        var saved = data.Notes[0] with { Title = draft.Title, Document = draft.Document, Version = 2 }; data.Notes[0] = saved; data.OnWrite = null;
        release.SetResult(new(NotebookWriteStatus.Success, saved));
        Assert.True(await import.WaitAsync(TimeSpan.FromSeconds(10))); Assert.Equal(3, vm.Note!.Version);
        Assert.Equal(1, data.Writes); Assert.False(vm.IsDirty); Assert.Equal("Saved", vm.SaveState);
        vm.Deactivate(); await vm.DeactivationTask;
    }
}
