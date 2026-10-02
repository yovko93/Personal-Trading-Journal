using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Desktop.Converters;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed class CalendarSummaryTests
{
    [Fact]
    public async Task DailyPresentationPreservesKnownEstimatedUnavailableAndEmptyEconomics()
    {
        CalendarViewModel vm = await CalendarSummaryFixture.CreateAsync();
        CalendarDayCell Day(int day) => vm.Weeks.SelectMany(w => w.Days).Single(d => d.Date == new DateOnly(2026, 9, day));
        Assert.Equal(PnLOutcome.Positive, Day(1).DailyOutcome);
        Assert.Equal(PnLOutcome.Negative, Day(2).DailyOutcome);
        var zero = Assert.Single(Day(3).DailySummaries);
        Assert.Equal(0m, zero.Amount);
        Assert.Equal(PnLOutcome.Zero, zero.Outcome);
        Assert.Equal("1 Trade", zero.TradeCountText);
        Assert.Contains("Verified Net", zero.Description);
        var estimate = Assert.Single(Day(4).DailySummaries);
        Assert.Equal(-285m, estimate.Amount);
        Assert.Contains("Estimated", estimate.Description);
        Assert.Contains("commission/fees unknown", Day(4).AccessibleName);
        var unknown = Assert.Single(Day(7).DailySummaries);
        Assert.Null(unknown.Amount);
        Assert.Equal("— USD", unknown.AmountText);
        Assert.Equal("1 Trade", unknown.TradeCountText);
        Assert.Equal(PnLOutcome.None, unknown.Outcome);
        Assert.Contains("unavailable", unknown.Description);
        Assert.Empty(Day(10).DailySummaries);
        Assert.False(Day(10).HasDailyTrades);
        Assert.Contains("No closed Trades on this date", Day(10).AccessibleName);
        var authoritative = vm.MonthData!.Currencies.Single(c => c.Currency == "USD")
            .Weeks.SelectMany(w => w.Days).Single(d => d.Date == Day(4).Date).Metrics;
        Assert.Same(authoritative, estimate.Metrics);
    }

    [Fact]
    public async Task SaturdayShowsOnlyFullMondaySundaySummaryWhileRetainingDailyReaderData()
    {
        CalendarViewModel vm = await CalendarSummaryFixture.CreateAsync();
        CalendarDayCell saturday = vm.Weeks[0].Days[5];
        Assert.Equal(new DateOnly(2026, 9, 5), saturday.Date);
        Assert.Equal("Week 1", saturday.WeekLabel);
        Assert.Equal(7m, Assert.Single(saturday.DailySummaries).Amount);
        Assert.False(saturday.ShowsDailySummary);
        var week = Assert.Single(saturday.WeeklySummaries);
        Assert.Equal(-205m, week.Amount); // Includes adjacent Aug 31 and Sunday Sep 6.
        Assert.Equal(7, week.Metrics.ClosedTradeCount);
        Assert.Contains("Estimated", week.Description);
        Assert.Same(vm.MonthData!.Currencies.Single(c => c.Currency == "USD").Weeks[0].Metrics, week.Metrics);
        Assert.Equal(11m, Assert.Single(vm.Weeks[0].Days[6].DailySummaries).Amount);
        CalendarDayCell quietSaturday = vm.Weeks[1].Days[5];
        Assert.Empty(quietSaturday.DailySummaries);
        Assert.False(quietSaturday.ShowsDailySummary);
        Assert.Equal(2, quietSaturday.WeeklySummaries.Count);
        Assert.Equal(3, quietSaturday.WeeklySummaries.Single(s => s.Currency == "USD").Metrics.ClosedTradeCount);
        CalendarDayCell lastSaturday = vm.Weeks[4].Days[5];
        Assert.False(lastSaturday.IsInDisplayedMonth);
        Assert.Equal("Week 5", lastSaturday.WeekLabel);
        Assert.Equal(10m, Assert.Single(lastSaturday.WeeklySummaries).Amount);
        Assert.Equal(2, lastSaturday.WeeklySummaries[0].Metrics.ClosedTradeCount);
        Assert.True(vm.Weeks[2].Days[5].HasEmptyWeek);
        Assert.Empty(vm.Weeks[2].Days[5].WeeklySummaries);
    }

    [Fact]
    public async Task MixedCurrenciesAreSeparateWithoutAnAggregateCellSign()
    {
        CalendarViewModel vm = await CalendarSummaryFixture.CreateAsync();
        CalendarDayCell mixed = vm.Weeks[1].Days[1];
        Assert.Equal(new[] { "EUR", "USD" }, mixed.DailySummaries.Select(s => s.Currency));
        Assert.Equal(new decimal?[] { -20m, 10m }, mixed.DailySummaries.Select(s => s.Amount));
        Assert.Equal(PnLOutcome.None, mixed.DailyOutcome);
        Assert.All(mixed.DailySummaries, s => Assert.True(s.TintBackground));
        Assert.All(vm.Weeks[0].Days[0].DailySummaries, s => Assert.False(s.TintBackground));
        Assert.Equal(PnLOutcome.None, vm.Weeks[0].Days[0].DailyOutcome);
        Assert.Equal(2, vm.Weeks[1].Days[5].WeeklySummaries.Count);
    }
}

internal static class CalendarSummaryFixture
{
    public static async Task<CalendarViewModel> CreateAsync()
    {
        var reader = new TradingCalendarReader(new FactsReader());
        var vm = new CalendarViewModel(reader, new Clock());
        await vm.ActivateAsync();
        return vm;
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FactsReader : IDashboardAnalyticsReader
    {
        public Task<DashboardAnalyticsSnapshot> GetAsync(DashboardAnalyticsQuery query, CancellationToken cancellationToken = default)
        {
            TradeAnalyticsFact[] facts =
            [
                Fact(8, 31, 2m), Fact(9, 1, 100m), Fact(9, 2, -40m), Fact(9, 3, 0m),
                Fact(9, 4, -285m, null), Fact(9, 5, 7m), Fact(9, 6, 11m),
                Fact(9, 7, null, null), Fact(9, 8, 10m), Fact(9, 8, -20m, currency: "EUR"),
                Fact(9, 9, -5m), Fact(9, 30, 4m), Fact(10, 4, 6m),
            ];
            return Task.FromResult(DashboardMetricCalculator.Calculate(facts.Where(f =>
                f.ClosedAtUtc >= query.ClosedFromUtc && f.ClosedAtUtc < query.ClosedBeforeUtc), cancellationToken));
        }
        private static TradeAnalyticsFact Fact(int month, int day, decimal? gross, decimal? costs = 0m, string currency = "USD")
        {
            var close = new DateTimeOffset(2026, month, day, 16, 0, 0, TimeSpan.Zero);
            return new(Guid.NewGuid(), TradeStatus.Closed, close.AddHours(-1), close, currency, null, gross, costs, gross - costs);
        }
    }
}
