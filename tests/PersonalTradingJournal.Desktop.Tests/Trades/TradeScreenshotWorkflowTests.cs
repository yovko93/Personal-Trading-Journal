using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed class TradeScreenshotWorkflowTests
{
    [Fact]
    public async Task DetailLoadReadsExactAuthoritativeScreenshotsForSelectedTrade()
    {
        ScreenshotFixture fixture = CreateFixture();
        TradeListItem item = CreateTradeListItem();
        TradeDetail detail = CreateTradeDetail(item);
        IReadOnlyList<TradeScreenshotListItem> screenshots =
            [CreateScreenshot(item.Id)];
        fixture.DetailReader.EnqueueResult(detail);
        fixture.ScreenshotReader.EnqueueResult(screenshots);

        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(item);

        Assert.Same(detail, fixture.ViewModel.SelectedTradeDetail);
        Assert.Equal([item.Id], fixture.ScreenshotReader.RequestedTradeIds);
        Assert.Same(screenshots, fixture.ViewModel.TradeScreenshots);
        Assert.True(fixture.ViewModel.HasTradeScreenshots);
        Assert.False(fixture.ViewModel.IsTradeScreenshotsLoading);
    }

    [Fact]
    public async Task ScreenshotReadFailureKeepsSuccessfulDetailUsable()
    {
        ScreenshotFixture fixture = CreateFixture();
        TradeListItem item = CreateTradeListItem();
        TradeDetail detail = CreateTradeDetail(item);
        fixture.DetailReader.EnqueueResult(detail);
        fixture.ScreenshotReader.EnqueueException(
            new InvalidOperationException("Database path details."));

        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(item);

        Assert.Same(detail, fixture.ViewModel.SelectedTradeDetail);
        Assert.True(fixture.ViewModel.IsTradeDetailVisible);
        Assert.Null(fixture.ViewModel.TradeDetailErrorMessage);
        Assert.False(fixture.ViewModel.IsTradeDetailNotFound);
        Assert.Empty(fixture.ViewModel.TradeScreenshots);
        Assert.Equal(
            "Screenshots could not be loaded.",
            fixture.ViewModel.TradeScreenshotsErrorMessage);
        Assert.DoesNotContain(
            "path",
            fixture.ViewModel.TradeScreenshotsErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SwitchingTradeClearsStaleScreenshotStateBeforeNextDetailLoads()
    {
        ScreenshotFixture fixture = CreateFixture();
        TradeListItem first = CreateTradeListItem(symbol: "NQ");
        TradeListItem second = CreateTradeListItem(symbol: "ES");
        fixture.DetailReader.EnqueueResult(CreateTradeDetail(first));
        fixture.ScreenshotReader.EnqueueResult([CreateScreenshot(first.Id)]);
        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(first);
        fixture.ViewModel.ShowAddScreenshotCommand.Execute(null);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.Entry;
        fixture.ViewModel.ScreenshotDescriptionText = "Stale draft";

        var secondReadStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecondRead = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.DetailReader.EnqueueBehavior(async cancellationToken =>
        {
            secondReadStarted.TrySetResult(true);
            _ = await releaseSecondRead.Task.WaitAsync(cancellationToken);
            return CreateTradeDetail(second);
        });

        Task switchTask =
            fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(second);
        await secondReadStarted.Task;

        Assert.Empty(fixture.ViewModel.TradeScreenshots);
        Assert.Null(fixture.ViewModel.TradeScreenshotsErrorMessage);
        Assert.False(fixture.ViewModel.IsAddScreenshotVisible);
        Assert.Null(fixture.ViewModel.SelectedScreenshotType);
        Assert.Equal(string.Empty, fixture.ViewModel.ScreenshotDescriptionText);

        releaseSecondRead.TrySetResult(true);
        await switchTask;
        Assert.Equal([first.Id, second.Id], fixture.ScreenshotReader.RequestedTradeIds);
    }

    [Fact]
    public async Task MissingDetailDoesNotReadScreenshotsAndClearsPriorState()
    {
        ScreenshotFixture fixture = CreateFixture();
        TradeListItem first = CreateTradeListItem();
        fixture.DetailReader.EnqueueResult(CreateTradeDetail(first));
        fixture.ScreenshotReader.EnqueueResult([CreateScreenshot(first.Id)]);
        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(first);
        TradeListItem missing = CreateTradeListItem();
        fixture.DetailReader.EnqueueResult(null);

        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(missing);

        Assert.Equal(1, fixture.ScreenshotReader.CallCount);
        Assert.Empty(fixture.ViewModel.TradeScreenshots);
        Assert.True(fixture.ViewModel.IsTradeDetailNotFound);
        Assert.Null(fixture.ViewModel.SelectedTradeDetail);
    }

    [Fact]
    public async Task CloseDetailClearsAllScreenshotState()
    {
        ScreenshotFixture fixture = CreateFixture();
        TradeListItem item = CreateTradeListItem();
        fixture.DetailReader.EnqueueResult(CreateTradeDetail(item));
        fixture.ScreenshotReader.EnqueueResult([CreateScreenshot(item.Id)]);
        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(item);
        fixture.ViewModel.ShowAddScreenshotCommand.Execute(null);
        await fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);
        Assert.True(fixture.ViewModel.HasScreenshotValidationError);

        fixture.ViewModel.CloseTradeDetailCommand.Execute(null);

        Assert.Empty(fixture.ViewModel.TradeScreenshots);
        Assert.Null(fixture.ViewModel.TradeScreenshotsErrorMessage);
        Assert.False(fixture.ViewModel.IsAddScreenshotVisible);
        Assert.Null(fixture.ViewModel.SelectedScreenshotType);
        Assert.Null(fixture.ViewModel.ScreenshotValidationErrorMessage);
        Assert.Null(fixture.ViewModel.ScreenshotSaveErrorMessage);
        Assert.Null(fixture.ViewModel.ScreenshotSuccessMessage);
    }

    [Fact]
    public async Task RetainedViewModelDoesNotRereadLoadedScreenshots()
    {
        ScreenshotFixture fixture = CreateFixture();
        TradeListItem item = CreateTradeListItem();
        fixture.DetailReader.EnqueueResult(CreateTradeDetail(item));
        fixture.ScreenshotReader.EnqueueResult([CreateScreenshot(item.Id)]);
        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(item);

        await fixture.ViewModel.EnsureLoadedAsync();
        await fixture.ViewModel.EnsureLoadedAsync();

        Assert.Equal(1, fixture.ScreenshotReader.CallCount);
        Assert.True(fixture.ViewModel.HasTradeScreenshots);
    }

    [Fact]
    public async Task RefreshReloadsVisibleTradeScreenshotsAuthoritatively()
    {
        ScreenshotFixture fixture = CreateFixture();
        TradeListItem item = CreateTradeListItem();
        IReadOnlyList<TradeScreenshotListItem> first =
            [CreateScreenshot(item.Id)];
        IReadOnlyList<TradeScreenshotListItem> refreshed =
            [CreateScreenshot(item.Id), CreateScreenshot(item.Id)];
        fixture.DetailReader.EnqueueResult(CreateTradeDetail(item));
        fixture.ScreenshotReader.EnqueueResult(first);
        fixture.ScreenshotReader.EnqueueResult(refreshed);
        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(item);

        await fixture.ViewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal([item.Id, item.Id], fixture.ScreenshotReader.RequestedTradeIds);
        Assert.Same(refreshed, fixture.ViewModel.TradeScreenshots);
        Assert.Null(fixture.ViewModel.TradeScreenshotsErrorMessage);
    }

    [Fact]
    public async Task RefreshScreenshotFailureRetainsPriorListAndReportsSafeError()
    {
        ScreenshotFixture fixture = CreateFixture();
        TradeListItem item = CreateTradeListItem();
        IReadOnlyList<TradeScreenshotListItem> initial =
            [CreateScreenshot(item.Id)];
        fixture.DetailReader.EnqueueResult(CreateTradeDetail(item));
        fixture.ScreenshotReader.EnqueueResult(initial);
        fixture.ScreenshotReader.EnqueueException(new IOException("Secret path"));
        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(item);

        await fixture.ViewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Same(initial, fixture.ViewModel.TradeScreenshots);
        Assert.Equal(
            "Screenshots could not be loaded.",
            fixture.ViewModel.TradeScreenshotsErrorMessage);
        Assert.Null(fixture.ViewModel.ErrorMessage);
        Assert.Null(fixture.ViewModel.TradeListErrorMessage);
    }

    [Fact]
    public async Task AddScreenshotRequiresAuthoritativeDetailAndHasNoDefaultType()
    {
        ScreenshotFixture fixture = CreateFixture();

        Assert.False(fixture.ViewModel.ShowAddScreenshotCommand.CanExecute(null));
        Assert.Null(fixture.ViewModel.SelectedScreenshotType);
        Assert.Equal(
            [
                TradeScreenshotType.PreTrade,
                TradeScreenshotType.Entry,
                TradeScreenshotType.Management,
                TradeScreenshotType.Exit,
                TradeScreenshotType.PostTrade,
                TradeScreenshotType.Other,
            ],
            fixture.ViewModel.ScreenshotTypeOptions);

        await LoadDetailAsync(fixture);

        Assert.True(fixture.ViewModel.ShowAddScreenshotCommand.CanExecute(null));
        fixture.ViewModel.ShowAddScreenshotCommand.Execute(null);
        Assert.True(fixture.ViewModel.IsAddScreenshotVisible);
        Assert.Null(fixture.ViewModel.SelectedScreenshotType);
    }

    [Fact]
    public async Task CancelClearsDraftAndFeedbackButPreservesDetailAndList()
    {
        ScreenshotFixture fixture = CreateFixture();
        TradeListItem item = CreateTradeListItem();
        TradeDetail detail = CreateTradeDetail(item);
        IReadOnlyList<TradeScreenshotListItem> screenshots =
            [CreateScreenshot(item.Id)];
        fixture.DetailReader.EnqueueResult(detail);
        fixture.ScreenshotReader.EnqueueResult(screenshots);
        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(item);
        fixture.ViewModel.ShowAddScreenshotCommand.Execute(null);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.Management;
        fixture.ViewModel.ScreenshotCapturedAtUtcText = "invalid";
        fixture.ViewModel.ScreenshotTimeframeText = "5m";
        fixture.ViewModel.ScreenshotDescriptionText = "Draft";
        await fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);

        fixture.ViewModel.CancelAddScreenshotCommand.Execute(null);

        Assert.False(fixture.ViewModel.IsAddScreenshotVisible);
        Assert.Null(fixture.ViewModel.SelectedScreenshotType);
        Assert.Equal(string.Empty, fixture.ViewModel.ScreenshotCapturedAtUtcText);
        Assert.Equal(string.Empty, fixture.ViewModel.ScreenshotTimeframeText);
        Assert.Equal(string.Empty, fixture.ViewModel.ScreenshotDescriptionText);
        Assert.Null(fixture.ViewModel.ScreenshotValidationErrorMessage);
        Assert.Null(fixture.ViewModel.ScreenshotSaveErrorMessage);
        Assert.Same(detail, fixture.ViewModel.SelectedTradeDetail);
        Assert.Same(screenshots, fixture.ViewModel.TradeScreenshots);
    }

    [Fact]
    public async Task MissingTypeBlocksPickerAndUseCase()
    {
        ScreenshotFixture fixture = CreateFixture();
        await LoadDetailAndOpenFormAsync(fixture);

        await fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);

        Assert.Equal(
            "Screenshot type is required.",
            fixture.ViewModel.ScreenshotValidationErrorMessage);
        Assert.Equal(0, fixture.FilePicker.CallCount);
        Assert.Equal(0, fixture.ScreenshotStore.AddCallCount);
    }

    [Fact]
    public async Task BlankCapturedTimeFlowsAsNull()
    {
        ScreenshotFixture fixture = CreateFixtureWithSelection();
        await LoadDetailAndOpenFormAsync(fixture);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.Entry;
        fixture.ViewModel.ScreenshotCapturedAtUtcText = "   ";

        await fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);

        Assert.Null(Assert.IsType<TradeScreenshot>(
            fixture.ScreenshotStore.AddedScreenshot).CapturedAtUtc);
    }

    [Theory]
    [InlineData("2026-09-10 13:30:00")]
    [InlineData("2026-09-10 13:30")]
    [InlineData("2026-09-10T13:30:00Z")]
    [InlineData("2026-09-10T13:30Z")]
    public async Task StrictUtcCapturedTimeFormatsParse(string text)
    {
        ScreenshotFixture fixture = CreateFixtureWithSelection();
        await LoadDetailAndOpenFormAsync(fixture);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.Exit;
        fixture.ViewModel.ScreenshotCapturedAtUtcText = text;

        await fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);

        Assert.Equal(
            new DateTimeOffset(2026, 9, 10, 13, 30, 0, TimeSpan.Zero),
            Assert.IsType<TradeScreenshot>(
                fixture.ScreenshotStore.AddedScreenshot).CapturedAtUtc);
    }

    [Fact]
    public async Task InvalidCapturedTimeBlocksPickerAndRetainsDraft()
    {
        ScreenshotFixture fixture = CreateFixtureWithSelection();
        await LoadDetailAndOpenFormAsync(fixture);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.PreTrade;
        fixture.ViewModel.ScreenshotCapturedAtUtcText =
            "2026-09-10T13:30:00+03:00";

        await fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);

        Assert.Equal(0, fixture.FilePicker.CallCount);
        Assert.Equal(0, fixture.ScreenshotStore.AddCallCount);
        Assert.True(fixture.ViewModel.IsAddScreenshotVisible);
        Assert.Equal(
            "2026-09-10T13:30:00+03:00",
            fixture.ViewModel.ScreenshotCapturedAtUtcText);
        Assert.Equal(
            "Captured time must be a valid UTC timestamp.",
            fixture.ViewModel.ScreenshotValidationErrorMessage);
    }

    [Fact]
    public async Task PickerCancelDoesNotSaveOrChangeDraft()
    {
        ScreenshotFixture fixture = CreateFixture();
        await LoadDetailAndOpenFormAsync(fixture);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.Other;
        fixture.ViewModel.ScreenshotTimeframeText = "Daily";
        fixture.ViewModel.ScreenshotDescriptionText = "Keep this";

        await fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.FilePicker.CallCount);
        Assert.Equal(0, fixture.ScreenshotStore.AddCallCount);
        Assert.True(fixture.ViewModel.IsAddScreenshotVisible);
        Assert.Equal(TradeScreenshotType.Other, fixture.ViewModel.SelectedScreenshotType);
        Assert.Equal("Daily", fixture.ViewModel.ScreenshotTimeframeText);
        Assert.Equal("Keep this", fixture.ViewModel.ScreenshotDescriptionText);
        Assert.Null(fixture.ViewModel.ScreenshotSaveErrorMessage);
        Assert.Null(fixture.ViewModel.ScreenshotSuccessMessage);
    }

    [Fact]
    public async Task SuccessfulAddUsesSelectedFactsDisposesStreamAndReloadsAuthoritatively()
    {
        var stream = new TrackingMemoryStream([1, 2, 3]);
        ScreenshotFixture fixture = CreateFixtureWithSelection(
            stream,
            "entry-chart.JPEG");
        TradeListItem item = CreateTradeListItem();
        TradeDetail detail = CreateTradeDetail(item);
        IReadOnlyList<TradeScreenshotListItem> authoritative =
            [CreateScreenshot(item.Id, fileName: "authoritative.png")];
        fixture.DetailReader.EnqueueResult(detail);
        fixture.ScreenshotReader.EnqueueResult([]);
        fixture.ScreenshotReader.EnqueueResult(authoritative);
        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(item);
        fixture.ViewModel.ShowAddScreenshotCommand.Execute(null);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.Entry;
        fixture.ViewModel.ScreenshotCapturedAtUtcText = "2026-09-10 13:30";
        fixture.ViewModel.ScreenshotTimeframeText = "  5m  ";
        fixture.ViewModel.ScreenshotDescriptionText = "  Breakout  ";

        await fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);

        TradeScreenshot saved = Assert.IsType<TradeScreenshot>(
            fixture.ScreenshotStore.AddedScreenshot);
        Assert.Equal(item.Id, saved.TradeId);
        Assert.Equal(TradeScreenshotType.Entry, saved.Type);
        Assert.Equal("entry-chart.JPEG", saved.FileName);
        Assert.Equal("5m", saved.Timeframe);
        Assert.Equal("Breakout", saved.Description);
        Assert.Same(stream, fixture.FileStorage.StoredContent);
        Assert.Equal(".JPEG", fixture.FileStorage.StoredExtension);
        Assert.True(stream.IsDisposed);
        Assert.Same(authoritative, fixture.ViewModel.TradeScreenshots);
        Assert.Single(fixture.ViewModel.TradeScreenshots);
        Assert.Equal("authoritative.png", fixture.ViewModel.TradeScreenshots[0].FileName);
        Assert.False(fixture.ViewModel.IsAddScreenshotVisible);
        Assert.Equal(
            "Screenshot added successfully.",
            fixture.ViewModel.ScreenshotSuccessMessage);
        Assert.Null(fixture.ViewModel.SuccessMessage);
        Assert.Null(fixture.ViewModel.ScreenshotSaveErrorMessage);
    }

    [Fact]
    public async Task SuccessfulWriteWithReloadFailureKeepsOldListAndSuccess()
    {
        ScreenshotFixture fixture = CreateFixtureWithSelection();
        TradeListItem item = CreateTradeListItem();
        IReadOnlyList<TradeScreenshotListItem> prior =
            [CreateScreenshot(item.Id, fileName: "prior.png")];
        fixture.DetailReader.EnqueueResult(CreateTradeDetail(item));
        fixture.ScreenshotReader.EnqueueResult(prior);
        fixture.ScreenshotReader.EnqueueException(new IOException("Database down"));
        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(item);
        fixture.ViewModel.ShowAddScreenshotCommand.Execute(null);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.PostTrade;

        await fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.ScreenshotStore.AddCallCount);
        Assert.Same(prior, fixture.ViewModel.TradeScreenshots);
        Assert.Equal(
            "Screenshot added successfully.",
            fixture.ViewModel.ScreenshotSuccessMessage);
        Assert.Equal(
            "Screenshots could not be loaded.",
            fixture.ViewModel.TradeScreenshotsErrorMessage);
        Assert.Null(fixture.ViewModel.ScreenshotSaveErrorMessage);
    }

    [Fact]
    public async Task SaveFailureDisposesStreamRetainsDraftAndUsesSafeMessage()
    {
        var stream = new TrackingMemoryStream([1, 2, 3]);
        ScreenshotFixture fixture = CreateFixtureWithSelection(stream);
        fixture.FileStorage.StoreException =
            new IOException("C:\\secret\\screenshot.png");
        await LoadDetailAndOpenFormAsync(fixture);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.Management;
        fixture.ViewModel.ScreenshotTimeframeText = "15m";
        fixture.ViewModel.ScreenshotDescriptionText = "Draft context";

        await fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);

        Assert.True(stream.IsDisposed);
        Assert.True(fixture.ViewModel.IsAddScreenshotVisible);
        Assert.Equal(TradeScreenshotType.Management, fixture.ViewModel.SelectedScreenshotType);
        Assert.Equal("15m", fixture.ViewModel.ScreenshotTimeframeText);
        Assert.Equal("Draft context", fixture.ViewModel.ScreenshotDescriptionText);
        Assert.Equal(
            "Screenshot could not be added.",
            fixture.ViewModel.ScreenshotSaveErrorMessage);
        Assert.Null(fixture.ViewModel.ScreenshotSuccessMessage);
        Assert.Empty(fixture.ViewModel.TradeScreenshots);
    }

    [Fact]
    public async Task StaleTradeFailureUsesSafeMessageAndRetainsDraft()
    {
        ScreenshotFixture fixture = CreateFixtureWithSelection();
        fixture.TradeExistenceReader.Exists = false;
        await LoadDetailAndOpenFormAsync(fixture);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.Entry;
        fixture.ViewModel.ScreenshotDescriptionText = "Still useful";

        await fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);

        Assert.Equal(
            "The selected trade is no longer available.",
            fixture.ViewModel.ScreenshotSaveErrorMessage);
        Assert.True(fixture.ViewModel.IsAddScreenshotVisible);
        Assert.Equal("Still useful", fixture.ViewModel.ScreenshotDescriptionText);
        Assert.Equal(0, fixture.ScreenshotStore.AddCallCount);
    }

    [Fact]
    public async Task CancellationPropagatesWithoutFailureOrSuccessAndDisposesStream()
    {
        var stream = new TrackingMemoryStream([1, 2, 3]);
        ScreenshotFixture fixture = CreateFixtureWithSelection(stream);
        fixture.ScreenshotStore.HoldAdd = true;
        await LoadDetailAndOpenFormAsync(fixture);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.Exit;

        Task saveTask = fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);
        await fixture.ScreenshotStore.AddStarted;
        fixture.ViewModel.SaveScreenshotCommand.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await saveTask);
        Assert.True(stream.IsDisposed);
        Assert.True(fixture.ViewModel.IsAddScreenshotVisible);
        Assert.Equal(TradeScreenshotType.Exit, fixture.ViewModel.SelectedScreenshotType);
        Assert.Null(fixture.ViewModel.ScreenshotSaveErrorMessage);
        Assert.Null(fixture.ViewModel.ScreenshotSuccessMessage);
    }

    [Fact]
    public async Task AddingScreenshotGatesAllCompetingCommands()
    {
        ScreenshotFixture fixture = CreateFixtureWithSelection();
        fixture.ScreenshotStore.HoldAdd = true;
        TradeListItem current = await LoadDetailAndOpenFormAsync(fixture);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.PreTrade;
        fixture.ViewModel.ShowManualEntryCommand.Execute(null);
        Assert.True(fixture.ViewModel.SaveManualTradeCommand.CanExecute(null));

        Task saveTask = fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);
        await fixture.ScreenshotStore.AddStarted;

        Assert.True(fixture.ViewModel.IsAddingScreenshot);
        Assert.False(fixture.ViewModel.SaveScreenshotCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.CancelAddScreenshotCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.ShowAddScreenshotCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.ShowTradeDetailCommand.CanExecute(current));
        Assert.False(fixture.ViewModel.CloseTradeDetailCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.RefreshCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.SaveManualTradeCommand.CanExecute(null));

        fixture.ScreenshotStore.ReleaseAdd();
        await saveTask;
        Assert.False(fixture.ViewModel.IsAddingScreenshot);
    }

    [Fact]
    public async Task ScreenshotWorkflowDoesNotMutateManualDraftOrRecentTrades()
    {
        ScreenshotFixture fixture = CreateFixtureWithSelection();
        TradeListItem item = CreateTradeListItem();
        IReadOnlyList<TradeListItem> recentTrades = [item];
        fixture.TradeListReader.EnqueueResult(recentTrades);
        await fixture.ViewModel.EnsureLoadedAsync();
        fixture.DetailReader.EnqueueResult(CreateTradeDetail(item));
        fixture.ScreenshotReader.EnqueueResult([]);
        fixture.ScreenshotReader.EnqueueResult([CreateScreenshot(item.Id)]);
        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(item);
        fixture.ViewModel.ShowManualEntryCommand.Execute(null);
        fixture.ViewModel.SelectedDirection = TradeDirection.Short;
        fixture.ViewModel.QuantityText = "2.5";
        fixture.ViewModel.EntryExecutedAtUtcText = "2026-09-10 13:30";
        fixture.ViewModel.EntryPriceText = "23950.25";
        fixture.ViewModel.ShowAddScreenshotCommand.Execute(null);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.Other;

        await fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);

        Assert.True(fixture.ViewModel.IsManualEntryVisible);
        Assert.Equal(TradeDirection.Short, fixture.ViewModel.SelectedDirection);
        Assert.Equal("2.5", fixture.ViewModel.QuantityText);
        Assert.Equal("2026-09-10 13:30", fixture.ViewModel.EntryExecutedAtUtcText);
        Assert.Equal("23950.25", fixture.ViewModel.EntryPriceText);
        Assert.Same(recentTrades, fixture.ViewModel.RecentTrades);
        Assert.Equal(1, fixture.TradeListReader.CallCount);
        Assert.Null(fixture.ViewModel.SuccessMessage);
        Assert.Equal(
            "Screenshot added successfully.",
            fixture.ViewModel.ScreenshotSuccessMessage);
    }

    [Fact]
    public async Task OpeningAnotherAddFormResetsDraftAndPriorScreenshotFeedback()
    {
        ScreenshotFixture fixture = CreateFixtureWithSelection();
        await LoadDetailAndOpenFormAsync(fixture);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.Entry;
        fixture.ViewModel.ScreenshotDescriptionText = "First";
        await fixture.ViewModel.SaveScreenshotCommand.ExecuteAsync(null);
        Assert.True(fixture.ViewModel.HasScreenshotSuccessMessage);
        fixture.ViewModel.SelectedScreenshotType = TradeScreenshotType.Other;
        fixture.ViewModel.ScreenshotCapturedAtUtcText = "stale";
        fixture.ViewModel.ScreenshotTimeframeText = "1h";
        fixture.ViewModel.ScreenshotDescriptionText = "stale";

        fixture.ViewModel.ShowAddScreenshotCommand.Execute(null);

        Assert.Null(fixture.ViewModel.SelectedScreenshotType);
        Assert.Equal(string.Empty, fixture.ViewModel.ScreenshotCapturedAtUtcText);
        Assert.Equal(string.Empty, fixture.ViewModel.ScreenshotTimeframeText);
        Assert.Equal(string.Empty, fixture.ViewModel.ScreenshotDescriptionText);
        Assert.Null(fixture.ViewModel.ScreenshotValidationErrorMessage);
        Assert.Null(fixture.ViewModel.ScreenshotSaveErrorMessage);
        Assert.Null(fixture.ViewModel.ScreenshotSuccessMessage);
    }

    private static async Task<TradeListItem> LoadDetailAsync(
        ScreenshotFixture fixture)
    {
        TradeListItem item = CreateTradeListItem();
        fixture.DetailReader.EnqueueResult(CreateTradeDetail(item));
        fixture.ScreenshotReader.EnqueueResult([]);
        await fixture.ViewModel.ShowTradeDetailCommand.ExecuteAsync(item);
        return item;
    }

    private static async Task<TradeListItem> LoadDetailAndOpenFormAsync(
        ScreenshotFixture fixture)
    {
        TradeListItem item = await LoadDetailAsync(fixture);
        fixture.ViewModel.ShowAddScreenshotCommand.Execute(null);
        return item;
    }

    private static ScreenshotFixture CreateFixtureWithSelection(
        TrackingMemoryStream? stream = null,
        string fileName = "chart.png")
    {
        ScreenshotFixture fixture = CreateFixture();
        fixture.FilePicker.Selection = new TradeScreenshotFileSelection(
            fileName,
            stream ?? new TrackingMemoryStream([1, 2, 3]));
        return fixture;
    }

    private static ScreenshotFixture CreateFixture()
    {
        var referenceDataReader = new FakeManualTradeReferenceDataReader();
        var tradeListReader = new FakeTradeListReader();
        var detailReader = new FakeTradeDetailReader();
        var screenshotReader = new FakeTradeScreenshotReader();
        var filePicker = new FakeTradeScreenshotFilePicker();
        var tradeExistenceReader = new FakeTradeExistenceReader();
        var fileStorage = new FakeTradeScreenshotFileStorage();
        var screenshotStore = new FakeTradeScreenshotStore();
        var timeProvider = new FixedTimeProvider();
        var accountStore = new FakeTradingAccountStore();
        var instrumentStore = new FakeInstrumentStore();
        var tradeStore = new FakeTradeStore();

        var viewModel = new TradesViewModel(
            referenceDataReader,
            tradeListReader,
            detailReader,
            new CreateManualTradeUseCase(
                accountStore,
                instrumentStore,
                tradeStore,
                timeProvider),
            screenshotReader,
            new AddTradeScreenshotUseCase(
                tradeExistenceReader,
                fileStorage,
                screenshotStore,
                timeProvider),
            filePicker);

        return new ScreenshotFixture(
            viewModel,
            tradeListReader,
            detailReader,
            screenshotReader,
            filePicker,
            tradeExistenceReader,
            fileStorage,
            screenshotStore);
    }

    private static TradeScreenshotListItem CreateScreenshot(
        Guid tradeId,
        string fileName = "chart.png")
    {
        return new TradeScreenshotListItem(
            Guid.NewGuid(),
            tradeId,
            TradeScreenshotType.Entry,
            fileName,
            new DateTimeOffset(2026, 9, 10, 13, 30, 0, TimeSpan.Zero),
            "5m",
            "Entry context",
            new DateTimeOffset(2026, 9, 10, 14, 0, 0, TimeSpan.Zero));
    }

    private static TradeListItem CreateTradeListItem(string symbol = "NQ")
    {
        DateTimeOffset openedAtUtc =
            new(2026, 9, 10, 13, 30, 0, TimeSpan.Zero);

        return new TradeListItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Primary Account",
            Guid.NewGuid(),
            symbol,
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

    private static TradeDetail CreateTradeDetail(TradeListItem item)
    {
        return new TradeDetail(
            item.Id,
            item.TradingAccountId,
            item.TradingAccountName,
            item.InstrumentId,
            item.InstrumentSymbol,
            $"{item.InstrumentSymbol} display name",
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
    }

    private sealed record ScreenshotFixture(
        TradesViewModel ViewModel,
        FakeTradeListReader TradeListReader,
        FakeTradeDetailReader DetailReader,
        FakeTradeScreenshotReader ScreenshotReader,
        FakeTradeScreenshotFilePicker FilePicker,
        FakeTradeExistenceReader TradeExistenceReader,
        FakeTradeScreenshotFileStorage FileStorage,
        FakeTradeScreenshotStore ScreenshotStore);

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
