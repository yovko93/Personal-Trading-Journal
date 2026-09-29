using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Dashboard;

public sealed class DashboardViewModelTests
{
    [Fact]
    public async Task AllIsDefaultAndUsesCompleteHistoryNotRecentTen()
    {
        var reader = Reader(Enumerable.Range(0, 121).Select(_ => Fact(1m)).ToArray());
        var vm = new DashboardViewModel(reader, Clock("2026-09-29T12:00:00Z"));
        await vm.ActivateAsync();
        Assert.Equal(DashboardPeriod.All, vm.Period);
        Assert.Null(reader.Queries[0].ClosedFromUtc);
        Assert.Null(reader.Queries[0].ClosedBeforeUtc);
        Assert.False(vm.NextCommand.CanExecute(null));
        Assert.False(vm.PreviousCommand.CanExecute(null));
        Assert.Equal(121m, vm.Selected!.Source.Metrics.Net.Total);
        Assert.Equal("121", vm.Selected.Cards.Single(c => c.Label == "Total Trades").Value);
        Assert.Equal(10, vm.Selected.RecentTrades.Count);
    }

    [Theory]
    [InlineData(DashboardPeriod.Week, "2026-03-08T12:00:00Z", "2026-03-02", "2026-03-08", 167)]
    [InlineData(DashboardPeriod.Week, "2026-11-01T12:00:00Z", "2026-10-26", "2026-11-01", 169)]
    [InlineData(DashboardPeriod.Month, "2026-01-01T03:00:00Z", "2025-12-01", "2025-12-31", 744)]
    [InlineData(DashboardPeriod.Year, "2026-01-01T03:00:00Z", "2025-01-01", "2025-12-31", 8760)]
    public async Task CurrentPeriodsUseNewYorkCalendarAndDst(DashboardPeriod period, string now, string first, string last, int hours)
    {
        var vm = new DashboardViewModel(Reader(), Clock(now)) { Period = period };
        await vm.LoadTask;
        Assert.Equal(DateOnly.Parse(first), vm.Query.ClosedFromNewYork);
        Assert.Equal(DateOnly.Parse(last), vm.Query.ClosedThroughNewYork);
        Assert.Equal(hours, (vm.Query.ClosedBeforeUtc - vm.Query.ClosedFromUtc)!.Value.TotalHours);
        Assert.False(vm.NextCommand.CanExecute(null));
        vm.NextCommand.Execute(null);
        Assert.Equal(DateOnly.Parse(first), vm.Query.ClosedFromNewYork);
    }

    [Theory]
    [InlineData(DashboardPeriod.Week, "2025-12-29", "2025-12-22")]
    [InlineData(DashboardPeriod.Month, "2026-01-01", "2025-12-01")]
    [InlineData(DashboardPeriod.Year, "2026-01-01", "2025-01-01")]
    public async Task PreviousNextCrossYearsAndSelectingAnotherPeriodResetsToCurrent(DashboardPeriod period, string current, string previous)
    {
        var vm = new DashboardViewModel(Reader(), Clock("2026-01-02T12:00:00Z")) { Period = period };
        await vm.LoadTask;
        Assert.Equal(DateOnly.Parse(current), vm.Query.ClosedFromNewYork);
        vm.PreviousCommand.Execute(null);
        await vm.LoadTask;
        Assert.Equal(DateOnly.Parse(previous), vm.Query.ClosedFromNewYork);
        Assert.True(vm.NextCommand.CanExecute(null));
        vm.NextCommand.Execute(null);
        await vm.LoadTask;
        Assert.Equal(DateOnly.Parse(current), vm.Query.ClosedFromNewYork);
        vm.PreviousCommand.Execute(null);
        vm.Period = DashboardPeriod.All;
        await vm.LoadTask;
        Assert.Null(vm.Query.ClosedFromUtc);
        vm.Period = period;
        await vm.LoadTask;
        Assert.Equal(DateOnly.Parse(current), vm.Query.ClosedFromNewYork);
    }

    [Fact]
    public async Task CurrencySelectionAppliesToCardsChartsSetupsAndRecentRows()
    {
        var vm = new DashboardViewModel(Reader(Fact(-285m, null), Fact(0m) with { Currency = "EUR" }), Clock("2026-09-29T12:00:00Z"));
        await vm.ActivateAsync();
        Assert.Equal(new[] { "EUR", "USD" }, vm.Currencies);
        vm.SelectedCurrency = "USD";
        Assert.True(vm.Selected!.Source.Metrics.EffectiveNet.IsEstimated);
        Assert.Contains("Estimated", vm.Selected.CoverageNote);
        Assert.Equal(-285m, Assert.Single(vm.Selected.DailyPnl).Value);
        Assert.Contains("estimated", Assert.Single(vm.Selected.RecentTrades));
        Assert.Contains("Estimated", Assert.Single(vm.Selected.Setups).Summary);
        Assert.Null(vm.Selected.Source.Metrics.Net.Total);
        vm.SelectedCurrency = "EUR";
        Assert.False(vm.Selected!.Source.Metrics.EffectiveNet.IsEstimated);
        Assert.Equal(0m, Assert.Single(vm.Selected.CumulativePnl).Value);
        await vm.RefreshAsync();
        Assert.Equal("EUR", vm.SelectedCurrency);
    }

