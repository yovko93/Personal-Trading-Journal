using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.ViewModels.Breakdown;

namespace PersonalTradingJournal.Desktop.Tests.Breakdown;

public sealed class BreakdownDrillDownTests
{
    [Fact]
    public async Task TableActionUsesExactRetainedSetupNotDuplicateLabelOrSelectedRowAndRemovedKeyClears()
    {
        using var f = new BreakdownFixture();
        await f.Vm.ActivateAsync(); f.Vm.Grouping = BreakdownGrouping.TradingSetup;
        var first = BreakdownGroupKey.ForSetup(Guid.NewGuid()); var second = BreakdownGroupKey.ForSetup(Guid.NewGuid());
        Guid firstTrade = Guid.NewGuid(), secondTrade = Guid.NewGuid();
        var metric = BreakdownPresentationTests.Metrics(1, 0, null);
        f.Reader.MakeResult = q => new(q, metric, [new(first, "Same historical Setup", metric with { TradeIds = new[] { firstTrade } }),
            new(second, "Same historical Setup", metric with { TradeIds = new[] { secondTrade } })]);
        await f.Vm.ApplyAsync();
        f.Vm.SelectedGroup = f.Vm.GroupRows.Single(r => r.Key == first);
        BreakdownTradeContext? captured = null;
        f.Vm.OpenTradesAsync = context => { captured = context; return Task.CompletedTask; };
        await f.Vm.ViewTradesCommand.ExecuteAsync(f.Vm.GroupRows.Single(r => r.Key == second));
        Assert.Equal(second, captured!.Selection.Key); Assert.Equal(secondTrade, Assert.Single(captured.Cohort.TradeIds));
        f.Reader.MakeResult = q => new(q, metric, [new(second, "Same historical Setup", metric with { TradeIds = new[] { secondTrade } })]);
        await f.Vm.RefreshAppliedAsync(); Assert.Null(f.Vm.SelectedGroupKey);
    }

    [Theory]
    [InlineData(BreakdownGrouping.Instrument)]
    [InlineData(BreakdownGrouping.Account)]
    [InlineData(BreakdownGrouping.TradingSetup)]
    [InlineData(BreakdownGrouping.Direction)]
    [InlineData(BreakdownGrouping.EntryWeekday)]
    [InlineData(BreakdownGrouping.EntryHour)]
    [InlineData(BreakdownGrouping.HoldingDuration)]
    public async Task AllDimensionsCaptureDisplayedIdsIncludingUnavailableValuesNotDraftFilters(BreakdownGrouping grouping)
    {
        using var f = new BreakdownFixture();
        f.Reader.MakeResult = q => BreakdownFixture.Snapshot(q, unavailable: true);
        await f.Vm.ActivateAsync(); f.Vm.Grouping = grouping; await f.Vm.ApplyAsync();
        var row = Assert.Single(f.Vm.GroupRows); var query = f.Vm.Result!.Query;
        f.Vm.Currency = "JPY";
        BreakdownTradeContext? captured = null;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        f.Vm.OpenTradesAsync = c => { calls++; captured = c; return release.Task; };
        Assert.True(f.Vm.ViewTradesCommand.CanExecute(row));
        Task open = f.Vm.ViewTradesCommand.ExecuteAsync(row);
        await f.Vm.ViewTradesCommand.ExecuteAsync(row);
        Assert.Equal(1, calls); Assert.False(f.Vm.ViewTradesCommand.CanExecute(row));
        Assert.Same(query, captured!.Selection.Query); Assert.Equal(row.Key, captured.Selection.Key);
        Assert.Equal(row.Source.Metrics.TradeIds, captured.Cohort.TradeIds);
        Assert.Equal(1, captured.OriginalCount); Assert.Equal("JPY", f.Vm.Currency);
        release.SetResult(); await open;
        await f.Vm.RefreshAppliedAsync();
        Assert.False(f.Vm.ViewTradesCommand.CanExecute(row));
        await f.Vm.ViewTradesCommand.ExecuteAsync(row); Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ReturnRefreshUsesLastSuccessfulQueryAfterFailedApplyAndKeepsDraftAndPresentation()
    {
        using var f = new BreakdownFixture(); await f.Vm.ActivateAsync();
        var applied = f.Vm.Result!.Query; var key = f.Vm.GroupRows[0].Key;
        f.Vm.SelectedGroup = f.Vm.GroupRows[0]; f.Vm.ChartMetric = BreakdownChartMetric.Trades;
        f.Vm.SortGroups(BreakdownSortColumn.Pnl);
        f.Vm.Currency = "JPY"; f.Reader.MakeResult = _ => throw new IOException("Synthetic");
        await f.Vm.ApplyAsync(); Assert.NotNull(f.Vm.ErrorMessage);
        f.Vm.Currency = ""; await f.Vm.ApplyAsync(); var validation = f.Vm.CurrencyValidation;
        f.Reader.MakeResult = q => BreakdownFixture.Snapshot(q);
        await f.Vm.RefreshAppliedAsync();
        Assert.Same(applied, f.Reader.Calls.Last().Query); Assert.Equal("", f.Vm.Currency);
        Assert.Equal(validation, f.Vm.CurrencyValidation); Assert.True(f.Vm.HasUnappliedChanges);
        Assert.Equal(key, f.Vm.SelectedGroupKey); Assert.Equal(BreakdownChartMetric.Trades, f.Vm.ChartMetric);
        Assert.Equal(BreakdownSortColumn.Pnl, f.Vm.SortColumn);
    }
}
