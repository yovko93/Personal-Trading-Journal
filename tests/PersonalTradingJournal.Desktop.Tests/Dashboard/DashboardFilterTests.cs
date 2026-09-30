using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.Dashboard;

public sealed class DashboardFilterTests
{
    [Fact]
    public async Task DefaultsAndSwitchingAccountsScopeAnalyticsAndRecentRowsIncludingInactiveAccounts()
    {
        var accounts = new Accounts(Account("Zulu"), Account("Archive", active: false));
        var analytics = Reader();
        var recent = new FakeTradeListReader();
        var vm = Create(accounts, analytics, recent);
        await vm.ActivateAsync();
        Assert.Equal("All accounts", vm.SelectedAccount.Name);
        Assert.Null(analytics.Queries.Last().TradingAccountId);
        Assert.Null(recent.RequestedQueries.Last().TradingAccountId);
        Assert.Equal(new[] { "All accounts", "Archive (inactive)", "Zulu" }, vm.Accounts.Select(a => a.Name));
        vm.SelectedAccount = vm.Accounts[1];
        await vm.LoadTask;
        Guid archived = accounts.Items[1].Id;
        Assert.Equal(archived, analytics.Queries.Last().TradingAccountId);
        Assert.Equal(archived, recent.RequestedQueries.Last().TradingAccountId);
        vm.SelectedAccount = null!; // A transient WPF ItemsSource selection reset must not widen the scope.
        Assert.Equal(archived, vm.SelectedAccount.Id);
        vm.Period = DashboardPeriod.Month;
        await vm.LoadTask;
        Assert.Equal(archived, vm.Query.TradingAccountId);
        Assert.Equal(archived, recent.RequestedQueries.Last().TradingAccountId);
        vm.SelectedAccount = vm.Accounts[0];
        await vm.LoadTask;
        Assert.Null(vm.Query.TradingAccountId);
        Assert.Null(recent.RequestedQueries.Last().TradingAccountId);
        Assert.Equal(DashboardPeriod.Month, vm.Period);
    }

    [Fact]
    public async Task DeletedSelectionClearsOldResultsWithoutFallingBackAndCanRecoverExplicitly()
    {
        var accounts = new Accounts(Account("Deleted later"));
        var analytics = Reader(DashboardViewModelTests.Fact(10m));
        var recent = new FakeTradeListReader();
        var vm = Create(accounts, analytics, recent);
        await vm.RefreshAsync();
        vm.SelectedAccount = vm.Accounts[1];
        await vm.LoadTask;
        Guid selected = vm.SelectedAccount.Id!.Value;
        Assert.NotNull(vm.Selected);
        int queries = analytics.Queries.Count, recentQueries = recent.RequestedQueries.Count;
        accounts.Items = [];
        await vm.RefreshAsync();
        Assert.Equal(selected, vm.SelectedAccount.Id);
        Assert.Contains("unavailable", vm.SelectedAccount.Name);
        Assert.Contains("no longer available", vm.ErrorMessage);
        Assert.Null(vm.Selected);
        Assert.Empty(vm.RecentTrades);
        Assert.Equal(queries, analytics.Queries.Count);
        Assert.Equal(recentQueries, recent.RequestedQueries.Count);
        vm.SelectedAccount = vm.Accounts[0];
        await vm.LoadTask;
        Assert.Null(vm.ErrorMessage);
        Assert.Null(vm.Query.TradingAccountId);
    }

    [Theory]
    [InlineData("2026-03-08", 23)]
    [InlineData("2026-11-01", 25)]
    public async Task InclusiveCustomDayUsesNewYorkDstAndDisablesCalendarNavigation(string date, int hours)
    {
        var vm = Create();
        vm.StartDate = vm.EndDate = DateTime.Parse(date);
        Assert.True(vm.ApplyRangeCommand.CanExecute(null));
        vm.ApplyRangeCommand.Execute(null);
        await vm.LoadTask;
        Assert.Equal(DashboardPeriod.Custom, vm.Period);
        Assert.Equal(DateOnly.Parse(date), vm.Query.ClosedFromNewYork);
        Assert.Equal(DateOnly.Parse(date), vm.Query.ClosedThroughNewYork);
        Assert.Equal(hours, (vm.Query.ClosedBeforeUtc - vm.Query.ClosedFromUtc)!.Value.TotalHours);
        Assert.False(vm.PreviousCommand.CanExecute(null));
        Assert.False(vm.NextCommand.CanExecute(null));
        Assert.Contains(date, vm.PeriodLabel);
        Assert.True(vm.IsEmpty);
        vm.Period = DashboardPeriod.Year;
        await vm.LoadTask;
        Assert.Equal(new DateTime(2026, 1, 1), vm.StartDate);
        Assert.Equal(new DateTime(2026, 12, 31), vm.EndDate);
        Assert.True(vm.PreviousCommand.CanExecute(null));
        vm.AllHistoryCommand.Execute(null);
        await vm.LoadTask;
        Assert.Equal(DashboardPeriod.All, vm.Period);
        Assert.Null(vm.Query.ClosedFromUtc);
        Assert.Null(vm.StartDate);
        Assert.Null(vm.EndDate);
    }