    [Fact]
    public void BestWorstUseDailyTotalsWithEarliestDateTiesAndUnavailableCoverage()
    {
        CurrencyTradeMetrics currency = Assert.Single(DashboardMetricCalculator.Calculate([
            Fact(10m, day: 2), Fact(10m, null, day: 1), Fact(-5m, day: 4), Fact(-5m, day: 3)]).Currencies);
        var presentation = new DashboardCurrencyPresentation(currency);
        Assert.Equal(new DateOnly(2026, 9, 1), presentation.BestDay!.Date);
        Assert.True(presentation.BestDay.IsEstimated);
        Assert.Equal(new DateOnly(2026, 9, 3), presentation.WorstDay!.Date);
        var unavailable = new DashboardCurrencyPresentation(Assert.Single(DashboardMetricCalculator.Calculate([Fact(10m), Fact(null, null)]).Currencies));
        Assert.Null(unavailable.BestDay);
        Assert.Null(unavailable.WorstDay);
    }

    [Fact]
    public async Task EmptyErrorCancellationAndRecoveryDoNotRetainOldResults()
    {
        var reader = Reader(Fact(10m));
        var vm = new DashboardViewModel(reader, Clock("2026-09-29T12:00:00Z"));
        await vm.RefreshAsync();
        reader.Read = (_, _) => throw new InvalidOperationException("sensitive internal details");
        await vm.RefreshAsync();
        Assert.NotNull(vm.ErrorMessage);
        Assert.DoesNotContain("sensitive", vm.ErrorMessage);
        Assert.Null(vm.Selected);
        reader.Read = (_, _) => Task.FromResult(DashboardMetricCalculator.Calculate([]));
        await vm.RefreshAsync();
        Assert.True(vm.IsEmpty);
        Assert.Null(vm.ErrorMessage);
        reader.Read = async (_, token) => { await Task.Delay(Timeout.Infinite, token); return DashboardMetricCalculator.Calculate([]); };
        Task loading = vm.RefreshAsync();
        vm.CancelCommand.Execute(null);
        await loading;
        Assert.False(vm.IsLoading);
        Assert.Contains("cancelled", vm.StatusMessage);
    }

    [Fact]
    public async Task CommitDuringActivePreCommitReadCannotBeOverwrittenByOldResult()
    {
        var old = new TaskCompletionSource<DashboardAnalyticsSnapshot>();
        var started = new TaskCompletionSource();
        var reader = new FakeDashboardAnalyticsReader { Read = (_, _) => { started.SetResult(); return old.Task; } };
        var vm = new DashboardViewModel(reader, Clock("2026-09-29T12:00:00Z"));
        Task before = vm.ActivateAsync();
        await started.Task;
        reader.Read = (_, _) => Task.FromResult(DashboardMetricCalculator.Calculate([Fact(20m)]));
        vm.OnDataCommitted();
        await vm.LoadTask;
        old.SetResult(DashboardMetricCalculator.Calculate([Fact(1m)]));
        await before;
        Assert.Equal(20m, vm.Selected!.Source.Metrics.Net.Total);
        vm.Deactivate();
        vm.OnDataCommitted();
        Assert.Equal(2, reader.Queries.Count);
        await vm.ActivateAsync();
        Assert.Equal(3, reader.Queries.Count);
    }

    [Fact]
    public async Task FilterChangeCancelsOldReadEvenIfReaderIgnoresCancellation()
    {
        var old = new TaskCompletionSource<DashboardAnalyticsSnapshot>();
        var started = new TaskCompletionSource();
        var reader = new FakeDashboardAnalyticsReader { Read = (_, _) => { started.SetResult(); return old.Task; } };
        var vm = new DashboardViewModel(reader, Clock("2026-09-29T12:00:00Z"));
        Task before = vm.ActivateAsync();
        await started.Task;
        reader.Read = (_, _) => Task.FromResult(DashboardMetricCalculator.Calculate([]));
        vm.Period = DashboardPeriod.Month;
        await vm.LoadTask;
        old.SetResult(DashboardMetricCalculator.Calculate([Fact(1m)]));
        await before;
        Assert.True(vm.IsEmpty);
        Assert.Null(vm.Selected);
        Assert.Equal(DashboardPeriod.Month, vm.Period);
    }

    internal static TradeAnalyticsFact Fact(decimal? gross, decimal? costs = 0m, int day = 1) =>
        new(Guid.NewGuid(), TradeStatus.Closed, new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new(2026, 9, day, 12, 0, 0, TimeSpan.Zero), "USD", null, gross, costs, gross - costs) { InstrumentSymbol = "TEST" };
    private static FakeDashboardAnalyticsReader Reader(params TradeAnalyticsFact[] facts) => new()
        { Read = (_, _) => Task.FromResult(DashboardMetricCalculator.Calculate(facts)) };
    private static TimeProvider Clock(string utc) => new FixedTime(DateTimeOffset.Parse(utc));
    private sealed class FixedTime(DateTimeOffset utc) : TimeProvider { public override DateTimeOffset GetUtcNow() => utc; }
}
