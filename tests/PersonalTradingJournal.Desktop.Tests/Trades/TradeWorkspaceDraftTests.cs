using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
using PersonalTradingJournal.Domain.Screenshots;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed partial class TradesViewModelTests
{
    [Fact]
    public async Task FailedCreationKeepsDescriptionAndStagedScreenshotAndCancelDiscardsBoth()
    {
        var fixture = CreateSaveFixture();
        var vm = fixture.ViewModel;
        vm.TradeDescriptionText = "Trade description, not screenshot metadata";
        var draft = new ManualTradeScreenshot(TradeScreenshotType.Entry, "5m", "synthetic.png", [1, 2, 3]);
        vm.StagedScreenshots.Add(new(draft, null));
        Assert.True(vm.TryBuildManualTradeCommand(out var command));
        Assert.Equal(vm.TradeDescriptionText, command!.Description);
        Assert.Same(draft, Assert.Single(command.Screenshots!));
        // The fixture deliberately has no atomic screenshot store: no fallback partial write is allowed.
        await vm.SaveManualTradeCommand.ExecuteAsync(null);
        Assert.NotNull(vm.SaveErrorMessage); Assert.True(vm.IsManualEntryVisible);
        Assert.Single(vm.StagedScreenshots); Assert.Equal(0, fixture.TradeStore.AddCallCount);
        Assert.Equal("Trade description, not screenshot metadata", vm.TradeDescriptionText);
        vm.CancelManualEntryCommand.Execute(null);
        Assert.Empty(vm.StagedScreenshots); Assert.Equal(string.Empty, vm.TradeDescriptionText);
        Assert.False(vm.IsAddScreenshotVisible);
    }

    [Fact]
    public async Task UnfinishedScreenshotFormCannotBeSilentlyDroppedByTradeSave()
    {
        var fixture = CreateSaveFixture(); var vm = fixture.ViewModel;
        vm.ShowAddScreenshotCommand.Execute(null);
        Assert.True(vm.IsAddScreenshotVisible);
        await vm.SaveManualTradeCommand.ExecuteAsync(null);
        Assert.Equal("Save or cancel the screenshot form before saving the Trade.", vm.SaveErrorMessage);
        Assert.Equal(0, fixture.TradeStore.AddCallCount);
    }

    [Fact]
    public async Task ManualCreationSavesDescriptionWithoutChangingExecutionFacts()
    {
        var fixture = CreateSaveFixture(); var vm = fixture.ViewModel;
        vm.TradeDescriptionText = "Optional description 🙂";
        await vm.SaveManualTradeCommand.ExecuteAsync(null);
        Assert.Equal("Optional description 🙂", fixture.TradeStore.AddedTrade!.Description);
        Assert.Equal(23950.25m, fixture.TradeStore.AddedTrade.Executions[0].Price);
        Assert.Equal(1.5m, fixture.TradeStore.AddedTrade.Executions[0].Commission);
    }
}
