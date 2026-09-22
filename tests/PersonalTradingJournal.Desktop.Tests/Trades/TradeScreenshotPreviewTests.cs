using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Domain.Trades;
using System.IO;
using System.Windows.Media;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed class TradeScreenshotPreviewTests
{
    [Fact]
    public async Task OpenRequiresVisibleMatchingTradeDetail()
    {
        PreviewFixture fixture = CreateFixture();
        TradeScreenshotListItem staleItem = CreateScreenshot(Guid.NewGuid());

        Assert.False(
            fixture.ViewModel.OpenScreenshotPreviewCommand.CanExecute(staleItem));

        TradeScreenshotListItem selectedItem = await LoadDetailAsync(fixture);

        Assert.True(
            fixture.ViewModel.OpenScreenshotPreviewCommand.CanExecute(selectedItem));
        Assert.False(
            fixture.ViewModel.OpenScreenshotPreviewCommand.CanExecute(staleItem));
        Assert.Equal(0, fixture.ContentReader.CallCount);
    }

    [Fact]
    public async Task SuccessfulOpenUsesIdDecodesExactStreamAndDisposesIt()
    {
        PreviewFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await LoadDetailAsync(fixture);
        var stream = new TrackingMemoryStream([1, 2, 3]);
        var image = new DrawingImage();
        fixture.ImageDecoder.ImageToReturn = image;
        fixture.ContentReader.EnqueueResult(
            new TradeScreenshotContent(
                item.Id,
                item.TradeId,
                "reader-name.png",
                stream));

        await fixture.ViewModel.OpenScreenshotPreviewCommand.ExecuteAsync(item);

        Assert.Equal([item.Id], fixture.ContentReader.RequestedScreenshotIds);
        Assert.Equal(1, fixture.ImageDecoder.CallCount);
        Assert.Same(stream, fixture.ImageDecoder.DecodedContent);
        Assert.True(stream.IsDisposed);
        Assert.Equal(item.Id, fixture.ViewModel.PreviewScreenshotId);
        Assert.Equal(item.FileName, fixture.ViewModel.PreviewScreenshotFileName);
        Assert.Same(image, fixture.ViewModel.PreviewScreenshotImage);
        Assert.True(fixture.ViewModel.IsScreenshotPreviewVisible);
        Assert.False(fixture.ViewModel.IsScreenshotPreviewLoading);
        Assert.Null(fixture.ViewModel.ScreenshotPreviewErrorMessage);
    }

    [Fact]
    public async Task MissingMetadataShowsStaleScreenshotMessage()
    {
        PreviewFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await LoadDetailAsync(fixture);
        fixture.ContentReader.EnqueueResult(null);

        await fixture.ViewModel.OpenScreenshotPreviewCommand.ExecuteAsync(item);

        Assert.Equal(
            "The screenshot is no longer available.",
            fixture.ViewModel.ScreenshotPreviewErrorMessage);
        Assert.True(fixture.ViewModel.HasScreenshotPreviewError);
        Assert.False(fixture.ViewModel.IsScreenshotPreviewVisible);
        Assert.Null(fixture.ViewModel.PreviewScreenshotImage);
        Assert.Equal(0, fixture.ImageDecoder.CallCount);
        Assert.Same(fixture.Screenshots, fixture.ViewModel.TradeScreenshots);
        Assert.Same(fixture.Detail, fixture.ViewModel.SelectedTradeDetail);

        fixture.ViewModel.CloseScreenshotPreviewCommand.Execute(null);
        Assert.Null(fixture.ViewModel.ScreenshotPreviewErrorMessage);
        Assert.Null(fixture.ViewModel.PreviewScreenshotId);
        Assert.Null(fixture.ViewModel.PreviewScreenshotFileName);
    }

    [Fact]
    public async Task MissingPhysicalFileShowsSafeSpecificMessage()
    {
        PreviewFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await LoadDetailAsync(fixture);
        fixture.ContentReader.EnqueueException(
            new FileNotFoundException("C:\\private\\stored-file.png"));

        await fixture.ViewModel.OpenScreenshotPreviewCommand.ExecuteAsync(item);

        Assert.Equal(
            "The screenshot file is missing.",
            fixture.ViewModel.ScreenshotPreviewErrorMessage);
        Assert.DoesNotContain(
            "private",
            fixture.ViewModel.ScreenshotPreviewErrorMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.Null(fixture.ViewModel.TradeDetailErrorMessage);
        Assert.Same(fixture.Detail, fixture.ViewModel.SelectedTradeDetail);
    }

    [Fact]
    public async Task GenericReaderFailureShowsSafeFallbackWithoutRawDetails()
    {
        PreviewFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await LoadDetailAsync(fixture);
        fixture.ContentReader.EnqueueException(
            new InvalidOperationException("StorageKey secret-key failed."));

        await fixture.ViewModel.OpenScreenshotPreviewCommand.ExecuteAsync(item);

        Assert.Equal(
            "Screenshot could not be opened.",
            fixture.ViewModel.ScreenshotPreviewErrorMessage);
        Assert.DoesNotContain(
            "StorageKey",
            fixture.ViewModel.ScreenshotPreviewErrorMessage,
            StringComparison.Ordinal);
        Assert.Null(fixture.ViewModel.TradeDetailErrorMessage);
        Assert.Null(fixture.ViewModel.TradeScreenshotsErrorMessage);
    }

    [Fact]
    public async Task DecoderFailureDisposesStreamAndShowsSafeFallback()
    {
        PreviewFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await LoadDetailAsync(fixture);
        var stream = new TrackingMemoryStream([9, 8, 7]);
        fixture.ContentReader.EnqueueResult(
            new TradeScreenshotContent(item.Id, item.TradeId, item.FileName, stream));
        fixture.ImageDecoder.Exception =
            new NotSupportedException("Codec internals.");

        await fixture.ViewModel.OpenScreenshotPreviewCommand.ExecuteAsync(item);

        Assert.True(stream.IsDisposed);
        Assert.Equal(
            "Screenshot could not be opened.",
            fixture.ViewModel.ScreenshotPreviewErrorMessage);
        Assert.DoesNotContain(
            "Codec",
            fixture.ViewModel.ScreenshotPreviewErrorMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.Null(fixture.ViewModel.PreviewScreenshotImage);
        Assert.False(fixture.ViewModel.IsScreenshotPreviewVisible);
    }

    [Fact]
    public async Task CancellationPropagatesClearsLoadingAndLeavesNoPartialPreview()
    {
        PreviewFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await LoadDetailAsync(fixture);
        fixture.ContentReader.HoldRead = true;

        Task openTask =
            fixture.ViewModel.OpenScreenshotPreviewCommand.ExecuteAsync(item);
        await fixture.ContentReader.ReadStarted;
        Assert.True(fixture.ViewModel.IsScreenshotPreviewLoading);
        fixture.ViewModel.OpenScreenshotPreviewCommand.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await openTask);
        Assert.True(fixture.ContentReader.CancellationToken.IsCancellationRequested);
        Assert.False(fixture.ViewModel.IsScreenshotPreviewLoading);
        Assert.False(fixture.ViewModel.IsScreenshotPreviewVisible);
        Assert.Null(fixture.ViewModel.PreviewScreenshotId);
        Assert.Null(fixture.ViewModel.PreviewScreenshotFileName);
        Assert.Null(fixture.ViewModel.PreviewScreenshotImage);
        Assert.Null(fixture.ViewModel.ScreenshotPreviewErrorMessage);
    }

    [Fact]
    public async Task OpeningSecondScreenshotReplacesFirstPreview()
    {
        TradeScreenshotListItem first = CreateScreenshot(Guid.NewGuid(), "first.png");
        TradeScreenshotListItem second = CreateScreenshot(first.TradeId, "second.png");
        PreviewFixture fixture = CreateFixture([first, second]);
        await LoadDetailAsync(fixture, first.TradeId);
        var firstImage = new DrawingImage();
        fixture.ImageDecoder.ImageToReturn = firstImage;
        fixture.ContentReader.EnqueueResult(CreateContent(first));
        await fixture.ViewModel.OpenScreenshotPreviewCommand.ExecuteAsync(first);
        Assert.Same(firstImage, fixture.ViewModel.PreviewScreenshotImage);

        var secondImage = new DrawingImage();
        fixture.ImageDecoder.ImageToReturn = secondImage;
        fixture.ContentReader.EnqueueResult(CreateContent(second));
        fixture.ContentReader.HoldRead = true;
        Task secondOpenTask =
            fixture.ViewModel.OpenScreenshotPreviewCommand.ExecuteAsync(second);

        Assert.True(fixture.ViewModel.IsScreenshotPreviewLoading);
        Assert.False(fixture.ViewModel.IsScreenshotPreviewVisible);
        Assert.Null(fixture.ViewModel.PreviewScreenshotImage);

        fixture.ContentReader.ReleaseRead();
        await secondOpenTask;

        Assert.Equal(second.Id, fixture.ViewModel.PreviewScreenshotId);
        Assert.Equal("second.png", fixture.ViewModel.PreviewScreenshotFileName);
        Assert.Same(secondImage, fixture.ViewModel.PreviewScreenshotImage);
        Assert.NotSame(firstImage, fixture.ViewModel.PreviewScreenshotImage);
        Assert.Equal([first.Id, second.Id], fixture.ContentReader.RequestedScreenshotIds);
    }

    [Fact]
    public async Task ClosePreviewClearsPreviewOnly()
    {
        PreviewFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await OpenSuccessfulPreviewAsync(fixture);

        fixture.ViewModel.CloseScreenshotPreviewCommand.Execute(null);

        Assert.False(fixture.ViewModel.IsScreenshotPreviewVisible);
        Assert.False(fixture.ViewModel.IsScreenshotPreviewLoading);
        Assert.Null(fixture.ViewModel.PreviewScreenshotId);
        Assert.Null(fixture.ViewModel.PreviewScreenshotFileName);
        Assert.Null(fixture.ViewModel.PreviewScreenshotImage);
        Assert.Null(fixture.ViewModel.ScreenshotPreviewErrorMessage);
        Assert.Same(fixture.Detail, fixture.ViewModel.SelectedTradeDetail);
        Assert.Same(fixture.Screenshots, fixture.ViewModel.TradeScreenshots);
        Assert.Contains(item, fixture.ViewModel.TradeScreenshots);
    }

    [Fact]
    public async Task ClosingTradeDetailClearsPreviewState()
    {
        PreviewFixture fixture = CreateFixture();
        _ = await OpenSuccessfulPreviewAsync(fixture);

        fixture.ViewModel.CloseTradeDetailCommand.Execute(null);

        Assert.False(fixture.ViewModel.IsTradeDetailVisible);
        Assert.Null(fixture.ViewModel.PreviewScreenshotId);
        Assert.Null(fixture.ViewModel.PreviewScreenshotFileName);
        Assert.Null(fixture.ViewModel.PreviewScreenshotImage);
        Assert.False(fixture.ViewModel.IsScreenshotPreviewVisible);
        Assert.Null(fixture.ViewModel.ScreenshotPreviewErrorMessage);
    }

    [Fact]
    public async Task SwitchingTradeClearsPreviousPreview()
    {
        PreviewFixture fixture = CreateFixture();
        TradeScreenshotListItem first = await OpenSuccessfulPreviewAsync(fixture);
        TradeListItem secondTrade = CreateTradeListItem();
        while (secondTrade.Id == first.TradeId)
        {
            secondTrade = CreateTradeListItem();
        }

        fixture.DetailReader.EnqueueResult(CreateTradeDetail(secondTrade));
        fixture.ScreenshotReader.EnqueueResult([]);
        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(secondTrade);

        Assert.Equal(secondTrade.Id, fixture.ViewModel.SelectedTradeDetail?.Id);
        Assert.Null(fixture.ViewModel.PreviewScreenshotId);
        Assert.Null(fixture.ViewModel.PreviewScreenshotImage);
        Assert.False(fixture.ViewModel.IsScreenshotPreviewVisible);
    }

    [Fact]
    public async Task PreviewDoesNotMutateListOrManualAndAddDrafts()
    {
        PreviewFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await LoadDetailAsync(fixture);
        fixture.ViewModel.ShowManualEntryCommand.Execute(null);
        fixture.ViewModel.SelectedDirection = TradeDirection.Short;
        fixture.ViewModel.QuantityText = "2.5";
        fixture.ViewModel.EntryExecutedAtNewYorkText = "2026-09-13 13:30";
        fixture.ViewModel.ShowAddScreenshotCommand.Execute(null);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.Management;
        fixture.ViewModel.ScreenshotCapturedAtUtcText = "2026-09-13 13:31";
        fixture.ViewModel.ScreenshotTimeframeText = "5m";
        fixture.ViewModel.ScreenshotDescriptionText = "Draft notes";
        fixture.ContentReader.EnqueueResult(CreateContent(item));

        await fixture.ViewModel.OpenScreenshotPreviewCommand.ExecuteAsync(item);

        Assert.Same(fixture.Screenshots, fixture.ViewModel.TradeScreenshots);
        Assert.True(fixture.ViewModel.IsManualEntryVisible);
        Assert.Equal(TradeDirection.Short, fixture.ViewModel.SelectedDirection);
        Assert.Equal("2.5", fixture.ViewModel.QuantityText);
        Assert.Equal("2026-09-13 13:30", fixture.ViewModel.EntryExecutedAtNewYorkText);
        Assert.True(fixture.ViewModel.IsAddScreenshotVisible);
        Assert.Equal(TradeScreenshotType.Management, fixture.ViewModel.SelectedScreenshotType);
        Assert.Equal("2026-09-13 13:31", fixture.ViewModel.ScreenshotCapturedAtUtcText);
        Assert.Equal("5m", fixture.ViewModel.ScreenshotTimeframeText);
        Assert.Equal("Draft notes", fixture.ViewModel.ScreenshotDescriptionText);
    }

    [Fact]
    public async Task PreviewLoadingGatesAnotherOpen()
    {
        PreviewFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await LoadDetailAsync(fixture);
        fixture.ContentReader.HoldRead = true;

        Task openTask =
            fixture.ViewModel.OpenScreenshotPreviewCommand.ExecuteAsync(item);
        await fixture.ContentReader.ReadStarted;

        Assert.True(fixture.ViewModel.IsScreenshotPreviewLoading);
        Assert.False(
            fixture.ViewModel.OpenScreenshotPreviewCommand.CanExecute(item));

        fixture.ContentReader.ReleaseRead();
        await openTask;
    }

    [Fact]
    public async Task AddingScreenshotGatesNewPreviewButKeepsExistingPreviewVisible()
    {
        PreviewFixture fixture = CreateFixture();
        TradeScreenshotListItem item = await OpenSuccessfulPreviewAsync(fixture);

        fixture.ViewModel.ShowAddScreenshotCommand.Execute(null);

        Assert.True(fixture.ViewModel.IsAddScreenshotVisible);
        Assert.True(fixture.ViewModel.IsScreenshotPreviewVisible);
        Assert.Same(fixture.ImageDecoder.ImageToReturn,
            fixture.ViewModel.PreviewScreenshotImage);
        Assert.True(
            fixture.ViewModel.OpenScreenshotPreviewCommand.CanExecute(item));

        fixture.ScreenshotFilePicker.Selection =
            new TradeScreenshotFileSelection(
                "new.png",
                new MemoryStream([1, 2, 3]));
        fixture.ScreenshotStore.HoldAdd = true;
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.Entry;
        Task saveTask = fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);
        await fixture.ScreenshotStore.AddStarted;

        Assert.True(fixture.ViewModel.IsAddingScreenshot);
        Assert.False(
            fixture.ViewModel.OpenScreenshotPreviewCommand.CanExecute(item));
        Assert.True(fixture.ViewModel.IsScreenshotPreviewVisible);

        fixture.ScreenshotStore.ReleaseAdd();
        await saveTask;
    }

    private static async Task<TradeScreenshotListItem> OpenSuccessfulPreviewAsync(
        PreviewFixture fixture)
    {
        TradeScreenshotListItem item = await LoadDetailAsync(fixture);
        fixture.ContentReader.EnqueueResult(CreateContent(item));
        await fixture.ViewModel.OpenScreenshotPreviewCommand.ExecuteAsync(item);
        return item;
    }

    private static async Task<TradeScreenshotListItem> LoadDetailAsync(
        PreviewFixture fixture,
        Guid? tradeId = null)
    {
        TradeListItem trade = CreateTradeListItem(tradeId);
        TradeDetail detail = CreateTradeDetail(trade);
        IReadOnlyList<TradeScreenshotListItem> screenshots =
            tradeId.HasValue
                ? fixture.Screenshots
                : [CreateScreenshot(trade.Id)];
        fixture.Detail = detail;
        fixture.Screenshots = screenshots;
        fixture.DetailReader.EnqueueResult(detail);
        fixture.ScreenshotReader.EnqueueResult(screenshots);
        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(trade);
        return screenshots[0];
    }

    private static PreviewFixture CreateFixture(
        IReadOnlyList<TradeScreenshotListItem>? screenshots = null)
    {
        var detailReader = new FakeTradeDetailReader();
        var screenshotReader = new FakeTradeScreenshotReader();
        var contentReader = new FakeTradeScreenshotContentReader();
        var imageDecoder = new FakeTradeScreenshotImageDecoder();
        var screenshotFilePicker = new FakeTradeScreenshotFilePicker();
        var screenshotFileStorage = new FakeTradeScreenshotFileStorage();
        var screenshotStore = new FakeTradeScreenshotStore();
        var timeProvider = new FixedTimeProvider();
        var accountStore = new FakeTradingAccountStore();
        var instrumentStore = new FakeInstrumentStore();
        var tradeStore = new FakeTradeStore();

        var viewModel = new TradesViewModel(
            new FakeManualTradeReferenceDataReader(),
            new FakeTradingSetupReader(),
            new FakeTradingMistakeReader(),
            new FakeTradeMistakeReader(),
            new FakeTradeListReader(),
            detailReader,
            new CreateManualTradeUseCase(
                accountStore,
                instrumentStore,
                new FakeTradingSetupStore(),
                tradeStore,
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
                screenshotFileStorage,
                screenshotStore,
                timeProvider),
            screenshotFilePicker,
            contentReader,
            imageDecoder,
            new DeleteTradeScreenshotUseCase(
                new FakeTradeScreenshotDeletionStore(),
                screenshotFileStorage),
            new FakeTradeScreenshotDeleteConfirmation());

        return new PreviewFixture(
            viewModel,
            detailReader,
            screenshotReader,
            contentReader,
            imageDecoder,
            screenshotFilePicker,
            screenshotStore)
        {
            Screenshots = screenshots ?? [],
        };
    }

    private static TradeScreenshotContent CreateContent(
        TradeScreenshotListItem item) =>
        new(
            item.Id,
            item.TradeId,
            item.FileName,
            new TrackingMemoryStream([1, 2, 3]));

    private static TradeScreenshotListItem CreateScreenshot(
        Guid tradeId,
        string fileName = "chart.png") =>
        new(
            Guid.NewGuid(),
            tradeId,
            TradeScreenshotType.Entry,
            fileName,
            null,
            "5m",
            "Entry context",
            new DateTimeOffset(2026, 9, 13, 14, 0, 0, TimeSpan.Zero));

    private static TradeListItem CreateTradeListItem(Guid? id = null)
    {
        DateTimeOffset openedAtUtc =
            new(2026, 9, 13, 13, 30, 0, TimeSpan.Zero);

        return new TradeListItem(
            id ?? Guid.NewGuid(),
            Guid.NewGuid(),
            "Primary Account",
            Guid.NewGuid(),
            "NQ",
            TradeDirection.Long,
            TradeStatus.Closed,
            openedAtUtc,
            openedAtUtc.AddMinutes(30),
            0m,
            23950m,
            23975m,
            3m,
            1250m,
            1247m,
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

    private sealed class PreviewFixture(
        TradesViewModel viewModel,
        FakeTradeDetailReader detailReader,
        FakeTradeScreenshotReader screenshotReader,
        FakeTradeScreenshotContentReader contentReader,
        FakeTradeScreenshotImageDecoder imageDecoder,
        FakeTradeScreenshotFilePicker screenshotFilePicker,
        FakeTradeScreenshotStore screenshotStore)
    {
        public TradesViewModel ViewModel { get; } = viewModel;

        public FakeTradeDetailReader DetailReader { get; } = detailReader;

        public FakeTradeScreenshotReader ScreenshotReader { get; } = screenshotReader;

        public FakeTradeScreenshotContentReader ContentReader { get; } = contentReader;

        public FakeTradeScreenshotImageDecoder ImageDecoder { get; } = imageDecoder;

        public FakeTradeScreenshotFilePicker ScreenshotFilePicker { get; } =
            screenshotFilePicker;

        public FakeTradeScreenshotStore ScreenshotStore { get; } = screenshotStore;

        public TradeDetail? Detail { get; set; }

        public IReadOnlyList<TradeScreenshotListItem> Screenshots { get; set; } = [];
    }

    private sealed class TrackingMemoryStream(byte[] buffer) : MemoryStream(buffer)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            IsDisposed = true;
            await base.DisposeAsync();
        }
    }
}
