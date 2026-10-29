using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookDetailSwitchTests
{
    [Theory]
    [InlineData(NotebookLeaveChoice.Save, NotebookWriteStatus.Success, true)]
    [InlineData(NotebookLeaveChoice.Discard, NotebookWriteStatus.Success, true)]
    [InlineData(NotebookLeaveChoice.Cancel, NotebookWriteStatus.Success, false)]
    [InlineData(NotebookLeaveChoice.Save, NotebookWriteStatus.StorageFailure, false)]
    public async Task DirtySelectionOnlyInstallsTargetAfterSuccessfulLeaveGuard(NotebookLeaveChoice choice, NotebookWriteStatus write, bool accepted)
    {
        var data = new NotebookFake(); var a = NotebookFake.Note("A") with { Document = NotebookEditingTests.Text("A body") };
        var b = NotebookFake.Note("B"); data.Notes.AddRange([a, b]);
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), new NotebookChoices { Choice = choice }, time: new NotebookClock());
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes.Single(n => n.Id == a.Id); await vm.LoadTask;
        vm.DraftTitle = "A edited"; var document = vm.DraftDocument; int loads = data.Loads; data.NextWrite = write;
        vm.SelectedNote = vm.Notes.Single(n => n.Id == b.Id); await vm.LoadTask;
        Assert.Equal(accepted ? b.Id : a.Id, vm.SelectedNote!.Id); Assert.Equal(accepted ? b.Id : a.Id, vm.Note!.Id);
        Assert.Equal(accepted ? b.Title : "A edited", vm.DraftTitle); Assert.Equal(loads + (accepted ? 1 : 0), data.Loads);
        Assert.Equal(!accepted, vm.IsDirty); Assert.False(vm.IsLoadingNote); Assert.True(vm.CanEditDraft);
        if (!accepted) Assert.Same(document, vm.DraftDocument);
        Assert.Equal(choice == NotebookLeaveChoice.Save ? 1 : 0, data.Writes);
        Assert.Equal(choice == NotebookLeaveChoice.Save && write == NotebookWriteStatus.Success ? "A edited" : a.Title,
            data.Notes.Single(n => n.Id == a.Id).Title);
        // A successful write refreshes search membership/counts; clean selection itself does not query the list.
        Assert.Equal(choice == NotebookLeaveChoice.Save && write == NotebookWriteStatus.Success ? 2 : 1, data.Queries.Count);
    }

    [Fact]
    public async Task NewerEditDuringGuardSaveVetoesSwitchAndRetainsDraft()
    {
        var data = new NotebookFake(); var a = NotebookFake.Note("A") with { Document = NotebookEditingTests.Text("A body") };
        var b = NotebookFake.Note("B"); data.Notes.AddRange([a, b]);
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService(), new NotebookChoices { Choice = NotebookLeaveChoice.Save }, time: new NotebookClock());
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes.Single(n => n.Id == a.Id); await vm.LoadTask;
        var entered = new TaskCompletionSource<SaveNotebookDraft>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        data.OnWrite = (command, _) => { entered.TrySetResult(Assert.IsType<SaveNotebookDraft>(command)); return release.Task; };
        vm.DraftTitle = "First"; vm.SelectedNote = vm.Notes.Single(n => n.Id == b.Id); var switching = vm.LoadTask;
        try
        {
            var captured = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(a.Id, vm.Note!.Id); Assert.Equal(a.Id, vm.SelectedNote!.Id); Assert.True(vm.CanEditDraft);
            vm.DraftTitle = "Newer"; vm.SetDraftDocument(NotebookEditingTests.Text("Newer body"));
            var persisted = a with { Title = captured.Title, Document = captured.Document, Version = a.Version + 1 };
            data.Notes[0] = persisted; data.OnWrite = null; release.SetResult(new(NotebookWriteStatus.Success, persisted));
            await switching.WaitAsync(TimeSpan.FromSeconds(10));
            var saved = data.Notes.Single(n => n.Id == a.Id);
            Assert.Equal("First", saved.Title); Assert.Equal(a.Version + 1, saved.Version); Assert.Equal(1, data.Writes);
            Assert.Equal(a.Id, vm.Note!.Id); Assert.Equal(a.Id, vm.SelectedNote!.Id);
            Assert.Equal("Newer", vm.DraftTitle); Assert.Equal(NotebookEditingTests.Text("Newer body").PlainText, vm.DraftDocument.PlainText);
            Assert.True(vm.IsDirty); Assert.True(vm.CanEditDraft); Assert.False(vm.IsLoadingNote);
            Assert.Equal(1, data.Loads); Assert.Equal(2, data.Queries.Count);
        }
        finally { release.TrySetResult(new(NotebookWriteStatus.Cancelled)); await switching.WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutOfOrderDetailCannotReplaceLatestRequestIncludingReturnToDisplayedNote(bool returnToA)
    {
        var data = new NotebookFake(); var a = NotebookFake.Note("A"); var b = NotebookFake.Note("B"); var c = NotebookFake.Note("C");
        data.Notes.AddRange([a, b, c]); using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService());
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes.Single(n => n.Id == a.Id); await vm.LoadTask;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<NotebookLoadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        data.OnLoad = (id, _) => id == b.Id ? Hold() : Task.FromResult(new NotebookLoadResult(NotebookLoadStatus.Success, id == a.Id ? a : c));
        Task<NotebookLoadResult> Hold() { entered.TrySetResult(); return release.Task; }
        vm.SelectedNote = vm.Notes.Single(n => n.Id == b.Id); var old = vm.LoadTask;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(a.Id, vm.Note!.Id); Assert.False(vm.CanEditDraft); Assert.True(vm.CanSelectNote);
            var target = returnToA ? a : c;
            vm.SelectedNote = vm.Notes.Single(n => n.Id == target.Id); await vm.LoadTask;
            release.SetResult(new(NotebookLoadStatus.Success, b)); await old;
            Assert.Equal(target.Id, vm.Note!.Id); Assert.Equal(target.Title, vm.DraftTitle);
            Assert.Equal(target.Id, vm.SelectedNote!.Id); Assert.False(vm.IsLoadingNote); Assert.True(vm.CanEditDraft);
            Assert.Single(data.Queries);
        }
        finally { release.TrySetResult(new(NotebookLoadStatus.Success, b)); await old.WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelOrDeactivateRestoresDisplayedOwnershipBeforeReenablingEditing(bool deactivate)
    {
        var data = new NotebookFake(); var a = NotebookFake.Note("A"); var b = NotebookFake.Note("B"); data.Notes.AddRange([a, b]);
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService()); await vm.ActivateAsync();
        vm.SelectedNote = vm.Notes.Single(n => n.Id == a.Id); await vm.LoadTask;
        var document = vm.DraftDocument;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<NotebookLoadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        data.OnLoad = (_, _) => { entered.TrySetResult(); return release.Task; };
        vm.SelectedNote = vm.Notes.Single(n => n.Id == b.Id); var read = vm.LoadTask;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Task cancellation;
            if (deactivate) { vm.Deactivate(); cancellation = vm.DeactivationTask; } else cancellation = vm.CancelAsync();
            release.SetResult(new(NotebookLoadStatus.Success, b)); await cancellation;
            Assert.Equal(a.Id, vm.Note!.Id); Assert.Equal(a.Id, vm.SelectedNote!.Id); Assert.Same(document, vm.DraftDocument);
            Assert.False(vm.IsLoadingNote); Assert.Equal(!deactivate, vm.CanEditDraft);
        }
        finally { release.TrySetResult(new(NotebookLoadStatus.Success, b)); await read.WaitAsync(TimeSpan.FromSeconds(10)); }
    }

    [Theory]
    [InlineData(NotebookLoadStatus.NotFound)]
    [InlineData(NotebookLoadStatus.UnsupportedContent)]
    [InlineData(NotebookLoadStatus.InvalidContent)]
    [InlineData(NotebookLoadStatus.TooLarge)]
    [InlineData(null)]
    public async Task UnavailableOrFailedTargetNeverExposesPreviousDraftAndRefreshReloads(NotebookLoadStatus? status)
    {
        var data = new NotebookFake(); var a = NotebookFake.Note("A"); var b = NotebookFake.Note("B"); data.Notes.AddRange([a, b]);
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService()); await vm.ActivateAsync();
        vm.SelectedNote = vm.Notes.Single(n => n.Id == a.Id); await vm.LoadTask;
        data.OnLoad = (_, _) => status is { } value ? Task.FromResult(new NotebookLoadResult(value,
            UnavailableMetadata: value == NotebookLoadStatus.NotFound ? null : new(b.Id, b.Title, b.Version, b.State))) : throw new IOException("synthetic");
        vm.SelectedNote = vm.Notes.Single(n => n.Id == b.Id); await vm.LoadTask;
        Assert.Null(vm.Note); Assert.Equal(b.Id, vm.SelectedNote!.Id); Assert.False(vm.CanEditDraft); Assert.False(vm.IsLoadingNote);
        Assert.NotEmpty(vm.ContentMessage); Assert.Empty(vm.DraftDocument.Blocks);
        Assert.Equal(status is null or NotebookLoadStatus.NotFound ? "" : b.Title, vm.DraftTitle);
        int queries = data.Queries.Count; data.OnLoad = null; await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(queries + 1, data.Queries.Count); Assert.Equal(b.Id, vm.Note!.Id); Assert.Equal(b.Title, vm.DraftTitle);
        Assert.True(vm.CanEditDraft);
    }
}
