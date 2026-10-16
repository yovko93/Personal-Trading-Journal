using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Desktop.ViewModels.Breakdown;

namespace PersonalTradingJournal.Desktop.Tests.Breakdown;

public sealed class BreakdownChartTests
{
    [Theory]
    [InlineData(100, 50, 0, 100, 0)]
    [InlineData(-100, -50, -100, 0, 1)]
    [InlineData(-100, 100, -100, 100, 0.5)]
    [InlineData(0, 0, 0, 1, 0)]
    public void SignedSharedGeometryPreservesKnownZeroAndUnavailable(int a, int b, double min, double max, double zero)
    {
        var p = BreakdownPresentationTests.Snapshot([BreakdownPresentationTests.Group(1, a), BreakdownPresentationTests.Group(2, b), BreakdownPresentationTests.Group(3, null)]);
        var chart = new BreakdownChartPresentation(p.Rows, BreakdownChartMetric.Pnl, "USD", "Net/Gross");
        Assert.Equal(min, chart.Minimum); Assert.Equal(max, chart.Maximum);
        Assert.All(chart.Rows, r => Assert.Equal(zero, r.ZeroPosition));
        Assert.Equal((decimal)a, chart.Rows[0].Value); Assert.NotNull(chart.Rows[0].ValuePosition);
        Assert.Null(chart.Rows[2].ValuePosition); Assert.Contains("Unavailable", chart.Rows[2].ValueText);
        Assert.Contains("USD", chart.Title); Assert.Contains("Net/Gross", chart.Title);
        if (a > 0) Assert.StartsWith("+", chart.Rows[0].ValueText);
    }

    [Fact]
    public void ExtremeDecimalsHaveFiniteGeometryWithoutArithmeticOverflow()
    {
        var p = BreakdownPresentationTests.Snapshot([BreakdownPresentationTests.Group(1, decimal.MinValue), BreakdownPresentationTests.Group(2, decimal.MaxValue)]);
        var chart = new BreakdownChartPresentation(p.Rows, BreakdownChartMetric.Pnl, "USD", "Net");
        Assert.Equal(0, chart.Rows[0].ValuePosition); Assert.Equal(1, chart.Rows[1].ValuePosition);
    }

    [Fact]
    public void CountsAndPercentUseReturnedValuesAndFixedPercentageScale()
    {
        var p = BreakdownPresentationTests.Snapshot([BreakdownPresentationTests.Group(1, 60.4m), BreakdownPresentationTests.Group(2, null)]);
        var rate = new BreakdownChartPresentation(p.Rows, BreakdownChartMetric.WinRate, "USD", "Net");
        Assert.Equal(100, rate.Maximum); Assert.Equal(.604, rate.Rows[0].ValuePosition!.Value, 10);
        Assert.Equal($"{60.4m:N1}%", rate.Rows[0].ValueText); Assert.Null(rate.Rows[1].Value);
        var count = new BreakdownChartPresentation(p.Rows, BreakdownChartMetric.Trades, "USD", "Net");
        Assert.All(count.Rows, r => Assert.Equal(1, r.Value)); Assert.Equal(0, count.Minimum);
        Assert.Equal("1 Trades", count.Rows[0].ValueText);
        var empty = new BreakdownChartPresentation([], BreakdownChartMetric.Pnl, "USD", "Unavailable");
        Assert.Empty(empty.Rows); Assert.NotEmpty(empty.EmptyMessage); Assert.True(double.IsFinite(empty.Maximum));
    }

    [Fact]
    public async Task SharedSelectionOrderingDetailsAndMetricAreLocalAndRetained()
    {
        using var f = new BreakdownFixture();
        f.Reader.MakeResult = q => new(q, BreakdownPresentationTests.Metrics(2, 2, 10m),
            [BreakdownPresentationTests.Group(1, -5m) with { Label = "Same" }, BreakdownPresentationTests.Group(2, 15m) with { Label = "Same" }]);
        await f.Vm.ActivateAsync();
        f.Vm.SelectedChartRow = f.Vm.Chart!.Rows[1]; Assert.Equal(BreakdownPresentationTests.Key(2), f.Vm.SelectedGroupKey);
        Assert.Same(f.Vm.SelectedGroup, f.Vm.SelectedDetails!.Group);
        Assert.Contains("15", f.Vm.SelectedDetails.Performance);
        f.Vm.SelectedGroup = f.Vm.GroupRows[0]; Assert.Same(f.Vm.SelectedGroup, f.Vm.SelectedChartRow!.Group);
        f.Vm.SortGroups(BreakdownSortColumn.Pnl); f.Vm.SortGroups(BreakdownSortColumn.Pnl);
        Assert.Equal(f.Vm.GroupRows.Select(g => g.Key), f.Vm.Chart.Rows.Select(r => r.Group.Key));
        f.Vm.ChartMetric = BreakdownChartMetric.WinRate;
        f.Vm.Currency = "JPY"; f.Vm.Mode = BreakdownCalculationMode.Gross;
        Assert.Contains("EUR", f.Vm.SelectedDetails!.Context); Assert.Contains("Net preferred", f.Vm.SelectedDetails.Context);
        Assert.True(f.Vm.SortDescending); Assert.Single(f.Reader.Calls);
        f.Vm.Deactivate(); await f.Vm.ActivateAsync(); Assert.Single(f.Reader.Calls);
        Assert.Equal(BreakdownChartMetric.WinRate, f.Vm.ChartMetric);
        var oldRow = f.Vm.SelectedChartRow; var oldGroup = f.Vm.SelectedGroup;
        await f.Vm.RefreshAsync();
        Assert.NotSame(oldGroup, f.Vm.SelectedDetails!.Group); Assert.Equal(oldGroup!.Key, f.Vm.SelectedDetails.Group.Key);
        f.Vm.SelectedGroup = f.Vm.GroupRows[1]; var key = f.Vm.SelectedGroupKey;
        f.Vm.SelectedChartRow = oldRow; f.Vm.SelectedGroup = oldGroup; Assert.Equal(key, f.Vm.SelectedGroupKey);
        f.Reader.MakeResult = q => new(q, BreakdownPresentationTests.Metrics(1, 1, 1), [BreakdownPresentationTests.Group(3, 1) with { Label = "Same" }]);
        await f.Vm.RefreshAsync(); Assert.Null(f.Vm.SelectedDetails); Assert.Null(f.Vm.SelectedChartRow); Assert.NotEmpty(f.Vm.SelectionPrompt);
    }

