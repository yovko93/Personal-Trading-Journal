using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookNotificationTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly NotebookFake Data = new();
        internal readonly NotebookClock Clock = new();
        internal readonly NotebookRecoveryFake Recovery = new();
        internal NotebookWorkspaceViewModel Vm = null!;
        internal static async Task<Fixture> Open()
        {
            var f = new Fixture(); f.Data.Notes.Add(NotebookFake.Note("Original"));
            f.Vm = new(f.Data, new FakeDialogService(), new NotebookChoices(), new NotebookJsonCodec(), f.Recovery, f.Clock);
            await f.Vm.ActivateAsync(); f.Vm.SelectedNote = f.Vm.Notes[0]; await f.Vm.LoadTask;
            return f;
        }
        public void Dispose() => Vm.Dispose();
    }

    [Theory]
    [InlineData(NotebookNotificationKind.Information, "ⓘ")]
    [InlineData(NotebookNotificationKind.Success, "✓")]
    [InlineData(NotebookNotificationKind.Warning, "⚠")]
    [InlineData(NotebookNotificationKind.Error, "!")]
    public async Task ExplicitSeverityExpiresAtTenSecondsWithoutChangingData(NotebookNotificationKind kind, string icon)
    {
        using var f = await Fixture.Open(); var vm = f.Vm; var note = vm.Note;
        vm.ShowNotification("Operation feedback", kind);
        Assert.Equal(icon, vm.Notification!.Icon); Assert.Equal($"{kind}: Operation feedback", vm.Notification.AccessibleText);
        var expiry = vm.NotificationExpiryTask;
        f.Clock.Advance(9999); Assert.NotNull(vm.Notification); Assert.False(expiry.IsCompleted);
        // Reading bindings is not another operation event.
        Assert.Equal("Operation feedback", vm.Notification.Text); Assert.Same(expiry, vm.NotificationExpiryTask);
        f.Clock.Advance(1); await expiry.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(vm.Notification); Assert.Same(note, vm.Note); Assert.Equal(0, f.Data.Writes);
    }

    [Fact]
    public async Task ReplacementIncludingIdenticalTextGetsFullIntervalAndInvalidatesOldTimer()
    {
        using var f = await Fixture.Open(); var vm = f.Vm;
        vm.ShowNotification("Same event text", NotebookNotificationKind.Information); var old = vm.NotificationExpiryTask;
        f.Clock.Advance(6000); vm.ShowNotification("Same event text", NotebookNotificationKind.Information);
        var current = vm.NotificationExpiryTask; Assert.NotSame(old, current); await old;
        f.Clock.Advance(4000); Assert.NotNull(vm.Notification); Assert.False(current.IsCompleted);
        f.Clock.Advance(5999); Assert.NotNull(vm.Notification);
        f.Clock.Advance(1); await current; Assert.Null(vm.Notification);
    }

    [Fact]
    public async Task AlreadyQueuedExpiryCannotDismissItsReplacement()
    {
        using var f = await Fixture.Open(); var vm = f.Vm;
        vm.ShowNotification("Old", NotebookNotificationKind.Warning); var old = vm.NotificationExpiryTask;
        f.Clock.Advance(10000); // The expiry continuation may already be queued on this context.
        vm.ShowNotification("New", NotebookNotificationKind.Success); await old;
        Assert.Equal("New", vm.Notification!.Text);
        f.Clock.Advance(9999); Assert.Equal("New", vm.Notification.Text);
        f.Clock.Advance(1); await vm.NotificationExpiryTask; Assert.Null(vm.Notification);
    }

    [Theory]
    [InlineData(NotebookWriteStatus.Conflict, NotebookNotificationKind.Warning)]
    [InlineData(NotebookWriteStatus.StorageFailure, NotebookNotificationKind.Error)]
    public async Task FailedCanonicalSaveRemainsActionableAfterAutomaticAndManualDismissal(NotebookWriteStatus status, NotebookNotificationKind kind)
    {
        using var f = await Fixture.Open(); var vm = f.Vm; var original = vm.Note;
        f.Data.NextWrite = status; vm.DraftTitle = "Retained input";
        Assert.False(await vm.SaveDraftAsync()); Assert.Equal(kind, vm.Notification!.Kind);
        var detail = vm.SaveFeedback; var recoveryMessage = vm.RecoveryMessage; var saveState = vm.SaveState;
        Assert.NotEmpty(detail); Assert.NotEmpty(f.Recovery.Drafts); Assert.True(vm.IsDirty);
        f.Clock.Advance(10000); await vm.NotificationExpiryTask;
        Assert.Null(vm.Notification); Assert.Equal(detail, vm.SaveFeedback); Assert.Equal(saveState, vm.SaveState);
        Assert.Equal(recoveryMessage, vm.RecoveryMessage); Assert.Same(original, vm.Note);
        Assert.True(vm.SaveCommand.CanExecute(null)); Assert.True(vm.DiscardCommand.CanExecute(null));
        vm.ShowNotification("Other feedback", NotebookNotificationKind.Information); var timer = vm.NotificationExpiryTask;
        vm.DismissNotificationCommand.Execute(null); await timer;
        Assert.Null(vm.Notification); Assert.Equal(detail, vm.SaveFeedback); Assert.Equal("Retained input", vm.DraftTitle);
        Assert.NotEmpty(f.Recovery.Drafts); Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task FieldValidationSurvivesDismissalAndCanonicalSaveDoesNotToast()
    {
        using var f = await Fixture.Open(); var vm = f.Vm;
        vm.DraftTitle = " "; Assert.False(await vm.SaveDraftAsync()); Assert.Contains("nonblank title", vm.SaveFeedback);
        Assert.Null(vm.Notification);
        vm.ShowNotification("Information", NotebookNotificationKind.Information); vm.DismissNotificationCommand.Execute(null);
        Assert.Contains("nonblank title", vm.SaveFeedback); Assert.Equal(" ", vm.DraftTitle);
        vm.DraftTitle = "Saved title"; Assert.True(await vm.SaveDraftAsync()); Assert.Empty(vm.SaveFeedback); Assert.Null(vm.Notification);
        vm.DraftTitle = "Autosaved title"; f.Clock.Advance(750); await vm.AutosaveTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(vm.IsDirty); Assert.Equal("Saved locally", vm.CompactSaveState); Assert.Null(vm.Notification);
        Assert.True(vm.BeginFolderCreation()); vm.FolderName = new string('x', 129);
        await vm.CreateFolderCommand.ExecuteAsync(null); Assert.NotEmpty(vm.FolderCreationMessage);
        var error = vm.FolderCreationMessage; vm.DismissNotificationCommand.Execute(null);
        Assert.Equal(error, vm.FolderCreationMessage); Assert.Equal(new string('x', 129), vm.FolderName);
    }

    [Fact]
    public async Task OperationSuccessFailureAndCancelledReadHaveExplicitSeverity()
    {
        using var f = await Fixture.Open(); var vm = f.Vm;
        await vm.NewNoteCommand.ExecuteAsync(null); Assert.Equal(NotebookNotificationKind.Success, vm.Notification!.Kind);
        Assert.Equal("Note created.", vm.Notification.Text);
        f.Data.OnList = (_, _) => throw new IOException("synthetic");
        await vm.RefreshCommand.ExecuteAsync(null); Assert.Equal(NotebookNotificationKind.Error, vm.Notification!.Kind);
        Assert.Contains("could not be loaded", vm.Notification.Text);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Data.OnList = async (_, ct) => { entered.SetResult(); await Task.Delay(Timeout.Infinite, ct); throw new InvalidOperationException(); };
        var pending = vm.RefreshCommand.ExecuteAsync(null); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await vm.CancelCommand.ExecuteAsync(null); await pending;
        Assert.Equal(NotebookNotificationKind.Warning, vm.Notification!.Kind); Assert.Contains("already be saved", vm.Notification.Text);
    }

    [Fact]
    public async Task DeactivationAndDisposalInvalidateTimersAndLateOperationFeedback()
    {
        using var f = await Fixture.Open(); var vm = f.Vm;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<NotebookWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Data.OnWrite = (_, _) => { entered.SetResult(); return release.Task; };
        var pending = vm.NewNoteCommand.ExecuteAsync(null); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        vm.ShowNotification("Pending", NotebookNotificationKind.Information); var timer = vm.NotificationExpiryTask;
        vm.Deactivate(); release.SetException(new IOException("late synthetic failure")); await pending; await vm.DeactivationTask; await timer;
        Assert.Null(vm.Notification);
        f.Data.OnWrite = null; await vm.ActivateAsync(); Assert.Null(vm.Notification);
        vm.ShowNotification("New activation", NotebookNotificationKind.Information); timer = vm.NotificationExpiryTask;
        vm.Dispose(); await timer; f.Clock.Advance(10000);
        vm.ShowNotification("After disposal", NotebookNotificationKind.Error); Assert.Null(vm.Notification);
    }

    [Fact]
    public async Task AcceptedSelectionClearsPreviousNotesFeedbackButCancelledNavigationRetainsDraft()
    {
        using var f = await Fixture.Open(); var vm = f.Vm;
        f.Data.Notes.Add(NotebookFake.Note("Other")); await vm.RefreshCommand.ExecuteAsync(null);
        var current = vm.Note!.Id; var other = vm.Notes.Single(x => x.Id != current);
        vm.ShowNotification("Old note feedback", NotebookNotificationKind.Success);
        vm.SelectedNote = other; await vm.LoadTask; Assert.Null(vm.Notification); Assert.Equal(other.Id, vm.Note!.Id);
        vm.DraftTitle = "Keep me"; f.Data.NextWrite = NotebookWriteStatus.Conflict;
        vm.SelectedNote = vm.Notes.Single(x => x.Id == current); await vm.LoadTask;
        Assert.Equal(other.Id, vm.Note!.Id); Assert.Equal("Keep me", vm.DraftTitle); Assert.True(vm.IsDirty);
        Assert.Equal(NotebookNotificationKind.Warning, vm.Notification!.Kind);
    }
}
