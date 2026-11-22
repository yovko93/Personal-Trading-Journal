using PersonalTradingJournal.Desktop.Tests.Rules;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed partial class TradesViewModelTests
{
    private const string SetupSuccess = "Trading setup updated successfully.";

    [Fact]
    public async Task LaterSetupSuccessGetsItsOwnFiveSeconds()
    {
        var clock = new FeedbackClock();
        var (vm, _) = await CreateSetupFeedbackFixture(clock);
        await SaveSetup(vm);
        Task firstExpiry = vm.SetupSuccessExpiryTask;
        clock.Advance(4);
        Assert.Equal(SetupSuccess, vm.TradingSetupSuccessMessage);

        await vm.ClearTradingSetupCommand.ExecuteAsync(null);
        Task secondExpiry = vm.SetupSuccessExpiryTask;
        Assert.NotSame(firstExpiry, secondExpiry);
        await firstExpiry.WaitAsync(TimeSpan.FromSeconds(5));

        clock.Advance(1);
        Assert.Equal(SetupSuccess, vm.TradingSetupSuccessMessage);
        clock.Advance(3);
        Assert.True(vm.HasTradingSetupSuccess);
        clock.Advance(1);
        await secondExpiry.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(vm.TradingSetupSuccessMessage);
        Assert.False(vm.HasTradingSetupSuccess);
    }

    [Fact]
    public async Task SetupFeedbackExpiryLeavesSaveErrorsAndValidationIntact()
    {
        var clock = new FeedbackClock();
        var (vm, store) = await CreateSetupFeedbackFixture(clock);
        await SaveSetup(vm);
        Task expiry = vm.SetupSuccessExpiryTask;
        store.GetException = new InvalidOperationException("Synthetic store failure.");
        await vm.ClearTradingSetupCommand.ExecuteAsync(null);
        Assert.True(vm.HasTradingSetupSaveError);
        Assert.Null(vm.TradingSetupSuccessMessage);
        string? error = vm.TradingSetupSaveErrorMessage;
        await expiry.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(5);
        Assert.Equal(error, vm.TradingSetupSaveErrorMessage);

        store.GetException = null;
        await vm.ClearTradingSetupCommand.ExecuteAsync(null);
        Task nextExpiry = vm.SetupSuccessExpiryTask;
        vm.ShowManualEntryCommand.Execute(null);
        Assert.False(vm.TryBuildManualTradeCommand(out _));
        string? validation = vm.ValidationErrorMessage;
        Assert.NotNull(validation);
        clock.Advance(5);
        await nextExpiry.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(vm.TradingSetupSuccessMessage);
        Assert.Equal(validation, vm.ValidationErrorMessage);
    }

    [Theory]
    [InlineData("navigation")]
    [InlineData("disposal")]
    [InlineData("inline reset")]
    public async Task LeavingTradeDetailsCancelsPendingSetupExpiry(string operation)
    {
        var clock = new FeedbackClock();
        var (vm, _) = await CreateSetupFeedbackFixture(clock);
        await SaveSetup(vm);
        Task expiry = vm.SetupSuccessExpiryTask;

        switch (operation)
        {
            case "navigation": vm.DeactivateBrowse(); break;
            case "disposal": vm.DisposeBrowse(); break;
            case "inline reset": vm.ResetTransientState(); break;
        }

        Assert.Null(vm.TradingSetupSuccessMessage);
        await expiry.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(5);
        Assert.False(vm.HasTradingSetupSuccess);
    }

    [Fact]
    public async Task LateSetupWriteCannotPublishFeedbackAfterNavigation()
    {
        var clock = new FeedbackClock();
        var (vm, store) = await CreateSetupFeedbackFixture(clock);
        store.HoldSave = true;
        Task initialExpiry = vm.SetupSuccessExpiryTask;
        Task save = SaveSetup(vm);
        await store.SaveStarted.WaitAsync(TimeSpan.FromSeconds(5));
        vm.DeactivateBrowse();
        store.ReleaseSave();
        await save.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, store.SaveCallCount);
        Assert.Null(vm.TradingSetupSuccessMessage);
        Assert.Same(initialExpiry, vm.SetupSuccessExpiryTask);
    }

    private static Task SaveSetup(TradesViewModel vm)
    {
        vm.OpenSetupEditorCommand.Execute(null);
        vm.SelectedTradeDetailTradingSetup = Assert.Single(vm.TradeDetailTradingSetupOptions);
        Assert.True(vm.SaveTradingSetupCommand.CanExecute(null));
        return vm.SaveTradingSetupCommand.ExecuteAsync(null);
    }

    private static async Task<(TradesViewModel ViewModel, FakeTradeMutationStore Store)> CreateSetupFeedbackFixture(TimeProvider clock)
    {
        var trade = CreateTradeListItem();
        var setup = CreateTradingSetup("Synthetic setup");
        var detail = CreateTradeDetail(trade);
        var details = new FakeTradeDetailReader();
        details.EnqueueResult(detail);
        details.EnqueueResult(detail with { TradingSetupId = setup.Id, TradingSetupName = setup.Name, IsTradingSetupActive = true });
        details.EnqueueResult(detail);
        var setups = new FakeTradingSetupReader();
        setups.EnqueueResult([CreateTradingSetupListItem(setup)]);
        var store = new FakeTradeMutationStore { TradeToReturn = CreateOpenDomainTrade(trade.Id, 1m) };
        var vm = CreateViewModel(tradeDetailReader: details, tradeMutationStore: store, tradingSetupReader: setups,
            tradingSetupStore: new FakeTradingSetupStore { Setup = setup }, timeProvider: clock);
        await vm.EnsureLoadedAsync();
        await vm.ShowTradeDetailCommand.ExecuteAsync(trade);
        return (vm, store);
    }
}