    [Fact]
    public async Task RefreshRebindsDetailsAndOlderRequestsCannotPublishChart()
    {
        using var f = new BreakdownFixture();
        f.Reader.MakeResult = q => new(q, BreakdownPresentationTests.Metrics(1, 1, 1), [BreakdownPresentationTests.Group(1, 1)]);
        await f.Vm.ActivateAsync(); f.Vm.SelectedGroup = f.Vm.GroupRows[0];
        f.Reader.Hold = true; f.Reader.Drain();
        var old = f.Vm.RefreshAsync(); var first = await f.Reader.Next();
        var current = f.Vm.RefreshAsync(); var second = await f.Reader.Next();
        second.Completion.SetResult(new(second.Query, BreakdownPresentationTests.Metrics(1, 1, 77), [BreakdownPresentationTests.Group(1, 77)])); await current;
        var chart = f.Vm.Chart; Assert.Contains("77", f.Vm.SelectedDetails!.Performance);
        first.Completion.SetResult(BreakdownFixture.Snapshot(first.Query)); await old;
        Assert.Same(chart, f.Vm.Chart); Assert.Same(f.Vm.SelectedGroup, f.Vm.SelectedChartRow!.Group);
        var failed = f.Vm.RefreshAsync(); var third = await f.Reader.Next(); third.Completion.SetException(new IOException("synthetic")); await failed;
        Assert.Same(chart, f.Vm.Chart); Assert.Contains("77", f.Vm.SelectedDetails!.Performance);
        var cancelled = f.Vm.RefreshAsync(); var fourth = await f.Reader.Next();
        f.Vm.CancelCommand.Execute(null);
        fourth.Completion.SetResult(BreakdownFixture.Snapshot(fourth.Query)); await cancelled;
        Assert.True(fourth.Token.IsCancellationRequested);
        Assert.Same(chart, f.Vm.Chart); Assert.Contains("77", f.Vm.SelectedDetails!.Performance);
    }

    [Fact]
    public void DetailsKeepSourceSpecificCoverageAndAverageLossMagnitude()
    {
        var metrics = BreakdownPresentationTests.Metrics(2, 1, null) with { FinancialSources = new(BreakdownCalculationMode.NetPreferred, 2, 0, 1, 0, 1) };
        metrics = metrics with { Selected = metrics.Selected with { Wins = 0, Losses = 1, BreakEvens = 0 } };
        var row = new BreakdownGroupPresentation(BreakdownPresentationTests.Group(1, null) with { Metrics = metrics }, "EUR");
        var details = new BreakdownGroupDetails(row, "Instrument · EUR · Net preferred · Gross fallback");
        Assert.Contains("1 financially evaluable", details.Counts); Assert.Contains("1 losses", details.Outcomes);
        Assert.Contains("Gross fallback: 1", details.Group.Metrics.CoverageDetails);
        Assert.Contains("unavailable: 1", details.Group.Metrics.CoverageDetails);
        Assert.Contains("Average loss (magnitude): —", details.Averages); Assert.Contains("Small sample", row.Metrics.Sample);
        var known = metrics with { Selected = metrics.Selected with
        {
            AverageWin = new(Application.Analytics.AveragePnlStatus.Defined, 12.34m),
            AverageLoss = new(Application.Analytics.AveragePnlStatus.Defined, 5.67m)
        } };
        var exact = new BreakdownGroupDetails(new(row.Source with { Metrics = known }, "EUR"), "context");
        Assert.Contains($"Average winner: {12.34m:N2} EUR", exact.Averages);
        Assert.Contains($"Average loss (magnitude): {5.67m:N2} EUR", exact.Averages);
    }
}
