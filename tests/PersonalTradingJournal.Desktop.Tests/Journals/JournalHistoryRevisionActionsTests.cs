using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalHistoryRevisionActionsTests
{
    [Fact]
    public async Task RevisionToggleSwitchRefreshAndPagesKeepOnlyTheCurrentSnapshotMarked()
    {
        var reader = new JournalHistoryTestReader();
        var account = Guid.NewGuid();
        for (int i = 0; i < 11; i++) reader.Add(new DateOnly(2026, 10, 7).AddDays(-i), account, 23);
        var vm = new JournalHistoryViewModel(reader, _ => true, new HistoryRepositoryProbe(reader));
        await vm.ActivateAsync(null); await vm.NextCommand.ExecuteAsync(null);
        var review = Assert.Single(vm.Entries); await vm.OpenCommand.ExecuteAsync(review);
        await vm.NextRevisionsCommand.ExecuteAsync(null);
        var first = vm.Revisions[0]; var second = vm.Revisions[1];
        await vm.ViewRevisionCommand.ExecuteAsync(first);
        Assert.Equal("Close revision", first.ActionLabel); Assert.True(first.IsExpanded);
        await vm.ViewRevisionCommand.ExecuteAsync(second);
        Assert.Equal("View revision", first.ActionLabel); Assert.False(first.IsExpanded);
        Assert.Equal("Close revision", second.ActionLabel);
        await vm.ViewRevisionCommand.ExecuteAsync(second);
        Assert.Null(vm.Snapshot); Assert.Same(review, vm.SelectedEntry);
        Assert.Equal("Page 2 · 11 reviews", vm.PageText);
        Assert.Equal("Page 2 · 23 revisions", vm.RevisionPageText);
        Assert.Equal(account, vm.SelectedEntry!.Item.AccountId);
        Assert.All(vm.Revisions, r => Assert.False(r.IsExpanded));
        await vm.ViewRevisionCommand.ExecuteAsync(first);
        await vm.RefreshAsync();
        Assert.Single(vm.Revisions, r => r.IsExpanded);
        Assert.Equal(first.Item.Revision, vm.Snapshot!.Revision);
        await vm.PreviousRevisionsCommand.ExecuteAsync(null);
        Assert.Null(vm.Snapshot); Assert.All(vm.Revisions, r => Assert.False(r.IsExpanded));
        await vm.ViewRevisionCommand.ExecuteAsync(vm.Revisions[0]);
        var active = vm.Revisions[0];
        await vm.OpenCommand.ExecuteAsync(vm.SelectedEntry);
        Assert.Null(vm.Snapshot); Assert.False(active.IsExpanded);
        await vm.PreviousCommand.ExecuteAsync(null);
        Assert.Empty(vm.Revisions); Assert.False(vm.HasRevisionView);
        vm.Deactivate();
    }

    [Fact]
    public async Task ClosingLoadingRevisionCancelsItAndLateSnapshotCannotRestoreCloseLabel()
    {
        var reader = new JournalHistoryTestReader();
        reader.Add(new(2026, 10, 7), revision: 2);
        var vm = new JournalHistoryViewModel(reader, _ => true);
        await vm.ActivateAsync(null); await vm.OpenCommand.ExecuteAsync(vm.Entries[0]);
        var row = vm.Revisions[0];
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<DailyJournalRevision?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        reader.Snapshot = (_, _, ct) => { token = ct; started.SetResult(); return pending.Task; };
        var loading = vm.ViewRevisionCommand.ExecuteAsync(row);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("Close revision", row.ActionLabel);
        await vm.ViewRevisionCommand.ExecuteAsync(row);
        Assert.True(token.IsCancellationRequested); Assert.False(row.IsExpanded);
        pending.SetResult(reader.Snapshots.Single(s => s.Revision == 2));
        await loading;
        Assert.Null(vm.Snapshot); Assert.Equal("View revision", row.ActionLabel);
        Assert.True(vm.HasSelectedEntry);
        vm.Deactivate();
    }

    [Fact]
    public async Task CurrentPreviewCompletesBeforeRevisionDecorationWithoutLosingRows()
    {
        var reader = new JournalHistoryTestReader();
        var item = reader.Add(new(2026, 10, 7), revision: 3);
        var repository = new HistoryRepositoryProbe(reader);
        var current = repository.Details(item.TradingDate, null);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<DailyJournalDetails?>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.Read = (_, _, _) => { started.TrySetResult(); return pending.Task; };
        int revisionReads = 0;
        reader.Revisions = (_, page, _) =>
        {
            Interlocked.Increment(ref revisionReads);
            return Task.FromResult(new JournalHistoryPage<JournalRevisionItem>(reader.Snapshots.OrderByDescending(s => s.Revision)
                .Select(s => new JournalRevisionItem(s.JournalId, s.Revision, s.IsDraft, s.SavedAtUtc)).ToArray(), 3, page, 20));
        };
        var vm = new JournalHistoryViewModel(reader, _ => true, repository);
        await vm.ActivateAsync(null);
        Task open = vm.OpenCommand.ExecuteAsync(vm.Entries[0]);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, revisionReads);
        pending.SetResult(current);
        await open;
        Assert.Equal(1, revisionReads);
        Assert.Equal(3, vm.Revisions.Count);
        Assert.True(vm.Revisions[0].IsCurrent);
        Assert.All(vm.Revisions.Skip(1), r => Assert.False(r.IsCurrent));
        vm.Deactivate();
    }

    [Fact]
    public async Task ExactScopeCurrentPreviewIsBoundedAndDoesNotChangeAggregatePageOrEditor()
    {
        var reader = new JournalHistoryTestReader();
        Guid account = Guid.NewGuid();
        for (int i = 0; i < 20; i++) reader.Add(new DateOnly(2026, 10, 7).AddDays(-i), i % 2 == 0 ? null : account, 3);
        var repository = new HistoryRepositoryProbe(reader);
        int edits = 0;
        var vm = new JournalHistoryViewModel(reader, _ => { edits++; return true; }, repository, new FakeDialogService());
        await vm.ActivateAsync(null);
        await vm.NextCommand.ExecuteAsync(null);
        var rows = vm.Entries;
        var row = rows.First(r => r.Item.AccountId == account);
        var latest = reader.Snapshots.Single(s => s.JournalId == row.Item.Id && s.Revision == 3);
        string longText = new('x', 500);
        reader.Snapshots[reader.Snapshots.IndexOf(latest)] = latest with { Text = longText, Review = new("One\nTwo\nThree\nFour", "", "Next") };
        await vm.OpenCommand.ExecuteAsync(row);
        Assert.Equal((row.Item.TradingDate, account), repository.Reads.Single());
        Assert.Same(rows, vm.Entries);
        Assert.Equal("Page 2 · 20 reviews", vm.PageText);
        Assert.Equal(0, edits);
        Assert.Equal(3, vm.Previews.Count);
        Assert.Equal(new string('x', 240) + "…", vm.Previews[0].Text);
        Assert.Equal("One\nTwo\nThree…", vm.Previews[1].Text);
        Assert.Equal("Next", vm.Previews[2].Text);
        await vm.ViewRevisionCommand.ExecuteAsync(vm.Revisions[0]);
        Assert.Equal(longText, vm.Snapshot!.Text);
        vm.CloseReviewCommand.Execute(null);
        Assert.Empty(vm.Previews);
        Assert.Same(rows, vm.Entries);
        vm.Deactivate();
    }

    [Fact]
    public async Task DeleteConfirmationCancelThenCommitReconcilesLastPageAndClosesDeletedSnapshotOnly()
    {
        var reader = new JournalHistoryTestReader();
        var item = reader.Add(new(2026, 10, 7), Guid.NewGuid(), 21);
        reader.Items[0] = item with { AccountName = "P 21" };
        var repository = new HistoryRepositoryProbe(reader);
        var dialogs = new FakeDialogService();
        var vm = new JournalHistoryViewModel(reader, _ => true, repository, dialogs);
        await vm.ActivateAsync(null);
        var list = vm.Entries;
        await vm.OpenCommand.ExecuteAsync(list[0]);
        Assert.False(vm.DeleteRevisionCommand.CanExecute(vm.Revisions[0]));
        Assert.True(vm.Revisions[0].IsCurrent);
        Assert.Contains("protected", vm.Revisions[0].DeleteHelp);
        await vm.NextRevisionsCommand.ExecuteAsync(null);
        var oldest = Assert.Single(vm.Revisions);
        await vm.ViewRevisionCommand.ExecuteAsync(oldest);
        Assert.Equal("Close revision", oldest.ActionLabel);
        await vm.DeleteRevisionCommand.ExecuteAsync(oldest);
        Assert.Empty(repository.Deletes);
        Assert.Equal(oldest.Item.Revision, vm.Snapshot!.Revision);
        Assert.Contains("2026-10-07", dialogs.ConfirmationRequest!.Message);
        Assert.Contains("P 21", dialogs.ConfirmationRequest.Message);
        Assert.Contains("Revision 1 · Draft", dialogs.ConfirmationRequest.Message);
        Assert.True(dialogs.ConfirmationRequest.IsDestructive);
        dialogs.ConfirmationResult = true;
        await vm.DeleteRevisionCommand.ExecuteAsync(oldest);
        Assert.Null(vm.Snapshot);
        Assert.False(oldest.IsExpanded);
        Assert.All(vm.Revisions, r => Assert.Equal("View revision", r.ActionLabel));
        Assert.Same(list, vm.Entries);
        Assert.Equal("Page 1 · 20 revisions", vm.RevisionPageText);
        Assert.Equal(new(item.Id, 1, 21), repository.Deletes.Single());
        Assert.Equal(20, vm.Revisions.Count);
        Assert.False(vm.NextRevisionsCommand.CanExecute(null));
        Assert.Contains("deleted", vm.DeleteStatus);
        await vm.ViewRevisionCommand.ExecuteAsync(vm.Revisions[0]);
        var retainedSnapshot = vm.Snapshot;
        await vm.DeleteRevisionCommand.ExecuteAsync(vm.Revisions[1]);
        Assert.Same(retainedSnapshot, vm.Snapshot); // Deleting another revision must not dismiss this view.
        Assert.Single(vm.Revisions, r => r.IsExpanded);
        Assert.Equal("Page 1 · 19 revisions", vm.RevisionPageText);
        vm.Deactivate();
    }

    [Theory]
    [InlineData("conflict")]
    [InlineData("failure")]
    [InlineData("cancel")]
    [InlineData("missing")]
    public async Task FailedDeletionPreservesListSnapshotAndOffersRecovery(string outcome)
    {
        var reader = new JournalHistoryTestReader();
        reader.Add(new(2026, 10, 7), revision: 3);
        var repository = new HistoryRepositoryProbe(reader)
        {
            Delete = (_, _) => outcome switch
            {
                "conflict" => Task.FromResult(DeleteJournalRevisionStatus.Conflict),
                "missing" => Task.FromResult(DeleteJournalRevisionStatus.NotFound),
                "cancel" => throw new OperationCanceledException(),
                _ => throw new InvalidOperationException("Synthetic failure")
            }
        };
        var vm = new JournalHistoryViewModel(reader, _ => true, repository, new FakeDialogService { ConfirmationResult = true });
        await vm.ActivateAsync(null);
        await vm.OpenCommand.ExecuteAsync(vm.Entries[0]);
        var revisions = vm.Revisions;
        await vm.ViewRevisionCommand.ExecuteAsync(revisions[1]);
        var snapshot = vm.Snapshot;
        await vm.DeleteRevisionCommand.ExecuteAsync(revisions[1]);
        Assert.Same(revisions, vm.Revisions);
        Assert.Same(snapshot, vm.Snapshot);
        Assert.Contains("refresh", vm.DeleteStatus!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, reader.Snapshots.Count);
        if (outcome is "conflict" or "missing") Assert.False(vm.DeleteRevisionCommand.CanExecute(revisions[1]));
        vm.Deactivate();
    }

    [Fact]
    public async Task LatePreviewCannotPopulateAnotherRowOrChangeFilter()
    {
        var reader = new JournalHistoryTestReader();
        var first = reader.Add(new(2026, 10, 7), Guid.NewGuid());
        reader.Add(new(2026, 10, 6));
        var pending = new TaskCompletionSource<DailyJournalDetails?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        var repository = new HistoryRepositoryProbe(reader);
        var stale = repository.Details(first.TradingDate, first.AccountId);
        repository.Read = (date, account, ct) =>
        {
            if (account != first.AccountId) return Task.FromResult(repository.Details(date, account));
            token = ct; started.TrySetResult(); return pending.Task;
        };
        var vm = new JournalHistoryViewModel(reader, _ => true, repository);
        await vm.ActivateAsync(null);
        var list = vm.Entries;
        Task old = vm.OpenCommand.ExecuteAsync(list[0]);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await vm.OpenCommand.ExecuteAsync(list[1]);
        Assert.True(token.IsCancellationRequested);
        pending.SetResult(stale);
        await old;
        Assert.Same(list[1], vm.SelectedEntry);
        Assert.Same(list, vm.Entries);
        Assert.Equal("Page 1 · 2 reviews", vm.PageText);
        vm.Deactivate();
    }
}

