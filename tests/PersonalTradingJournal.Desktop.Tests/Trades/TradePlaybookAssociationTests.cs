using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
namespace PersonalTradingJournal.Desktop.Tests.Trades;
public sealed partial class TradesViewModelTests
{
    internal static async Task<TradesViewModel> CreateAssociationRenderModel(System.Windows.Media.ImageSource? image = null, bool imported = false, bool zeroNet = false, bool unknownNet = false)
    {
        var setup = CreateTradingSetup("Synthetic NQ range retest"); var item = CreateTradeListItem();
        if (zeroNet) item = item with { GrossPnL = 3.50m, NetPnL = 0m };
        if (unknownNet) item = item with { GrossPnL = null, NetPnL = null };
        var detail = CreateTradeDetail(item);
        var assigned = new FakeTradeMistakeReader();
        if (imported)
        {
            detail = detail with { IsImported = true, Description = "Imported annotation", TradingSetupId = setup.Id, TradingSetupName = setup.Name };
            assigned.EnqueueResult([CreateTradeMistakeListItem(item.Id, Guid.NewGuid(), "Saved mistake")]);
        }
        var reader = new FakeTradeDetailReader(); reader.EnqueueResult(detail);
        var list = new FakeTradeListReader();
        list.EnqueueResult([item with { TradingAccountName = "A long synthetic account name for the responsive Trade row" }]);
        var mistake = CreateTradingMistake("Missed entry confirmation");
        var mistakes = new FakeTradingMistakeReader();
        mistakes.EnqueueResult([CreateTradingMistakeListItem(mistake)]);
        var setups = new FakeTradingSetupReader();
        setups.EnqueueResult(imported
            ? [CreateTradingSetupListItem(setup), CreateTradingSetupListItem(CreateTradingSetup("Another setup"))]
            : [CreateTradingSetupListItem(setup)]);
        var screenshots = new FakeTradeScreenshotReader(); var content = new FakeTradeScreenshotContentReader();
        if (image is not null)
        {
            var screenshot = new PersonalTradingJournal.Application.Screenshots.TradeScreenshotListItem(Guid.NewGuid(), item.Id,
                PersonalTradingJournal.Domain.Screenshots.TradeScreenshotType.Entry, "legacy.png", DateTimeOffset.UtcNow, "5m", "Legacy metadata preserved, not displayed", DateTimeOffset.UtcNow);
            screenshots.EnqueueResult([screenshot]);
            content.EnqueueResult(new(screenshot.Id, item.Id, screenshot.FileName, new System.IO.MemoryStream([1, 2, 3])));
        }
        var vm = CreateViewModel(tradeDetailReader: reader, tradeListReader: list, tradingSetupReader: setups, tradingMistakeReader: mistakes, tradeScreenshotReader: screenshots,
            tradeMistakeReader: assigned, importedDescriptionStore: new RecordingDescriptions(),
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
