using PersonalTradingJournal.Desktop.ViewModels.Breakdown;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;

namespace PersonalTradingJournal.Desktop.Tests.Breakdown;

public sealed class BreakdownAccessibilityTests
{
    [Theory]
    [InlineData("currency", "CurrencyFilter")]
    [InlineData("start", "FromDate")]
    [InlineData("end", "ThroughDate")]
    [InlineData("reversed", "ThroughDate")]
    public async Task InvalidApplyRequestsRelevantInputWithoutReadingOrLosingResult(string invalid, string target)
    {
        using var f = new BreakdownFixture(); await f.Vm.ActivateAsync();
        var result = f.Vm.Result; int reads = f.Reader.Calls.Count;
        var focus = new List<string>(); f.Vm.FocusRequested += focus.Add;
        if (invalid == "currency") f.Vm.Currency = "";
        else
        {
            f.Vm.Period = DashboardPeriod.Custom;
            f.Vm.StartDateText = invalid == "start" ? "bad" : "2026-10-10";
            f.Vm.EndDateText = invalid == "end" ? "bad" : "2026-10-09";
        }
        await f.Vm.ApplyAsync();
        Assert.Equal(target, Assert.Single(focus)); Assert.Equal(reads, f.Reader.Calls.Count); Assert.Same(result, f.Vm.Result);
        f.Vm.Deactivate(); await f.Vm.ApplyAsync(); Assert.Single(focus);
    }

    [Fact]
    public async Task CancelRequestsRetryFocusButLateCompletionAndSortDoNotRequestFocus()
    {
        using var f = new BreakdownFixture(); await f.Vm.ActivateAsync();
        var targets = new List<string>(); f.Vm.FocusRequested += targets.Add;
        f.Reader.Hold = true; f.Reader.Drain(); var load = f.Vm.RefreshAsync(); var call = await f.Reader.Next();
        f.Vm.CancelCommand.Execute(null); Assert.Equal("RefreshButton", Assert.Single(targets));
        call.Completion.SetResult(BreakdownFixture.Snapshot(call.Query)); await load;
        f.Vm.SortGroups(BreakdownSortColumn.Pnl); f.Vm.SelectedGroup = f.Vm.GroupRows[0];
        Assert.Single(targets); Assert.Contains("cancelled", f.Vm.StatusMessage);
    }

    [Theory]
    [InlineData(BreakdownChartMetric.Pnl, "Selected P&L")]
    [InlineData(BreakdownChartMetric.Trades, "Eligible closed Trades")]
    [InlineData(BreakdownChartMetric.WinRate, "Win Rate")]
    public void ChartAccessibleEquivalentIncludesMetricGroupValueAndCoverage(BreakdownChartMetric metric, string name)
    {
        var groups = BreakdownPresentationTests.Snapshot([BreakdownPresentationTests.Group(1, -25m), BreakdownPresentationTests.Group(2, 0m), BreakdownPresentationTests.Group(3, null)]).Rows;
        var chart = new BreakdownChartPresentation(groups, metric, "USD", "Net preferred");
        foreach (var row in chart.Rows)
        {
            Assert.Contains(name, row.AccessibleDescription); Assert.Contains(row.Group.Label, row.AccessibleDescription);
            Assert.Contains(row.ValueText, row.AccessibleDescription); Assert.Contains(row.Group.Metrics.Coverage, row.AccessibleDescription);
        }
        if (metric == BreakdownChartMetric.Pnl)
        {
            Assert.Contains("Unavailable", chart.Rows[2].AccessibleDescription);
            Assert.Contains("0.00", chart.Rows[1].AccessibleDescription);
        }
    }
}
