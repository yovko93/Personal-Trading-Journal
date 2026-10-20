using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

internal sealed class NotebookClock : TimeProvider
{
    private long _milliseconds;
    private readonly List<Timer> _timers = [];
    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero).AddMilliseconds(_milliseconds);
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_timers)
        {
            var timer = new Timer(callback, state, _milliseconds + (long)dueTime.TotalMilliseconds);
            _timers.Add(timer); return timer;
        }
    }
    internal void Advance(int milliseconds)
    {
        Timer[] due;
        lock (_timers)
        {
            _milliseconds += milliseconds;
            due = _timers.Where(x => !x.Disposed && x.Due <= _milliseconds).ToArray();
            foreach (var timer in due) timer.Dispose();
        }
        foreach (var timer in due) timer.Callback(timer.State);
    }
    private sealed class Timer(TimerCallback callback, object? state, long due) : ITimer
    {
        internal readonly TimerCallback Callback = callback;
        internal readonly object? State = state;
        internal readonly long Due = due;
        internal bool Disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
        public void Dispose() => Disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
internal sealed class NotebookRecoveryFake : INotebookRecoveryStore
{
    internal readonly Dictionary<Guid, NotebookRecoveryDraft> Drafts = [];
    internal NotebookWriteStatus PutStatus = NotebookWriteStatus.Success, RemoveStatus = NotebookWriteStatus.Success;
    internal NotebookLoadStatus LoadStatus = NotebookLoadStatus.Success;
    internal int Puts, Removes;
    public Task<NotebookRecoveryPage> ListAsync(Guid? after = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new NotebookRecoveryPage([.. Drafts.Values.Select(x => new NotebookRecoveryItem(x.Id, x.NoteId, x.Revision, x.Title, x.CheckpointedAtUtc))], null));
    public Task<NotebookRecoveryLoad> LoadAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(new NotebookRecoveryLoad(LoadStatus, LoadStatus == NotebookLoadStatus.Success ? Drafts.GetValueOrDefault(id) : null));
    public Task<NotebookWriteStatus> PutAsync(NotebookRecoveryDraft draft, long expectedRevision, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Puts);
        if (PutStatus != NotebookWriteStatus.Success) return Task.FromResult(PutStatus);
        if ((Drafts.GetValueOrDefault(draft.Id)?.Revision ?? 0) != expectedRevision) return Task.FromResult(NotebookWriteStatus.Conflict);
        Drafts[draft.Id] = draft; return Task.FromResult(NotebookWriteStatus.Success);
    }
    public Task<NotebookWriteStatus> RemoveAsync(Guid id, long expectedRevision, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Removes);
        if (RemoveStatus != NotebookWriteStatus.Success) return Task.FromResult(RemoveStatus);
        if (Drafts.TryGetValue(id, out var draft) && draft.Revision != expectedRevision) return Task.FromResult(NotebookWriteStatus.Conflict);
        Drafts.Remove(id); return Task.FromResult(NotebookWriteStatus.Success);
    }
}
public sealed class NotebookAutosaveTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        internal readonly NotebookFake Data = new();
        internal readonly NotebookRecoveryFake Recovery = new();
        internal readonly NotebookClock Clock = new();
        internal readonly NotebookChoices Choices = new();
        internal NotebookWorkspaceViewModel Vm = null!;
        internal static async Task<Fixture> Open()
        {
            var f = new Fixture();
            f.Data.Notes.Add(NotebookFake.Note("Original") with { Document = NotebookEditingTests.Text("Original body") });
            f.Vm = f.NewWorkspace();
            await f.Vm.ActivateAsync(); f.Vm.SelectedNote = f.Vm.Notes[0]; await f.Vm.LoadTask; return f;
        }
        internal NotebookWorkspaceViewModel NewWorkspace() => new(Data, new FakeDialogService { ConfirmationResult = true }, Choices, new NotebookJsonCodec(), Recovery, Clock);
        internal async Task Quiet()
        {
            Clock.Advance(750);
            await Vm.AutosaveTask.WaitAsync(TimeSpan.FromSeconds(10));
        }
        public async ValueTask DisposeAsync()
        {
            Vm.Dispose(); await Vm.DeactivationTask.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(Vm.IsBusy); Assert.False(Vm.IsSaving);
        }
    }
    [Fact]
    public async Task QuietWindowReplacesTypingAndUndoCancelsPendingWork()
    {
        await using var f = await Fixture.Open(); var vm = f.Vm;
        f.Clock.Advance(5000); Assert.Equal(0, f.Data.Writes); Assert.Equal(0, f.Recovery.Puts);
        vm.DraftTitle = "First"; var old = vm.AutosaveTask;
        f.Clock.Advance(749); Assert.Equal(0, f.Data.Writes);
        vm.DraftTitle = "Final"; await old.WaitAsync(TimeSpan.FromSeconds(10));
        f.Clock.Advance(1); Assert.Equal(0, f.Data.Writes);
        await f.Quiet();
        Assert.Equal(1, f.Data.Writes); Assert.Equal("Final", vm.Note!.Title); Assert.Equal("Saved", vm.SaveState);
        Assert.Empty(f.Recovery.Drafts);
        vm.DraftTitle = "Undo this"; var pending = vm.AutosaveTask;
        vm.DraftTitle = "Final"; await pending.WaitAsync(TimeSpan.FromSeconds(10));
        await f.Quiet(); Assert.Equal(1, f.Data.Writes); Assert.False(vm.IsDirty);
    }
    [Fact]
    public async Task ManualSaveSupersedesTimerAndFlushAllowsNavigationWithoutPrompt()
    {
        await using var f = await Fixture.Open(); var vm = f.Vm;
        vm.DraftTitle = "Manual"; Assert.True(await vm.SaveDraftAsync()); await f.Quiet();
        Assert.Equal(1, f.Data.Writes);
        vm.DraftTitle = "Departure"; Assert.True(await vm.PrepareLeaveAsync()); vm.CompleteLeave();
        Assert.Equal(2, f.Data.Writes); Assert.Equal(0, f.Choices.Prompts); Assert.False(vm.IsDirty);
        vm.Deactivate(); await vm.DeactivationTask;
        await vm.ActivateAsync(); Assert.Equal("Departure", vm.Note!.Title);
    }
    [Fact]
    public async Task EditsDuringSaveKeepNewDraftAndUseReturnedVersionWithoutOverlappingWrites()
    {
        await using var f = await Fixture.Open(); var vm = f.Vm;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new List<long>();
        f.Data.OnWrite = async (c, _) =>
        {
            var save = Assert.IsType<SaveNotebookDraft>(c);
            expected.Add(save.ExpectedVersion);
            entered.TrySetResult(); await release.Task;
            var n = f.Data.Notes[0] with { Title = save.Title, Document = save.Document, Version = save.ExpectedVersion + 1 };
            f.Data.Notes[0] = n; return new(NotebookWriteStatus.Success, n);
        };
        vm.DraftTitle = "Older"; var saveTask = vm.SaveDraftAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            vm.DraftTitle = "Newer"; vm.SetDraftDocument(NotebookEditingTests.Text("Newer body"));
            Assert.True(vm.IsSaving); Assert.Equal(1, f.Data.Writes);
            release.TrySetResult(); Assert.False(await saveTask.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal("Newer", vm.DraftTitle); Assert.True(vm.IsDirty); Assert.NotEqual("Saved", vm.SaveState);
            await f.Quiet();
            Assert.Equal(new long[] { 1, 2 }, expected); Assert.Equal(2, f.Data.Writes);
            Assert.False(vm.IsDirty); Assert.Contains("Newer body", vm.Note!.Document.PlainText);
        }
        finally { release.TrySetResult(); await saveTask.WaitAsync(TimeSpan.FromSeconds(10)); }
    }
    [Theory]
    [InlineData(NotebookWriteStatus.StorageFailure)]
    [InlineData(NotebookWriteStatus.Conflict)]
    public async Task FailedAutosaveCheckpointsFurtherEditsButNeverRetriesCanonicalUntilSave(NotebookWriteStatus failure)
    {
        await using var f = await Fixture.Open(); var vm = f.Vm;
        f.Data.NextWrite = failure; vm.DraftTitle = "Recover me"; await f.Quiet();
        Assert.True(vm.IsDirty); Assert.NotEqual("Saved", vm.SaveState); Assert.Single(f.Recovery.Drafts);
        await f.Quiet(); Assert.Equal(1, f.Data.Writes);
        vm.DraftTitle = "Latest recoverable"; await f.Quiet();
        Assert.Equal(1, f.Data.Writes); Assert.Equal("Latest recoverable", Assert.Single(f.Recovery.Drafts).Value.Title);
        Assert.False(await vm.PrepareLeaveAsync()); Assert.Equal(1, f.Choices.Prompts);
        Assert.Equal("Latest recoverable", vm.DraftTitle);
        f.Data.NextWrite = NotebookWriteStatus.Success;
        Assert.True(await vm.SaveDraftAsync()); Assert.Equal(2, f.Data.Writes); Assert.Empty(f.Recovery.Drafts);
    }
    [Fact]
    public async Task ExplicitDiscardDrainsPendingWorkAndCannotResurrectIt()
    {
        await using var f = await Fixture.Open(); var vm = f.Vm;
        f.Data.NextWrite = NotebookWriteStatus.StorageFailure; vm.DraftTitle = "Failure"; await f.Quiet();
        vm.DraftTitle = "Pending"; var pending = vm.AutosaveTask;
        await vm.DiscardCommand.ExecuteAsync(null); await pending.WaitAsync(TimeSpan.FromSeconds(10));
        await f.Quiet();
        Assert.False(vm.IsDirty); Assert.Equal("Original", vm.DraftTitle); Assert.Empty(f.Recovery.Drafts); Assert.Equal(1, f.Data.Writes);
    }
    [Fact]
    public async Task InvalidTitleIsCheckpointedButNotSavedAndRequiresExplicitRetry()
    {
        await using var f = await Fixture.Open(); var vm = f.Vm;
        vm.DraftTitle = ""; await f.Quiet();
        Assert.Equal(0, f.Data.Writes); Assert.Equal("", Assert.Single(f.Recovery.Drafts).Value.Title);
        vm.DraftTitle = "Valid"; await f.Quiet(); Assert.Equal(0, f.Data.Writes);
        Assert.True(await vm.SaveDraftAsync()); Assert.Equal("Valid", vm.Note!.Title);
    }
    [Fact]
    public async Task FailedCheckpointBlocksCanonicalAndFailedCleanupDoesNotDenyCommittedSave()
    {
        await using var f = await Fixture.Open(); var vm = f.Vm;
        f.Recovery.PutStatus = NotebookWriteStatus.StorageFailure;
        vm.DraftTitle = "Retained"; await f.Quiet();
        Assert.True(vm.IsDirty); Assert.Equal(0, f.Data.Writes); Assert.Contains("failed", vm.RecoveryMessage);
        await f.Quiet(); Assert.Equal(1, f.Recovery.Puts);
        f.Recovery.PutStatus = NotebookWriteStatus.Success; f.Recovery.RemoveStatus = NotebookWriteStatus.StorageFailure;
        Assert.True(await vm.SaveDraftAsync()); Assert.Equal("Saved", vm.SaveState);
        Assert.Single(f.Recovery.Drafts); Assert.Equal(1, f.Data.Writes);
        vm.Deactivate(); await vm.DeactivationTask; f.Recovery.RemoveStatus = NotebookWriteStatus.Success;
        using var fresh = f.NewWorkspace();
        await fresh.ActivateAsync(); await fresh.RecoverCommand.ExecuteAsync(Assert.Single(fresh.RecoveryDrafts));
        Assert.False(fresh.IsDirty); Assert.Equal("Saved", fresh.SaveState); Assert.Empty(f.Recovery.Drafts);
        Assert.Equal(1, f.Data.Writes);
    }
    [Fact]
    public async Task DirtyMetadataMutationFlushesFirstAndImageReferenceBodyRemainsEditable()
    {
        await using var f = await Fixture.Open(); var vm = f.Vm;
        vm.DraftTitle = "Before pin"; await vm.PinCommand.ExecuteAsync(null);
        Assert.True(vm.Note!.IsPinned); Assert.Equal(3, vm.Note.Version);
        Assert.Equal("Before pin", vm.Note.Title); Assert.Equal(2, f.Data.Writes); Assert.Equal(0, f.Choices.Prompts);
        var image = NotebookNode.Image(Guid.NewGuid(), Guid.NewGuid(), "Caption", "Alt");
        f.Data.Notes[0] = f.Data.Notes[0] with { Document = new([image]) }; await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.CanEditBody); Assert.True(vm.CanEditDraft);
        vm.DraftTitle = "Image title"; await f.Quiet();
        Assert.Same(image, vm.Note!.Document.ImageNodes.Single()); Assert.Equal("Image title", vm.Note.Title);
    }
    [Fact]
    public async Task LaterAncestorVetoRetainsDraftAndRecheckpointsWithoutRetryingFailure()
    {
        await using var f = await Fixture.Open(); var vm = f.Vm;
        f.Data.NextWrite = NotebookWriteStatus.StorageFailure;
        vm.DraftTitle = "Keep after veto"; await f.Quiet();
        f.Choices.Choice = NotebookLeaveChoice.Discard;
        Assert.True(await vm.PrepareLeaveAsync()); Assert.True(vm.IsDirty); Assert.Empty(f.Recovery.Drafts);
        vm.ClearLeaveConsent(); Assert.False(vm.TryLeave());
        await f.Quiet(); Assert.Equal("Keep after veto", Assert.Single(f.Recovery.Drafts).Value.Title);
        Assert.Equal(1, f.Data.Writes);
        Assert.True(await vm.PrepareLeaveAsync()); vm.CompleteLeave(); await f.Quiet();
        Assert.False(vm.IsDirty); Assert.Empty(f.Recovery.Drafts);
    }
    [Fact]
    public async Task CancellationAfterCommitAcknowledgesSavedVersionWithoutLateRetry()
    {
        await using var f = await Fixture.Open(); var vm = f.Vm;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Data.OnWrite = async (c, _) =>
        {
            var d = Assert.IsType<SaveNotebookDraft>(c); entered.TrySetResult(); await release.Task;
            var n = f.Data.Notes[0] with { Title = d.Title, Document = d.Document, Version = d.ExpectedVersion + 1 };
            f.Data.Notes[0] = n; return new(NotebookWriteStatus.Success, n);
        };
        vm.DraftTitle = "Committed"; var save = vm.SaveDraftAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); var cancel = vm.CancelAsync();
            release.TrySetResult(); await save.WaitAsync(TimeSpan.FromSeconds(10)); await cancel.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("Committed", vm.Note!.Title); Assert.False(vm.IsDirty); Assert.Equal("Saved", vm.SaveState);
            Assert.Single(f.Recovery.Drafts); // Cleanup can be cancelled; next recovery compares canonical content.
            await f.Quiet(); Assert.Equal(1, f.Data.Writes);
        }
        finally { release.TrySetResult(); await save.WaitAsync(TimeSpan.FromSeconds(10)); }
    }
    [Fact]
    public async Task DisposeCancelsPendingTimerAndDrainsAnEnteredWriteBeforeFreshActivation()
    {
        await using var f = await Fixture.Open(); var vm = f.Vm;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Data.OnWrite = async (_, ct) =>
        {
            entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, ct); return new(NotebookWriteStatus.Success); }
            finally { ended.TrySetResult(); }
        };
        vm.DraftTitle = "Keep checkpoint"; var save = vm.SaveDraftAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            vm.Dispose(); await vm.DeactivationTask.WaitAsync(TimeSpan.FromSeconds(10)); await save;
            Assert.True(ended.Task.IsCompleted); Assert.Single(f.Recovery.Drafts);
            f.Clock.Advance(10000); Assert.Equal(1, f.Data.Writes); Assert.Equal("Original", vm.Note!.Title);
            using var fresh = f.NewWorkspace(); await fresh.ActivateAsync();
            Assert.Single(fresh.RecoveryDrafts);
        }
        finally { vm.Dispose(); await vm.DeactivationTask.WaitAsync(TimeSpan.FromSeconds(10)); }
    }
    [Theory]
    [InlineData("unchanged")]
    [InlineData("newer")]
    [InlineData("deleted")]
    [InlineData("archived")]
    [InlineData("saved")]
    public async Task FreshWorkspaceComparesRecoveryWithoutOverwritingSources(string state)
    {
        await using var f = await Fixture.Open(); var original = f.Vm.Note!;
        var draft = new NotebookRecoveryDraft(Guid.NewGuid(), original.Id, original.Version, 10, "Recovered", NotebookEditingTests.Text("Recovered body"), f.Clock.GetUtcNow());
        f.Recovery.Drafts.Add(draft.Id, draft);
        if (state == "newer") f.Data.Notes[0] = original with { Version = 2 };
        if (state == "deleted") f.Data.Notes.Clear();
        if (state == "archived") f.Data.Notes[0] = original with { State = NotebookNoteState.Archived, Version = 2 };
        if (state == "saved") f.Data.Notes[0] = original with { Title = draft.Title, Document = draft.Document, Version = 2 };
        using var vm = f.NewWorkspace(); await vm.ActivateAsync();
        await vm.RecoverCommand.ExecuteAsync(Assert.Single(vm.RecoveryDrafts));
        Assert.Equal(0, f.Data.Writes); f.Clock.Advance(750);
        Assert.Equal(state != "saved", vm.IsDirty);
        if (state == "saved") { Assert.Empty(f.Recovery.Drafts); Assert.Equal("Saved", vm.SaveState); return; }
        Assert.Equal("Recovered", vm.DraftTitle); Assert.Contains("Recovered body", vm.DraftDocument.PlainText);
        Assert.Equal(state == "unchanged", await vm.SaveDraftAsync());
        Assert.Equal(state == "unchanged" ? 1 : 0, f.Data.Writes);
        vm.Dispose(); await vm.DeactivationTask;
    }
    [Theory]
    [InlineData(NotebookLoadStatus.InvalidContent)]
    [InlineData(NotebookLoadStatus.UnsupportedContent)]
    [InlineData(NotebookLoadStatus.TooLarge)]
    public async Task UnsupportedRecoveryIsRetainedUntilExplicitConfirmedDiscard(NotebookLoadStatus status)
    {
        await using var f = await Fixture.Open();
        var draft = new NotebookRecoveryDraft(Guid.NewGuid(), f.Vm.Note!.Id, 1, 3, "Protected", NotebookDocument.Empty, f.Clock.GetUtcNow());
        f.Recovery.Drafts.Add(draft.Id, draft); f.Recovery.LoadStatus = status;
        await f.Vm.RefreshCommand.ExecuteAsync(null);
        await f.Vm.RecoverCommand.ExecuteAsync(Assert.Single(f.Vm.RecoveryDrafts));
        Assert.Single(f.Recovery.Drafts); Assert.Equal(0, f.Data.Writes);
        await f.Vm.DeleteRecoveryCommand.ExecuteAsync(Assert.Single(f.Vm.RecoveryDrafts));
        Assert.Empty(f.Recovery.Drafts); Assert.Equal(0, f.Data.Writes);
    }
}
