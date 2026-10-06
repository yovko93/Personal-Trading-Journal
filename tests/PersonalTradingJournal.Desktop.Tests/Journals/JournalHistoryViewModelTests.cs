using System.Globalization;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalHistoryViewModelTests
{
    private static readonly DateOnly Day = new(2026, 10, 5);

    [Fact]
    public async Task PagesScopesAndRevisionPagesAreBoundedAndDoNotEagerlyReadText()
    {
        var reader = new JournalHistoryTestReader();
        var account = Guid.NewGuid();
        for (int i = 0; i < 43; i++) reader.Add(Day.AddDays(-i));
        var scoped = reader.Add(Day, account, 23);
        var vm = new JournalHistoryViewModel(reader, _ => true);
        await vm.ActivateAsync(null);
        Assert.Equal(10, vm.Entries.Count);
        Assert.Equal("2026-10-05", vm.Entries[0].DateText);
        await vm.NextCommand.ExecuteAsync(null);
        Assert.Equal("2026-09-25", vm.Entries[0].DateText);
        await vm.NextCommand.ExecuteAsync(null);
        await vm.NextCommand.ExecuteAsync(null);
        await vm.NextCommand.ExecuteAsync(null);
        Assert.Equal(3, vm.Entries.Count);
        Assert.False(vm.NextCommand.CanExecute(null));
        await vm.PreviousCommand.ExecuteAsync(null);
        Assert.Contains("Page 4", vm.PageText);
        await vm.SetScopeAsync(account);
        Assert.Equal(scoped.Id, Assert.Single(vm.Entries).Item.Id);
        Assert.Contains("inactive", vm.Entries[0].ScopeText);
        await vm.OpenCommand.ExecuteAsync(vm.Entries[0]);
        Assert.Equal(20, vm.Revisions.Count);
        Assert.Equal(23, vm.Revisions[0].Item.Revision);
        await vm.NextRevisionsCommand.ExecuteAsync(null);
        Assert.Equal(3, vm.Revisions.Count);
        Assert.False(vm.NextRevisionsCommand.CanExecute(null));
        Assert.Equal(0, reader.SnapshotReads);
        await vm.ViewRevisionCommand.ExecuteAsync(vm.Revisions[^1]);
        Assert.Equal("  text 1\r\n", vm.Snapshot!.Text);
        Assert.Contains("UTC-4", vm.SnapshotDescription);
        Assert.Contains("New York", vm.SnapshotDescription);
        Assert.Contains("Read-only", vm.SnapshotDescription);
        Assert.Equal(new DailyReviewAnswers("well", "improve", "next"), vm.Snapshot.Review);
        await vm.SetScopeAsync(null);
        Assert.Null(vm.Snapshot);
        Assert.Null(vm.SelectedEntry);
        Assert.Empty(vm.Revisions);
        Assert.Contains("Page 1", vm.PageText);
        vm.Deactivate();
    }

    [Fact]
    public async Task HistoryOpenHonorsEditorsDirtyTextAndAnswersGuardAndIgnoresObsoleteRow()
    {
        var reader = new JournalHistoryTestReader();
        reader.Add(Day.AddDays(-1));
        var repository = new Repository();
        var dialogs = new FakeDialogService();
        var vm = new JournalViewModel(repository, new FakeTradingAccountReader(), dialogs,
            new(new FakeTradingCalendarDayReader(), new FakeTradingAccountReader()), new Clock(), reader);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        vm.Text = "local unsaved text";
        vm.WentWell = "local answer";
        var row = vm.History!.Entries[0];
        await vm.History.OpenCommand.ExecuteAsync(row);
        Assert.NotNull(dialogs.ConfirmationRequest);
        Assert.Equal(Day, DateOnly.FromDateTime(vm.SelectedDate!.Value));
        Assert.Equal("local unsaved text", vm.Text);
        Assert.Equal("local answer", vm.WentWell);
        Assert.Null(vm.History.SelectedEntry);
        Assert.Empty(vm.History.Revisions);
        dialogs.ConfirmationResult = true;
        await vm.History.OpenCommand.ExecuteAsync(row);
        await vm.LoadTask;
        vm.OpenEditorCommand.Execute(null);
        Assert.Equal(row.Item.TradingDate, DateOnly.FromDateTime(vm.SelectedDate!.Value));
        Assert.Equal(row, vm.History.SelectedEntry);
        vm.Text = "new local draft";
        await vm.History.ViewRevisionCommand.ExecuteAsync(vm.History.Revisions[0]);
        Assert.Equal("new local draft", vm.Text);
        Assert.Equal(0, repository.Writes);
        await vm.History.RefreshCommand.ExecuteAsync(null);
        await vm.History.OpenCommand.ExecuteAsync(row); // Old button after rebind is ignored.
        Assert.Equal("new local draft", vm.Text);
        await vm.History.OpenCommand.ExecuteAsync(vm.History.Entries[0]); // Same key preserves dirty edits without arming Calendar reactivation.
        Assert.Equal("new local draft", vm.Text);
        Assert.True(vm.TryLeave()); // Explicit discard on leaving must still take effect on return.
        vm.Deactivate();
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        Assert.Empty(vm.Text);
        vm.Deactivate();
    }

    [Fact]
    public async Task LateAccountPageCannotReplaceNewScopeAndCancellationTokenIsObserved()
    {
        var started = Signal();
        var delayed = new TaskCompletionSource<JournalHistoryPage<JournalHistoryItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        Guid account = Guid.NewGuid();
        var reader = new JournalHistoryTestReader();
        var old = reader.Add(Day);
        var current = reader.Add(Day, account);
        reader.Browse = (scope, page, ct) =>
        {
            if (scope is null) { token = ct; started.TrySetResult(); return delayed.Task; }
            return Task.FromResult(new JournalHistoryPage<JournalHistoryItem>([current], 1, page, 20));
        };
        var vm = new JournalHistoryViewModel(reader, _ => true);
        Task first = vm.ActivateAsync(null);
        await Wait(started.Task);
        await vm.SetScopeAsync(account);
        Assert.True(token.IsCancellationRequested);
        delayed.SetResult(new([old], 1, 1, 20));
        await Wait(first);
        Assert.Equal(account, Assert.Single(vm.Entries).Item.AccountId);
        vm.Deactivate();
    }

    [Fact]
    public async Task LateRevisionPageCannotReplaceNewSelectedReview()
    {
        var started = Signal();
        var delayed = new TaskCompletionSource<JournalHistoryPage<JournalRevisionItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new JournalHistoryTestReader();
        var old = reader.Add(Day);
        var current = reader.Add(Day.AddDays(-1));
        CancellationToken token = default;
        reader.Revisions = (id, page, ct) =>
        {
            if (id == old.Id) { token = ct; started.TrySetResult(); return delayed.Task; }
            return Task.FromResult(new JournalHistoryPage<JournalRevisionItem>([new(id, 1, true, JournalHistoryTestReader.Now)], 1, page, 20));
        };
        var vm = new JournalHistoryViewModel(reader, _ => true);
        await vm.ActivateAsync(null);
        Task first = vm.OpenCommand.ExecuteAsync(vm.Entries[0]);
        await Wait(started.Task);
        await vm.OpenCommand.ExecuteAsync(vm.Entries[1]);
        Assert.True(token.IsCancellationRequested);
        delayed.SetResult(new([new(old.Id, 10, false, JournalHistoryTestReader.Now)], 1, 1, 20));
        await Wait(first);
        Assert.Equal(current.Id, vm.SelectedEntry!.Item.Id);
        Assert.Equal(current.Id, Assert.Single(vm.Revisions).Item.JournalId);
        vm.Deactivate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateSnapshotCannotReplaceNewSelectionOrInactivePage(bool leave)
    {
        var reader = new JournalHistoryTestReader();
        var entry = reader.Add(Day, revision: 2);
        var started = Signal();
        var delayed = new TaskCompletionSource<DailyJournalRevision?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        reader.Snapshot = (id, revision, ct) =>
        {
            if (revision == 2) { token = ct; started.TrySetResult(); return delayed.Task; }
            return Task.FromResult<DailyJournalRevision?>(reader.Snapshots.Single(r => r.JournalId == id && r.Revision == revision));
        };
        var vm = new JournalHistoryViewModel(reader, _ => true);
        await vm.ActivateAsync(null);
        await vm.OpenCommand.ExecuteAsync(vm.Entries[0]);
        Task first = vm.ViewRevisionCommand.ExecuteAsync(vm.Revisions[0]);
        await Wait(started.Task);
        if (leave) vm.Deactivate();
        else await vm.ViewRevisionCommand.ExecuteAsync(vm.Revisions[1]);
        Assert.True(token.IsCancellationRequested);
        delayed.SetResult(reader.Snapshots.Single(r => r.JournalId == entry.Id && r.Revision == 2));
        await Wait(first);
        if (leave) Assert.Null(vm.Snapshot);
        else Assert.Equal(1, vm.Snapshot!.Revision);
        vm.Deactivate();
    }

    [Fact]
    public async Task EmptyErrorsAndCancelledReadsHaveExplicitRecoveryWithoutScopeFallback()
    {
        var reader = new JournalHistoryTestReader();
        Guid account = Guid.NewGuid();
        var vm = new JournalHistoryViewModel(reader, _ => true);
        await vm.ActivateAsync(account);
        Assert.Contains("No saved reviews", vm.StatusText);
        reader.Browse = (_, _, _) => throw new InvalidOperationException("injected");
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Contains("Refresh to retry", vm.StatusText);
        var started = Signal();
        var pending = new TaskCompletionSource<JournalHistoryPage<JournalHistoryItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.Browse = (_, _, _) => { started.TrySetResult(); return pending.Task; };
        Task refresh = vm.RefreshCommand.ExecuteAsync(null);
        await Wait(started.Task);
        vm.CancelCommand.Execute(null);
        pending.SetResult(new([], 0, 1, 20));
        await Wait(refresh);
        Assert.Contains("cancelled", vm.StatusText);
        reader.Browse = null;
        var item = reader.Add(Day, account);
        reader.Items[0] = item with { AccountName = null, AccountState = DailyJournalAccountState.Unavailable };
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(account, Assert.Single(vm.Entries).Item.AccountId);
        Assert.Contains("unavailable", vm.Entries[0].ScopeText);
        Assert.Contains("scope retained", vm.Entries[0].ScopeText);
        vm.Deactivate();
    }

    internal sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => JournalHistoryTestReader.Now; }
    [Fact]
    public async Task RevisionFailuresMissingSnapshotsAndPageChangesHaveSafeRecovery()
    {
        var reader = new JournalHistoryTestReader();
        for (int i = 0; i < 21; i++) reader.Add(Day.AddDays(-i), revision: 2);
        var vm = new JournalHistoryViewModel(reader, _ => true);
        await vm.ActivateAsync(null);
        reader.Revisions = (_, _, _) => throw new InvalidOperationException("injected");
        await vm.OpenCommand.ExecuteAsync(vm.Entries[0]);
        Assert.Contains("Refresh history", vm.RevisionStatusText);
        Assert.Empty(vm.Revisions);
        reader.Revisions = null;
        await vm.RefreshCommand.ExecuteAsync(null);
        var revision = vm.Revisions[0];
        reader.Snapshot = (_, _, _) => Task.FromResult<DailyJournalRevision?>(null);
        await vm.ViewRevisionCommand.ExecuteAsync(revision);
        Assert.Contains("unavailable", vm.SnapshotStatusText);
        Assert.False(vm.HasSnapshot);
        reader.Snapshot = (_, _, _) => throw new InvalidOperationException("injected");
        await vm.ViewRevisionCommand.ExecuteAsync(revision);
        Assert.Contains("retry", vm.SnapshotStatusText);
        reader.Snapshot = null;
        await vm.ViewRevisionCommand.ExecuteAsync(revision);
        Assert.True(vm.HasSnapshot);
        await vm.NextCommand.ExecuteAsync(null);
        await vm.NextCommand.ExecuteAsync(null);
        Assert.Single(vm.Entries);
        Assert.Empty(vm.Revisions);
        Assert.Null(vm.SelectedEntry);
        Assert.False(vm.HasSnapshot);
        await vm.ViewRevisionCommand.ExecuteAsync(revision); // Disconnected old revision cannot reappear.
        Assert.False(vm.HasSnapshot);
        vm.Deactivate();
    }
    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(20)]
    public async Task ReviewPagesContainTenEntriesWithStableOrderAndExactNavigation(int count)
    {
        var reader = new JournalHistoryTestReader();
        for (int i = 0; i < count; i++) reader.Add(Day.AddDays(-i));
        var vm = new JournalHistoryViewModel(reader, _ => true);
        await vm.ActivateAsync(null);
        var firstIds = vm.Entries.Select(r => r.Item.Id).ToArray();
        Assert.Equal(Math.Min(10, count), firstIds.Length);
        Assert.False(vm.PreviousCommand.CanExecute(null));
        Assert.Equal(count > 10, vm.NextCommand.CanExecute(null));
        Assert.Equal(reader.Items.Take(10).Select(r => r.Id), firstIds);
        if (count > 10)
        {
            await vm.NextCommand.ExecuteAsync(null);
            Assert.Equal(count - 10, vm.Entries.Count);
            Assert.Equal(Day.AddDays(-10), vm.Entries[0].Item.TradingDate);
            Assert.True(vm.PreviousCommand.CanExecute(null));
            Assert.False(vm.NextCommand.CanExecute(null));
            await vm.PreviousCommand.ExecuteAsync(null);
            Assert.Equal(firstIds, vm.Entries.Select(r => r.Item.Id));
        }
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(firstIds, vm.Entries.Select(r => r.Item.Id));
        vm.Deactivate();
    }

    [Fact]
    public async Task CloseViewRetainsReviewPageScopeAndRevisionPageAndRejectsPendingSnapshot()
    {
        var reader = new JournalHistoryTestReader();
        Guid account = Guid.NewGuid();
        for (int i = 0; i < 11; i++) reader.Add(Day.AddDays(-i), account, 23);
        var vm = new JournalHistoryViewModel(reader, _ => true);
        await vm.ActivateAsync(account);
        await vm.NextCommand.ExecuteAsync(null);
        var selected = Assert.Single(vm.Entries);
        await vm.OpenCommand.ExecuteAsync(selected);
        await vm.NextRevisionsCommand.ExecuteAsync(null);
        await vm.ViewRevisionCommand.ExecuteAsync(vm.Revisions[0]);
        Assert.True(vm.CloseViewCommand.CanExecute(null));
        vm.CloseViewCommand.Execute(null);
        Assert.False(vm.HasRevisionView);
        Assert.Null(vm.Snapshot);
        Assert.Same(selected, vm.SelectedEntry);
        Assert.Equal(account, vm.SelectedEntry!.Item.AccountId);
        Assert.Equal("Page 2 · 11 reviews", vm.PageText);
        Assert.Equal("Page 2 · 23 revisions", vm.RevisionPageText);
        Assert.Equal(3, vm.Revisions.Count);
        var started = Signal();
        var pending = new TaskCompletionSource<DailyJournalRevision?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        reader.Snapshot = (_, _, ct) => { token = ct; started.TrySetResult(); return pending.Task; };
        Task load = vm.ViewRevisionCommand.ExecuteAsync(vm.Revisions[0]);
        await Wait(started.Task);
        vm.CloseViewCommand.Execute(null);
        Assert.True(token.IsCancellationRequested);
        pending.SetResult(reader.Snapshots.First(r => r.JournalId == selected.Item.Id));
        await Wait(load);
        Assert.False(vm.HasRevisionView);
        Assert.Same(selected, vm.SelectedEntry);
        Assert.Equal(2, reader.SnapshotReads); // Close adds no reads or writes.
        vm.Deactivate();
    }

    [Theory]
    [InlineData("2026-03-08T06:59:59Z", "2026-03-08T01:59:59", "UTC-5", "en-US")]
    [InlineData("2026-03-08T07:00:00Z", "2026-03-08T03:00:00", "UTC-4", "en-US")]
    [InlineData("2026-11-01T05:30:00Z", "2026-11-01T01:30:00", "UTC-4", "en-US")]
    [InlineData("2026-11-01T06:30:00Z", "2026-11-01T01:30:00", "UTC-5", "en-US")]
    [InlineData("2026-03-08T06:59:59Z", "2026-03-08T01:59:59", "UTC-5", "bg-BG")]
    [InlineData("2026-03-08T07:00:00Z", "2026-03-08T03:00:00", "UTC-4", "bg-BG")]
    [InlineData("2026-11-01T05:30:00Z", "2026-11-01T01:30:00", "UTC-4", "bg-BG")]
    [InlineData("2026-11-01T06:30:00Z", "2026-11-01T01:30:00", "UTC-5", "bg-BG")]
    public void RevisionTimesUseConciseCultureAwareNewYorkClockAndActualDstOffset(
        string utc, string clock, string offset, string cultureName)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            var instant = DateTimeOffset.Parse(utc, CultureInfo.InvariantCulture).AddTicks(1234567);
            var item = new JournalRevisionItem(Guid.NewGuid(), 2, false, instant);
            var row = new JournalRevisionRow(item);
            var expectedClock = DateTime.ParseExact(clock, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            Assert.Equal(expectedClock.ToString("G", culture) + " " + offset + " · New York", row.SavedAtText);
            Assert.Contains(row.SavedAtText, row.Description);
            Assert.Contains(row.SavedAtText, row.ViewAccessibleName);
            Assert.DoesNotContain("UTC+0", row.SavedAtText);
            Assert.DoesNotContain("1234567", row.SavedAtText);
            Assert.Equal(instant, row.Item.SavedAtUtc); // Presentation never mutates the audit instant.
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public async Task CloseReviewDiffersFromCloseViewAndPreservesDirtyEditorDateScopeAndPage()
    {
        var reader = new JournalHistoryTestReader();
        for (int i = 0; i < 11; i++) reader.Add(Day.AddDays(-i), revision: 23);
        var repository = new Repository();
        var vm = new JournalViewModel(repository, new FakeTradingAccountReader(), new FakeDialogService(),
            new(new FakeTradingCalendarDayReader(), new FakeTradingAccountReader()), new Clock(), reader);
        await vm.ActivateAsync();
        var history = vm.History!;
        await history.NextCommand.ExecuteAsync(null);
        var row = Assert.Single(history.Entries);
        await history.OpenCommand.ExecuteAsync(row);
        await vm.LoadTask;
        vm.OpenEditorCommand.Execute(null);
        vm.Text = "local journal";
        vm.WentWell = "local well";
        vm.NeedsImprovement = "local improve";
        vm.NextTradingDay = "local next";
        await history.NextRevisionsCommand.ExecuteAsync(null);
        await history.ViewRevisionCommand.ExecuteAsync(history.Revisions[0]);
        history.CloseViewCommand.Execute(null);
        Assert.Same(row, history.SelectedEntry);
        Assert.Equal(3, history.Revisions.Count);
        await history.ViewRevisionCommand.ExecuteAsync(history.Revisions[0]);
        history.CloseReviewCommand.Execute(null);
        Assert.False(history.HasSelectedEntry);
        Assert.Null(history.Snapshot);
        Assert.Empty(history.Revisions);
        Assert.False(history.HasRevisionView);
        Assert.False(history.CloseReviewCommand.CanExecute(null));
        Assert.Equal("Page 2 · 11 reviews", history.PageText);
        Assert.Same(row, Assert.Single(history.Entries));
        Assert.Equal(row.Item.TradingDate, DateOnly.FromDateTime(vm.SelectedDate!.Value));
        Assert.Null(vm.SelectedAccount.Id);
        Assert.Equal(new[] { "local journal", "local well", "local improve", "local next" },
            new[] { vm.Text, vm.WentWell, vm.NeedsImprovement, vm.NextTradingDay });
        Assert.True(vm.IsDirty);
        Assert.True(vm.IsEditorOpen);
        Assert.Equal(0, repository.Writes);
        vm.Deactivate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CloseReviewCancelsPendingRevisionOrSnapshotWithoutChangingAccountPage(bool snapshot)
    {
        var reader = new JournalHistoryTestReader();
        Guid account = Guid.NewGuid();
        for (int i = 0; i < 11; i++) reader.Add(Day.AddDays(-i), account);
        var vm = new JournalHistoryViewModel(reader, _ => true);
        await vm.ActivateAsync(account);
        await vm.NextCommand.ExecuteAsync(null);
        var row = Assert.Single(vm.Entries);
        var started = Signal();
        var revisionResult = new TaskCompletionSource<JournalHistoryPage<JournalRevisionItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshotResult = new TaskCompletionSource<DailyJournalRevision?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        Task pending;
        if (snapshot)
        {
            await vm.OpenCommand.ExecuteAsync(row);
            reader.Snapshot = (_, _, ct) => { token = ct; started.TrySetResult(); return snapshotResult.Task; };
            pending = vm.ViewRevisionCommand.ExecuteAsync(vm.Revisions[0]);
        }
        else
        {
            reader.Revisions = (_, _, ct) => { token = ct; started.TrySetResult(); return revisionResult.Task; };
            pending = vm.OpenCommand.ExecuteAsync(row);
        }
        await Wait(started.Task);
        vm.CloseReviewCommand.Execute(null);
        Assert.True(token.IsCancellationRequested);
        if (snapshot) snapshotResult.SetResult(reader.Snapshots.First(r => r.JournalId == row.Item.Id));
        else revisionResult.SetResult(new([new(row.Item.Id, 1, true, JournalHistoryTestReader.Now)], 1, 1, 20));
        await Wait(pending);
        Assert.Null(vm.SelectedEntry);
        Assert.Empty(vm.Revisions);
        Assert.Null(vm.Snapshot);
        Assert.False(vm.IsBusy);
        Assert.Equal("Page 2 · 11 reviews", vm.PageText);
        Assert.Equal(account, Assert.Single(vm.Entries).Item.AccountId);
        vm.Deactivate();
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task Wait(Task task) => task.WaitAsync(TimeSpan.FromSeconds(10));
    private sealed class Repository : IDailyJournalRepository
    {
        public int Writes { get; private set; }
        public Task<DailyJournalDetails?> GetAsync(DateOnly tradingDate, Guid? tradingAccountId = null, CancellationToken cancellationToken = default) => Task.FromResult<DailyJournalDetails?>(null);
        public Task<IReadOnlyList<DailyJournalRevision>> GetHistoryAsync(Guid journalId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DailyJournalWriteResult> CreateAsync(CreateDailyJournalCommand command, CancellationToken cancellationToken = default) { Writes++; throw new NotSupportedException(); }
        public Task<DailyJournalWriteResult> UpdateAsync(UpdateDailyJournalCommand command, CancellationToken cancellationToken = default) { Writes++; throw new NotSupportedException(); }
    }
}
