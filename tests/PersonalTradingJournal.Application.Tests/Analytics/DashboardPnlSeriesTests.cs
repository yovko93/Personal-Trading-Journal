using System.Globalization;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Analytics;

public sealed class DashboardPnlSeriesTests
{
    [Fact]
    public void DailyWeeklyAndCumulativeExamplePreservesUnknownNetAcrossYearBoundary()
    {
        CurrencyTradeMetrics result = Single(
            Fact("2026-12-31", 10m, 2m), Fact("2026-12-31", -3m),
            Fact("2027-01-03", 5m, null), Fact("2027-01-04", 0m));

        Assert.Equal(new[] { Day("2026-12-31"), Day("2027-01-03"), Day("2027-01-04") }, result.Days.Select(d => d.NewYorkDate));
        Assert.Equal(2, result.Days[0].Metrics.ClosedTradeCount);
        Assert.Equal(7m, result.Days[0].Metrics.Gross.Total);
        Assert.Equal(5m, result.Days[0].Metrics.Net.Total);
        Assert.Equal(5m, result.Days[0].CumulativeMetrics.Net.Total);
        Assert.Equal(5m, result.Days[1].Metrics.Gross.Total);
        Assert.Null(result.Days[1].Metrics.Net.Total);
        Assert.Null(result.Days[1].Metrics.Net.KnownSubtotal);
        Assert.Equal(0m, result.Days[2].Metrics.Net.Total);
        Assert.Equal(12m, result.Days[2].CumulativeMetrics.Gross.Total);
        Assert.Null(result.Days[2].CumulativeMetrics.Net.Total);
        Assert.Equal(5m, result.Days[2].CumulativeMetrics.Net.KnownSubtotal);
        Assert.Equal(new MetricCoverage(4, 3), result.Days[2].CumulativeMetrics.Net.Coverage);

        Assert.Equal(new[] { Day("2026-12-28"), Day("2027-01-04") }, result.Weeks.Select(w => w.WeekStartingMonday));
        Assert.Equal(3, result.Weeks[0].Metrics.ClosedTradeCount);
        Assert.Equal(12m, result.Weeks[0].Metrics.Gross.Total);
        Assert.Null(result.Weeks[0].Metrics.Net.Total);
        Assert.Equal(5m, result.Weeks[0].Metrics.Net.KnownSubtotal);
        Assert.Equal(new MetricCoverage(3, 2), result.Weeks[0].Metrics.Net.Coverage);
        Assert.Equal(0m, result.Weeks[1].Metrics.Net.Total);
        Assert.Null(result.Weeks[1].CumulativeMetrics.Net.Total);
        Assert.Equal(result.Metrics, result.Weeks[^1].CumulativeMetrics);
        Assert.Equal(result.Metrics, result.Days[^1].CumulativeMetrics);
    }

    [Theory]
    [InlineData("2026-08-31", "2026-08-31")]
    [InlineData("2026-09-01", "2026-08-31")]
    [InlineData("2026-09-06", "2026-08-31")]
    [InlineData("2026-09-07", "2026-09-07")]
    [InlineData("2027-01-01", "2026-12-28")]
    [InlineData("0001-01-01", "0001-01-01")]
    [InlineData("9999-12-31", "9999-12-27")]
    public void WeeksStartOnMondayAcrossMonthsYearsAndRepresentableDateEdges(string date, string monday)
    {
        Assert.Equal(Day(monday), DashboardMetricCalculator.GetWeekStartingMonday(Day(date)));
    }

    [Theory]
    [InlineData("2026-03-08T06:59:59Z", "2026-03-08T07:00:00Z", "2026-03-08", "2026-03-02")]
    [InlineData("2026-11-01T05:30:00Z", "2026-11-01T06:30:00Z", "2026-11-01", "2026-10-26")]
    public void DstTransitionsKeepBothInstantsInTheirLocalDayAndWeek(string first, string second, string day, string monday)
    {
        TradeAnalyticsFact a = Fact(day, 1m) with { ClosedAtUtc = DateTimeOffset.Parse(first, CultureInfo.InvariantCulture) };
        TradeAnalyticsFact b = Fact(day, 2m) with { ClosedAtUtc = DateTimeOffset.Parse(second, CultureInfo.InvariantCulture) };
        CurrencyTradeMetrics result = Single(a, b);
        Assert.Equal(Day(day), Assert.Single(result.Days).NewYorkDate);
        Assert.Equal(Day(monday), Assert.Single(result.Weeks).WeekStartingMonday);
        Assert.Equal(3m, result.Days[0].Metrics.Net.Total);
        Assert.Equal(2, result.Weeks[0].Metrics.ClosedTradeCount);
    }

    [Fact]
    public void MondayUtcBeforeLocalMidnightBelongsToPreviousNewYorkWeek()
    {
        TradeAnalyticsFact trade = Fact("2026-09-07", 1m) with
        { ClosedAtUtc = new DateTimeOffset(2026, 9, 7, 3, 59, 59, TimeSpan.Zero) };
        CurrencyTradeMetrics result = Single(trade);
        Assert.Equal(Day("2026-09-06"), Assert.Single(result.Days).NewYorkDate);
        Assert.Equal(Day("2026-08-31"), Assert.Single(result.Weeks).WeekStartingMonday);
    }

