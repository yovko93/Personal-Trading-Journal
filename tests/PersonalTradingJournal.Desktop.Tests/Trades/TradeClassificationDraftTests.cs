using PersonalTradingJournal.Desktop.Tests.TestDoubles;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed partial class TradesViewModelTests
{
    [Fact]
    public async Task ClassificationCancelKeepsSavedValuesAndProtectsTheSelectedTrade()
    {
        var item = CreateTradeListItem();
        var second = CreateTradeListItem(symbol: "ES");
        var current = CreateTradingSetup("Saved setup");
        var replacement = CreateTradingSetup("Draft replacement");
        var savedMistake = CreateTradeMistakeListItem(item.Id, Guid.NewGuid(), "Saved mistake", note: "Saved note");
        var candidate = CreateTradingMistake("Draft mistake");

        var detail = CreateTradeDetail(item) with { TradingSetupId = current.Id, TradingSetupName = current.Name };
        var details = new FakeTradeDetailReader();
        details.EnqueueResult(detail);
        details.EnqueueResult(CreateTradeDetail(second));
        var setups = new FakeTradingSetupReader();
        setups.EnqueueResult([CreateTradingSetupListItem(current), CreateTradingSetupListItem(replacement)]);
        var mistakes = new FakeTradingMistakeReader();
        mistakes.EnqueueResult([CreateTradingMistakeListItem(candidate)]);
        var assigned = new FakeTradeMistakeReader();
        assigned.EnqueueResult([savedMistake]);
        var mistakeStore = new FakeTradeMistakeStore();
        var mutationStore = new FakeTradeMutationStore();
        var vm = CreateViewModel(tradeDetailReader: details, tradingSetupReader: setups,
            tradingMistakeReader: mistakes, tradeMistakeReader: assigned, tradeMistakeStore: mistakeStore,
            tradeMutationStore: mutationStore);
        await vm.EnsureLoadedAsync();
        await vm.ShowTradeDetailCommand.ExecuteAsync(item);

        Assert.False(vm.HasTradeClassificationDraft);
        Assert.Equal("Change Setup", vm.TradeSetupEditorLabel);
        vm.OpenSetupEditorCommand.Execute(null);
        vm.SelectedTradeDetailTradingSetup = vm.TradeDetailTradingSetupOptions.Single(setup => setup.Id == replacement.Id);
        Assert.Same(detail, vm.SelectedTradeDetail);
        Assert.False(vm.TryLeaveBreakdownScope());
        Assert.False(vm.CloseTradeDetailCommand.CanExecute(null));
        Assert.False(vm.OpenMistakeEditorCommand.CanExecute(null));
        await vm.ShowTradeByIdAsync(second.Id);
        Assert.Equal(1, details.CallCount);

        vm.CancelSetupEditorCommand.Execute(null);
        Assert.False(vm.HasTradeClassificationDraft);
        Assert.Equal(current.Id, vm.SelectedTradeDetailTradingSetup?.Id);
        Assert.Equal(0, mutationStore.SaveCallCount);

        vm.OpenMistakeEditorCommand.Execute(null);
        vm.SelectedTradingMistake = Assert.Single(vm.AvailableTradingMistakes);
        vm.TradeMistakeNoteText = "Draft note";
        Assert.False(vm.ShowTradeDetailCommand.CanExecute(second));
        Assert.False(vm.ShowSelectedTradeEditCommand.CanExecute(null));
        vm.CancelMistakeEditorCommand.Execute(null);
        Assert.Empty(vm.TradeMistakeNoteText);
        Assert.Null(vm.SelectedTradingMistake);
        Assert.Same(savedMistake, Assert.Single(vm.TradeMistakes));
        Assert.Equal(0, mistakeStore.AddCallCount);
        Assert.Equal(0, mistakeStore.RemoveCallCount);
        Assert.True(vm.TryLeaveBreakdownScope());

        Assert.True(vm.ShowTradeDetailCommand.CanExecute(second));
        await vm.ShowTradeDetailCommand.ExecuteAsync(second);
        Assert.Equal(second.Id, vm.SelectedTradeDetail?.Id);
        Assert.False(vm.HasTradeClassificationDraft);
    }
}