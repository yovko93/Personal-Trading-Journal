using System.Collections.Immutable;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookLinkTests
{
    internal static readonly Guid Target = Guid.Parse("4b25732d-6b19-4f79-9d1e-c42e4b9d0364");
    internal static NotebookLinkTarget Choice => new(NotebookTargetKind.TradingSetup, Target, "Study", "Trading Setup · Active");
    private static async Task<NotebookWorkspaceViewModel> Open(NotebookFake store, NotebookChoices? interaction = null,
        NotebookRecoveryFake? recovery = null, NotebookClock? time = null)
    {
        if (store.Notes.Count == 0) store.Notes.Add(NotebookFake.Note() with { Document = NotebookEditingTests.Text("Original") });
        store.OnTargets ??= (_, _) => Task.FromResult(new NotebookTargetPage([Choice], 1, null));
        store.OnResolve ??= (links, _) => Task.FromResult<ImmutableArray<NotebookResolvedLink>>([.. links.Select(x => new NotebookResolvedLink(x, NotebookTargetAvailability.Available, "Current study"))]);
        var vm = new NotebookWorkspaceViewModel(store, new FakeDialogService(), interaction ?? new() { Choice = NotebookLeaveChoice.Save },
            new NotebookJsonCodec(), recovery, time);
        await vm.ActivateAsync(); vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        return vm;
    }
    private static async Task Pick(NotebookWorkspaceViewModel vm)
    { vm.LinkPickerOpen = true; await vm.LinkSearchTask; vm.SelectedLinkTarget = vm.LinkTargets[0]; }
    [Fact]
    public async Task SearchPagingAndLateCompletionKeepLatestRequestAndTypedIdentity()
    {
        var store = new NotebookFake(); using var vm = await Open(store);
        var pending = new TaskCompletionSource<NotebookTargetPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); CancellationToken oldToken = default;
        store.OnTargets = (query, ct) => { if (query.Search == "old") { oldToken = ct; entered.TrySetResult(); return pending.Task; }
            return Task.FromResult(new NotebookTargetPage([Choice with { Kind = query.Kind, Label = query.Search, Id = query.Cursor is null ? Target : Guid.NewGuid() }], 2, query.Cursor is null ? "next" : null)); };
        vm.LinkPickerOpen = true; await vm.LinkSearchTask;
        vm.LinkSearch = "old"; var old = vm.LinkSearchTask; await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        vm.LinkTargetKind = NotebookTargetKind.TradingSetup; vm.LinkSearch = "new"; await vm.LinkSearchTask;
        Assert.True(oldToken.IsCancellationRequested);
        pending.SetResult(new([Choice with { Label = "old" }], 1, null)); await old;
        Assert.Equal("new", Assert.Single(vm.LinkTargets).Label); Assert.True(vm.HasMoreLinkTargets);
        await vm.MoreLinkTargetsCommand.ExecuteAsync(null); Assert.Equal(2, vm.LinkTargets.Count); Assert.False(vm.HasMoreLinkTargets);
        Assert.All(vm.LinkTargets, x => Assert.Equal(NotebookTargetKind.TradingSetup, x.Target.Kind));
        store.OnTargets = (_, _) => throw new IOException("private"); await vm.SearchLinkTargetsCommand.ExecuteAsync(null);
        Assert.Contains("retry", vm.TargetMessage); Assert.DoesNotContain("private", vm.TargetMessage);
        store.OnTargets = (_, _) => Task.FromResult(new NotebookTargetPage([], 0, null)); await vm.SearchLinkTargetsCommand.ExecuteAsync(null);
        Assert.Contains("No matching", vm.TargetMessage);
    }
    [Fact]
    public async Task DirtyDraftSavesBeforeLinkAndUnlinkWithoutReplacingEditorOrImages()
    {
        var store = new NotebookFake(); using var vm = await Open(store); await Pick(vm); long revision = vm.EditorRevision;
        vm.DraftTitle = "Edited title"; var document = NotebookEditingTests.Text("Exact content\n"); vm.SetDraftDocument(document);
        await vm.AddLinkCommand.ExecuteAsync(null);
        Assert.Equal(2, store.Writes); Assert.False(vm.IsDirty); Assert.Equal("Edited title", vm.Note!.Title);
        Assert.Same(document, vm.DraftDocument); Assert.Equal(revision, vm.EditorRevision);
        Assert.True(Assert.Single(vm.LinkTargets).IsLinked); Assert.False(vm.AddLinkCommand.CanExecute(null));
        var linked = Assert.Single(vm.LinkedItems); Assert.Equal(Target, linked.Link.TargetId); Assert.Equal(NotebookTargetKind.TradingSetup, linked.Link.Kind);
        await vm.RemoveLinkedItemCommand.ExecuteAsync(linked);
        Assert.Empty(vm.Note.Links); Assert.Empty(vm.LinkedItems); Assert.Equal(3, store.Writes); Assert.Equal(revision, vm.EditorRevision);
    }
    [Theory]
    [InlineData(NotebookWriteStatus.Conflict)] [InlineData(NotebookWriteStatus.Cancelled)] [InlineData(NotebookWriteStatus.StorageFailure)]
    [InlineData(NotebookWriteStatus.UnavailableTarget)]
    public async Task FailedMutationPreservesDraftAndNeverSubstitutesAnotherRecord(NotebookWriteStatus status)
    {
        var store = new NotebookFake(); using var vm = await Open(store); await Pick(vm);
        store.NextWrite = status; var original = vm.Note; var editor = vm.EditorRevision;
        await vm.AddLinkCommand.ExecuteAsync(null);
        Assert.Same(original, vm.Note); Assert.Empty(vm.LinkedItems); Assert.Equal(editor, vm.EditorRevision); Assert.NotEmpty(vm.LinkMessage);
    }
    [Fact]
    public async Task InFlightAutosaveDrainsAndNewerTypingFlushesBeforeCapturedLinkMutation()
    {
        var store = new NotebookFake(); var recovery = new NotebookRecoveryFake(); var clock = new NotebookClock();
        using var vm = await Open(store, recovery: recovery, time: clock); await Pick(vm);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.OnWrite = (command, _) => { entered.TrySetResult(); return finish.Task; };
        vm.DraftTitle = "First"; clock.Advance(750); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        vm.DraftTitle = "Newer";
        var add = vm.AddLinkCommand.ExecuteAsync(null); Assert.False(await vm.PrepareLeaveAsync());
        var persisted = store.Notes[0] with { Title = "First", Version = 2 }; store.Notes[0] = persisted;
        finish.SetResult(new(NotebookWriteStatus.Success, persisted));
        store.OnWrite = null; await add;
        Assert.Equal("Newer", vm.DraftTitle); Assert.Equal("Newer", vm.Note!.Title); Assert.Single(vm.Note.Links);
        Assert.Equal(Target, vm.Note.Links[0].TargetId); Assert.False(vm.IsDirty);
    }
    [Fact]
    public async Task FailedSaveAndCancelledConsentKeepUnsavedDraftAndDoNotAddLinks()
    {
        var store = new NotebookFake(); var choices = new NotebookChoices { Choice = NotebookLeaveChoice.Cancel };
        using var vm = await Open(store, choices); await Pick(vm); vm.DraftTitle = "Retained";
        await vm.AddLinkCommand.ExecuteAsync(null); Assert.True(vm.IsDirty); Assert.Equal(0, store.Writes);
        choices.Choice = NotebookLeaveChoice.Save; store.NextWrite = NotebookWriteStatus.StorageFailure;
        await vm.AddLinkCommand.ExecuteAsync(null); Assert.Equal("Retained", vm.DraftTitle); Assert.True(vm.IsDirty); Assert.Empty(vm.Note!.Links);
    }
    [Fact]
    public async Task LabelsAndUnavailableStatesRefreshWithoutDirtyingOrRemovingReferences()
    {
        var store = new NotebookFake(); using var vm = await Open(store); await Pick(vm); await vm.AddLinkCommand.ExecuteAsync(null);
        long version = vm.Note!.Version, editor = vm.EditorRevision; int writes = store.Writes;
        store.OnResolve = (links, _) => Task.FromResult<ImmutableArray<NotebookResolvedLink>>([.. links.Select(x => new NotebookResolvedLink(x, NotebookTargetAvailability.Available, "Renamed", true))]);
        await vm.RefreshLinkedItemsCommand.ExecuteAsync(null); Assert.Equal("Renamed", vm.LinkedItems[0].Label); Assert.Contains("Inactive", vm.LinkedItems[0].Context);
        store.OnResolve = (links, _) => Task.FromResult<ImmutableArray<NotebookResolvedLink>>([.. links.Select(x => new NotebookResolvedLink(x, NotebookTargetAvailability.Unavailable, x.LastKnownLabel))]);
        await vm.RefreshLinkedItemsCommand.ExecuteAsync(null); var unavailable = Assert.Single(vm.LinkedItems);
        Assert.False(vm.OpenLinkedItemCommand.CanExecute(unavailable)); Assert.True(vm.RemoveLinkedItemCommand.CanExecute(unavailable));
        Assert.Equal(version, vm.Note.Version); Assert.Equal(editor, vm.EditorRevision); Assert.Equal(writes, store.Writes); Assert.False(vm.IsDirty);
    }
    [Fact]
    public async Task DisposalAndRepeatedActivationCannotPublishOldPickerResultsOrOverwriteRecoveryDraft()
    {
        var store = new NotebookFake(); using var vm = await Open(store); vm.DraftTitle = "Recovery retained";
        vm.Deactivate(); await vm.DeactivationTask; await vm.ActivateAsync(); await vm.ActivateAsync();
        Assert.Equal("Recovery retained", vm.DraftTitle); Assert.True(vm.IsDirty);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<NotebookTargetPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.OnTargets = (_, _) => { entered.TrySetResult(); return result.Task; }; vm.LinkPickerOpen = true; var pending = vm.LinkSearchTask;
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); vm.Dispose(); result.SetResult(new([Choice], 1, null)); await pending;
        Assert.Empty(vm.LinkTargets); Assert.Equal("Recovery retained", vm.DraftTitle);
    }
    [Fact]
    public async Task LateNavigationResolutionCannotOpenAnotherSelectedNoteOrDisposedWorkspace()
    {
        var store = new NotebookFake(); using var vm = await Open(store);
        store.Notes.Add(NotebookFake.Note("Other") with { Document = NotebookEditingTests.Text("Other content") });
        await Pick(vm); await vm.AddLinkCommand.ExecuteAsync(null); // Mutation refreshes the list containing both notes.
        var pending = new TaskCompletionSource<ImmutableArray<NotebookResolvedLink>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.OnResolve = (links, _) => { entered.TrySetResult(); return pending.Task; }; int opens = 0;
        vm.OpenLinkedTarget = (_, _) => { opens++; return Task.FromResult(true); };
        var item = vm.LinkedItems[0]; var navigation = vm.OpenLinkedItemCommand.ExecuteAsync(item); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        vm.SelectedNote = vm.Notes.Single(n => n.Title == "Other");
        pending.SetResult([new(item.Link, NotebookTargetAvailability.Available, "Study")]); await navigation; await vm.LoadTask;
        Assert.Equal(0, opens); Assert.Equal("Other", vm.DraftTitle);
    }
    [Fact]
    public async Task LateMetadataAcknowledgementAfterDisposalCannotReplaceTheEditor()
    {
        var store = new NotebookFake(); using var vm = await Open(store); await Pick(vm);
        var original = vm.Note!;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.OnWrite = (_, _) => { entered.TrySetResult(); return result.Task; };
        var add = vm.AddLinkCommand.ExecuteAsync(null); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); vm.Dispose();
        result.SetResult(new(NotebookWriteStatus.Success, original with { Version = original.Version + 1, Links = [new(Choice.Kind, Choice.Id, Choice.Label)] }));
        await add; Assert.Same(original, vm.Note); Assert.Empty(vm.LinkedItems);
    }
}
