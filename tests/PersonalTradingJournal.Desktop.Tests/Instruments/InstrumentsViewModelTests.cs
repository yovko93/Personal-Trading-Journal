using System.Globalization;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Instruments;
using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Desktop.Tests.Instruments;

[CollectionDefinition(CultureSensitiveCollection.Name, DisableParallelization = true)]
public sealed class CultureSensitiveCollection
{
    public const string Name = "Culture-sensitive Desktop tests";
}

[Collection(CultureSensitiveCollection.Name)]
public sealed class InstrumentsViewModelTests
{
    [Fact]
    public async Task EnsureLoadedAsync_AfterSuccessfulLoad_DoesNotQueryReaderAgain()
    {
        var instruments = new[] { CreateListItem(isActive: true) };
        var reader = new FakeInstrumentReader();
        reader.EnqueueResult(instruments);
        InstrumentsViewModel viewModel = CreateViewModel(reader);

        await viewModel.EnsureLoadedAsync();
        await viewModel.EnsureLoadedAsync();

        Assert.Same(instruments, viewModel.Instruments);
        Assert.True(viewModel.HasInstruments);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task RefreshAsync_WhenReaderFails_RetainsExistingRowsAndShowsListError()
    {
        var instruments = new[] { CreateListItem(isActive: true) };
        var reader = new FakeInstrumentReader();
        reader.EnqueueResult(instruments);
        reader.EnqueueException(new InvalidOperationException("Read failed."));
        InstrumentsViewModel viewModel = CreateViewModel(reader);
        await viewModel.EnsureLoadedAsync();

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Same(instruments, viewModel.Instruments);
        Assert.Equal("Instruments could not be loaded.", viewModel.ErrorMessage);
        Assert.True(viewModel.HasError);
    }

    [Fact]
    public async Task RefreshAsync_AfterFailure_ClearsErrorAndReplacesRows()
    {
        var initialInstruments = new[] { CreateListItem(isActive: true) };
        var refreshedInstruments = new[]
        {
            CreateListItem(isActive: true),
            CreateListItem(isActive: false),
        };
        var reader = new FakeInstrumentReader();
        reader.EnqueueResult(initialInstruments);
        reader.EnqueueException(new InvalidOperationException("Read failed."));
        reader.EnqueueResult(refreshedInstruments);
        InstrumentsViewModel viewModel = CreateViewModel(reader);
        await viewModel.EnsureLoadedAsync();
        await viewModel.RefreshCommand.ExecuteAsync(null);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Same(refreshedInstruments, viewModel.Instruments);
        Assert.Null(viewModel.ErrorMessage);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task CreateInstrumentAsync_WhenSymbolIsBlank_KeepsFormOpenAndDoesNotWrite()
    {
        var store = new FakeInstrumentStore();
        InstrumentsViewModel viewModel = CreateViewModel(store: store);
        viewModel.ShowCreateFormCommand.Execute(null);
        viewModel.Symbol = "   ";
        viewModel.DisplayName = "E-mini S&P 500";
        viewModel.Currency = "USD";
        viewModel.TickSizeText = "0.25";
        viewModel.TickValueText = "12.50";

        await viewModel.CreateInstrumentCommand.ExecuteAsync(null);

        Assert.Equal("Symbol is required.", viewModel.CreateErrorMessage);
        Assert.True(viewModel.IsSymbolInvalid);
        Assert.True(viewModel.IsCreateFormVisible);
        Assert.Equal(0, store.AddCallCount);

        viewModel.Symbol = "ES";

        Assert.False(viewModel.IsSymbolInvalid);
        Assert.Null(viewModel.CreateErrorMessage);
    }

    [Fact]
    public async Task CreateInstrumentAsync_WhenWriteFails_RetainsFormValuesAndShowsCreateError()
    {
        var store = new FakeInstrumentStore
        {
            AddException = new InvalidOperationException("Write failed."),
        };
        InstrumentsViewModel viewModel = CreateViewModel(store: store);
        OpenValidCreateForm(viewModel);

        await viewModel.CreateInstrumentCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsCreateFormVisible);
        Assert.Equal("ES", viewModel.Symbol);
        Assert.Equal("E-mini S&P 500", viewModel.DisplayName);
        Assert.Equal(AssetClass.Futures, viewModel.SelectedAssetClass);
        Assert.Equal("CME", viewModel.Exchange);
        Assert.Equal("USD", viewModel.Currency);
        Assert.Equal("1", viewModel.TickSizeText);
        Assert.Equal("10", viewModel.TickValueText);
        Assert.Equal("Instrument could not be created.", viewModel.CreateErrorMessage);
    }

    [Fact]
    public async Task CreateInstrumentAsync_WhenWriteAndReloadSucceed_ClosesAndResetsFormUsingReaderProjection()
    {
        var initialInstruments = new[] { CreateListItem(isActive: true) };
        var refreshedInstruments = new[]
        {
            CreateListItem(isActive: true),
            CreateListItem(isActive: false),
        };
        var reader = new FakeInstrumentReader();
        reader.EnqueueResult(initialInstruments);
        reader.EnqueueResult(refreshedInstruments);
        var store = new FakeInstrumentStore();
        InstrumentsViewModel viewModel = CreateViewModel(reader, store);
        await viewModel.EnsureLoadedAsync();
        OpenValidCreateForm(viewModel);

        await viewModel.CreateInstrumentCommand.ExecuteAsync(null);

        Assert.Equal(1, store.AddCallCount);
        Assert.Same(refreshedInstruments, viewModel.Instruments);
        Assert.False(viewModel.IsCreateFormVisible);
        AssertCreateFormIsReset(viewModel);
        Assert.Null(viewModel.CreateErrorMessage);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task CreateInstrumentAsync_WhenWriteSucceedsAndReloadFails_ClosesFormAndShowsRefreshMessage()
    {
        var initialInstruments = new[] { CreateListItem(isActive: true) };
        var reader = new FakeInstrumentReader();
        reader.EnqueueResult(initialInstruments);
        reader.EnqueueException(new InvalidOperationException("Read failed."));
        var store = new FakeInstrumentStore();
        InstrumentsViewModel viewModel = CreateViewModel(reader, store);
        await viewModel.EnsureLoadedAsync();
        OpenValidCreateForm(viewModel);

        await viewModel.CreateInstrumentCommand.ExecuteAsync(null);

        Assert.Equal(1, store.AddCallCount);
        Assert.False(viewModel.IsCreateFormVisible);
        AssertCreateFormIsReset(viewModel);
        Assert.Null(viewModel.CreateErrorMessage);
        Assert.Equal(
            "Instrument was created, but the list could not be refreshed. Refresh to see the latest data.",
            viewModel.ErrorMessage);
        Assert.Same(initialInstruments, viewModel.Instruments);
    }

    [Fact]
    public async Task DeactivateInstrumentAsync_WhenWriteFails_ShowsLifecycleErrorOnly()
    {
        InstrumentListItem activeItem = CreateListItem(isActive: true);
        var instruments = new[] { activeItem };
        var reader = new FakeInstrumentReader();
        reader.EnqueueResult(instruments);
        var store = new FakeInstrumentStore
        {
            InstrumentToReturn = CreateAggregate(activeItem.Id, isActive: true),
            UpdateException = new InvalidOperationException("Write failed."),
        };
        InstrumentsViewModel viewModel = CreateViewModel(reader, store);
        await viewModel.EnsureLoadedAsync();

        await viewModel.DeactivateInstrumentCommand.ExecuteAsync(activeItem);

        Assert.Equal(
            "Instrument status could not be changed.",
            viewModel.LifecycleErrorMessage);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Same(instruments, viewModel.Instruments);
    }

    [Fact]
    public async Task DeactivateInstrumentAsync_WhenWriteSucceedsAndReloadFails_DoesNotReportLifecycleFailure()
    {
        InstrumentListItem activeItem = CreateListItem(isActive: true);
        var instruments = new[] { activeItem };
        var reader = new FakeInstrumentReader();
        reader.EnqueueResult(instruments);
        reader.EnqueueException(new InvalidOperationException("Read failed."));
        var store = new FakeInstrumentStore
        {
            InstrumentToReturn = CreateAggregate(activeItem.Id, isActive: true),
        };
        InstrumentsViewModel viewModel = CreateViewModel(reader, store);
        await viewModel.EnsureLoadedAsync();

        await viewModel.DeactivateInstrumentCommand.ExecuteAsync(activeItem);

        Assert.Equal(1, store.UpdateCallCount);
        Assert.False(Assert.IsType<Instrument>(store.UpdatedInstrument).IsActive);
        Assert.Null(viewModel.LifecycleErrorMessage);
        Assert.Equal(
            "Instrument status changed, but the list could not be refreshed. Refresh to see the latest status.",
            viewModel.ErrorMessage);
        Assert.Same(instruments, viewModel.Instruments);
    }

    [Fact]
    public async Task DeactivateInstrumentAsync_WhenInstrumentIsMissing_ShowsNotFoundMessage()
    {
        InstrumentListItem activeItem = CreateListItem(isActive: true);
        var reader = new FakeInstrumentReader();
        reader.EnqueueResult([activeItem]);
        var store = new FakeInstrumentStore();
        InstrumentsViewModel viewModel = CreateViewModel(reader, store);
        await viewModel.EnsureLoadedAsync();

        await viewModel.DeactivateInstrumentCommand.ExecuteAsync(activeItem);

        Assert.Equal(
            "Instrument no longer exists. Refresh the list.",
            viewModel.LifecycleErrorMessage);
        Assert.Equal(0, store.UpdateCallCount);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public void LifecycleCommands_ReflectRowStateAndAreDisabledWhileCreateFormIsOpen()
    {
        InstrumentListItem activeItem = CreateListItem(isActive: true);
        InstrumentListItem inactiveItem = CreateListItem(isActive: false);
        InstrumentsViewModel viewModel = CreateViewModel();

        Assert.True(viewModel.DeactivateInstrumentCommand.CanExecute(activeItem));
        Assert.False(viewModel.ActivateInstrumentCommand.CanExecute(activeItem));
        Assert.True(viewModel.ActivateInstrumentCommand.CanExecute(inactiveItem));
        Assert.False(viewModel.DeactivateInstrumentCommand.CanExecute(inactiveItem));

        viewModel.ShowCreateFormCommand.Execute(null);

        Assert.False(viewModel.ActivateInstrumentCommand.CanExecute(inactiveItem));
        Assert.False(viewModel.DeactivateInstrumentCommand.CanExecute(activeItem));
    }

    [Fact]
    public async Task CreateInstrumentAsync_ParsesDecimalsUsingCurrentCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var store = new FakeInstrumentStore();
            InstrumentsViewModel viewModel = CreateViewModel(store: store);
            OpenValidCreateForm(viewModel);
            viewModel.TickSizeText = "0,25";
            viewModel.TickValueText = "12,50";

            await viewModel.CreateInstrumentCommand.ExecuteAsync(null);

            Instrument instrument = Assert.IsType<Instrument>(store.AddedInstrument);
            Assert.Equal(0.25m, instrument.TickSize);
            Assert.Equal(12.50m, instrument.TickValue);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task CreateInstrumentAsync_UsesInvariantDecimalFallback()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var store = new FakeInstrumentStore();
            InstrumentsViewModel viewModel = CreateViewModel(store: store);
            OpenValidCreateForm(viewModel);
            viewModel.TickSizeText = "0.25";
            viewModel.TickValueText = "12.50";

            await viewModel.CreateInstrumentCommand.ExecuteAsync(null);

            Instrument instrument = Assert.IsType<Instrument>(store.AddedInstrument);
            Assert.Equal(0.25m, instrument.TickSize);
            Assert.Equal(12.50m, instrument.TickValue);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task ViewInstrumentAsync_LoadsAuthoritativeDetails()
    {
        InstrumentDetails details = CreateDetails();
        var reader = new FakeInstrumentReader { DetailsToReturn = details };
        InstrumentsViewModel viewModel = CreateViewModel(reader);

        await viewModel.ViewInstrumentCommand.ExecuteAsync(details.Id);

        Assert.Same(details, viewModel.SelectedInstrument);
        Assert.True(viewModel.HasSelectedInstrument);
        Assert.Equal(1, reader.DetailsCallCount);
    }

    [Fact]
    public async Task EditInstrumentAsync_PrepopulatesFieldsAndCancelDoesNotPersist()
    {
        InstrumentDetails details = CreateDetails();
        var reader = new FakeInstrumentReader { DetailsToReturn = details };
        var store = new FakeInstrumentStore { InstrumentToReturn = CreateAggregate(details.Id, true) };
        InstrumentsViewModel viewModel = CreateViewModel(reader, store);

        await viewModel.EditInstrumentCommand.ExecuteAsync(details.Id);

        Assert.True(viewModel.IsEditFormVisible);
        Assert.Equal(details.Symbol, viewModel.EditSymbol);
        Assert.Equal(details.AssetClass, viewModel.EditSelectedAssetClass);
        Assert.Equal(details.PointValue, viewModel.EditPointValuePreview);

        viewModel.CancelEditCommand.Execute(null);

        Assert.False(viewModel.IsEditFormVisible);
        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public async Task SaveChangesAsync_UpdatesAndClosesEditForm()
    {
        InstrumentDetails details = CreateDetails();
        var reader = new FakeInstrumentReader { DetailsToReturn = details };
        reader.EnqueueResult([CreateListItem(true) with { Id = details.Id }]);
        var store = new FakeInstrumentStore { InstrumentToReturn = CreateAggregate(details.Id, true) };
        InstrumentsViewModel viewModel = CreateViewModel(reader, store);
        await viewModel.EditInstrumentCommand.ExecuteAsync(details.Id);
        viewModel.EditSymbol = " mnq ";
        viewModel.EditDisplayName = "Micro Nasdaq";
        viewModel.EditTickValueText = "0.50";

        await viewModel.SaveChangesCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsEditFormVisible);
        Assert.Equal(1, store.UpdateCallCount);
        Assert.Equal("MNQ", viewModel.SelectedInstrument?.Symbol);
        Assert.Equal(2m, viewModel.SelectedInstrument?.PointValue);
    }

    [Fact]
    public async Task SaveChangesAsync_InvalidTickSizeShowsFieldErrorWithoutWrite()
    {
        InstrumentDetails details = CreateDetails();
        var reader = new FakeInstrumentReader { DetailsToReturn = details };
        var store = new FakeInstrumentStore { InstrumentToReturn = CreateAggregate(details.Id, true) };
        InstrumentsViewModel viewModel = CreateViewModel(reader, store);
        await viewModel.EditInstrumentCommand.ExecuteAsync(details.Id);
        viewModel.EditTickSizeText = "0";

        await viewModel.SaveChangesCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsEditTickSizeInvalid);
        Assert.Equal("Tick size must be greater than zero.", viewModel.EditErrorMessage);
        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public async Task SaveChangesAsync_ReferencedAssetClassChangeShowsSafeFormError()
    {
        InstrumentDetails details = CreateDetails();
        var reader = new FakeInstrumentReader { DetailsToReturn = details };
        var store = new FakeInstrumentStore { InstrumentToReturn = CreateAggregate(details.Id, true) };
        var deletionStore = new FakeInstrumentDeletionStore { HasTrades = true };
        InstrumentsViewModel viewModel = CreateViewModel(reader, store, deletionStore);
        await viewModel.EditInstrumentCommand.ExecuteAsync(details.Id);
        viewModel.EditSelectedAssetClass = AssetClass.Crypto;

        await viewModel.SaveChangesCommand.ExecuteAsync(null);

        Assert.Equal(
            "Asset class cannot be changed because this instrument is used by existing trades.",
            viewModel.EditErrorMessage);
        Assert.True(viewModel.IsEditFormVisible);
        Assert.Equal(0, store.UpdateCallCount);
    }

    [Fact]
    public async Task DeleteInstrumentAsync_CancelDoesNotDelete()
    {
        InstrumentDetails details = CreateDetails();
        var store = new FakeInstrumentStore { InstrumentToReturn = CreateAggregate(details.Id, true) };
        var deletionStore = new FakeInstrumentDeletionStore();
        var dialogService = new FakeDialogService { ConfirmationResult = false };
        InstrumentsViewModel viewModel = CreateViewModel(
            store: store,
            deletionStore: deletionStore,
            dialogService: dialogService);

        await viewModel.DeleteInstrumentCommand.ExecuteAsync(details.Id);

        Assert.Equal("Delete instrument?", dialogService.ConfirmationRequest?.Title);
        Assert.Equal(0, deletionStore.DeleteCallCount);
    }

    [Fact]
    public async Task DeleteInstrumentAsync_ReferencedShowsInformationDialog()
    {
        InstrumentDetails details = CreateDetails();
        var store = new FakeInstrumentStore { InstrumentToReturn = CreateAggregate(details.Id, true) };
        var deletionStore = new FakeInstrumentDeletionStore { HasTrades = true };
        var dialogService = new FakeDialogService { ConfirmationResult = true };
        InstrumentsViewModel viewModel = CreateViewModel(
            store: store,
            deletionStore: deletionStore,
            dialogService: dialogService);

        await viewModel.DeleteInstrumentCommand.ExecuteAsync(details.Id);

        Assert.Equal("Cannot delete instrument", dialogService.InformationRequest?.Title);
        Assert.Contains("Deactivate it instead", dialogService.InformationRequest?.Message);
        Assert.Equal(0, deletionStore.DeleteCallCount);
    }

    [Fact]
    public async Task DeleteInstrumentAsync_SuccessClearsSelectionAndReloads()
    {
        InstrumentDetails details = CreateDetails();
        var reader = new FakeInstrumentReader { DetailsToReturn = details };
        reader.EnqueueResult([CreateListItem(true) with { Id = details.Id }]);
        reader.EnqueueResult([]);
        var store = new FakeInstrumentStore { InstrumentToReturn = CreateAggregate(details.Id, true) };
        var deletionStore = new FakeInstrumentDeletionStore();
        var dialogService = new FakeDialogService { ConfirmationResult = true };
        InstrumentsViewModel viewModel = CreateViewModel(reader, store, deletionStore, dialogService);
        await viewModel.EnsureLoadedAsync();
        await viewModel.ViewInstrumentCommand.ExecuteAsync(details.Id);

        await viewModel.DeleteInstrumentCommand.ExecuteAsync(details.Id);

        Assert.Equal(1, deletionStore.DeleteCallCount);
        Assert.Empty(viewModel.Instruments);
        Assert.Null(viewModel.SelectedInstrument);
    }

    private static InstrumentsViewModel CreateViewModel(
        FakeInstrumentReader? reader = null,
        FakeInstrumentStore? store = null,
        FakeInstrumentDeletionStore? deletionStore = null,
        FakeDialogService? dialogService = null)
    {
        reader ??= new FakeInstrumentReader();
        store ??= new FakeInstrumentStore();
        deletionStore ??= new FakeInstrumentDeletionStore();
        dialogService ??= new FakeDialogService();
        var timeProvider = new FixedTimeProvider();

        return new InstrumentsViewModel(
            reader,
            new CreateInstrumentUseCase(store, timeProvider),
            new InstrumentLifecycleUseCase(store, timeProvider),
            new GetInstrumentDetailsUseCase(reader),
            new UpdateInstrumentUseCase(store, deletionStore, timeProvider),
            new DeleteInstrumentUseCase(store, deletionStore),
            dialogService);
    }

    private static void OpenValidCreateForm(InstrumentsViewModel viewModel)
    {
        viewModel.ShowCreateFormCommand.Execute(null);
        viewModel.Symbol = "ES";
        viewModel.DisplayName = "E-mini S&P 500";
        viewModel.SelectedAssetClass = AssetClass.Futures;
        viewModel.Exchange = "CME";
        viewModel.Currency = "USD";
        viewModel.TickSizeText = "1";
        viewModel.TickValueText = "10";
    }

    private static void AssertCreateFormIsReset(InstrumentsViewModel viewModel)
    {
        Assert.Equal(string.Empty, viewModel.Symbol);
        Assert.Equal(string.Empty, viewModel.DisplayName);
        Assert.Equal(AssetClass.Futures, viewModel.SelectedAssetClass);
        Assert.Equal(string.Empty, viewModel.Exchange);
        Assert.Equal(string.Empty, viewModel.Currency);
        Assert.Equal(string.Empty, viewModel.TickSizeText);
        Assert.Equal(string.Empty, viewModel.TickValueText);
    }

    private static InstrumentListItem CreateListItem(bool isActive)
    {
        return new InstrumentListItem(
            Guid.NewGuid(),
            "ES",
            "E-mini S&P 500",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            12.50m,
            50m,
            isActive);
    }

    private static InstrumentDetails CreateDetails()
    {
        DateTimeOffset createdAtUtc = FixedTimeProvider.FixedUtcNow.AddDays(-2);
        return new InstrumentDetails(
            Guid.NewGuid(),
            "ES",
            "E-mini S&P 500",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            12.50m,
            50m,
            true,
            createdAtUtc,
            createdAtUtc);
    }

    private static Instrument CreateAggregate(Guid id, bool isActive)
    {
        DateTimeOffset createdAtUtc = FixedTimeProvider.FixedUtcNow.AddDays(-2);
        DateTimeOffset updatedAtUtc = FixedTimeProvider.FixedUtcNow.AddDays(-1);

        return Instrument.Rehydrate(
            id,
            "ES",
            "E-mini S&P 500",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            12.50m,
            isActive,
            createdAtUtc,
            updatedAtUtc);
    }
}