    [Fact]
    public async Task InvalidOrUnappliedDraftCannotChangeAppliedResults()
    {
        var reader = Reader();
        var vm = Create(analytics: reader);
        await vm.RefreshAsync();
        Assert.False(vm.ApplyRangeCommand.CanExecute(null));
        vm.StartDate = new(2026, 9, 30);
        Assert.NotNull(vm.RangeValidationMessage);
        vm.EndDate = new(2026, 9, 1);
        Assert.Contains("on or before", vm.RangeValidationMessage);
        Assert.False(vm.ApplyRangeCommand.CanExecute(null));
        vm.ApplyRangeCommand.Execute(null);
        Assert.Equal(DashboardPeriod.All, vm.Period);
        Assert.Null(vm.Query.ClosedFromUtc);
        Assert.Single(reader.Queries);
        vm.EndDate = new(2026, 9, 30);
        Assert.Null(vm.RangeValidationMessage);
        Assert.True(vm.ApplyRangeCommand.CanExecute(null));
        Assert.Null(vm.Query.ClosedFromUtc); // Explicit Apply prevents partial draft reads.
        vm.RejectInvalidDate(isStart: true);
        Assert.Null(vm.StartDate);
        Assert.False(vm.ApplyRangeCommand.CanExecute(null));
        Assert.NotNull(vm.RangeValidationMessage);
        vm.StartDate = vm.EndDate = DateTime.MaxValue.Date;
        Assert.False(vm.ApplyRangeCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("2026-01-01T03:00:00Z", "Today", "2025-12-31", "2025-12-31")]
    [InlineData("2026-01-05T12:00:00Z", "LastWeek", "2025-12-29", "2026-01-04")]
    [InlineData("2026-01-04T12:00:00Z", "LastWeek", "2025-12-22", "2025-12-28")]
    [InlineData("2026-01-10T12:00:00Z", "LastMonth", "2025-12-01", "2025-12-31")]
    [InlineData("2024-03-10T12:00:00Z", "LastMonth", "2024-02-01", "2024-02-29")]
    public async Task ShortcutsUseCompleteNewYorkCalendarPeriods(string now, string shortcut, string first, string last)
    {
        var vm = Create(now: now);
        var command = shortcut switch { "Today" => vm.TodayCommand, "LastWeek" => vm.LastWeekCommand, _ => vm.LastMonthCommand };
        command.Execute(null);
        await vm.LoadTask;
        Assert.Equal(DashboardPeriod.Custom, vm.Period);
        Assert.Equal(DateOnly.Parse(first), vm.Query.ClosedFromNewYork);
        Assert.Equal(DateOnly.Parse(last), vm.Query.ClosedThroughNewYork);
        Assert.False(vm.PreviousCommand.CanExecute(null));
        Assert.False(vm.NextCommand.CanExecute(null));
    }

    [Fact]
    public async Task AccountAndCustomRangeChangeRejectOlderReadEvenWhenItIgnoresCancellation()
    {
        var accounts = new Accounts(Account("First"), Account("Second"));
        var reader = Reader();
        var vm = Create(accounts, reader);
        await vm.ActivateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<DashboardAnalyticsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.Read = (_, _) => { entered.SetResult(); return delayed.Task; };
        vm.SelectedAccount = vm.Accounts[1];
        Task old = vm.LoadTask;
        await entered.Task;
        reader.Read = (_, _) => Task.FromResult(DashboardMetricCalculator.Calculate([DashboardViewModelTests.Fact(25m)]));
        vm.SelectedAccount = vm.Accounts[2];
        vm.TodayCommand.Execute(null);
        await vm.LoadTask;
        delayed.SetResult(DashboardMetricCalculator.Calculate([DashboardViewModelTests.Fact(999m)]));
        await old;
        Assert.Equal(accounts.Items[1].Id, vm.SelectedAccount.Id);
        Assert.Equal(DashboardPeriod.Custom, vm.Period);
        Assert.Equal(25m, vm.Selected!.Source.Metrics.EffectiveNet.Total);
        Assert.Null(vm.ErrorMessage);
    }

    private static DashboardViewModel Create(Accounts? accounts = null, FakeDashboardAnalyticsReader? analytics = null,
        FakeTradeListReader? recent = null, string now = "2026-09-30T12:00:00Z") =>
        new(analytics ?? Reader(), new FixedTime(DateTimeOffset.Parse(now)), recent ?? new(), accounts ?? new());
    private static FakeDashboardAnalyticsReader Reader(params TradeAnalyticsFact[] facts) => new()
        { Read = (_, _) => Task.FromResult(DashboardMetricCalculator.Calculate(facts)) };
    private static AccountListItem Account(string name, bool active = true) =>
        new(Guid.NewGuid(), name, TradingAccountType.Personal, null, null, "USD", null, active);
    private sealed class FixedTime(DateTimeOffset instant) : TimeProvider { public override DateTimeOffset GetUtcNow() => instant; }
    private sealed class Accounts(params AccountListItem[] items) : ITradingAccountReader
    {
        public IReadOnlyList<AccountListItem> Items { get; set; } = items;
        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Items); }
        public Task<TradingAccountDetails?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
