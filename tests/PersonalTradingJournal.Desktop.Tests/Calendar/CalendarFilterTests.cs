using System.Collections.Concurrent;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed class CalendarFilterTests
{
    [Fact]
    public async Task DefaultsIncludeInactiveAccountsAndKeepCurrencyAndSaturdayResultsSeparate()
    {
        var data = new Data();
        var vm = await Create(data);
        Assert.Null(vm.SelectedAccount.Id);
        Assert.Equal("All accounts", vm.SelectedAccount.Name);
        Assert.Equal("All currencies", vm.SelectedCurrency);
        Assert.Equal(new[] { "All currencies", "EUR", "USD" }, vm.Currencies);
        Assert.Contains(vm.Accounts, a => a.Id == data.Historical.Id && a.Name.EndsWith("(inactive)"));
        CalendarDayCell saturday = Cell(vm);
        Assert.False(saturday.ShowsDailySummary);
        Assert.Equal(2, saturday.WeeklySummaries.Count);
        Assert.Equal(119m, saturday.WeeklySummaries.Single(s => s.Currency == "USD").Amount);
        await vm.SelectDayCommand.ExecuteAsync(saturday);
        Assert.Equal(3, vm.DayDetails!.ClosedTradeCount);
        Assert.Equal(2, vm.DaySummaries.Count);
        Assert.Equal(109m, vm.DaySummaries.Single(s => s.Currency == "USD").Amount);
    }

    [Fact]
    public async Task AccountAndCurrencyFilterBothReadersGridCountsAndDayDetailsWithoutChangingEconomics()
    {
        var data = new Data();
        var vm = await Create(data);
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm));
        vm.SelectedAccount = vm.Accounts.Single(a => a.Id == data.Historical.Id);
        await vm.LoadTask;
        vm.SelectedCurrency = "USD";
        await vm.LoadTask;
        Assert.Equal(Data.Saturday, vm.SelectedDate);
        Assert.All(data.MonthQueries.TakeLast(2), q => Assert.Equal(data.Historical.Id, q.TradingAccountId));
        Assert.All(data.DayQueries.TakeLast(2), q => Assert.Equal(data.Historical.Id, q.TradingAccountId));
        var week = Assert.Single(Cell(vm).WeeklySummaries);
        Assert.Equal("USD", week.Currency);
        Assert.Equal(19m, week.Amount); // Saturday 9 + Sunday 10, not the other Account's 100.
        Assert.Equal(2, week.Metrics.ClosedTradeCount);
        Assert.Equal(9m, Assert.Single(Cell(vm).DailySummaries).Amount);
        var row = Assert.Single(vm.DayTrades).Trade;
        Assert.Equal(data.Historical.Id, row.TradingAccountId);
        Assert.Equal("USD", row.Currency);
        Assert.Null(row.NetPnL);
        Assert.Equal(9m, Assert.Single(vm.DaySummaries).Amount);
        Assert.True(vm.DaySummaries[0].Metrics.EffectiveNet.IsEstimated);
        Assert.Equal("1 closed Trade", vm.DayTradeCountText);
        vm.SelectedCurrency = "EUR";
        await vm.LoadTask;
        Assert.Equal(-2m, Assert.Single(vm.DaySummaries).Amount);
        Assert.Equal(-2m, Assert.Single(Cell(vm).WeeklySummaries).Amount);
        Assert.Equal(1, vm.DayDetails!.ClosedTradeCount);
        vm.SelectedCurrency = "All currencies";
        await vm.LoadTask;
        Assert.Equal(2, vm.DayDetails!.ClosedTradeCount);
        Assert.Equal(2, vm.DaySummaries.Count);
    }

    [Fact]
    public async Task EmptyMonthsAndMissingCurrencyKeepSelectionsAcrossNavigationAndToday()
    {
        var data = new Data();
        var vm = await Create(data);
        vm.SelectedAccount = vm.Accounts.Single(a => a.Id == data.Historical.Id);
        await vm.LoadTask;
        vm.SelectedCurrency = "EUR";
        await vm.LoadTask;
        vm.NextCommand.Execute(null);
        await vm.LoadTask;
        Assert.Empty(vm.MonthData!.Currencies);
        Assert.Equal("EUR", vm.SelectedCurrency);
        Assert.Contains("EUR", vm.Currencies);
        Assert.Equal(data.Historical.Id, vm.SelectedAccount.Id);
        Assert.All(vm.Weeks.SelectMany(w => w.Days), d => Assert.Empty(d.DailySummaries));
        await vm.SelectDayCommand.ExecuteAsync(vm.Weeks[1].Days[5]);
        Assert.True(vm.IsSelectedDayEmpty);
        vm.TodayCommand.Execute(null);
        await vm.LoadTask;
        Assert.Equal(new DateOnly(2026, 9, 1), vm.SelectedMonth);
        Assert.Equal("EUR", vm.SelectedCurrency);
        Assert.Equal(data.Historical.Id, vm.SelectedAccount.Id);
        Assert.Null(vm.SelectedDate);
        Assert.Equal(-2m, Assert.Single(Cell(vm).WeeklySummaries).Amount);
        vm.Deactivate();
        await vm.ActivateAsync();
        Assert.Equal("EUR", vm.SelectedCurrency);
        Assert.Equal(data.Historical.Id, vm.SelectedAccount.Id);
        Assert.Equal(-2m, Assert.Single(Cell(vm).WeeklySummaries).Amount);
    }

    [Fact]
    public async Task DeletedAccountRemainsExplicitlySelectedAndNeverLoadsAllAccountResults()
    {
        var data = new Data();
        var vm = await Create(data);
        vm.SelectedAccount = vm.Accounts.Single(a => a.Id == data.Historical.Id);
        await vm.LoadTask;
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm));
        data.AccountRows = [data.Other];
        int monthCalls = data.MonthQueries.Count, dayCalls = data.DayQueries.Count;
        await vm.RefreshAsync();
        Assert.Equal(data.Historical.Id, vm.SelectedAccount.Id);
        Assert.Contains("(unavailable)", vm.SelectedAccount.Name);
        Assert.Contains("no longer available", vm.ErrorMessage);
        Assert.Contains("no longer available", vm.DayErrorMessage);
        Assert.Null(vm.MonthData);
        Assert.Empty(vm.DayTrades);
        Assert.Equal(monthCalls, data.MonthQueries.Count);
        Assert.Equal(dayCalls, data.DayQueries.Count);
        vm.SelectedAccount = vm.Accounts.Single(a => a.Id is null);
        await vm.LoadTask;
        Assert.Null(vm.ErrorMessage);
        Assert.Null(vm.DayErrorMessage);
        Assert.NotNull(vm.MonthData);
        Assert.Equal(3, vm.DayDetails!.ClosedTradeCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RapidFilterChangesCancelAndDiscardBothLateMonthAndDayReads(bool changeAccount)
    {
        var data = new Data();
        var vm = await Create(data);
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm));
        var monthStarted = Signal();
        var dayStarted = Signal();
        var oldMonth = new TaskCompletionSource<TradingCalendarMonth>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldDay = new TaskCompletionSource<TradingCalendarDayDetails>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken monthToken = default, dayToken = default;
        data.MonthDelay = (q, token) => { monthToken = token; monthStarted.TrySetResult(); return oldMonth.Task; };
        data.DayDelay = (q, token) => { dayToken = token; dayStarted.TrySetResult(); return oldDay.Task; };
        Task old = vm.RefreshAsync();
        await Task.WhenAll(monthStarted.Task, dayStarted.Task).WaitAsync(TimeSpan.FromSeconds(10));
        TradingCalendarMonth previousMonth = await data.Month(new(2026, 9));
        TradingCalendarDayDetails previousDay = data.Day(new(Data.Saturday));
        data.MonthDelay = null;
        data.DayDelay = null;
        if (changeAccount) vm.SelectedAccount = vm.Accounts.Single(a => a.Id == data.Other.Id);
        else vm.SelectedCurrency = "EUR";
        await vm.LoadTask;
        Assert.True(monthToken.IsCancellationRequested);
        Assert.True(dayToken.IsCancellationRequested);
        oldMonth.SetResult(previousMonth);
        oldDay.SetResult(previousDay);
        await old;
        Assert.Single(vm.DayTrades);
        Assert.Single(vm.DaySummaries);
        Assert.Single(Cell(vm).WeeklySummaries);
        Assert.Equal(changeAccount ? 100m : -2m, vm.DaySummaries[0].Amount);
        Assert.Equal(changeAccount ? "USD" : "EUR", vm.DaySummaries[0].Currency);
        Assert.False(vm.IsLoading);
        Assert.False(vm.IsDayLoading);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static CalendarDayCell Cell(CalendarViewModel vm) => CalendarDayDetailsTests.Cell(vm, Data.Saturday);
    private static async Task<CalendarViewModel> Create(Data data)
    {
        var vm = new CalendarViewModel(data, new Clock(), data, data);
        await vm.ActivateAsync();
        return vm;
    }
    private sealed class Clock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero); }

    private sealed class Data : ITradingCalendarReader, ITradingCalendarDayReader, ITradingAccountReader, IDashboardAnalyticsReader
    {
        public static readonly DateOnly Saturday = new(2026, 9, 5);
        public AccountListItem Historical { get; } = new(Guid.NewGuid(), "Historical", TradingAccountType.Personal, null, null, "USD", 0m, false);
        public AccountListItem Other { get; } = new(Guid.NewGuid(), "Other", TradingAccountType.Personal, null, null, "USD", 0m, true);
        public IReadOnlyList<AccountListItem> AccountRows { get; set; }
        private readonly TradeListItem[] _rows;
        public ConcurrentQueue<TradingCalendarQuery> MonthQueries { get; } = new();
        public ConcurrentQueue<TradingCalendarDayQuery> DayQueries { get; } = new();
        public Func<TradingCalendarQuery, CancellationToken, Task<TradingCalendarMonth>>? MonthDelay { get; set; }
        public Func<TradingCalendarDayQuery, CancellationToken, Task<TradingCalendarDayDetails>>? DayDelay { get; set; }
        public Data()
        {
            AccountRows = [Historical, Other];
            _rows = [Row(Saturday, Historical.Id, "USD", 9m, null), Row(Saturday, Historical.Id, "EUR", -2m, -2m),
                Row(Saturday.AddDays(1), Historical.Id, "USD", 10m, 10m), Row(Saturday, Other.Id, "USD", 100m, 100m)];
        }
        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(AccountRows); }
        public Task<TradingAccountDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradingCalendarMonth> GetAsync(TradingCalendarQuery q, CancellationToken ct = default)
        { MonthQueries.Enqueue(q); return MonthDelay?.Invoke(q, ct) ?? Month(q); }
        public Task<TradingCalendarDayDetails> GetAsync(TradingCalendarDayQuery q, CancellationToken ct = default)
        { DayQueries.Enqueue(q); return DayDelay?.Invoke(q, ct) ?? Task.FromResult(Day(q)); }
        public Task<TradingCalendarMonth> Month(TradingCalendarQuery q) => new TradingCalendarReader(this).GetAsync(q);
        public Task<DashboardAnalyticsSnapshot> GetAsync(DashboardAnalyticsQuery q, CancellationToken ct = default) =>
            Task.FromResult(DashboardMetricCalculator.Calculate(_rows.Where(r => (!q.TradingAccountId.HasValue || r.TradingAccountId == q.TradingAccountId) &&
                r.ClosedAtUtc >= q.ClosedFromUtc && r.ClosedAtUtc < q.ClosedBeforeUtc).Select(r =>
                    new TradeAnalyticsFact(r.Id, r.Status, r.OpenedAtUtc, r.ClosedAtUtc, r.Currency, null, r.GrossPnL, r.TotalCosts, r.NetPnL)), ct));
        public TradingCalendarDayDetails Day(TradingCalendarDayQuery q) => TradingCalendarDayDetails.Create(q.Date,
            _rows.Where(r => (!q.TradingAccountId.HasValue || r.TradingAccountId == q.TradingAccountId) &&
                DashboardMetricCalculator.GetNewYorkCloseDate(r.ClosedAtUtc!.Value) == q.Date));
        private static TradeListItem Row(DateOnly date, Guid account, string currency, decimal gross, decimal? net) =>
            CalendarDayDetailsTests.Row(date, gross, net) with { TradingAccountId = account, Currency = currency };
    }
}
