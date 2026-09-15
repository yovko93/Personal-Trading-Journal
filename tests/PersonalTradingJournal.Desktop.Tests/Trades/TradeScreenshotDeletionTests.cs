using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Domain.Trades;
using System.IO;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed class TradeScreenshotDeletionTests
{
    [Fact]
    public async Task DeleteRequiresVisibleMatchingTradeDetail()
    {
        DeletionFixture fixture = CreateFixture();
        TradeScreenshotListItem item = CreateScreenshot(Guid.NewGuid());

        Assert.False(fixture.ViewModel.DeleteScreenshotCommand.CanExecute(item));

        TradeScreenshotListItem loadedItem = await LoadDetailAsync(fixture);
        TradeScreenshotListItem crossTradeItem = CreateScreenshot(Guid.NewGuid());

        Assert.True(
            fixture.ViewModel.DeleteScreenshotCommand.CanExecute(loadedItem));
        Assert.False(
            fixture.ViewModel.DeleteScreenshotCommand.CanExecute(crossTradeItem));
    }

    [Fact]
    public async Task ConfirmationDeclineUsesOriginalFileNameAndChangesNoState()
    {
        DeletionFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await LoadDetailAsync(
            fixture,
            fileName: "Original Chart.PNG");
        fixture.Confirmation.Result = false;
        IReadOnlyList<TradeScreenshotListItem> originalList =
            fixture.ViewModel.TradeScreenshots;

        await fixture.ViewModel.DeleteScreenshotCommand.ExecuteAsync(item);

        Assert.Equal(1, fixture.Confirmation.CallCount);
        Assert.Equal("Original Chart.PNG", fixture.Confirmation.FileName);
        Assert.Equal(0, fixture.DeletionStore.CallCount);
        Assert.Same(originalList, fixture.ViewModel.TradeScreenshots);
        Assert.False(fixture.ViewModel.IsDeletingScreenshot);
        Assert.Null(fixture.ViewModel.ScreenshotDeleteErrorMessage);
        Assert.Null(fixture.ViewModel.ScreenshotDeleteWarningMessage);
        Assert.Null(fixture.ViewModel.ScreenshotDeleteSuccessMessage);
    }

    [Fact]
    public async Task ConfirmedDeleteUsesExactIdsReloadsAuthoritativeListAndShowsSuccess()
    {
        DeletionFixture fixture = CreateFixture();
        TradeListItem recentTrade = CreateTradeListItem();
        fixture.TradeListReader.EnqueueResult([recentTrade]);
        await fixture.ViewModel.EnsureLoadedAsync();
        TradeScreenshotListItem item = await LoadDetailAsync(
            fixture,
            trade: recentTrade);
        TradeScreenshotListItem remaining = CreateScreenshot(recentTrade.Id, "after.png");
        fixture.ScreenshotReader.EnqueueResult([remaining]);
        ConfigureSuccessfulDelete(fixture, item);
        fixture.ViewModel.QuantityText = "unchanged manual draft";

        await fixture.ViewModel.DeleteScreenshotCommand.ExecuteAsync(item);

        Assert.Equal(recentTrade.Id, fixture.DeletionStore.TradeId);
        Assert.Equal(item.Id, fixture.DeletionStore.ScreenshotId);
        Assert.Equal([recentTrade.Id, recentTrade.Id],
            fixture.ScreenshotReader.RequestedTradeIds);
        Assert.Equal(CancellationToken.None, fixture.ScreenshotReader.CancellationToken);
        Assert.Equal([remaining], fixture.ViewModel.TradeScreenshots);
        Assert.Equal(
            "Screenshot deleted successfully.",
            fixture.ViewModel.ScreenshotDeleteSuccessMessage);
        Assert.Null(fixture.ViewModel.ScreenshotDeleteWarningMessage);
        Assert.Null(fixture.ViewModel.ScreenshotDeleteErrorMessage);
        Assert.Equal("unchanged manual draft", fixture.ViewModel.QuantityText);
        Assert.Equal([recentTrade], fixture.ViewModel.RecentTrades);
    }

    [Fact]
    public async Task CleanupFailureIsSuccessfulWithNonFatalWarningAndNoDeleteError()
    {
        DeletionFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await LoadDetailAsync(fixture);
        fixture.ScreenshotReader.EnqueueResult([]);
        ConfigureSuccessfulDelete(fixture, item, "screenshots/private/chart.png");
        fixture.FileStorage.DeleteException =
            new IOException("C:\\private\\details must not leak");

        await fixture.ViewModel.DeleteScreenshotCommand.ExecuteAsync(item);

        Assert.Equal(
            "Screenshot deleted successfully.",
            fixture.ViewModel.ScreenshotDeleteSuccessMessage);
        Assert.Equal(
            "Screenshot was removed, but its local file could not be cleaned up.",
            fixture.ViewModel.ScreenshotDeleteWarningMessage);
        Assert.DoesNotContain(
            "private",
            fixture.ViewModel.ScreenshotDeleteWarningMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.Null(fixture.ViewModel.ScreenshotDeleteErrorMessage);
        Assert.Equal("screenshots/private/chart.png", fixture.FileStorage.DeletedStorageKey);
        Assert.Equal(CancellationToken.None, fixture.FileStorage.DeleteCancellationToken);
    }

    [Fact]
    public async Task CommittedDeleteClearsOnlyMatchingPreview()
    {
        DeletionFixture matchingFixture = CreateFixture();
        TradeScreenshotListItem matching = await LoadDetailAsync(matchingFixture);
        await OpenPreviewAsync(matchingFixture, matching);
        matchingFixture.ScreenshotReader.EnqueueResult([]);
        ConfigureSuccessfulDelete(matchingFixture, matching);

        await matchingFixture.ViewModel.DeleteScreenshotCommand.ExecuteAsync(matching);

        Assert.False(matchingFixture.ViewModel.IsScreenshotPreviewVisible);
        Assert.Null(matchingFixture.ViewModel.PreviewScreenshotId);

        DeletionFixture otherFixture = CreateFixture();
        TradeListItem trade = CreateTradeListItem();
        TradeScreenshotListItem deleted = CreateScreenshot(trade.Id, "deleted.png");
        TradeScreenshotListItem previewed = CreateScreenshot(trade.Id, "previewed.png");
        await LoadDetailAsync(otherFixture, trade, [deleted, previewed]);
        await OpenPreviewAsync(otherFixture, previewed);
        otherFixture.ScreenshotReader.EnqueueResult([previewed]);
        ConfigureSuccessfulDelete(otherFixture, deleted);

        await otherFixture.ViewModel.DeleteScreenshotCommand.ExecuteAsync(deleted);

        Assert.True(otherFixture.ViewModel.IsScreenshotPreviewVisible);
        Assert.Equal(previewed.Id, otherFixture.ViewModel.PreviewScreenshotId);
    }

    [Fact]
    public async Task PreCommitFailureRetainsListAndPreviewAndShowsSafeError()
    {
        DeletionFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await LoadDetailAsync(fixture);
        await OpenPreviewAsync(fixture, item);
        IReadOnlyList<TradeScreenshotListItem> originalList =
            fixture.ViewModel.TradeScreenshots;
        fixture.DeletionStore.Exception =
            new InvalidOperationException("database connection secret");

        await fixture.ViewModel.DeleteScreenshotCommand.ExecuteAsync(item);

        Assert.Same(originalList, fixture.ViewModel.TradeScreenshots);
        Assert.True(fixture.ViewModel.IsScreenshotPreviewVisible);
        Assert.Equal(item.Id, fixture.ViewModel.PreviewScreenshotId);
        Assert.Equal(
            "Screenshot could not be deleted.",
            fixture.ViewModel.ScreenshotDeleteErrorMessage);
        Assert.DoesNotContain(
            "secret",
            fixture.ViewModel.ScreenshotDeleteErrorMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.Null(fixture.ViewModel.ScreenshotDeleteSuccessMessage);
    }

    [Fact]
    public async Task MissingScreenshotShowsSafeErrorAndBestEffortReloads()
    {
        DeletionFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await LoadDetailAsync(fixture);
        TradeScreenshotListItem authoritative =
            CreateScreenshot(item.TradeId, "authoritative.png");
        fixture.ScreenshotReader.EnqueueResult([authoritative]);
        fixture.DeletionStore.Result = null;

        await fixture.ViewModel.DeleteScreenshotCommand.ExecuteAsync(item);

        Assert.Equal(
            "The screenshot is no longer available.",
            fixture.ViewModel.ScreenshotDeleteErrorMessage);
        Assert.Equal([authoritative], fixture.ViewModel.TradeScreenshots);
        Assert.Equal(2, fixture.ScreenshotReader.CallCount);
        Assert.Equal(CancellationToken.None, fixture.ScreenshotReader.CancellationToken);
        Assert.Null(fixture.ViewModel.TradeDetailErrorMessage);
    }

    [Fact]
    public async Task CancellationPropagatesClearsBusyStateAndRetainsListAndPreview()
    {
        DeletionFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await LoadDetailAsync(fixture);
        await OpenPreviewAsync(fixture, item);
        IReadOnlyList<TradeScreenshotListItem> originalList =
            fixture.ViewModel.TradeScreenshots;
        ConfigureSuccessfulDelete(fixture, item);
        fixture.DeletionStore.HoldDelete = true;

        Task deleteTask =
            fixture.ViewModel.DeleteScreenshotCommand.ExecuteAsync(item);
        await fixture.DeletionStore.DeleteStarted;
        Assert.True(fixture.ViewModel.IsDeletingScreenshot);
        fixture.ViewModel.DeleteScreenshotCommand.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await deleteTask);
        Assert.False(fixture.ViewModel.IsDeletingScreenshot);
        Assert.True(fixture.DeletionStore.CancellationToken.IsCancellationRequested);
        Assert.Same(originalList, fixture.ViewModel.TradeScreenshots);
        Assert.True(fixture.ViewModel.IsScreenshotPreviewVisible);
        Assert.Equal(item.Id, fixture.ViewModel.PreviewScreenshotId);
        Assert.Null(fixture.ViewModel.ScreenshotDeleteErrorMessage);
    }

    [Fact]
    public async Task PostCommitReloadFailureKeepsSuccessAndClearsKnownStaleList()
    {
        DeletionFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await LoadDetailAsync(fixture);
        ConfigureSuccessfulDelete(fixture, item);
        fixture.ScreenshotReader.EnqueueException(
            new InvalidOperationException("read failure details"));

        await fixture.ViewModel.DeleteScreenshotCommand.ExecuteAsync(item);

        Assert.Equal(
            "Screenshot deleted successfully.",
            fixture.ViewModel.ScreenshotDeleteSuccessMessage);
        Assert.Empty(fixture.ViewModel.TradeScreenshots);
        Assert.Equal(
            "Screenshots could not be loaded.",
            fixture.ViewModel.TradeScreenshotsErrorMessage);
        Assert.Null(fixture.ViewModel.ScreenshotDeleteErrorMessage);
        Assert.Equal(CancellationToken.None, fixture.ScreenshotReader.CancellationToken);
    }

    [Fact]
    public async Task BusyDeletionGatesCompetingCommandsAndPreservesAddDraft()
    {
        DeletionFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await LoadDetailAsync(fixture);
        fixture.ViewModel.ShowAddScreenshotCommand.Execute(null);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.Entry;
        fixture.ViewModel.ScreenshotDescriptionText = "keep this draft";
        ConfigureSuccessfulDelete(fixture, item);
        fixture.DeletionStore.HoldDelete = true;

        Task deleteTask =
            fixture.ViewModel.DeleteScreenshotCommand.ExecuteAsync(item);
        await fixture.DeletionStore.DeleteStarted;

        Assert.False(fixture.ViewModel.DeleteScreenshotCommand.CanExecute(item));
        Assert.False(fixture.ViewModel.RefreshCommand.CanExecute(null));
        Assert.False(
            fixture.ViewModel.ShowTradeDetailCommand.CanExecute(
                CreateTradeListItem()));
        Assert.False(fixture.ViewModel.CloseTradeDetailCommand.CanExecute(null));
        Assert.False(
            fixture.ViewModel.OpenScreenshotPreviewCommand.CanExecute(item));
        Assert.False(fixture.ViewModel.ShowAddScreenshotCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.CancelAddScreenshotCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.SaveScreenshotCommand.CanExecute(null));

        fixture.DeletionStore.ReleaseDelete();
        await deleteTask;

        Assert.True(fixture.ViewModel.IsAddScreenshotVisible);
        Assert.Equal(TradeScreenshotType.Entry,
            fixture.ViewModel.SelectedScreenshotType);
        Assert.Equal("keep this draft",
            fixture.ViewModel.ScreenshotDescriptionText);
    }

    [Fact]
    public async Task DeleteFeedbackDoesNotClearAddScreenshotFeedback()
    {
        DeletionFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await LoadDetailAsync(fixture);
        fixture.ViewModel.ShowAddScreenshotCommand.Execute(null);
        await fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);
        string? addError = fixture.ViewModel.ScreenshotValidationErrorMessage;
        ConfigureSuccessfulDelete(fixture, item);
        fixture.ScreenshotReader.EnqueueResult([]);

        await fixture.ViewModel.DeleteScreenshotCommand.ExecuteAsync(item);

        Assert.Equal("Screenshot type is required.", addError);
        Assert.Equal(addError, fixture.ViewModel.ScreenshotValidationErrorMessage);
        Assert.Equal(
            "Screenshot deleted successfully.",
            fixture.ViewModel.ScreenshotDeleteSuccessMessage);
    }

    private static void ConfigureSuccessfulDelete(
        DeletionFixture fixture,
        TradeScreenshotListItem item,
        string storageKey = "screenshots/chart.png")
    {
        fixture.DeletionStore.Result = new TradeScreenshotDeletionInfo(
            item.Id,
            item.TradeId,
            storageKey);
    }

    private static async Task OpenPreviewAsync(
        DeletionFixture fixture,
        TradeScreenshotListItem item)
    {
        fixture.ContentReader.EnqueueResult(
            new TradeScreenshotContent(
                item.Id,
                item.TradeId,
                item.FileName,
                new MemoryStream([1, 2, 3])));
        await fixture.ViewModel.OpenScreenshotPreviewCommand.ExecuteAsync(item);
        Assert.True(fixture.ViewModel.IsScreenshotPreviewVisible);
    }

    private static async Task<TradeScreenshotListItem> LoadDetailAsync(
        DeletionFixture fixture,
        TradeListItem? trade = null,
        IReadOnlyList<TradeScreenshotListItem>? screenshots = null,
        string fileName = "chart.png")
    {
        trade ??= CreateTradeListItem();
        screenshots ??= [CreateScreenshot(trade.Id, fileName)];
        fixture.DetailReader.EnqueueResult(CreateTradeDetail(trade));
        fixture.ScreenshotReader.EnqueueResult(screenshots);

        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(trade);

        return screenshots[0];
    }

    private static DeletionFixture CreateFixture()
    {
        var tradeListReader = new FakeTradeListReader();
        var detailReader = new FakeTradeDetailReader();
        var screenshotReader = new FakeTradeScreenshotReader();
        var contentReader = new FakeTradeScreenshotContentReader();
        var imageDecoder = new FakeTradeScreenshotImageDecoder();
        var fileStorage = new FakeTradeScreenshotFileStorage();
        var deletionStore = new FakeTradeScreenshotDeletionStore();
        var confirmation = new FakeTradeScreenshotDeleteConfirmation();
        var timeProvider = new FixedTimeProvider();

        var viewModel = new TradesViewModel(
            new FakeManualTradeReferenceDataReader(),
            new FakeTradingSetupReader(),
            new FakeTradingMistakeReader(),
            new FakeTradeMistakeReader(),
            tradeListReader,
            detailReader,
            new CreateManualTradeUseCase(
                new FakeTradingAccountStore(),
                new FakeInstrumentStore(),
                new FakeTradingSetupStore(),
                new FakeTradeStore(),
                timeProvider),
            new SetTradeTradingSetupUseCase(
                new FakeTradeMutationStore(),
                new FakeTradingSetupStore(),
                timeProvider),
            new AssignTradeMistakeUseCase(
                new FakeTradeExistenceReader(),
                new FakeTradingMistakeStore(),
                new FakeTradeMistakeStore(),
                timeProvider),
            new RemoveTradeMistakeUseCase(new FakeTradeMistakeStore()),
            new CloseManualTradeUseCase(
                new FakeTradeMutationStore(),
                timeProvider),
            screenshotReader,
            new AddTradeScreenshotUseCase(
                new FakeTradeExistenceReader(),
                fileStorage,
                new FakeTradeScreenshotStore(),
                timeProvider),
            new FakeTradeScreenshotFilePicker(),
            contentReader,
            imageDecoder,
            new DeleteTradeScreenshotUseCase(deletionStore, fileStorage),
            confirmation);

        return new DeletionFixture(
            viewModel,
            tradeListReader,
            detailReader,
            screenshotReader,
            contentReader,
            fileStorage,
            deletionStore,
            confirmation);
    }

    private static TradeScreenshotListItem CreateScreenshot(
        Guid tradeId,
        string fileName = "chart.png") =>
        new(
            Guid.NewGuid(),
            tradeId,
            TradeScreenshotType.Entry,
            fileName,
            new DateTimeOffset(2026, 9, 13, 13, 30, 0, TimeSpan.Zero),
            "5m",
            "Entry context",
            new DateTimeOffset(2026, 9, 13, 14, 0, 0, TimeSpan.Zero));

    private static TradeListItem CreateTradeListItem()
    {
        DateTimeOffset openedAtUtc =
            new(2026, 9, 13, 13, 30, 0, TimeSpan.Zero);

        return new TradeListItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Primary Account",
            Guid.NewGuid(),
            "NQ",
            TradeDirection.Long,
            TradeStatus.Closed,
            openedAtUtc,
            openedAtUtc.AddHours(1),
            0m,
            23950.25m,
            23975.50m,
            3.50m,
            1262.50m,
            1259m,
            "USD");
    }

    private static TradeDetail CreateTradeDetail(TradeListItem item) =>
        new(
            item.Id,
            item.TradingAccountId,
            item.TradingAccountName,
            item.InstrumentId,
            item.InstrumentSymbol,
            "Nasdaq-100 E-mini",
            null,
            null,
            null,
            item.Direction,
            item.Status,
            item.OpenedAtUtc,
            item.ClosedAtUtc,
            item.OpenQuantity,
            item.AverageEntryPrice,
            item.AverageExitPrice,
            item.TotalCosts,
            item.GrossPnL,
            item.NetPnL,
            20m,
            item.Currency,
            []);

    private sealed record DeletionFixture(
        TradesViewModel ViewModel,
        FakeTradeListReader TradeListReader,
        FakeTradeDetailReader DetailReader,
        FakeTradeScreenshotReader ScreenshotReader,
        FakeTradeScreenshotContentReader ContentReader,
        FakeTradeScreenshotFileStorage FileStorage,
        FakeTradeScreenshotDeletionStore DeletionStore,
        FakeTradeScreenshotDeleteConfirmation Confirmation);
}
