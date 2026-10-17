using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.Breakdown;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    [Fact]
    public async Task DrillDownAndBackKeepDraftAndRefreshAppliedQueryThroughShell()
    {
        using var breakdown = new BreakdownFixture();
        var f = CreateFixture(breakdown: breakdown.Vm); using var main = f.Main;
        main.NavigateCommand.Execute(NavigationDestination.Breakdown); await breakdown.Vm.LoadTask;
        var applied = breakdown.Vm.Result!.Query;
        var row = Assert.Single(breakdown.Vm.GroupRows);
        breakdown.Vm.SelectedGroup = row; breakdown.Vm.Currency = "JPY";
        await breakdown.Vm.ViewTradesCommand.ExecuteAsync(row);
        Assert.Equal(NavigationDestination.Trades, main.CurrentDestination);
        Assert.Equal(row.Source.Metrics.TradeIds, Assert.Single(f.TradeListReader.RequestedQueries).Cohort!.TradeIds);
        Assert.True(f.Trades.HasBreakdownScope);
        f.Trades.ShowManualEntryCommand.Execute(null); f.Trades.QuantityText = "7";
        await f.Trades.BackToBreakdownCommand.ExecuteAsync(null);
        Assert.Equal(NavigationDestination.Trades, main.CurrentDestination); Assert.Equal("7", f.Trades.QuantityText);
        Assert.False(main.CanCloseWindow());
        f.Trades.ResetTransientState();
        await f.Trades.BackToBreakdownCommand.ExecuteAsync(null);
        Assert.Equal(NavigationDestination.Breakdown, main.CurrentDestination);
        Assert.Same(applied, breakdown.Reader.Calls.Last().Query);
        Assert.Equal("JPY", breakdown.Vm.Currency); Assert.True(breakdown.Vm.HasUnappliedChanges);
        Assert.Equal(row.Key, breakdown.Vm.SelectedGroupKey);
    }

    [Fact]
    public async Task BreakdownNavigationRetainsDraftAndResultAndHonorsJournalVeto()
    {
        using var breakdown = new BreakdownFixture();
        var f = CreateFixture(breakdown: breakdown.Vm);
        using var main = f.Main;
        main.NavigateCommand.Execute(NavigationDestination.Breakdown); await breakdown.Vm.LoadTask;
        Assert.Same(breakdown.Vm, main.CurrentContentViewModel);
        var result = breakdown.Vm.Result;
        breakdown.Vm.Currency = "USD";
        main.NavigateCommand.Execute(NavigationDestination.Dashboard);
        main.NavigateCommand.Execute(NavigationDestination.Breakdown); await breakdown.Vm.LoadTask;
        Assert.Same(result, breakdown.Vm.Result); Assert.Equal("USD", breakdown.Vm.Currency); Assert.Single(breakdown.Reader.Calls);
        main.NavigateCommand.Execute(NavigationDestination.Journal); await f.Journal.LoadTask;
        f.Journal.OpenEditorCommand.Execute(null); f.Journal.Text = "Synthetic unsaved Journal";
        main.NavigateCommand.Execute(NavigationDestination.Breakdown);
        Assert.Equal(NavigationDestination.Journal, main.CurrentDestination); Assert.Single(breakdown.Reader.Calls);
    }

    [Fact]
    public async Task BreakdownAcceptedOwnerCloseCancelsReadAndNeverPublishesLateResult()
    {
        using var breakdown = new BreakdownFixture();
        var f = CreateFixture(breakdown: breakdown.Vm); using var main = f.Main;
        breakdown.Reader.Hold = true;
        main.NavigateCommand.Execute(NavigationDestination.Breakdown);
        var call = await breakdown.Reader.Next();
        Assert.True(main.TryCloseWindow()); Assert.True(call.Token.IsCancellationRequested);
        call.Completion.SetResult(BreakdownFixture.Snapshot(call.Query)); await breakdown.Vm.LoadTask;
        Assert.Null(breakdown.Vm.Result); Assert.False(breakdown.Vm.IsLoading);
    }
}
