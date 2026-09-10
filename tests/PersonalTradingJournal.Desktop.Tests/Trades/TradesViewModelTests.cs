using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed class TradesViewModelTests
{
    [Fact]
    public void NewViewModelUsesEmptyManualEntryDefaults()
    {
        var viewModel = new TradesViewModel(new FakeManualTradeReferenceDataReader());

        Assert.Null(viewModel.SelectedDirection);
        Assert.Equal(string.Empty, viewModel.QuantityText);
        Assert.Equal(string.Empty, viewModel.EntryExecutedAtUtcText);
        Assert.Equal(string.Empty, viewModel.EntryPriceText);
        Assert.Equal("0", viewModel.EntryCommissionText);
        Assert.Equal("0", viewModel.EntryFeesText);
        Assert.False(viewModel.HasExit);
        Assert.Equal(string.Empty, viewModel.ExitExecutedAtUtcText);
        Assert.Equal(string.Empty, viewModel.ExitPriceText);
        Assert.Equal("0", viewModel.ExitCommissionText);
        Assert.Equal("0", viewModel.ExitFeesText);
    }

    [Fact]
    public void ManualEntryFactsRemainAsEnteredWithoutParsing()
    {
        var viewModel = new TradesViewModel(new FakeManualTradeReferenceDataReader());

        PopulateRepresentativeTradeFacts(viewModel);

        AssertRepresentativeTradeFacts(viewModel);
    }

    [Fact]
    public void DisablingExitClearsOnlyExitFacts()
    {
        var viewModel = new TradesViewModel(new FakeManualTradeReferenceDataReader());
        PopulateRepresentativeTradeFacts(viewModel);

        viewModel.HasExit = false;

        Assert.False(viewModel.HasExit);
        Assert.Equal(TradeDirection.Short, viewModel.SelectedDirection);
        Assert.Equal("2.5", viewModel.QuantityText);
        Assert.Equal("2026-09-10 13:30:00", viewModel.EntryExecutedAtUtcText);
        Assert.Equal("23950.25", viewModel.EntryPriceText);
        Assert.Equal("1.50", viewModel.EntryCommissionText);
        Assert.Equal("0.25", viewModel.EntryFeesText);
        Assert.Equal(string.Empty, viewModel.ExitExecutedAtUtcText);
        Assert.Equal(string.Empty, viewModel.ExitPriceText);
        Assert.Equal("0", viewModel.ExitCommissionText);
        Assert.Equal("0", viewModel.ExitFeesText);
    }

    [Fact]
    public async Task EnsureLoadedAsyncPopulatesOptionsOnlyOnceAfterSuccess()
    {
        ManualTradeReferenceData referenceData = CreateReferenceData();
        var reader = new FakeManualTradeReferenceDataReader();
        reader.EnqueueResult(referenceData);
        var viewModel = new TradesViewModel(reader);

        await viewModel.EnsureLoadedAsync();
        await viewModel.EnsureLoadedAsync();

        Assert.Same(referenceData.Accounts, viewModel.AccountOptions);
        Assert.Same(referenceData.Instruments, viewModel.InstrumentOptions);
        Assert.True(viewModel.HasReferenceData);
        Assert.Null(viewModel.SelectedAccount);
        Assert.Null(viewModel.SelectedInstrument);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task NormalLoadsAlwaysRequestActiveReferencesOnly()
    {
        var reader = new FakeManualTradeReferenceDataReader();
        reader.EnqueueResult(CreateReferenceData());
        reader.EnqueueResult(CreateReferenceData());
        var viewModel = new TradesViewModel(reader);

        await viewModel.EnsureLoadedAsync();
        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal([false, false], reader.IncludeInactiveRequests);
    }

    [Fact]
    public async Task RefreshReplacesBothOptionCollections()
    {
        ManualTradeReferenceData initial = CreateReferenceData();
        ManualTradeReferenceData refreshed = CreateReferenceData(
            accountName: "Refreshed Account",
            instrumentSymbol: "ES");
        var reader = new FakeManualTradeReferenceDataReader();
        reader.EnqueueResult(initial);
        reader.EnqueueResult(refreshed);
        var viewModel = new TradesViewModel(reader);
        await viewModel.EnsureLoadedAsync();

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Same(refreshed.Accounts, viewModel.AccountOptions);
        Assert.Same(refreshed.Instruments, viewModel.InstrumentOptions);
    }

    [Fact]
    public async Task RefreshFailureRetainsOptionsAndShowsSafeError()
    {
        ManualTradeReferenceData initial = CreateReferenceData();
        var reader = new FakeManualTradeReferenceDataReader();
        reader.EnqueueResult(initial);
        reader.EnqueueException(new InvalidOperationException("Technical details."));
        var viewModel = new TradesViewModel(reader);
        await viewModel.EnsureLoadedAsync();

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Same(initial.Accounts, viewModel.AccountOptions);
        Assert.Same(initial.Instruments, viewModel.InstrumentOptions);
        Assert.Equal("Trade reference data could not be loaded.", viewModel.ErrorMessage);
        Assert.True(viewModel.HasError);
    }

    [Fact]
    public async Task SuccessfulRefreshAfterFailureClearsError()
    {
        ManualTradeReferenceData initial = CreateReferenceData();
        ManualTradeReferenceData refreshed = CreateReferenceData(
            accountName: "Recovered Account");
        var reader = new FakeManualTradeReferenceDataReader();
        reader.EnqueueResult(initial);
        reader.EnqueueException(new InvalidOperationException("Read failed."));
        reader.EnqueueResult(refreshed);
        var viewModel = new TradesViewModel(reader);
        await viewModel.EnsureLoadedAsync();
        await viewModel.RefreshCommand.ExecuteAsync(null);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Same(refreshed.Accounts, viewModel.AccountOptions);
        Assert.Same(refreshed.Instruments, viewModel.InstrumentOptions);
        Assert.Null(viewModel.ErrorMessage);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task RefreshRebindsSelectionsToNewInstancesWithMatchingIds()
    {
        Guid accountId = Guid.NewGuid();
        Guid instrumentId = Guid.NewGuid();
        ManualTradeReferenceData initial = CreateReferenceData(accountId, instrumentId);
        ManualTradeReferenceData refreshed = CreateReferenceData(
            accountId,
            instrumentId,
            "Updated Account",
            "ES");
        var reader = new FakeManualTradeReferenceDataReader();
        reader.EnqueueResult(initial);
        reader.EnqueueResult(refreshed);
        var viewModel = new TradesViewModel(reader);
        await viewModel.EnsureLoadedAsync();
        viewModel.SelectedAccount = Assert.Single(initial.Accounts);
        viewModel.SelectedInstrument = Assert.Single(initial.Instruments);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(accountId, viewModel.SelectedAccount!.Id);
        Assert.Equal(instrumentId, viewModel.SelectedInstrument!.Id);
        Assert.Same(Assert.Single(refreshed.Accounts), viewModel.SelectedAccount);
        Assert.Same(Assert.Single(refreshed.Instruments), viewModel.SelectedInstrument);
        Assert.NotSame(Assert.Single(initial.Accounts), viewModel.SelectedAccount);
        Assert.NotSame(Assert.Single(initial.Instruments), viewModel.SelectedInstrument);
    }

    [Fact]
    public async Task RefreshRetainsNonReferenceTradeFacts()
    {
        Guid accountId = Guid.NewGuid();
        Guid instrumentId = Guid.NewGuid();
        ManualTradeReferenceData initial = CreateReferenceData(accountId, instrumentId);
        ManualTradeReferenceData refreshed = CreateReferenceData(
            accountId,
            instrumentId,
            "Updated Account",
            "ES");
        var reader = new FakeManualTradeReferenceDataReader();
        reader.EnqueueResult(initial);
        reader.EnqueueResult(refreshed);
        var viewModel = new TradesViewModel(reader);
        await viewModel.EnsureLoadedAsync();
        viewModel.ShowManualEntryCommand.Execute(null);
        viewModel.SelectedAccount = Assert.Single(initial.Accounts);
        viewModel.SelectedInstrument = Assert.Single(initial.Instruments);
        PopulateRepresentativeTradeFacts(viewModel);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        AssertRepresentativeTradeFacts(viewModel);
        Assert.Same(Assert.Single(refreshed.Accounts), viewModel.SelectedAccount);
        Assert.Same(Assert.Single(refreshed.Instruments), viewModel.SelectedInstrument);
        Assert.True(viewModel.IsManualEntryVisible);
    }

    [Fact]
    public async Task RefreshClearsSelectionsWhenIdsAreNoLongerAvailable()
    {
        ManualTradeReferenceData initial = CreateReferenceData();
        var reader = new FakeManualTradeReferenceDataReader();
        reader.EnqueueResult(initial);
        reader.EnqueueResult(new ManualTradeReferenceData([], []));
        var viewModel = new TradesViewModel(reader);
        await viewModel.EnsureLoadedAsync();
        viewModel.SelectedAccount = Assert.Single(initial.Accounts);
        viewModel.SelectedInstrument = Assert.Single(initial.Instruments);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Null(viewModel.SelectedAccount);
        Assert.Null(viewModel.SelectedInstrument);
        Assert.False(viewModel.HasReferenceData);
    }

    [Fact]
    public void CancelClosesShellAndResetsEntireManualEntryForm()
    {
        ManualTradeReferenceData referenceData = CreateReferenceData();
        var viewModel = new TradesViewModel(new FakeManualTradeReferenceDataReader())
        {
            SelectedAccount = Assert.Single(referenceData.Accounts),
            SelectedInstrument = Assert.Single(referenceData.Instruments),
        };

        viewModel.ShowManualEntryCommand.Execute(null);
        PopulateRepresentativeTradeFacts(viewModel);

        viewModel.CancelManualEntryCommand.Execute(null);

        Assert.False(viewModel.IsManualEntryVisible);
        Assert.Null(viewModel.SelectedAccount);
        Assert.Null(viewModel.SelectedInstrument);
        Assert.Null(viewModel.SelectedDirection);
        Assert.Equal(string.Empty, viewModel.QuantityText);
        Assert.Equal(string.Empty, viewModel.EntryExecutedAtUtcText);
        Assert.Equal(string.Empty, viewModel.EntryPriceText);
        Assert.Equal("0", viewModel.EntryCommissionText);
        Assert.Equal("0", viewModel.EntryFeesText);
        Assert.False(viewModel.HasExit);
        Assert.Equal(string.Empty, viewModel.ExitExecutedAtUtcText);
        Assert.Equal(string.Empty, viewModel.ExitPriceText);
        Assert.Equal("0", viewModel.ExitCommissionText);
        Assert.Equal("0", viewModel.ExitFeesText);
    }

    [Fact]
    public void ManualEntryCommandsReflectShellVisibility()
    {
        var viewModel = new TradesViewModel(new FakeManualTradeReferenceDataReader());

        Assert.True(viewModel.ShowManualEntryCommand.CanExecute(null));
        Assert.False(viewModel.CancelManualEntryCommand.CanExecute(null));

        viewModel.ShowManualEntryCommand.Execute(null);

        Assert.False(viewModel.ShowManualEntryCommand.CanExecute(null));
        Assert.True(viewModel.CancelManualEntryCommand.CanExecute(null));
    }

    private static ManualTradeReferenceData CreateReferenceData(
        Guid? accountId = null,
        Guid? instrumentId = null,
        string accountName = "Primary Account",
        string instrumentSymbol = "NQ")
    {
        return new ManualTradeReferenceData(
            [
                new ManualTradeAccountOption(
                    accountId ?? Guid.NewGuid(),
                    accountName,
                    TradingAccountType.Personal,
                    "Broker",
                    "ACCOUNT-1",
                    "USD",
                    true),
            ],
            [
                new ManualTradeInstrumentOption(
                    instrumentId ?? Guid.NewGuid(),
                    instrumentSymbol,
                    $"{instrumentSymbol} display name",
                    AssetClass.Futures,
                    "CME",
                    "USD",
                    0.25m,
                    5m,
                    20m,
                    true),
            ]);
    }

    private static void PopulateRepresentativeTradeFacts(TradesViewModel viewModel)
    {
        viewModel.SelectedDirection = TradeDirection.Short;
        viewModel.QuantityText = "2.5";
        viewModel.EntryExecutedAtUtcText = "2026-09-10 13:30:00";
        viewModel.EntryPriceText = "23950.25";
        viewModel.EntryCommissionText = "1.50";
        viewModel.EntryFeesText = "0.25";
        viewModel.HasExit = true;
        viewModel.ExitExecutedAtUtcText = "2026-09-10 14:15:00";
        viewModel.ExitPriceText = "23900.00";
        viewModel.ExitCommissionText = "1.50";
        viewModel.ExitFeesText = "0.25";
    }

    private static void AssertRepresentativeTradeFacts(TradesViewModel viewModel)
    {
        Assert.Equal(TradeDirection.Short, viewModel.SelectedDirection);
        Assert.Equal("2.5", viewModel.QuantityText);
        Assert.Equal("2026-09-10 13:30:00", viewModel.EntryExecutedAtUtcText);
        Assert.Equal("23950.25", viewModel.EntryPriceText);
        Assert.Equal("1.50", viewModel.EntryCommissionText);
        Assert.Equal("0.25", viewModel.EntryFeesText);
        Assert.True(viewModel.HasExit);
        Assert.Equal("2026-09-10 14:15:00", viewModel.ExitExecutedAtUtcText);
        Assert.Equal("23900.00", viewModel.ExitPriceText);
        Assert.Equal("1.50", viewModel.ExitCommissionText);
        Assert.Equal("0.25", viewModel.ExitFeesText);
    }
}
