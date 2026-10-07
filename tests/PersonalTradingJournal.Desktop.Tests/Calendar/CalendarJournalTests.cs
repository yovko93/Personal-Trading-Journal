using System.Collections.Concurrent;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed class CalendarJournalTests
{
    private static readonly DateOnly Saturday = new(2026, 9, 5);
    private static readonly DateOnly Adjacent = new(2026, 8, 31);

    [Theory]
    [InlineData(2021, 2, 28)]
    [InlineData(2026, 9, 35)]
    [InlineData(2026, 8, 42)]
    public async Task VisibleGridIsOneBoundedExactAllAccountStatusBatch(int year, int month, int days)
    {
        var source = new StatusSource();
        CalendarViewModel vm = Create(source);
        while (vm.SelectedMonth < new DateOnly(year, month, 1)) vm.NextCommand.Execute(null);
        while (vm.SelectedMonth > new DateOnly(year, month, 1)) vm.PreviousCommand.Execute(null);
        await vm.ActivateAsync();
        var call = Assert.Single(source.Calls);
        Assert.Null(call.Account);
        Assert.Equal(vm.Weeks[0].Days[0].Date, call.From);
        Assert.Equal(vm.Weeks[^1].Days[^1].Date, call.Through);
        Assert.Equal(days, call.Through.DayNumber - call.From.DayNumber + 1);
        Assert.False(vm.IsJournalLoading);
        Assert.All(vm.Weeks.SelectMany(w => w.Days), day => Assert.False(day.HasJournal));
    }

    [Fact]
    public async Task EmptyTradingDaysAdjacentDatesAndSaturdayUseDailyJournalStateWithoutAffectingTradeTotals()
    {
        var source = new StatusSource();
        source.Set(null, Status(Saturday, true), Status(Adjacent, false));
        CalendarViewModel vm = Create(source);
        await vm.ActivateAsync();
        Assert.True(vm.IsMonthEmpty);
        Assert.Empty(vm.MonthlySummaries);
        CalendarDayCell saturday = Cell(vm, Saturday);
        Assert.False(saturday.ShowsDailySummary);
        Assert.Empty(saturday.WeeklySummaries);
        Assert.Equal("Draft", saturday.JournalStatusText);
        Assert.Equal("Completed", Cell(vm, Adjacent).JournalStatusText);
        Assert.False(Cell(vm, Adjacent).IsInDisplayedMonth);
        Assert.Contains("Daily Journal draft", saturday.AccessibleName);
        await vm.SelectDayCommand.ExecuteAsync(saturday);
        Assert.True(vm.IsSelectedDayEmpty);
        Assert.True(vm.CanOpenDayJournal);
        Assert.Equal("Continue Journal", vm.DayJournalActionText);
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm, Adjacent));
        Assert.Equal("Open Journal", vm.DayJournalActionText);
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm, Saturday.AddDays(1)));
        Assert.Equal("Add Journal", vm.DayJournalActionText);
        Assert.Null(vm.SelectedDayJournalStatus);
        Assert.Contains("No Journal", vm.DayJournalStatusMessage);
    }

    [Fact]
    public async Task AccountScopeIsExactAndIndependentOfCurrencyAndPersistsAcrossMonthNavigation()
    {
        var source = new StatusSource();
        var accounts = new AccountsSource();
        source.Set(null, Status(Saturday, true));
        source.Set(accounts.Historical.Id, Status(Saturday, false));
        CalendarViewModel vm = Create(source, accounts);
        await vm.ActivateAsync();
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm, Saturday));
        Assert.Equal("Continue Journal", vm.DayJournalActionText);
        vm.SelectedAccount = vm.Accounts.Single(a => a.Id == accounts.Historical.Id);
        await vm.LoadTask;
        Assert.Equal("Open Journal", vm.DayJournalActionText);
        Assert.Contains("inactive", vm.SelectedAccount.Name);
        vm.SelectedCurrency = "EUR";
        await vm.LoadTask;
        Assert.Equal("Completed", Cell(vm, Saturday).JournalStatusText);
        Assert.Equal("Open Journal", vm.DayJournalActionText);
        Assert.Equal(accounts.Historical.Id, source.Calls.Last().Account);
        Assert.Equal("EUR", vm.SelectedCurrency);
        vm.NextCommand.Execute(null);
        await vm.LoadTask;
        Assert.Equal(accounts.Historical.Id, vm.SelectedAccount.Id);
        Assert.Equal("EUR", vm.SelectedCurrency);
        Assert.Null(vm.SelectedDate);
        Assert.All(vm.Weeks.SelectMany(w => w.Days), day => Assert.False(day.HasJournal));
        vm.PreviousCommand.Execute(null);
        await vm.LoadTask;
        Assert.Equal("Completed", Cell(vm, Saturday).JournalStatusText);
        vm.SelectedAccount = vm.Accounts.Single(a => a.Id is null);
        await vm.LoadTask;
        Assert.Equal("Draft", Cell(vm, Saturday).JournalStatusText);
        Assert.Null(source.Calls.Last().Account);
    }

    [Fact]
    public async Task NavigationUsesCapturedDateAndExactScopeAndRejectsChangedSelections()
    {
        var source = new StatusSource();
        CalendarViewModel vm = Create(source);
        await vm.ActivateAsync();
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm, Saturday));
        List<(DateOnly Date, Guid? Account)> requests = [];
        vm.OpenJournalAsync = (date, account) => { requests.Add((date, account.Id)); return Task.CompletedTask; };
        await vm.NavigateToJournalAsync(Saturday, vm.SelectedAccount);
        Assert.Equal((Saturday, (Guid?)null), Assert.Single(requests));
        Assert.True(vm.JournalNavigationTask.IsCompletedSuccessfully);
        await vm.NavigateToJournalAsync(Saturday, new(Guid.NewGuid(), "Wrong account"));
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm, Saturday.AddDays(1)));
        await vm.NavigateToJournalAsync(Saturday, vm.SelectedAccount);
        Assert.Single(requests);
        vm.Deactivate();
        await vm.NavigateToJournalAsync(Saturday.AddDays(1), vm.SelectedAccount);
        Assert.Single(requests);
    }

    [Fact]
    public async Task JournalCommitRefreshesDraftCompletedReopenIndicatorsOnlyAndRetainsSelectedContext()
    {
        var source = new StatusSource();
        CalendarViewModel vm = Create(source);
        await vm.ActivateAsync();
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm, Saturday));
        TradingCalendarMonth month = vm.MonthData!;
        TradingCalendarDayDetails day = vm.DayDetails!;
        CalendarDayCell cell = Cell(vm, Saturday);
        foreach (var expected in new[] { (true, "Draft", "Continue Journal"), (false, "Completed", "Open Journal"), (true, "Draft", "Continue Journal") })
        {
            source.Set(null, Status(Saturday, expected.Item1));
            vm.OnJournalCommitted();
            await vm.JournalLoadTask;
            Assert.Equal(expected.Item2, cell.JournalStatusText);
            Assert.Equal(expected.Item3, vm.DayJournalActionText);
            Assert.Same(month, vm.MonthData);
            Assert.Same(day, vm.DayDetails);
            Assert.Same(cell, Cell(vm, Saturday));
            Assert.Equal(Saturday, vm.SelectedDate);
            Assert.Equal(new DateOnly(2026, 9, 1), vm.SelectedMonth);
        }
        vm.Deactivate();
        int calls = source.Calls.Count;
        source.Set(null);
        vm.OnJournalCommitted();
        Assert.Equal(calls, source.Calls.Count);
        await vm.ActivateAsync();
        Assert.False(Cell(vm, Saturday).HasJournal);
        Assert.Equal("Add Journal", vm.DayJournalActionText);
    }

    [Fact]
    public async Task DeletedAccountStaysSelectedAndBlocksJournalNavigationWithoutAllAccountFallback()
    {
        var source = new StatusSource();
        var accounts = new AccountsSource();
        source.Set(null, Status(Saturday, true));
        source.Set(accounts.Historical.Id, Status(Saturday, false));
        CalendarViewModel vm = Create(source, accounts);
        await vm.ActivateAsync();
        vm.SelectedAccount = vm.Accounts.Single(a => a.Id == accounts.Historical.Id);
        await vm.LoadTask;
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm, Saturday));
        accounts.Rows = [];
        int calls = source.Calls.Count;
        await vm.RefreshAsync();
        Assert.Equal(calls, source.Calls.Count);
        Assert.Equal(accounts.Historical.Id, vm.SelectedAccount.Id);
        Assert.Contains("unavailable", vm.SelectedAccount.Name);
        Assert.Contains("no longer available", vm.JournalErrorMessage);
        Assert.False(vm.CanOpenDayJournal);
        Assert.False(Cell(vm, Saturday).HasJournal);
        int navigation = 0;
        vm.OpenJournalAsync = (_, _) => { navigation++; return Task.CompletedTask; };
        await vm.NavigateToJournalAsync(Saturday, vm.SelectedAccount);
        Assert.Equal(0, navigation);
        vm.SelectedAccount = vm.Accounts.Single(a => a.Id is null);
        await vm.LoadTask;
        Assert.Null(vm.JournalErrorMessage);
        Assert.True(vm.CanOpenDayJournal);
        Assert.Equal("Draft", Cell(vm, Saturday).JournalStatusText);
    }

    [Fact]
    public async Task StatusFailureIsDistinctFromNoEntryAndManualRefreshRecovers()
    {
        var source = new StatusSource { Handler = (_, _, _, _) => throw new IOException("Synthetic status failure") };
        CalendarViewModel vm = Create(source);
        await vm.ActivateAsync();
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm, Saturday));
        Assert.False(vm.CanOpenDayJournal);
        Assert.Contains("Refresh", vm.JournalErrorMessage);
        Assert.Equal(vm.JournalErrorMessage, vm.DayJournalStatusMessage);
        Assert.NotNull(vm.MonthData);
        Assert.True(vm.IsSelectedDayEmpty);
        source.Handler = null;
        source.Set(null, Status(Saturday, true));
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Null(vm.JournalErrorMessage);
        Assert.True(vm.CanOpenDayJournal);
        Assert.Equal("Continue Journal", vm.DayJournalActionText);
    }

    [Theory]
    [InlineData("month")]
    [InlineData("account")]
    [InlineData("currency")]
    [InlineData("cancel")]
    [InlineData("deactivate")]
    public async Task LateStatusReadCannotRestoreOldScopeOrSurviveCancellation(string transition)
    {
        var source = new StatusSource();
        var accounts = new AccountsSource();
        CalendarViewModel vm = Create(source, accounts);
        await vm.ActivateAsync();
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm, Saturday));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<IReadOnlyList<DailyJournalStatus>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken previousToken = default;
        source.Handler = (_, _, _, token) => { previousToken = token; started.SetResult(); return delayed.Task; };
        Task previous = vm.RefreshAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(vm.IsJournalLoading);
        Assert.False(vm.CanOpenDayJournal);
        source.Handler = null;
        if (transition == "month") vm.NextCommand.Execute(null);
        else if (transition == "account") vm.SelectedAccount = vm.Accounts.Single(a => a.Id == accounts.Historical.Id);
        else if (transition == "currency") vm.SelectedCurrency = "EUR";
        else if (transition == "cancel") vm.CancelCommand.Execute(null);
        else vm.Deactivate();
        if (transition is "month" or "account" or "currency") await vm.LoadTask;
        Assert.True(previousToken.IsCancellationRequested);
        delayed.SetResult([Status(Saturday, false)]);
        await previous;
        Assert.All(vm.Weeks.SelectMany(w => w.Days), cell => Assert.False(cell.HasJournal));
        Assert.Null(vm.SelectedDayJournalStatus);
        Assert.False(vm.IsJournalLoading);
        Assert.False(vm.IsBusy);
        if (transition is "cancel" or "deactivate") Assert.False(vm.CanOpenDayJournal);
        else if (transition != "month") Assert.Equal("Add Journal", vm.DayJournalActionText);
    }

    [Fact]
    public async Task LateCommitStatusCannotReplaceNewerCommittedStatus()
    {
        var source = new StatusSource();
        CalendarViewModel vm = Create(source);
        await vm.ActivateAsync();
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm, Saturday));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<IReadOnlyList<DailyJournalStatus>>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.Handler = (_, _, _, _) => { started.SetResult(); return delayed.Task; };
        vm.OnJournalCommitted();
        Task previous = vm.JournalLoadTask;
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        source.Handler = null;
        DailyJournalStatus latest = Status(Saturday, false) with { Revision = 9 };
        source.Set(null, latest);
        vm.OnJournalCommitted();
        await vm.JournalLoadTask;
        delayed.SetResult([Status(Saturday, true)]);
        await previous;
        Assert.Equal(latest, Cell(vm, Saturday).JournalStatus);
        Assert.Equal(latest, vm.SelectedDayJournalStatus);
        Assert.Equal("Open Journal", vm.DayJournalActionText);
    }

    private static CalendarDayCell Cell(CalendarViewModel vm, DateOnly date) => vm.Weeks.SelectMany(w => w.Days).Single(d => d.Date == date);
    private static DailyJournalStatus Status(DateOnly date, bool draft) => new(Guid.NewGuid(), date, draft, 1);
    private static CalendarViewModel Create(StatusSource source, AccountsSource? accounts = null) => new(
        new MonthReader(), new Clock(), new FakeTradingCalendarDayReader(), accounts ?? new AccountsSource(), journalStatusReader: source);
    private sealed class Clock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero); }
    private sealed class MonthReader : ITradingCalendarReader
    {
        public Task<TradingCalendarMonth> GetAsync(TradingCalendarQuery query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateOnly[] dates = Enumerable.Range(0, query.GridEnd.DayNumber - query.GridStart.DayNumber + 1).Select(query.GridStart.AddDays).ToArray();
            return Task.FromResult(new TradingCalendarMonth(query.MonthStart, query.GridStart, query.GridEnd, dates, []));
        }
    }
    private sealed class AccountsSource : ITradingAccountReader
    {
        public AccountListItem Historical { get; } = new(Guid.NewGuid(), "Historical", TradingAccountType.Personal, null, null, "USD", 0m, false);
        public IReadOnlyList<AccountListItem> Rows { get; set; }
        public AccountsSource() => Rows = [Historical];
        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Rows); }
        public Task<TradingAccountDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class StatusSource : IDailyJournalStatusReader
    {
        private readonly Dictionary<Guid, IReadOnlyList<DailyJournalStatus>> _rows = [];
        public ConcurrentQueue<(DateOnly From, DateOnly Through, Guid? Account, CancellationToken Token)> Calls { get; } = new();
        public Func<DateOnly, DateOnly, Guid?, CancellationToken, Task<IReadOnlyList<DailyJournalStatus>>>? Handler { get; set; }
        public void Set(Guid? account, params DailyJournalStatus[] statuses) => _rows[account ?? Guid.Empty] = statuses.Select(s => s with { TradingAccountId = account }).ToArray();
        public Task<IReadOnlyList<DailyJournalStatus>> GetAsync(DateOnly from, DateOnly through, Guid? tradingAccountId = null, CancellationToken cancellationToken = default)
        {
            Calls.Enqueue((from, through, tradingAccountId, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            return Handler?.Invoke(from, through, tradingAccountId, cancellationToken) ?? Task.FromResult<IReadOnlyList<DailyJournalStatus>>(
                _rows.GetValueOrDefault(tradingAccountId ?? Guid.Empty, []).Where(s => s.TradingDate >= from && s.TradingDate <= through).ToArray());
        }
    }
}
