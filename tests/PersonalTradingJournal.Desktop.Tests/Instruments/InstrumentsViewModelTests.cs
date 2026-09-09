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
        Assert.True(viewModel.IsCreateFormVisible);
        Assert.Equal(0, store.AddCallCount);
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

    private static InstrumentsViewModel CreateViewModel(
        FakeInstrumentReader? reader = null,
        FakeInstrumentStore? store = null)
    {
        reader ??= new FakeInstrumentReader();
        store ??= new FakeInstrumentStore();
        var timeProvider = new FixedTimeProvider();

        return new InstrumentsViewModel(
            reader,
            new CreateInstrumentUseCase(store, timeProvider),
            new InstrumentLifecycleUseCase(store, timeProvider));
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
