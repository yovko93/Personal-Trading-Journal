using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
namespace PersonalTradingJournal.Desktop.Tests.Trades;
public sealed partial class TradesViewModelTests
{
    internal static async Task<TradesViewModel> CreateAssociationRenderModel(System.Windows.Media.ImageSource? image = null)
    {
        var setup = CreateTradingSetup("Synthetic NQ range retest"); var item = CreateTradeListItem();
        var reader = new FakeTradeDetailReader(); reader.EnqueueResult(CreateTradeDetail(item));
        var list = new FakeTradeListReader();
        list.EnqueueResult([item with { TradingAccountName = "A long synthetic account name for the responsive Trade row" }]);
        var mistake = CreateTradingMistake("Missed entry confirmation");
        var mistakes = new FakeTradingMistakeReader();
        mistakes.EnqueueResult([CreateTradingMistakeListItem(mistake)]);
        var setups = new FakeTradingSetupReader(); setups.EnqueueResult([CreateTradingSetupListItem(setup)]);
        var screenshots = new FakeTradeScreenshotReader(); var content = new FakeTradeScreenshotContentReader();
        if (image is not null)
        {
            var screenshot = new PersonalTradingJournal.Application.Screenshots.TradeScreenshotListItem(Guid.NewGuid(), item.Id,
                PersonalTradingJournal.Domain.Screenshots.TradeScreenshotType.Entry, "legacy.png", DateTimeOffset.UtcNow, "5m", "Legacy metadata preserved, not displayed", DateTimeOffset.UtcNow);
            screenshots.EnqueueResult([screenshot]);
            content.EnqueueResult(new(screenshot.Id, item.Id, screenshot.FileName, new System.IO.MemoryStream([1, 2, 3])));
        }
        var vm = CreateViewModel(tradeDetailReader: reader, tradeListReader: list, tradingSetupReader: setups, tradingMistakeReader: mistakes, tradeScreenshotReader: screenshots,
            tradeScreenshotContentReader: content, tradeScreenshotImageDecoder: new FakeTradeScreenshotImageDecoder { ImageToReturn = image ?? new System.Windows.Media.DrawingImage() });
        await vm.EnsureLoadedAsync(); await vm.ShowTradeDetailCommand.ExecuteAsync(item);
        return vm;
    }
    [Fact]
    public void TradeCaptureCommandsAndDependenciesAreRetired()
    {
        var type = typeof(TradesViewModel);
        foreach (var name in new[] { "LoadPlaybookHistoryCommand", "RefreshAssociationPreviewCommand", "OpenCapturedImageCommand", "CapturedAssociations" })
            Assert.Null(type.GetProperty(name));
        Assert.DoesNotContain(type.GetConstructors().SelectMany(c => c.GetParameters()), p => p.ParameterType.Name.Contains("Playbook"));
    }
}
