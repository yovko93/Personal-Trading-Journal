using PersonalTradingJournal.Application.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed partial class JournalViewModelTests
{
    [Fact]
    public async Task CancellationTooLateToUndoDeleteStillPublishesCommitOnce()
    {
        var repository = new Repository { Journal = Details("saved", draft: false, review: new("Well", "Improve", "Next")) };
        var vm = Create(repository, dialogs: new Dialogs { ConfirmResult = true });
        await vm.ActivateAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.DeleteBehavior = async (_, _) =>
        {
            started.SetResult();
            await release.Task;
            return new(DailyJournalWriteStatus.Deleted, null); // Already committed, despite late cancellation.
        };
        int commits = 0;
        vm.JournalDataCommitted += (_, _) => commits++;
        var deletion = vm.DeleteCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        vm.CancelOperationCommand.Execute(null);
        release.SetResult();
        await deletion;
        Assert.Equal(1, commits);
        Assert.False(vm.IsExisting);
        Assert.Null(vm.ErrorMessage);
    }

    [Theory]
    [InlineData(DailyJournalWriteStatus.Deleted)]
    [InlineData(DailyJournalWriteStatus.Conflict)]
    [InlineData(DailyJournalWriteStatus.NotFound)]
    public async Task DeletionRequiresExactConfirmationAndPublishesOnlyCommittedRemoval(DailyJournalWriteStatus status)
    {
        var repository = new Repository { Journal = Details("saved") };
        var entry = repository.Journal.Entry;
        var dialogs = new Dialogs();
        var vm = Create(repository, dialogs: dialogs);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        vm.Text = "local edits";
        int calls = 0, commits = 0;
        repository.DeleteBehavior = (command, _) =>
        {
            calls++;
            Assert.Equal(entry.Id, command.JournalId);
            Assert.Equal(entry.Revision, command.ExpectedRevision);
            return Task.FromResult(new DailyJournalWriteResult(status, null));
        };
        vm.JournalDataCommitted += (_, _) => commits++;
        await vm.DeleteCommand.ExecuteAsync(null);
        Assert.Equal(0, calls);
        Assert.Equal("local edits", vm.Text);
        Assert.True(vm.IsEditorOpen);
        var confirmation = Assert.Single(dialogs.Requests);
        Assert.Contains("2026-09-09", confirmation.Message);
        Assert.Contains("All accounts", confirmation.Message);
        Assert.Contains("ALL revision history", confirmation.Message);
        Assert.Contains("permanently", confirmation.Message);
        Assert.True(confirmation.IsDestructive);
        dialogs.ConfirmResult = true;
        await vm.DeleteCommand.ExecuteAsync(null);
        Assert.Equal(1, calls);
        Assert.Equal(status == DailyJournalWriteStatus.Deleted ? 1 : 0, commits);
        Assert.Null(vm.SelectedAccount.Id);
        Assert.Equal(Day.ToDateTime(TimeOnly.MinValue), vm.SelectedDate);
        if (status == DailyJournalWriteStatus.Deleted)
        {
            Assert.False(vm.IsExisting);
            Assert.False(vm.IsEditorOpen);
            Assert.False(vm.IsDirty);
            Assert.Equal("No entry", vm.EntryStateLabel);
            Assert.True(vm.OpenEditorCommand.CanExecute(null));
        }
        else
        {
            Assert.True(vm.IsEditorOpen && vm.IsDirty);
            Assert.Equal("local edits", vm.Text);
            Assert.NotNull(vm.ErrorMessage);
            Assert.False(vm.DeleteCommand.CanExecute(null));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrCancelledDeletionRetainsDraftAndBlocksOverlappingOperations(bool cancel)
    {
        var repository = new Repository { Journal = Details("saved") };
        var vm = Create(repository, dialogs: new Dialogs { ConfirmResult = true });
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        vm.WentWell = "Keep answer";
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.DeleteBehavior = async (_, ct) =>
        {
            started.SetResult();
            await release.Task;
            ct.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Synthetic failure");
        };
        int commits = 0;
        vm.JournalDataCommitted += (_, _) => commits++;
        var deletion = vm.DeleteCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.False(vm.DeleteCommand.CanExecute(null));
        Assert.False(vm.TryLeave());
        if (cancel) vm.CancelOperationCommand.Execute(null);
        release.SetResult();
        await deletion;
        Assert.True(vm.IsExisting && vm.IsEditorOpen && vm.IsDirty);
        Assert.Equal("Keep answer", vm.WentWell);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Equal(0, commits);
        Assert.True(vm.DeleteCommand.CanExecute(null));
    }
}
