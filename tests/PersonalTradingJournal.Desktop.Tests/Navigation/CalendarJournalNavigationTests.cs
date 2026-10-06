using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task CalendarJournalCallbackOpensExactDateAndDistinctAccountEntry(bool accountSpecific, bool draft)
    {
        DateOnly day = new(2026, 11, 1);
        Guid? accountId = accountSpecific ? Guid.NewGuid() : null;
        var entry = new DailyJournalEntry(day, accountId, "Exact journal", draft, FixedTimeProvider.FixedUtcNow,
            new("Well", "Improve", "Next"));
        var repository = new NavigationJournalRepository
        {
            Read = (date, scope, _) => Task.FromResult<DailyJournalDetails?>(date == day && scope == accountId
                ? new(entry, accountSpecific ? DailyJournalAccountState.Unavailable : DailyJournalAccountState.AllAccounts,
                    accountSpecific ? "Historical" : null) : null),
        };
        var fixture = CreateJournalFixture(repository);
        using var main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        var calendar = Assert.IsType<CalendarViewModel>(main.CurrentContentViewModel);
        await calendar.LoadTask;

        await calendar.OpenJournalAsync!(day, new(accountId, accountSpecific ? "Historical" : "All accounts"));

        Assert.Equal(NavigationDestination.Journal, main.CurrentDestination);
        Assert.Same(fixture.Journal, main.CurrentContentViewModel);
        Assert.Equal(day.ToDateTime(TimeOnly.MinValue), fixture.Journal.SelectedDate);
        Assert.Equal(accountId, fixture.Journal.SelectedAccount.Id);
        Assert.Equal(entry.Revision, fixture.Journal.Revision);
        Assert.Equal(day, Assert.Single(repository.ReadCalls).Date);
        Assert.Equal(accountId, Assert.Single(repository.ReadCalls).AccountId);
        Assert.Equal("Exact journal", fixture.Journal.Text);
        Assert.Equal(draft, fixture.Journal.IsDraft);
        Assert.Equal(!accountSpecific && draft, fixture.Journal.CanEdit);
        Assert.Empty(repository.CreateCalls);
    }

    [Fact]
    public async Task CalendarJournalNavigationRetainsCalendarMonthDateAndExactFiltersOnReturn()
    {
        var fixture = CreateJournalFixture();
        using var main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        var calendar = Assert.IsType<CalendarViewModel>(main.CurrentContentViewModel);
        await calendar.LoadTask;
        calendar.PreviousCommand.Execute(null);
        await calendar.LoadTask;
        calendar.SelectedAccount = new(Guid.NewGuid(), "Unavailable historical account");
        await calendar.LoadTask;
        calendar.SelectedCurrency = "EUR";
        await calendar.LoadTask;
        CalendarDayCell saturday = calendar.Weeks.SelectMany(week => week.Days).First(cell => cell.IsSaturday);
        await calendar.SelectDayCommand.ExecuteAsync(saturday);
        DateOnly month = calendar.SelectedMonth;
        Guid? accountId = calendar.SelectedAccount.Id;
        string currency = calendar.SelectedCurrency;

        await calendar.OpenJournalAsync!(saturday.Date, calendar.SelectedAccount);
        Assert.Equal(NavigationDestination.Journal, main.CurrentDestination);
        Assert.Equal(saturday.Date.ToDateTime(TimeOnly.MinValue), fixture.Journal.SelectedDate);
        Assert.Equal(accountId, fixture.Journal.SelectedAccount.Id);
        Assert.False(fixture.Journal.SelectedAccount.IsAvailable);

        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        await calendar.LoadTask;
        Assert.Same(calendar, main.CurrentContentViewModel);
        Assert.Equal(month, calendar.SelectedMonth);
        Assert.Equal(saturday.Date, calendar.SelectedDate);
        Assert.Equal(accountId, calendar.SelectedAccount.Id);
        Assert.Equal(currency, calendar.SelectedCurrency);
    }

    [Fact]
    public async Task RetainedDifferentJournalDraftVetoKeepsCalendarAndDoesNotReadTargetScope()
    {
        var fixture = CreateJournalFixture();
        using var main = fixture.Main;
        await fixture.Journal.ActivateAsync();
        fixture.Journal.OpenEditorCommand.Execute(null);
        fixture.Journal.Text = "Keep both drafts safe";
        fixture.Journal.WentWell = "Retained review";
        fixture.Journal.Deactivate();
        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        var calendar = Assert.IsType<CalendarViewModel>(main.CurrentContentViewModel);
        await calendar.LoadTask;
        DateTime? originalDate = fixture.Journal.SelectedDate;

        await calendar.OpenJournalAsync!(DateOnly.FromDateTime(originalDate!.Value).AddDays(1), new(Guid.NewGuid(), "Other"));

        Assert.Equal(NavigationDestination.Calendar, main.CurrentDestination);
        Assert.Equal(originalDate, fixture.Journal.SelectedDate);
        Assert.Null(fixture.Journal.SelectedAccount.Id);
        Assert.Equal("Keep both drafts safe", fixture.Journal.Text);
        Assert.Equal("Retained review", fixture.Journal.WentWell);
        Assert.Single(fixture.Repository.ReadCalls);
        Assert.NotNull(fixture.Dialogs.ConfirmationRequest);
        Assert.Empty(fixture.Repository.CreateCalls);
    }

    [Fact]
    public async Task SameScopeCalendarNavigationPreservesInactiveJournalDraftWithoutPromptOrReload()
    {
        var fixture = CreateJournalFixture();
        using var main = fixture.Main;
        await fixture.Journal.ActivateAsync();
        fixture.Journal.OpenEditorCommand.Execute(null);
        fixture.Journal.NextTradingDay = "Keep local answer";
        DateOnly day = DateOnly.FromDateTime(fixture.Journal.SelectedDate!.Value);
        fixture.Journal.Deactivate();
        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        var calendar = Assert.IsType<CalendarViewModel>(main.CurrentContentViewModel);
        await calendar.LoadTask;

        await calendar.OpenJournalAsync!(day, new(null, "All accounts"));

        Assert.Equal(NavigationDestination.Journal, main.CurrentDestination);
        Assert.Equal("Keep local answer", fixture.Journal.NextTradingDay);
        Assert.True(fixture.Journal.IsDirty);
        Assert.True(fixture.Journal.CanEdit);
        Assert.Single(fixture.Repository.ReadCalls);
        Assert.Null(fixture.Dialogs.ConfirmationRequest);
    }

    [Fact]
    public async Task DisposingShellUnwiresCalendarJournalNavigationAndCommitSubscription()
    {
        var fixture = CreateJournalFixture();
        var main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        var calendar = Assert.IsType<CalendarViewModel>(main.CurrentContentViewModel);
        await calendar.LoadTask;
        Assert.NotNull(calendar.OpenJournalAsync);
        main.Dispose();
        Assert.Null(calendar.OpenJournalAsync);
        Assert.Empty(fixture.Repository.ReadCalls);
    }

    [Fact]
    public async Task ShellRefreshesExactCalendarStatusAfterDraftSaveCompleteAndReopenOnReturn()
    {
        var repository = new CalendarNavigationJournalRepository();
        var journal = new JournalViewModel(repository, new FakeTradingAccountReader(), new FakeDialogService { ConfirmationResult = true },
            new(new FakeTradingCalendarDayReader(), new FakeTradingAccountReader()), new FixedTimeProvider());
        var fixture = CreateFixture(journalViewModel: journal, journalStatusReader: repository);
        using var main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        var calendar = Assert.IsType<CalendarViewModel>(main.CurrentContentViewModel);
        await calendar.LoadTask;
        var cell = calendar.Weeks.SelectMany(w => w.Days).Single(d => d.Date == new DateOnly(2026, 9, 12));
        await calendar.SelectDayCommand.ExecuteAsync(cell);
        Assert.False(cell.HasJournal);
        Assert.Equal(1, repository.StatusReads);

        await calendar.NavigateToJournalAsync(cell.Date, calendar.SelectedAccount);
        Assert.Equal(NavigationDestination.Journal, main.CurrentDestination);
        Assert.True(journal.IsEditorOpen && journal.CanEdit); // Add opens immediately, no second click.
        journal.WentWell = "Well";
        journal.NeedsImprovement = "Improve";
        journal.NextTradingDay = "Next";
        await journal.SaveCommand.ExecuteAsync(null);
        Assert.Equal(1, repository.StatusReads); // Inactive Calendar is invalidated, not queried.
        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        await calendar.LoadTask;
        Assert.Equal(2, repository.StatusReads);
        Assert.Equal("Draft", cell.JournalStatusText);
        Assert.True(cell.IsSaturday);

        await calendar.NavigateToJournalAsync(cell.Date, calendar.SelectedAccount);
        journal.OpenEditorCommand.Execute(null);
        await journal.CompleteReviewCommand.ExecuteAsync(null);
        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        await calendar.LoadTask;
        Assert.Equal(3, repository.StatusReads);
        Assert.Equal("Completed", cell.JournalStatusText);

        await calendar.NavigateToJournalAsync(cell.Date, calendar.SelectedAccount);
        await journal.ReopenReviewCommand.ExecuteAsync(null);
        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        await calendar.LoadTask;
        Assert.Equal(4, repository.StatusReads);
        Assert.Equal("Draft", cell.JournalStatusText);
        Assert.Equal(cell.Date, calendar.SelectedDate);
        Assert.Equal(new DateOnly(2026, 9, 1), calendar.SelectedMonth);
        Assert.Null(calendar.SelectedAccount.Id);
        Assert.Equal("All currencies", calendar.SelectedCurrency);
        Assert.True(calendar.IsSelectedDayEmpty);
        await calendar.NavigateToJournalAsync(cell.Date, calendar.SelectedAccount);
        await journal.DeleteCommand.ExecuteAsync(null);
        Assert.False(journal.IsExisting);
        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        await calendar.LoadTask;
        Assert.Equal(5, repository.StatusReads);
        Assert.False(cell.HasJournal);
        Assert.Equal(cell.Date, calendar.SelectedDate);
        Assert.Equal(new DateOnly(2026, 9, 1), calendar.SelectedMonth);
    }

    [Theory]
    [InlineData(DailyJournalWriteStatus.Created)]
    [InlineData(DailyJournalWriteStatus.Updated)]
    [InlineData(DailyJournalWriteStatus.Unchanged)]
    [InlineData(DailyJournalWriteStatus.Conflict)]
    [InlineData(DailyJournalWriteStatus.AccountUnavailable)]
    public async Task ShellCommitNotificationRefreshesActiveCalendarOnlyForCommittedChanges(DailyJournalWriteStatus status)
    {
        var repository = new CalendarNavigationJournalRepository { WriteStatus = status };
        var journal = new JournalViewModel(repository, new FakeTradingAccountReader(), new FakeDialogService(),
            new(new FakeTradingCalendarDayReader(), new FakeTradingAccountReader()), new FixedTimeProvider());
        var fixture = CreateFixture(journalViewModel: journal, journalStatusReader: repository);
        using var main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        var calendar = Assert.IsType<CalendarViewModel>(main.CurrentContentViewModel);
        await calendar.LoadTask;
        await journal.ActivateAsync();
        journal.OpenEditorCommand.Execute(null);
        journal.Text = "Committed scope";
        await journal.SaveCommand.ExecuteAsync(null);
        await calendar.JournalLoadTask;
        Assert.Equal(status is DailyJournalWriteStatus.Created or DailyJournalWriteStatus.Updated ? 2 : 1,
            repository.StatusReads);
        Assert.Equal(NavigationDestination.Calendar, main.CurrentDestination);
        Assert.Null(calendar.SelectedAccount.Id);
    }

    private sealed class CalendarNavigationJournalRepository : IDailyJournalRepository, IDailyJournalStatusReader
    {
        public Task<DailyJournalWriteResult> DeleteAsync(DeleteDailyJournalCommand command, CancellationToken cancellationToken = default)
        {
            Assert.Equal(_journal!.Entry.Id, command.JournalId);
            Assert.Equal(_journal.Entry.Revision, command.ExpectedRevision);
            _journal = null;
            return Task.FromResult(new DailyJournalWriteResult(DailyJournalWriteStatus.Deleted, null));
        }

        private DailyJournalDetails? _journal;
        private int _statusReads;
        public int StatusReads => _statusReads;
        public DailyJournalWriteStatus? WriteStatus { get; init; }
        public Task<DailyJournalDetails?> GetAsync(DateOnly date, Guid? accountId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(_journal?.Entry.TradingDate == date && _journal.Entry.TradingAccountId == accountId ? _journal : null);
        public Task<IReadOnlyList<DailyJournalStatus>> GetAsync(DateOnly from, DateOnly through, Guid? accountId = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _statusReads);
            var entry = _journal?.Entry;
            return Task.FromResult<IReadOnlyList<DailyJournalStatus>>(entry is not null && entry.TradingAccountId == accountId
                && entry.TradingDate >= from && entry.TradingDate <= through ? [new(entry.Id, entry.TradingDate, entry.IsDraft, entry.Revision)] : []);
        }
        public Task<DailyJournalWriteResult> CreateAsync(CreateDailyJournalCommand command, CancellationToken cancellationToken = default)
        {
            if (WriteStatus is DailyJournalWriteStatus.Conflict or DailyJournalWriteStatus.AccountUnavailable)
                return Task.FromResult(new DailyJournalWriteResult(WriteStatus.Value, null));
            _journal = new(new(command.TradingDate, command.TradingAccountId, command.Text, command.IsDraft,
                FixedTimeProvider.FixedUtcNow, command.Review ?? DailyReviewAnswers.Empty), DailyJournalAccountState.AllAccounts, null);
            return Task.FromResult(new DailyJournalWriteResult(WriteStatus ?? DailyJournalWriteStatus.Created, _journal));
        }
        public Task<DailyJournalWriteResult> UpdateAsync(UpdateDailyJournalCommand command, CancellationToken cancellationToken = default)
        {
            var previous = _journal!.Entry;
            _journal = _journal with { Entry = DailyJournalEntry.Rehydrate(previous.Id, previous.TradingDate, previous.TradingAccountId,
                command.Text, command.IsDraft, previous.Revision + 1, previous.CreatedAtUtc, previous.UpdatedAtUtc,
                command.Review ?? previous.Review) };
            return Task.FromResult(new DailyJournalWriteResult(WriteStatus ?? DailyJournalWriteStatus.Updated, _journal));
        }
        public Task<IReadOnlyList<DailyJournalRevision>> GetHistoryAsync(Guid journalId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Calendar navigation must not load history.");
    }
}
