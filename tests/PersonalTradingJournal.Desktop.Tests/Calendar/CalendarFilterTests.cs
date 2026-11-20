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
    public async Task EveningTradeAppearsOnNextGridDateAndBothDaysRemainSelectable()
    {
        var data = new Data();
        var date = new DateOnly(2026, 9, 9);
        var close = new DateTimeOffset(2026, 9, 9, 22, 12, 0, TimeSpan.Zero);
        var evening = data.Rows[0] with { Id = Guid.NewGuid(), OpenedAtUtc = close.AddHours(-1), ClosedAtUtc = close };
        data.Rows = [evening];
        var vm = await Create(data);

        var previousCell = CalendarDayDetailsTests.Cell(vm, date);
        var nextCell = CalendarDayDetailsTests.Cell(vm, date.AddDays(1));
        Assert.Empty(previousCell.DailySummaries);
        Assert.Equal(9m, Assert.Single(nextCell.DailySummaries).Amount);
        await vm.SelectDayCommand.ExecuteAsync(previousCell);
        Assert.Equal(previousCell.Date, vm.SelectedDate);
        await vm.SelectDayCommand.ExecuteAsync(nextCell);
        Assert.Equal(nextCell.Date, vm.SelectedDate);
        Assert.Equal(9m, Assert.Single(nextCell.DailySummaries).Amount);
    }

    [Theory]
    [InlineData("create", 14)]
    [InlineData("edit", -285)]
    [InlineData("delete", 0)]
    public async Task CommittedChangesRefreshGridWithoutResettingDateAndFilters(string operation, int amount)
    {
        var data = new Data();
        var vm = await Create(data);
        vm.SelectedAccount = vm.Accounts.Single(a => a.Id == data.Historical.Id);
        await vm.LoadTask;
        vm.SelectedCurrency = "USD";
        await vm.LoadTask;
        CalendarDayCell selected = Cell(vm);
        await vm.SelectDayCommand.ExecuteAsync(selected);
        var weeks = vm.Weeks;
        var original = data.Rows.Single(r => r.TradingAccountId == data.Historical.Id && r.Currency == "USD" &&
            DashboardMetricCalculator.GetNewYorkCloseDate(r.ClosedAtUtc!.Value) == Data.Saturday);
        data.Rows = operation switch
        {
            "create" => [.. data.Rows, original with { Id = Guid.NewGuid(), GrossPnL = 5m, NetPnL = 5m, TotalCosts = 0m }],
            "edit" => data.Rows.Select(r => r.Id == original.Id ? r with { GrossPnL = -285m, NetPnL = null } : r).ToArray(),
            _ => data.Rows.Where(r => r.Id != original.Id).ToArray(),
        };
        int monthReads = data.MonthQueries.Count;
        vm.OnDataCommitted();
        await vm.LoadTask;
        Assert.Equal(monthReads + 1, data.MonthQueries.Count);
        Assert.Equal(new DateOnly(2026, 9, 1), vm.SelectedMonth);
        Assert.Equal(Data.Saturday, vm.SelectedDate);
        Assert.Equal(data.Historical.Id, vm.SelectedAccount.Id);
        Assert.Equal("USD", vm.SelectedCurrency);
        Assert.Same(weeks, vm.Weeks);
        Assert.Same(selected, Cell(vm));
        Assert.True(selected.IsSelected);
        Assert.Equal(amount, Cell(vm).DailySummaries.Sum(s => s.Amount ?? 0m));
        Assert.Equal(amount + 10m, Assert.Single(selected.WeeklySummaries).Amount); // Sunday included once.
        Assert.Equal(operation == "delete", !Cell(vm).HasDailyTrades);
        Assert.False(vm.IsBusy);
        vm.Deactivate();
        vm.OnDataCommitted();
        Assert.Equal(monthReads + 1, data.MonthQueries.Count);
    }

    [Fact]
    public async Task ManualRefreshAndCommitRejectLateMonthReadsAndCancelStopsLoading()
    {
        var data = new Data();
        var vm = await Create(data);
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm));
        var monthStarted = Signal();
        var oldMonth = new TaskCompletionSource<TradingCalendarMonth>(TaskCreationOptions.RunContinuationsAsynchronously);
        var previousMonth = vm.MonthData!;
        CancellationToken monthToken = default;
        data.MonthDelay = (_, token) => { monthToken = token; monthStarted.TrySetResult(); return oldMonth.Task; };
        Task old = vm.RefreshCommand.ExecuteAsync(null);
        await monthStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(vm.IsBusy);
        Assert.Contains("Loading", vm.AccessibleStatus);
        Assert.True(Cell(vm).IsBusy);
        vm.CancelCommand.Execute(null);
        Assert.True(monthToken.IsCancellationRequested);
        Assert.False(vm.IsBusy);
        Assert.Contains("cancelled", vm.StatusMessage);
        data.MonthDelay = null;
        data.Rows = [];
        vm.OnDataCommitted();
        await vm.LoadTask;
        oldMonth.SetResult(previousMonth);
        await old;
        Assert.True(vm.IsMonthEmpty);
        Assert.False(Cell(vm).HasDailyTrades);
        Assert.Empty(Cell(vm).WeeklySummaries);
        Assert.Null(vm.StatusMessage);
        Assert.False(Cell(vm).IsBusy);
    }

    [Fact]
    public async Task EmptyMonthDoesNotConfuseAdjacentActivityZeroOrUnavailableWithNoTradesAndErrorsRecover()
    {
        var data = new Data();
        var vm = await Create(data);
        data.Rows = [CalendarDayDetailsTests.Row(new(2026, 8, 31), 5m, 5m)];
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.IsMonthEmpty); // Visible adjacent-month activity is not activity in September.
        Assert.Empty(vm.MonthlySummaries);
        Assert.Equal("No closed Trades", vm.MonthlyStatusText);
        Assert.True(CalendarDayDetailsTests.Cell(vm, new(2026, 8, 31)).HasDailyTrades);
        foreach (decimal? amount in new decimal?[] { 0m, null })
        {
            data.Rows = [CalendarDayDetailsTests.Row(Data.Saturday, amount, amount)];
            await vm.RefreshAsync();
            await vm.SelectDayCommand.ExecuteAsync(Cell(vm));
            Assert.False(vm.IsMonthEmpty);
            Assert.True(Cell(vm).HasDailyTrades);
            Assert.Equal(amount, Assert.Single(Cell(vm).DailySummaries).Amount);
            Assert.Equal(amount, Assert.Single(vm.MonthlySummaries).Amount);
            Assert.Equal(amount is null ? "— USD" : "0.00 USD", vm.MonthlySummaries[0].AmountText);
        }
        data.MonthDelay = (_, _) => throw new IOException("Synthetic failure");
        await vm.RefreshAsync();
        Assert.Contains("Refresh", vm.ErrorMessage);
        Assert.False(vm.IsMonthEmpty);
        Assert.False(Cell(vm).IsDataLoaded);
        Assert.NotEmpty(vm.Weeks);
        Assert.Empty(vm.MonthlySummaries);
        Assert.Equal("—", vm.MonthlyStatusText);
        Assert.Equal(Data.Saturday, vm.SelectedDate);
        data.MonthDelay = null;
        data.Rows = [];
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Null(vm.ErrorMessage);
        Assert.True(vm.IsMonthEmpty);
        Assert.False(Cell(vm).HasDailyTrades);
    }

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
        Assert.Equal(new[] { "EUR", "USD" }, vm.MonthlySummaries.Select(s => s.Summary.Currency));
        Assert.Equal(new decimal?[] { -2m, 119m }, vm.MonthlySummaries.Select(s => s.Amount));
        await vm.SelectDayCommand.ExecuteAsync(saturday);
        Assert.Equal(2, Cell(vm).DailySummaries.Count);
        Assert.Equal(109m, Cell(vm).DailySummaries.Single(s => s.Currency == "USD").Amount);
    }

    [Fact]
    public async Task AccountAndCurrencyFilterGridCountsWithoutChangingEconomics()
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
        var week = Assert.Single(Cell(vm).WeeklySummaries);
        Assert.Equal("USD", week.Currency);
        Assert.Equal(19m, week.Amount); // Saturday 9 + Sunday 10, not the other Account's 100.
        Assert.Equal(2, week.Metrics.ClosedTradeCount);
        Assert.Equal(19m, Assert.Single(vm.MonthlySummaries).Amount);
        Assert.Equal(2, vm.MonthlySummaries[0].Summary.Coverage.ClosedTradeCount);
        Assert.True(vm.MonthlySummaries[0].Summary.IsEstimated);
        Assert.Contains("commission/fees unknown", vm.MonthlySummaries[0].Description);
        Assert.Equal(9m, Assert.Single(Cell(vm).DailySummaries).Amount);
        Assert.Contains("9.00 USD; 1 Trade", Cell(vm).DateTooltip);
        Assert.DoesNotContain("EUR", Cell(vm).DateTooltip);
        Assert.True(Cell(vm).DailySummaries[0].Metrics.EffectiveNet.IsEstimated);
        vm.SelectedCurrency = "EUR";
        await vm.LoadTask;
        Assert.Equal(-2m, Assert.Single(Cell(vm).DailySummaries).Amount);
        Assert.Equal(-2m, Assert.Single(Cell(vm).WeeklySummaries).Amount);
        Assert.Contains("-2.00 EUR; 1 Trade", Cell(vm).DateTooltip);
        Assert.DoesNotContain("USD", Cell(vm).DateTooltip);
        Assert.Equal(-2m, Assert.Single(vm.MonthlySummaries).Amount);
        vm.SelectedCurrency = "All currencies";
        await vm.LoadTask;
        Assert.Equal(2, Cell(vm).DailySummaries.Count);
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
        Assert.Empty(vm.MonthlySummaries);
        Assert.Equal("No closed Trades", vm.MonthlyStatusText);
        Assert.Equal("EUR", vm.SelectedCurrency);
        Assert.Contains("EUR", vm.Currencies);
        Assert.Equal(data.Historical.Id, vm.SelectedAccount.Id);
        Assert.All(vm.Weeks.SelectMany(w => w.Days), d => Assert.Empty(d.DailySummaries));
        var octoberDay = vm.Weeks[1].Days[5];
        await vm.SelectDayCommand.ExecuteAsync(octoberDay);
        Assert.Equal(octoberDay.Date, vm.SelectedDate);
        Assert.False(octoberDay.HasDailyTrades);
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
        int monthCalls = data.MonthQueries.Count;
        await vm.RefreshAsync();
        Assert.Equal(data.Historical.Id, vm.SelectedAccount.Id);
        Assert.Contains("(unavailable)", vm.SelectedAccount.Name);
        Assert.Contains("no longer available", vm.ErrorMessage);
        Assert.Null(vm.MonthData);
        Assert.Empty(vm.MonthlySummaries);
        Assert.Equal("—", vm.MonthlyStatusText);
        Assert.Equal(monthCalls, data.MonthQueries.Count);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(data.Historical.Id, vm.SelectedAccount.Id);
        Assert.Contains("(unavailable)", vm.SelectedAccount.Name);
        Assert.Contains("no longer available", vm.ErrorMessage);
        Assert.False(vm.IsMonthEmpty);
        Assert.False(Cell(vm).HasDailyTrades);
        vm.SelectedAccount = vm.Accounts.Single(a => a.Id is null);
        await vm.LoadTask;
        Assert.Null(vm.ErrorMessage);
        Assert.NotNull(vm.MonthData);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RapidFilterChangesCancelAndDiscardLateMonthReads(bool changeAccount)
    {
        var data = new Data();
        var vm = await Create(data);
        await vm.SelectDayCommand.ExecuteAsync(Cell(vm));
        var monthStarted = Signal();
        var oldMonth = new TaskCompletionSource<TradingCalendarMonth>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken monthToken = default;
        data.MonthDelay = (q, token) => { monthToken = token; monthStarted.TrySetResult(); return oldMonth.Task; };
        Task old = vm.RefreshAsync();
        await monthStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        TradingCalendarMonth previousMonth = await data.Month(new(2026, 9));
        data.MonthDelay = null;
        if (changeAccount) vm.SelectedAccount = vm.Accounts.Single(a => a.Id == data.Other.Id);
        else vm.SelectedCurrency = "EUR";
        await vm.LoadTask;
        Assert.True(monthToken.IsCancellationRequested);
        oldMonth.SetResult(previousMonth);
        await old;
        Assert.Single(Cell(vm).DailySummaries);
        Assert.Single(Cell(vm).WeeklySummaries);
        Assert.Equal(changeAccount ? 100m : -2m, Cell(vm).DailySummaries[0].Amount);
        Assert.Equal(changeAccount ? "USD" : "EUR", Cell(vm).DailySummaries[0].Currency);
        Assert.False(vm.IsLoading);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static CalendarDayCell Cell(CalendarViewModel vm) => CalendarDayDetailsTests.Cell(vm, Data.Saturday);
    private static async Task<CalendarViewModel> Create(Data data)
    {
        var vm = new CalendarViewModel(data, new Clock(), new TestDoubles.FakeTradingCalendarDayReader(), data);
        await vm.ActivateAsync();
        return vm;
    }
    private sealed class Clock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero); }

    private sealed class Data : ITradingCalendarReader, ITradingAccountReader, IDashboardAnalyticsReader
    {
        public static readonly DateOnly Saturday = new(2026, 9, 5);
        public AccountListItem Historical { get; } = new(Guid.NewGuid(), "Historical", TradingAccountType.Personal, null, null, "USD", 0m, false);
        public AccountListItem Other { get; } = new(Guid.NewGuid(), "Other", TradingAccountType.Personal, null, null, "USD", 0m, true);
        public IReadOnlyList<AccountListItem> AccountRows { get; set; }
        public TradeListItem[] Rows { get; set; }
        public ConcurrentQueue<TradingCalendarQuery> MonthQueries { get; } = new();
        public Func<TradingCalendarQuery, CancellationToken, Task<TradingCalendarMonth>>? MonthDelay { get; set; }
        public Data()
        {
            AccountRows = [Historical, Other];
            Rows = [Row(Saturday, Historical.Id, "USD", 9m, null), Row(Saturday, Historical.Id, "EUR", -2m, -2m),
                Row(Saturday.AddDays(1), Historical.Id, "USD", 10m, 10m), Row(Saturday, Other.Id, "USD", 100m, 100m)];
        }
        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(AccountRows); }
        public Task<TradingAccountDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradingCalendarMonth> GetAsync(TradingCalendarQuery q, CancellationToken ct = default)
        { MonthQueries.Enqueue(q); return MonthDelay?.Invoke(q, ct) ?? Month(q); }
        public Task<TradingCalendarMonth> Month(TradingCalendarQuery q) => new TradingCalendarReader(this).GetAsync(q);
        public Task<DashboardAnalyticsSnapshot> GetAsync(DashboardAnalyticsQuery q, CancellationToken ct = default) =>
            Task.FromResult(DashboardMetricCalculator.Calculate(Rows.Where(r => (!q.TradingAccountId.HasValue || r.TradingAccountId == q.TradingAccountId) &&
                r.ClosedAtUtc >= q.ClosedFromUtc && r.ClosedAtUtc < q.ClosedBeforeUtc).Select(r =>
                    new TradeAnalyticsFact(r.Id, r.Status, r.OpenedAtUtc, r.ClosedAtUtc, r.Currency, null, r.GrossPnL, r.TotalCosts, r.NetPnL)), ct, q.GetCloseDate));
        private static TradeListItem Row(DateOnly date, Guid account, string currency, decimal gross, decimal? net) =>
            CalendarDayDetailsTests.Row(date, gross, net) with { TradingAccountId = account, Currency = currency };
    }
}