internal sealed class HistoryRepositoryProbe(JournalHistoryTestReader reader) : IDailyJournalRepository, IDailyJournalRevisionWriter
{
    public List<(DateOnly, Guid?)> Reads { get; } = [];
    public List<DeleteJournalRevisionCommand> Deletes { get; } = [];
    public Func<DateOnly, Guid?, CancellationToken, Task<DailyJournalDetails?>>? Read { get; set; }
    public Func<DeleteJournalRevisionCommand, CancellationToken, Task<DeleteJournalRevisionStatus>>? Delete { get; set; }
    public DailyJournalDetails? Details(DateOnly date, Guid? account)
    {
        var row = reader.Items.SingleOrDefault(i => i.TradingDate == date && i.AccountId == account);
        if (row is null) return null;
        var snapshot = reader.Snapshots.Single(s => s.JournalId == row.Id && s.Revision == row.Revision);
        return new(DailyJournalEntry.Rehydrate(row.Id, date, account, snapshot.Text, snapshot.IsDraft, snapshot.Revision,
            JournalHistoryTestReader.Now, snapshot.SavedAtUtc, snapshot.Review), row.AccountState, row.AccountName);
    }
    public Task<DailyJournalDetails?> GetAsync(DateOnly date, Guid? account = null, CancellationToken cancellationToken = default)
    { Reads.Add((date, account)); return Read?.Invoke(date, account, cancellationToken) ?? Task.FromResult(Details(date, account)); }
    public Task<DeleteJournalRevisionStatus> DeleteRevisionAsync(DeleteJournalRevisionCommand command, CancellationToken cancellationToken = default)
    {
        Deletes.Add(command);
        if (Delete is not null) return Delete(command, cancellationToken);
        reader.Snapshots.RemoveAll(s => s.JournalId == command.JournalId && s.Revision == command.Revision);
        return Task.FromResult(DeleteJournalRevisionStatus.Deleted);
    }
    public Task<DailyJournalWriteResult> DeleteAsync(DeleteDailyJournalCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<DailyJournalRevision>> GetHistoryAsync(Guid journalId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<DailyJournalWriteResult> CreateAsync(CreateDailyJournalCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<DailyJournalWriteResult> UpdateAsync(UpdateDailyJournalCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
