using System.Globalization;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Desktop.Converters;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Dashboard;

public sealed class DashboardViewModelTests
{
    [Theory]
    [InlineData(25, PnLOutcome.Positive)]
    [InlineData(-25, PnLOutcome.Negative)]
    [InlineData(0, PnLOutcome.Zero)]
    [InlineData(null, PnLOutcome.None)]
    public void NetAndDayCardsExposeNullableNumericOutcomeForThemeColoring(int? amount, PnLOutcome expected)
    {
        decimal? pnl = amount;
        var source = Assert.Single(DashboardMetricCalculator.Calculate([
            Fact(pnl, pnl.HasValue ? 0m : null)]).Currencies);
        var cards = new DashboardCurrencyPresentation(source).Cards;
        var converter = new PnLOutcomeConverter();
        foreach (string label in new[] { "Net P&L", "Best Day", "Worst Day" })
        {
            DashboardCard card = Assert.Single(cards, c => c.Label == label);
            Assert.Equal(pnl, card.PnlValue);
            Assert.Equal(expected, converter.Convert(card.PnlValue, typeof(PnLOutcome), null, CultureInfo.InvariantCulture));
            Assert.Equal(pnl is null ? (label == "Net P&L" ? "—" : "N/A") : DashboardCurrencyPresentation.Money(pnl, "USD"), card.Value);
        }
    }