    [Fact]
    public void UnknownFirstThenKnownZeroHasPartialZeroSubtotalNotCompleteZero()
    {
        CurrencyTradeMetrics result = Single(Fact("2026-09-01", 0m, null), Fact("2026-09-14", 0m));
        Assert.Null(result.Days[0].CumulativeMetrics.Net.KnownSubtotal);
        Assert.Equal(MetricCoverageStatus.Unavailable, result.Days[0].CumulativeMetrics.Net.Coverage.Status);
        Assert.Equal(0m, result.Days[1].Metrics.Net.Total);
        Assert.Equal(0m, result.Days[1].CumulativeMetrics.Net.KnownSubtotal);
        Assert.Null(result.Days[1].CumulativeMetrics.Net.Total);
        Assert.Equal(MetricCoverageStatus.Partial, result.Weeks[1].CumulativeMetrics.Net.Coverage.Status);
        Assert.Equal(0m, result.Weeks[1].CumulativeMetrics.Gross.Total);
        // No September 7 week or intervening empty days are manufactured as zero outcomes.
        Assert.Equal(2, result.Weeks.Count);
        Assert.Equal(2, result.Days.Count);
    }

    [Fact]
    public void CurrencyPrefixesAreIndependentAndOrderingDoesNotDependOnInputOrder()
    {
        TradeAnalyticsFact[] facts = [Fact("2026-09-08", 5m), Fact("2026-09-01", 1m, null),
            Fact("2026-09-01", 2m) with { Currency = "EUR" }, Fact("2026-09-08", -2m) with { Currency = "EUR" }];
        DashboardAnalyticsSnapshot forward = DashboardMetricCalculator.Calculate(facts);
        DashboardAnalyticsSnapshot reverse = DashboardMetricCalculator.Calculate(facts.Reverse());
        Assert.Equal(new[] { "EUR", "USD" }, forward.Currencies.Select(c => c.Currency));
        for (int index = 0; index < 2; index++)
        {
            Assert.Equal(forward.Currencies[index].Days.ToArray(), reverse.Currencies[index].Days.ToArray());
            Assert.Equal(forward.Currencies[index].Weeks.ToArray(), reverse.Currencies[index].Weeks.ToArray());
        }
        Assert.Equal(0m, forward.Currencies[0].Weeks[^1].CumulativeMetrics.Net.Total);
        Assert.Null(forward.Currencies[1].Weeks[^1].CumulativeMetrics.Net.Total);
        Assert.Equal(5m, forward.Currencies[1].Weeks[^1].CumulativeMetrics.Net.KnownSubtotal);
    }

    [Fact]
    public void MissingGrossDoesNotBecomeKnownGrossInLaterPrefixes()
    {
        CurrencyTradeMetrics result = Single(Fact("2026-09-01", null), Fact("2026-09-02", 2m));
        Assert.Null(result.Days[1].CumulativeMetrics.Gross.Total);
        Assert.Equal(2m, result.Days[1].CumulativeMetrics.Gross.KnownSubtotal);
        Assert.Equal(new MetricCoverage(2, 1), result.Weeks[0].Metrics.Gross.Coverage);
    }

    [Fact]
    public void EmptyAndOpenOnlyPopulationsHaveNoPeriodPoints()
    {
        Assert.Empty(DashboardMetricCalculator.Calculate([]).Currencies);
        CurrencyTradeMetrics open = Single(Fact("2026-09-01", null) with { Status = TradeStatus.Open, ClosedAtUtc = null });
        Assert.Empty(open.Days);
        Assert.Empty(open.Weeks);
        Assert.Equal(1, open.ExcludedOpenTradeCount);
    }

    [Theory]
    [InlineData("2026-09-01")]
    [InlineData("2026-09-02")]
    [InlineData("2026-09-14")]
    public void PeriodOrPrefixOverflowRejectsTheWholeCalculation(string secondDate)
    {
        Assert.Throws<OverflowException>(() => Single(Fact("2026-09-01", decimal.MaxValue), Fact(secondDate, 1m)));
    }

    [Fact]
    public void DecimalPrecisionIsNotRoundedForSeries()
    {
        const decimal tiny = .0000000000000000000000000001m;
        CurrencyTradeMetrics result = Single(Fact("2026-09-01", tiny), Fact("2026-09-02", tiny));
        Assert.Equal(tiny, result.Days[0].Metrics.Net.Total);
        Assert.Equal(2m * tiny, result.Weeks[0].CumulativeMetrics.Net.Total);
    }

    [Fact]
    public void CancelledCalculationReturnsNoSeries()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => DashboardMetricCalculator.Calculate(
            [Fact("2026-09-01", 1m)], cancellation.Token));
    }

    private static DateOnly Day(string value) => DateOnly.Parse(value, CultureInfo.InvariantCulture);
    private static CurrencyTradeMetrics Single(params TradeAnalyticsFact[] facts) =>
        Assert.Single(DashboardMetricCalculator.Calculate(facts).Currencies);
    private static TradeAnalyticsFact Fact(string day, decimal? gross, decimal? costs = 0m)
    {
        DateTimeOffset close = new(Day(day).ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc));
        return new(Guid.NewGuid(), TradeStatus.Closed, close.AddDays(-10), close, "USD", null, gross, costs, gross - costs);
    }
}