    [Fact]
    public void CardsAreConciseAndRingIncludesBreakEvensWithoutInventingUnavailablePercentages()
    {
        var presentation = new DashboardCurrencyPresentation(Assert.Single(DashboardMetricCalculator.Calculate(
            [Fact(100m), Fact(-40m), Fact(0m), Fact(20m, null)]).Currencies));
        Assert.DoesNotContain(presentation.Cards, c => c.Label is "Gross P&L" or "Verified Net P&L");
        DashboardCard net = Assert.Single(presentation.Cards, c => c.Label == "Net P&L");
        Assert.True(net.IsEstimated);
        Assert.DoesNotContain("coverage", net.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("commission/fees unknown", net.Explanation);
        DashboardCard day = presentation.Cards.Single(c => c.Label == "Best Day");
        Assert.Equal("2026-09-01", day.Date);
        Assert.Equal(DashboardCurrencyPresentation.Money(80m, "USD"), day.Value);
        Assert.True(day.IsEstimated);
        var ring = presentation.Cards.Single(c => c.Label == "Win Rate").Ring!;
        Assert.Equal(0.5m, ring.WinsShare);
        Assert.Equal(0.25m, ring.LossesShare);
        Assert.Equal(0.25m, ring.BreakEvenShare);
        Assert.Contains("2 wins, 1 losses, 1 break-even", ring.Description);
        Assert.Contains("Estimated", ring.Description);
        foreach (var facts in new[] { Array.Empty<TradeAnalyticsFact>(), new[] { Fact(null, null) }, new[] { Fact(10m), Fact(null, null) } })
        {
            var metrics = facts.Length == 0
                ? DashboardMetricCalculator.Calculate([Fact(0m)]).Currencies[0].Metrics.EffectiveNet with
                    { Coverage = new(0, 0), WinRatePercent = null }
                : DashboardMetricCalculator.Calculate(facts).Currencies[0].Metrics.EffectiveNet;
            var unavailable = new WinRatePresentation(metrics);
            Assert.False(unavailable.IsAvailable);
            Assert.Equal(facts.Length == 0 ? "0%" : "N/A", unavailable.Value);
            Assert.Equal(0m, unavailable.WinsShare + unavailable.LossesShare + unavailable.BreakEvenShare);
        }
        var zero = new WinRatePresentation(DashboardMetricCalculator.Calculate([Fact(0m)]).Currencies[0].Metrics.EffectiveNet);
        Assert.True(zero.IsAvailable);
        Assert.Equal(1m, zero.BreakEvenShare);
    }

    [Fact]
    public async Task RecentRowsAreIndependentOfCurrencyAndEmptyPeriodAndViewUsesTheExactRow()
    {
        var recent = new FakeTradeListReader();
        var open = new PersonalTradingJournal.Application.Trades.TradeListItem(Guid.NewGuid(), Guid.NewGuid(), "Test Account",
            Guid.NewGuid(), "OPEN", TradeDirection.Long, TradeStatus.Open, DateTimeOffset.UtcNow, null, 2m, 100m,
            null, null, null, null, "EUR", 2m);
        recent.EnqueueResult([open]);
        recent.EnqueueResult([open]);
        var vm = new DashboardViewModel(Reader(Fact(20m)), Clock("2026-09-29T12:00:00Z"), recent, new FakeTradingAccountReader());
        PersonalTradingJournal.Application.Trades.TradeListItem? viewed = null;
        vm.OpenTradeAsync = item => { viewed = item; return Task.CompletedTask; };
        await vm.RefreshAsync();
        vm.SelectedCurrency = "USD";
        Assert.Same(open, Assert.Single(vm.RecentTrades));
        vm.Period = DashboardPeriod.Month;
        await vm.LoadTask;
        Assert.Same(open, Assert.Single(vm.RecentTrades));
        Assert.All(recent.RequestedQueries, q =>
        {
            Assert.Equal(1, q.PageNumber); Assert.Equal(10, q.PageSize);
            Assert.Equal(PersonalTradingJournal.Application.Trades.TradeListSortColumn.OpenedAtUtc, q.SortColumn);
            Assert.Equal(PersonalTradingJournal.Application.Trades.TradeListSortDirection.Descending, q.SortDirection);
        });
        Assert.DoesNotContain("New York", vm.PeriodLabel);
        await vm.ViewTradeCommand.ExecuteAsync(open);
        Assert.Same(open, viewed);
    }

    [Fact]
    public async Task AllIsDefaultAndUsesCompleteHistoryNotRecentTen()
    {
        var reader = Reader(Enumerable.Range(0, 121).Select(_ => Fact(1m)).ToArray());
        var vm = new DashboardViewModel(reader, Clock("2026-09-29T12:00:00Z"), new FakeTradeListReader(), new FakeTradingAccountReader());
        await vm.ActivateAsync();
        Assert.Equal(DashboardPeriod.All, vm.Period);
        Assert.Null(reader.Queries[0].ClosedFromUtc);
        Assert.Null(reader.Queries[0].ClosedBeforeUtc);
        Assert.False(vm.NextCommand.CanExecute(null));
        Assert.False(vm.PreviousCommand.CanExecute(null));
        Assert.Equal(121m, vm.Selected!.Source!.Metrics.Net.Total);
        Assert.Equal("121", vm.Selected.Cards.Single(c => c.Label == "Total Trades").Value);
        Assert.Empty(vm.RecentTrades); // Never sourced from the analytics subset.
    }

    [Fact]
    public async Task SupersededRecentReadCannotOverwriteCommittedRows()
    {
        var recent = new FakeTradeListReader();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = new TaskCompletionSource<IReadOnlyList<PersonalTradingJournal.Application.Trades.TradeListItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        recent.EnqueueBehavior(_ => { started.SetResult(); return old.Task; });
        var row = new PersonalTradingJournal.Application.Trades.TradeListItem(Guid.NewGuid(), Guid.NewGuid(), "New",
            Guid.NewGuid(), "NEW", TradeDirection.Long, TradeStatus.Open, DateTimeOffset.UtcNow, null, 1m, 10m,
            null, null, null, null, "USD", 1m);
        recent.EnqueueResult([row]);
        var vm = new DashboardViewModel(Reader(), Clock("2026-09-29T12:00:00Z"), recent, new FakeTradingAccountReader());
        Task before = vm.ActivateAsync();
        await started.Task;
        vm.OnDataCommitted();
        await vm.LoadTask;
        old.SetResult([row with { Id = Guid.NewGuid(), InstrumentSymbol = "OLD" }]);
        await before;
        Assert.Same(row, Assert.Single(vm.RecentTrades));
        Assert.True(vm.IsEmpty); // Analytics empty, but latest persisted open Trade remains visible.
    }

    [Theory]
    [InlineData(DashboardPeriod.Week, "2026-03-08T12:00:00Z", "2026-03-02", "2026-03-08", 167)]
    [InlineData(DashboardPeriod.Week, "2026-11-01T12:00:00Z", "2026-10-26", "2026-11-01", 169)]
    [InlineData(DashboardPeriod.Month, "2026-01-01T03:00:00Z", "2025-12-01", "2025-12-31", 744)]
    [InlineData(DashboardPeriod.Year, "2026-01-01T03:00:00Z", "2025-01-01", "2025-12-31", 8760)]
    public async Task CurrentPeriodsUseNewYorkCalendarAndDst(DashboardPeriod period, string now, string first, string last, int hours)
    {
        var vm = new DashboardViewModel(Reader(), Clock(now), new FakeTradeListReader(), new FakeTradingAccountReader()) { Period = period };
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
        var vm = new DashboardViewModel(Reader(), Clock("2026-01-02T12:00:00Z"), new FakeTradeListReader(), new FakeTradingAccountReader()) { Period = period };
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
    public async Task CurrencySelectionAppliesToCardsChartsAndSetups()
    {
        var vm = new DashboardViewModel(Reader(Fact(-285m, null), Fact(0m) with { Currency = "EUR" }), Clock("2026-09-29T12:00:00Z"), new FakeTradeListReader(), new FakeTradingAccountReader());
        await vm.ActivateAsync();
        Assert.Equal(new[] { "EUR", "USD" }, vm.Currencies);
        vm.SelectedCurrency = "USD";
        Assert.True(vm.Selected!.Source!.Metrics.EffectiveNet.IsEstimated);
        Assert.Contains("Estimated", vm.Selected.CoverageNote);
        Assert.Equal(-285m, Assert.Single(vm.Selected.DailyPnl).Value);
        Assert.True(vm.Selected.Cards.Single(c => c.Label == "Net P&L").IsEstimated);
        Assert.Contains("Estimated", Assert.Single(vm.Selected.Setups).Summary);
        Assert.Null(vm.Selected.Source!.Metrics.Net.Total);
        vm.SelectedCurrency = "EUR";
        Assert.False(vm.Selected!.Source!.Metrics.EffectiveNet.IsEstimated);
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
        var vm = new DashboardViewModel(reader, Clock("2026-09-29T12:00:00Z"), new FakeTradeListReader(), new FakeTradingAccountReader());
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
        Assert.True(vm.HasNoRecentTrades);
        reader.Read = async (_, token) => { await Task.Delay(Timeout.Infinite, token); return DashboardMetricCalculator.Calculate([]); };
        Task loading = vm.RefreshAsync();
        Assert.False(vm.HasNoRecentTrades);
        vm.CancelCommand.Execute(null);
        await loading;
        Assert.False(vm.IsLoading);
        Assert.Contains("cancelled", vm.StatusMessage);
        Assert.False(vm.HasNoRecentTrades);
    }

    [Fact]
    public async Task CommitDuringActivePreCommitReadCannotBeOverwrittenByOldResult()
    {
        var old = new TaskCompletionSource<DashboardAnalyticsSnapshot>();
        var started = new TaskCompletionSource();
        var reader = new FakeDashboardAnalyticsReader { Read = (_, _) => { started.SetResult(); return old.Task; } };
        var vm = new DashboardViewModel(reader, Clock("2026-09-29T12:00:00Z"), new FakeTradeListReader(), new FakeTradingAccountReader());
        Task before = vm.ActivateAsync();
        await started.Task;
        reader.Read = (_, _) => Task.FromResult(DashboardMetricCalculator.Calculate([Fact(20m)]));
        vm.OnDataCommitted();
        await vm.LoadTask;
        old.SetResult(DashboardMetricCalculator.Calculate([Fact(1m)]));
        await before;
        Assert.Equal(20m, vm.Selected!.Source!.Metrics.Net.Total);
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
        var vm = new DashboardViewModel(reader, Clock("2026-09-29T12:00:00Z"), new FakeTradeListReader(), new FakeTradingAccountReader());
        Task before = vm.ActivateAsync();
        await started.Task;
        reader.Read = (_, _) => Task.FromResult(DashboardMetricCalculator.Calculate([]));
        vm.Period = DashboardPeriod.Month;
        await vm.LoadTask;
        old.SetResult(DashboardMetricCalculator.Calculate([Fact(1m)]));
        await before;
        Assert.True(vm.IsEmpty);
        Assert.True(vm.Selected!.IsEmpty);
        Assert.Null(vm.Selected.Source);
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
