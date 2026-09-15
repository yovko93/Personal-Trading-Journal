using System.Globalization;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Desktop.Tests.Instruments;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

[Collection(CultureSensitiveCollection.Name)]
public sealed class TradesViewModelTests
{
    private static readonly DateTimeOffset CloseExecutedAtUtc =
        new(2026, 9, 8, 14, 15, 0, TimeSpan.Zero);

    [Fact]
    public void NewViewModelUsesEmptyManualEntryDefaults()
    {
        var viewModel = CreateViewModel(new FakeManualTradeReferenceDataReader());

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
        Assert.Null(viewModel.ValidationErrorMessage);
        Assert.False(viewModel.HasValidationError);
        Assert.False(viewModel.IsSaving);
        Assert.Null(viewModel.SaveErrorMessage);
        Assert.False(viewModel.HasSaveError);
        Assert.Null(viewModel.SuccessMessage);
        Assert.False(viewModel.HasSuccessMessage);
        Assert.Null(viewModel.SelectedTradeDetail);
        Assert.False(viewModel.IsTradeDetailVisible);
        Assert.False(viewModel.IsTradeDetailLoading);
        Assert.False(viewModel.IsTradeDetailNotFound);
        Assert.Null(viewModel.TradeDetailErrorMessage);
        Assert.False(viewModel.HasTradeDetailError);
    }

    [Fact]
    public void ManualEntryFactsRemainAsEnteredWithoutParsing()
    {
        var viewModel = CreateViewModel(new FakeManualTradeReferenceDataReader());

        PopulateRepresentativeTradeFacts(viewModel);

        AssertRepresentativeTradeFacts(viewModel);
    }

    [Fact]
    public void SelectedInstrumentDerivesFuturesQuantityPresentationAndEconomics()
    {
        TradesViewModel viewModel = CreateViewModel();
        viewModel.QuantityText = "draft quantity";
        var futures = new ManualTradeInstrumentOption(
            Guid.NewGuid(),
            "CUSTOM",
            "Custom Futures Contract",
            AssetClass.Futures,
            "XEUR",
            "EUR",
            0.5m,
            3.75m,
            7.5m,
            true);

        viewModel.SelectedInstrument = futures;

        Assert.True(viewModel.IsSelectedInstrumentFutures);
        Assert.Equal("Contracts", viewModel.QuantityLabel);
        Assert.Contains("Whole contracts", viewModel.QuantityHint, StringComparison.Ordinal);
        Assert.Contains("CUSTOM", viewModel.SelectedInstrumentEconomicsText, StringComparison.Ordinal);
        Assert.Contains(
            "Custom Futures Contract",
            viewModel.SelectedInstrumentEconomicsText,
            StringComparison.Ordinal);
        Assert.Contains("7.5 EUR / point", viewModel.SelectedInstrumentEconomicsText, StringComparison.Ordinal);
        Assert.Equal("draft quantity", viewModel.QuantityText);

        viewModel.SelectedInstrument = futures with { AssetClass = AssetClass.Equity };

        Assert.False(viewModel.IsSelectedInstrumentFutures);
        Assert.Equal("Quantity", viewModel.QuantityLabel);
        Assert.Equal("Enter the traded quantity.", viewModel.QuantityHint);
        Assert.Equal("draft quantity", viewModel.QuantityText);
    }

    [Fact]
    public void SelectedInstrumentChangeRaisesAllDerivedPresentationNotifications()
    {
        TradesViewModel viewModel = CreateViewModel();
        var changedProperties = new HashSet<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.SelectedInstrument = new ManualTradeInstrumentOption(
            Guid.NewGuid(),
            "FUT",
            "Futures Contract",
            AssetClass.Futures,
            null,
            "USD",
            0.25m,
            0.5m,
            2m,
            true);

        Assert.Contains(nameof(TradesViewModel.SelectedInstrument), changedProperties);
        Assert.Contains(nameof(TradesViewModel.IsSelectedInstrumentFutures), changedProperties);
        Assert.Contains(nameof(TradesViewModel.QuantityLabel), changedProperties);
        Assert.Contains(nameof(TradesViewModel.QuantityHint), changedProperties);
        Assert.Contains(
            nameof(TradesViewModel.SelectedInstrumentEconomicsText),
            changedProperties);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2.0")]
    public void TryBuildManualTradeCommandAcceptsWholeFuturesContracts(
        string quantityText)
    {
        TradesViewModel viewModel = CreateValidTradeForm(
            assetClass: AssetClass.Futures,
            quantityText: quantityText,
            instrumentSymbol: "CUSTOM");

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.True(succeeded);
        Assert.NotNull(command);
        Assert.Equal(decimal.Parse(quantityText, CultureInfo.InvariantCulture), command.Quantity);
    }

    [Fact]
    public void TryBuildManualTradeCommandRejectsFractionalFuturesContracts()
    {
        TradesViewModel viewModel = CreateValidTradeForm(
            assetClass: AssetClass.Futures,
            quantityText: "1.5",
            instrumentSymbol: "CUSTOM");

        bool succeeded = viewModel.TryBuildManualTradeCommand(out _);

        Assert.False(succeeded);
        Assert.Equal(
            TradeQuantityPolicy.FuturesWholeContractsMessage,
            viewModel.ValidationErrorMessage);
    }

    [Theory]
    [InlineData(AssetClass.Equity)]
    [InlineData(AssetClass.Forex)]
    [InlineData(AssetClass.Crypto)]
    [InlineData(AssetClass.Option)]
    [InlineData(AssetClass.Other)]
    public void TryBuildManualTradeCommandKeepsFractionalNonFuturesQuantity(
        AssetClass assetClass)
    {
        TradesViewModel viewModel = CreateValidTradeForm(
            assetClass: assetClass,
            quantityText: "0.5",
            instrumentSymbol: "NQ");

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.True(succeeded);
        Assert.NotNull(command);
        Assert.Equal(0.5m, command.Quantity);
    }

    [Fact]
    public async Task FractionalFuturesUxValidationBlocksUseCaseAndPersistence()
    {
        ManualTradeSaveFixture fixture = CreateSaveFixture(quantityText: "1.5");

        await fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);

        Assert.Equal(
            TradeQuantityPolicy.FuturesWholeContractsMessage,
            fixture.ViewModel.ValidationErrorMessage);
        Assert.Equal(0, fixture.AccountStore.GetCallCount);
        Assert.Equal(0, fixture.InstrumentStore.GetCallCount);
        Assert.Equal(0, fixture.TradeStore.AddCallCount);
        Assert.True(fixture.ViewModel.IsManualEntryVisible);
        Assert.Equal("1.5", fixture.ViewModel.QuantityText);
    }

    [Fact]
    public void DisablingExitClearsOnlyExitFacts()
    {
        var viewModel = CreateViewModel(new FakeManualTradeReferenceDataReader());
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
    public void TryBuildManualTradeCommandBuildsValidOpenLongTrade()
    {
        TradesViewModel viewModel = CreateValidTradeForm();

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.True(succeeded);
        Assert.NotNull(command);
        Assert.Equal(viewModel.SelectedAccount!.Id, command.TradingAccountId);
        Assert.Equal(viewModel.SelectedInstrument!.Id, command.InstrumentId);
        Assert.Equal(TradeDirection.Long, command.Direction);
        Assert.Equal(2.5m, command.Quantity);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 10, 13, 30, 0, TimeSpan.Zero),
            command.Entry.ExecutedAtUtc);
        Assert.Equal(TimeSpan.Zero, command.Entry.ExecutedAtUtc.Offset);
        Assert.Equal(23950.25m, command.Entry.Price);
        Assert.Equal(1.50m, command.Entry.Commission);
        Assert.Equal(0.25m, command.Entry.Fees);
        Assert.Null(command.Exit);
        Assert.Null(viewModel.ValidationErrorMessage);
        Assert.False(viewModel.HasValidationError);
    }

    [Fact]
    public void TryBuildManualTradeCommandBuildsValidClosedShortTrade()
    {
        TradesViewModel viewModel = CreateValidTradeForm(
            hasExit: true,
            direction: TradeDirection.Short);

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.True(succeeded);
        Assert.NotNull(command);
        Assert.Equal(TradeDirection.Short, command.Direction);
        Assert.NotNull(command.Exit);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 10, 14, 15, 0, TimeSpan.Zero),
            command.Exit.ExecutedAtUtc);
        Assert.Equal(TimeSpan.Zero, command.Exit.ExecutedAtUtc.Offset);
        Assert.Equal(23900m, command.Exit.Price);
        Assert.Equal(1.50m, command.Exit.Commission);
        Assert.Equal(0.25m, command.Exit.Fees);
        Assert.Null(viewModel.ValidationErrorMessage);
    }

    [Fact]
    public void TryBuildManualTradeCommandDoesNotPromoteSelectorPolicyToValidation()
    {
        TradesViewModel viewModel = CreateValidTradeForm();
        viewModel.SelectedAccount = viewModel.SelectedAccount! with
        {
            Currency = "EUR",
            IsActive = false,
        };
        viewModel.SelectedInstrument = viewModel.SelectedInstrument! with
        {
            Currency = "USD",
            IsActive = false,
        };
        viewModel.EntryPriceText = "-0.10";

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.True(succeeded);
        Assert.NotNull(command);
        Assert.Equal(viewModel.SelectedAccount.Id, command.TradingAccountId);
        Assert.Equal(viewModel.SelectedInstrument.Id, command.InstrumentId);
        Assert.Equal(-0.10m, command.Entry.Price);
    }

    [Theory]
    [InlineData("account", "Trading account is required.")]
    [InlineData("instrument", "Instrument is required.")]
    [InlineData("direction", "Direction is required.")]
    public void TryBuildManualTradeCommandRejectsMissingRequiredSelection(
        string missingSelection,
        string expectedMessage)
    {
        TradesViewModel viewModel = CreateValidTradeForm();
        switch (missingSelection)
        {
            case "account":
                viewModel.SelectedAccount = null;
                break;
            case "instrument":
                viewModel.SelectedInstrument = null;
                break;
            case "direction":
                viewModel.SelectedDirection = null;
                break;
        }

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.False(succeeded);
        Assert.Null(command);
        Assert.Equal(expectedMessage, viewModel.ValidationErrorMessage);
        Assert.True(viewModel.HasValidationError);
    }

    [Fact]
    public void TryBuildManualTradeCommandRejectsUndefinedDirection()
    {
        TradesViewModel viewModel = CreateValidTradeForm();
        viewModel.SelectedDirection = (TradeDirection)999;

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.False(succeeded);
        Assert.Null(command);
        Assert.Equal("Direction is invalid.", viewModel.ValidationErrorMessage);
    }

    [Theory]
    [InlineData("", "Quantity must be a valid number.")]
    [InlineData("not-a-number", "Quantity must be a valid number.")]
    [InlineData("1e3", "Quantity must be a valid number.")]
    [InlineData("0", "Quantity must be greater than zero.")]
    [InlineData("-0.01", "Quantity must be greater than zero.")]
    public void TryBuildManualTradeCommandRejectsInvalidQuantity(
        string quantityText,
        string expectedMessage)
    {
        TradesViewModel viewModel = CreateValidTradeForm();
        viewModel.QuantityText = quantityText;

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.False(succeeded);
        Assert.Null(command);
        Assert.Equal(expectedMessage, viewModel.ValidationErrorMessage);
    }

    [Fact]
    public void TryBuildManualTradeCommandParsesCurrentCultureDecimals()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("bg-BG");
            TradesViewModel viewModel = CreateValidTradeForm();
            viewModel.QuantityText = "2,5";
            viewModel.EntryPriceText = "23950,25";
            viewModel.EntryCommissionText = "1,50";
            viewModel.EntryFeesText = "0,25";

            bool succeeded = viewModel.TryBuildManualTradeCommand(
                out CreateManualTradeCommand? command);

            Assert.True(succeeded);
            Assert.NotNull(command);
            Assert.Equal(2.5m, command.Quantity);
            Assert.Equal(23950.25m, command.Entry.Price);
            Assert.Equal(1.50m, command.Entry.Commission);
            Assert.Equal(0.25m, command.Entry.Fees);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void TryBuildManualTradeCommandFallsBackToInvariantDecimals()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("bg-BG");
            TradesViewModel viewModel = CreateValidTradeForm();

            bool succeeded = viewModel.TryBuildManualTradeCommand(
                out CreateManualTradeCommand? command);

            Assert.True(succeeded);
            Assert.NotNull(command);
            Assert.Equal(2.5m, command.Quantity);
            Assert.Equal(23950.25m, command.Entry.Price);
            Assert.Equal(1.50m, command.Entry.Commission);
            Assert.Equal(0.25m, command.Entry.Fees);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("2026-09-10 13:30:00")]
    [InlineData("2026-09-10 13:30")]
    [InlineData("2026-09-10T13:30:00Z")]
    [InlineData("2026-09-10T13:30Z")]
    public void TryBuildManualTradeCommandParsesSupportedUtcTimestamp(
        string timestampText)
    {
        TradesViewModel viewModel = CreateValidTradeForm();
        viewModel.EntryExecutedAtUtcText = timestampText;

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.True(succeeded);
        Assert.NotNull(command);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 10, 13, 30, 0, TimeSpan.Zero),
            command.Entry.ExecutedAtUtc);
        Assert.Equal(TimeSpan.Zero, command.Entry.ExecutedAtUtc.Offset);
    }

    [Theory]
    [InlineData("")]
    [InlineData("09/10/2026 13:30")]
    [InlineData("2026-09-10T13:30:00+03:00")]
    public void TryBuildManualTradeCommandRejectsUnsupportedEntryTimestamp(
        string timestampText)
    {
        TradesViewModel viewModel = CreateValidTradeForm();
        viewModel.EntryExecutedAtUtcText = timestampText;

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.False(succeeded);
        Assert.Null(command);
        Assert.Equal(
            "Entry time must be a valid UTC timestamp.",
            viewModel.ValidationErrorMessage);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("-5", -5)]
    public void TryBuildManualTradeCommandAcceptsZeroOrNegativeEntryPrice(
        string priceText,
        decimal expectedPrice)
    {
        TradesViewModel viewModel = CreateValidTradeForm();
        viewModel.EntryPriceText = priceText;

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.True(succeeded);
        Assert.NotNull(command);
        Assert.Equal(expectedPrice, command.Entry.Price);
    }

    [Fact]
    public void TryBuildManualTradeCommandRejectsMalformedEntryPrice()
    {
        TradesViewModel viewModel = CreateValidTradeForm();
        viewModel.EntryPriceText = "not-a-price";

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.False(succeeded);
        Assert.Null(command);
        Assert.Equal(
            "Entry price must be a valid number.",
            viewModel.ValidationErrorMessage);
    }

    [Fact]
    public void TryBuildManualTradeCommandTreatsBlankExecutionCostsAsZero()
    {
        TradesViewModel viewModel = CreateValidTradeForm(hasExit: true);
        viewModel.EntryCommissionText = string.Empty;
        viewModel.EntryFeesText = "   ";
        viewModel.ExitCommissionText = "   ";
        viewModel.ExitFeesText = string.Empty;

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.True(succeeded);
        Assert.NotNull(command);
        Assert.Equal(0m, command.Entry.Commission);
        Assert.Equal(0m, command.Entry.Fees);
        Assert.NotNull(command.Exit);
        Assert.Equal(0m, command.Exit.Commission);
        Assert.Equal(0m, command.Exit.Fees);
    }

    [Theory]
    [InlineData(
        "entryCommission",
        "-0.01",
        "Entry commission must be a valid non-negative number.")]
    [InlineData(
        "entryFees",
        "-0.01",
        "Entry fees must be a valid non-negative number.")]
    [InlineData(
        "exitCommission",
        "-0.01",
        "Exit commission must be a valid non-negative number.")]
    [InlineData(
        "exitFees",
        "invalid",
        "Exit fees must be a valid non-negative number.")]
    public void TryBuildManualTradeCommandRejectsInvalidExecutionCost(
        string field,
        string value,
        string expectedMessage)
    {
        TradesViewModel viewModel = CreateValidTradeForm(hasExit: true);
        switch (field)
        {
            case "entryCommission":
                viewModel.EntryCommissionText = value;
                break;
            case "entryFees":
                viewModel.EntryFeesText = value;
                break;
            case "exitCommission":
                viewModel.ExitCommissionText = value;
                break;
            case "exitFees":
                viewModel.ExitFeesText = value;
                break;
        }

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.False(succeeded);
        Assert.Null(command);
        Assert.Equal(expectedMessage, viewModel.ValidationErrorMessage);
    }

    [Fact]
    public void TryBuildManualTradeCommandIgnoresExitFieldsForOpenTrade()
    {
        TradesViewModel viewModel = CreateValidTradeForm();
        viewModel.ExitExecutedAtUtcText = "not-a-time";
        viewModel.ExitPriceText = "not-a-price";
        viewModel.ExitCommissionText = "invalid";
        viewModel.ExitFeesText = "invalid";

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.True(succeeded);
        Assert.NotNull(command);
        Assert.Null(command.Exit);
    }

    [Theory]
    [InlineData("time", "Exit time must be a valid UTC timestamp.")]
    [InlineData("price", "Exit price must be a valid number.")]
    public void TryBuildManualTradeCommandRequiresEnabledExitFields(
        string invalidField,
        string expectedMessage)
    {
        TradesViewModel viewModel = CreateValidTradeForm(hasExit: true);
        if (invalidField == "time")
        {
            viewModel.ExitExecutedAtUtcText = string.Empty;
        }
        else
        {
            viewModel.ExitPriceText = "not-a-price";
        }

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.False(succeeded);
        Assert.Null(command);
        Assert.Equal(expectedMessage, viewModel.ValidationErrorMessage);
    }

    [Fact]
    public void TryBuildManualTradeCommandRejectsExitEarlierThanEntry()
    {
        TradesViewModel viewModel = CreateValidTradeForm(hasExit: true);
        viewModel.ExitExecutedAtUtcText = "2026-09-10 13:29:59";

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.False(succeeded);
        Assert.Null(command);
        Assert.Equal(
            "Exit time cannot be earlier than entry time.",
            viewModel.ValidationErrorMessage);
    }

    [Fact]
    public void TryBuildManualTradeCommandAcceptsEqualEntryAndExitTimes()
    {
        TradesViewModel viewModel = CreateValidTradeForm(hasExit: true);
        viewModel.ExitExecutedAtUtcText = viewModel.EntryExecutedAtUtcText;

        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.True(succeeded);
        Assert.NotNull(command);
        Assert.NotNull(command.Exit);
        Assert.Equal(command.Entry.ExecutedAtUtc, command.Exit.ExecutedAtUtc);
    }

    [Fact]
    public void SuccessfulBuildClearsPriorValidationFailure()
    {
        TradesViewModel viewModel = CreateValidTradeForm();
        viewModel.QuantityText = "invalid";
        Assert.False(viewModel.TryBuildManualTradeCommand(out _));
        Assert.True(viewModel.HasValidationError);

        viewModel.QuantityText = "2.5";
        bool succeeded = viewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.True(succeeded);
        Assert.NotNull(command);
        Assert.Null(viewModel.ValidationErrorMessage);
        Assert.False(viewModel.HasValidationError);
    }

    [Fact]
    public async Task SaveManualTradeCommandDoesNotWriteWhenValidationFails()
    {
        var tradeStore = new FakeTradeStore();
        var tradeListReader = new FakeTradeListReader();
        TradesViewModel viewModel = CreateViewModel(
            tradeListReader: tradeListReader,
            tradeStore: tradeStore);
        viewModel.ShowManualEntryCommand.Execute(null);

        await viewModel.SaveManualTradeCommand.ExecuteAsync(null);

        Assert.Equal(
            "Trading account is required.",
            viewModel.ValidationErrorMessage);
        Assert.Equal(0, tradeStore.AddCallCount);
        Assert.True(viewModel.IsManualEntryVisible);
        Assert.Null(viewModel.SaveErrorMessage);
        Assert.Null(viewModel.SuccessMessage);
        Assert.False(viewModel.IsSaving);
        Assert.Equal(0, tradeListReader.CallCount);
    }

    [Fact]
    public async Task SaveManualTradeCommandPersistsOpenLongTradeAndResetsForm()
    {
        ManualTradeSaveFixture fixture = CreateSaveFixture();

        await fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.TradeStore.AddCallCount);
        Trade trade = Assert.IsType<Trade>(fixture.TradeStore.AddedTrade);
        Assert.Equal(
            Assert.Single(fixture.ReferenceData.Accounts).Id,
            trade.TradingAccountId);
        Assert.Equal(
            Assert.Single(fixture.ReferenceData.Instruments).Id,
            trade.InstrumentId);
        Assert.Equal(TradeDirection.Long, trade.Direction);
        Assert.Equal(TradeStatus.Open, trade.Status);
        Assert.Equal(2m, trade.OpenQuantity);
        Assert.Equal(20m, trade.Pricing.PointValue);
        Assert.Equal("USD", trade.Pricing.Currency);
        TradeExecution execution = Assert.Single(trade.Executions);
        Assert.Equal(ExecutionSide.Buy, execution.Side);
        Assert.Equal(2m, execution.Quantity);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 10, 13, 30, 0, TimeSpan.Zero),
            execution.ExecutedAtUtc);
        Assert.Equal(23950.25m, execution.Price);
        Assert.Equal(1.50m, execution.Commission);
        Assert.Equal(0.25m, execution.Fees);
        Assert.False(fixture.ViewModel.IsManualEntryVisible);
        AssertManualEntryFormReset(fixture.ViewModel);
        Assert.Null(fixture.ViewModel.ValidationErrorMessage);
        Assert.Null(fixture.ViewModel.SaveErrorMessage);
        Assert.Equal(
            "Trade saved successfully.",
            fixture.ViewModel.SuccessMessage);
        Assert.True(fixture.ViewModel.HasSuccessMessage);
        Assert.False(fixture.ViewModel.IsSaving);
        Assert.Equal(1, fixture.TradeListReader.CallCount);
    }

    [Fact]
    public async Task SuccessfulSaveReloadsExactAuthoritativeRowsWithoutLocalAppend()
    {
        ManualTradeSaveFixture fixture = CreateSaveFixture();
        IReadOnlyList<TradeListItem> initial =
            [CreateTradeListItem(symbol: "NQ")];
        IReadOnlyList<TradeListItem> authoritative =
        [
            CreateTradeListItem(symbol: "ES"),
            CreateTradeListItem(symbol: "YM"),
        ];
        fixture.TradeListReader.EnqueueResult(initial);
        fixture.TradeListReader.EnqueueResult(authoritative);
        await fixture.ViewModel.EnsureLoadedAsync();

        await fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.TradeStore.AddCallCount);
        Assert.Equal(2, fixture.TradeListReader.CallCount);
        Assert.Equal([50, 50], fixture.TradeListReader.RequestedLimits);
        Assert.Same(authoritative, fixture.ViewModel.RecentTrades);
        Assert.Collection(
            fixture.ViewModel.RecentTrades,
            trade => Assert.Equal("ES", trade.InstrumentSymbol),
            trade => Assert.Equal("YM", trade.InstrumentSymbol));
        Assert.Equal(1, fixture.ReferenceDataReader.CallCount);
        Assert.False(fixture.ViewModel.IsManualEntryVisible);
        AssertManualEntryFormReset(fixture.ViewModel);
        Assert.Equal(
            "Trade saved successfully.",
            fixture.ViewModel.SuccessMessage);
        Assert.Null(fixture.ViewModel.SaveErrorMessage);
        Assert.Null(fixture.ViewModel.TradeListErrorMessage);
    }

    [Fact]
    public async Task PostSaveReloadFailureRetainsRowsAndSuccessfulWriteOutcome()
    {
        ManualTradeSaveFixture fixture = CreateSaveFixture();
        IReadOnlyList<TradeListItem> initial =
            [CreateTradeListItem(symbol: "NQ")];
        fixture.TradeListReader.EnqueueResult(initial);
        fixture.TradeListReader.EnqueueException(
            new InvalidOperationException("Read failed."));
        await fixture.ViewModel.EnsureLoadedAsync();

        await fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.TradeStore.AddCallCount);
        Assert.Equal(2, fixture.TradeListReader.CallCount);
        Assert.Same(initial, fixture.ViewModel.RecentTrades);
        Assert.False(fixture.ViewModel.IsManualEntryVisible);
        AssertManualEntryFormReset(fixture.ViewModel);
        Assert.Equal(
            "Trade saved successfully.",
            fixture.ViewModel.SuccessMessage);
        Assert.Null(fixture.ViewModel.SaveErrorMessage);
        Assert.Equal(
            "Trades could not be loaded.",
            fixture.ViewModel.TradeListErrorMessage);
        Assert.False(fixture.ViewModel.IsSaving);
    }

    [Fact]
    public async Task ExplicitRefreshRecoversPostSaveReloadWithoutRepeatingWrite()
    {
        ManualTradeSaveFixture fixture = CreateSaveFixture();
        IReadOnlyList<TradeListItem> initial =
            [CreateTradeListItem(symbol: "NQ")];
        IReadOnlyList<TradeListItem> recovered =
            [CreateTradeListItem(symbol: "ES")];
        fixture.TradeListReader.EnqueueResult(initial);
        fixture.TradeListReader.EnqueueException(
            new InvalidOperationException("Read failed."));
        fixture.TradeListReader.EnqueueResult(recovered);
        await fixture.ViewModel.EnsureLoadedAsync();
        await fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);

        await fixture.ViewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.TradeStore.AddCallCount);
        Assert.Equal(3, fixture.TradeListReader.CallCount);
        Assert.Same(recovered, fixture.ViewModel.RecentTrades);
        Assert.Null(fixture.ViewModel.TradeListErrorMessage);
        Assert.Equal(
            "Trade saved successfully.",
            fixture.ViewModel.SuccessMessage);
    }

    [Fact]
    public async Task PostCommitCancellationDoesNotCancelAuthoritativeReload()
    {
        ManualTradeSaveFixture fixture = CreateSaveFixture();
        IReadOnlyList<TradeListItem> authoritative =
            [CreateTradeListItem(symbol: "ES")];
        fixture.TradeListReader.EnqueueResult(authoritative);
        fixture.TradeListReader.HoldRead = true;

        Task saveTask =
            fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);
        await fixture.TradeListReader.ReadStarted;
        fixture.ViewModel.SaveManualTradeCommand.Cancel();

        Assert.True(fixture.ViewModel.IsSaving);
        Assert.True(fixture.ViewModel.IsTradeListLoading);
        Assert.False(fixture.TradeListReader.CancellationToken.CanBeCanceled);

        fixture.TradeListReader.ReleaseRead();
        await saveTask;

        Assert.Equal(1, fixture.TradeStore.AddCallCount);
        Assert.Equal(1, fixture.TradeListReader.CallCount);
        Assert.Same(authoritative, fixture.ViewModel.RecentTrades);
        Assert.Equal(
            "Trade saved successfully.",
            fixture.ViewModel.SuccessMessage);
        Assert.Null(fixture.ViewModel.SaveErrorMessage);
        Assert.Null(fixture.ViewModel.TradeListErrorMessage);
        Assert.False(fixture.ViewModel.IsSaving);
    }

    [Fact]
    public async Task SaveManualTradeCommandPersistsClosedShortTrade()
    {
        ManualTradeSaveFixture fixture = CreateSaveFixture(
            hasExit: true,
            direction: TradeDirection.Short);

        await fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.TradeStore.AddCallCount);
        Trade trade = Assert.IsType<Trade>(fixture.TradeStore.AddedTrade);
        Assert.Equal(TradeDirection.Short, trade.Direction);
        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(0m, trade.OpenQuantity);
        Assert.Collection(
            trade.Executions,
            entry =>
            {
                Assert.Equal(ExecutionSide.Sell, entry.Side);
                Assert.Equal(2m, entry.Quantity);
                Assert.Equal(
                    new DateTimeOffset(
                        2026,
                        9,
                        10,
                        13,
                        30,
                        0,
                        TimeSpan.Zero),
                    entry.ExecutedAtUtc);
                Assert.Equal(23950.25m, entry.Price);
                Assert.Equal(1.50m, entry.Commission);
                Assert.Equal(0.25m, entry.Fees);
            },
            exit =>
            {
                Assert.Equal(ExecutionSide.Buy, exit.Side);
                Assert.Equal(2m, exit.Quantity);
                Assert.Equal(
                    new DateTimeOffset(
                        2026,
                        9,
                        10,
                        14,
                        15,
                        0,
                        TimeSpan.Zero),
                    exit.ExecutedAtUtc);
                Assert.Equal(23900m, exit.Price);
                Assert.Equal(1.50m, exit.Commission);
                Assert.Equal(0.25m, exit.Fees);
            });
    }

    [Fact]
    public async Task SaveManualTradeCommandUsesDomainInstrumentPricing()
    {
        ManualTradeSaveFixture fixture = CreateSaveFixture(
            selectorPointValue: 999m);
        Assert.Equal(
            999m,
            Assert.Single(fixture.ReferenceData.Instruments).PointValue);

        await fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);

        Trade trade = Assert.IsType<Trade>(fixture.TradeStore.AddedTrade);
        Assert.Equal(20m, trade.Pricing.PointValue);
    }

    [Fact]
    public async Task SaveManualTradeCommandPreservesDraftOnTechnicalFailure()
    {
        ManualTradeSaveFixture fixture = CreateSaveFixture(
            hasExit: true,
            direction: TradeDirection.Short);
        fixture.TradeStore.AddException =
            new InvalidOperationException("Database unavailable.");
        ManualTradeAccountOption selectedAccount =
            Assert.IsType<ManualTradeAccountOption>(
                fixture.ViewModel.SelectedAccount);
        ManualTradeInstrumentOption selectedInstrument =
            Assert.IsType<ManualTradeInstrumentOption>(
                fixture.ViewModel.SelectedInstrument);

        await fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.TradeStore.AddCallCount);
        Assert.True(fixture.ViewModel.IsManualEntryVisible);
        Assert.Same(selectedAccount, fixture.ViewModel.SelectedAccount);
        Assert.Same(selectedInstrument, fixture.ViewModel.SelectedInstrument);
        AssertRepresentativeTradeFacts(fixture.ViewModel, "2");
        Assert.Equal(
            "Trade could not be saved.",
            fixture.ViewModel.SaveErrorMessage);
        Assert.True(fixture.ViewModel.HasSaveError);
        Assert.Null(fixture.ViewModel.SuccessMessage);
        Assert.False(fixture.ViewModel.IsSaving);
        Assert.Equal(0, fixture.TradeListReader.CallCount);
    }

    [Fact]
    public async Task SaveManualTradeCommandReportsStaleReferenceAndPreservesDraft()
    {
        ManualTradeSaveFixture fixture = CreateSaveFixture(
            hasExit: true,
            direction: TradeDirection.Short);
        fixture.AccountStore.AccountToReturn = null;
        ManualTradeAccountOption selectedAccount =
            Assert.IsType<ManualTradeAccountOption>(
                fixture.ViewModel.SelectedAccount);
        ManualTradeInstrumentOption selectedInstrument =
            Assert.IsType<ManualTradeInstrumentOption>(
                fixture.ViewModel.SelectedInstrument);

        await fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);

        Assert.Equal(0, fixture.TradeStore.AddCallCount);
        Assert.True(fixture.ViewModel.IsManualEntryVisible);
        Assert.Same(selectedAccount, fixture.ViewModel.SelectedAccount);
        Assert.Same(selectedInstrument, fixture.ViewModel.SelectedInstrument);
        AssertRepresentativeTradeFacts(fixture.ViewModel, "2");
        Assert.Equal(
            "The selected trading account or instrument is no longer available. " +
            "Refresh the reference data and try again.",
            fixture.ViewModel.SaveErrorMessage);
        Assert.Null(fixture.ViewModel.SuccessMessage);
        Assert.False(fixture.ViewModel.IsSaving);
        Assert.Equal(0, fixture.TradeListReader.CallCount);
    }

    [Fact]
    public async Task SaveManualTradeCommandCanRetryAfterTechnicalFailure()
    {
        ManualTradeSaveFixture fixture = CreateSaveFixture();
        fixture.TradeStore.AddException =
            new InvalidOperationException("First attempt failed.");
        await fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);
        Assert.True(fixture.ViewModel.HasSaveError);
        fixture.TradeStore.AddException = null;

        await fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);

        Assert.Equal(2, fixture.TradeStore.AddCallCount);
        Assert.False(fixture.ViewModel.IsManualEntryVisible);
        Assert.Null(fixture.ViewModel.SaveErrorMessage);
        Assert.Equal(
            "Trade saved successfully.",
            fixture.ViewModel.SuccessMessage);
        AssertManualEntryFormReset(fixture.ViewModel);
    }

    [Fact]
    public async Task ShowManualEntryCommandClearsSuccessForNewDraft()
    {
        ManualTradeSaveFixture fixture = CreateSaveFixture();
        await fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);
        Assert.True(fixture.ViewModel.HasSuccessMessage);

        fixture.ViewModel.ShowManualEntryCommand.Execute(null);

        Assert.True(fixture.ViewModel.IsManualEntryVisible);
        Assert.Null(fixture.ViewModel.SuccessMessage);
        Assert.False(fixture.ViewModel.HasSuccessMessage);
        Assert.Null(fixture.ViewModel.SaveErrorMessage);
        Assert.Null(fixture.ViewModel.ValidationErrorMessage);
        AssertManualEntryFormReset(fixture.ViewModel);
    }

    [Fact]
    public async Task CancelManualEntryCommandClearsSaveErrorAndResetsDraft()
    {
        ManualTradeSaveFixture fixture = CreateSaveFixture(
            hasExit: true,
            direction: TradeDirection.Short);
        fixture.TradeStore.AddException =
            new InvalidOperationException("Save failed.");
        await fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);
        Assert.True(fixture.ViewModel.HasSaveError);

        fixture.ViewModel.CancelManualEntryCommand.Execute(null);

        Assert.False(fixture.ViewModel.IsManualEntryVisible);
        Assert.Null(fixture.ViewModel.SaveErrorMessage);
        Assert.Null(fixture.ViewModel.ValidationErrorMessage);
        AssertManualEntryFormReset(fixture.ViewModel);
    }

    [Fact]
    public void SaveManualTradeCommandEligibilityUsesFormAndOperationState()
    {
        TradesViewModel viewModel = CreateViewModel();

        Assert.False(viewModel.SaveManualTradeCommand.CanExecute(null));

        viewModel.ShowManualEntryCommand.Execute(null);

        Assert.True(viewModel.SaveManualTradeCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveManualTradeCommandIsDisabledWhileTradeListIsLoading()
    {
        var tradeListReader = new FakeTradeListReader
        {
            HoldRead = true,
        };
        TradesViewModel viewModel = CreateViewModel(
            tradeListReader: tradeListReader);
        viewModel.ShowManualEntryCommand.Execute(null);

        Task loadTask = viewModel.EnsureLoadedAsync();
        await tradeListReader.ReadStarted;

        Assert.True(viewModel.IsTradeListLoading);
        Assert.False(viewModel.SaveManualTradeCommand.CanExecute(null));

        tradeListReader.ReleaseRead();
        await loadTask;

        Assert.False(viewModel.IsTradeListLoading);
        Assert.True(viewModel.SaveManualTradeCommand.CanExecute(null));
    }

    [Fact]
    public async Task SavingDisablesCompetingCommandsUntilWriteCompletes()
    {
        ManualTradeSaveFixture fixture = CreateSaveFixture();
        fixture.TradeStore.HoldAdd = true;

        Task saveTask =
            fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);
        await fixture.TradeStore.AddStarted;

        Assert.True(fixture.ViewModel.IsSaving);
        Assert.False(fixture.ViewModel.RefreshCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.ShowManualEntryCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.CancelManualEntryCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.SaveManualTradeCommand.CanExecute(null));

        fixture.TradeStore.ReleaseAdd();
        await saveTask;

        Assert.False(fixture.ViewModel.IsSaving);
        Assert.Equal(
            "Trade saved successfully.",
            fixture.ViewModel.SuccessMessage);
    }

    [Fact]
    public async Task SaveCancellationPropagatesAndPreservesDraft()
    {
        ManualTradeSaveFixture fixture = CreateSaveFixture(
            hasExit: true,
            direction: TradeDirection.Short);
        fixture.TradeStore.HoldAdd = true;

        Task saveTask =
            fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);
        await fixture.TradeStore.AddStarted;
        fixture.ViewModel.SaveManualTradeCommand.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await saveTask);
        Assert.True(
            fixture.TradeStore.CancellationToken.IsCancellationRequested);
        Assert.True(fixture.ViewModel.IsManualEntryVisible);
        AssertRepresentativeTradeFacts(fixture.ViewModel, "2");
        Assert.Null(fixture.ViewModel.SaveErrorMessage);
        Assert.Null(fixture.ViewModel.SuccessMessage);
        Assert.False(fixture.ViewModel.IsSaving);
        Assert.Equal(0, fixture.TradeListReader.CallCount);
    }

    [Fact]
    public async Task EnsureLoadedAsyncPopulatesOptionsOnlyOnceAfterSuccess()
    {
        ManualTradeReferenceData referenceData = CreateReferenceData();
        var reader = new FakeManualTradeReferenceDataReader();
        reader.EnqueueResult(referenceData);
        var viewModel = CreateViewModel(reader);

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
    public async Task EnsureLoadedAsyncPopulatesRecentTradesOnceWithPresentationLimit()
    {
        IReadOnlyList<TradeListItem> trades = [CreateTradeListItem()];
        var tradeListReader = new FakeTradeListReader();
        tradeListReader.EnqueueResult(trades);
        var viewModel = CreateViewModel(tradeListReader: tradeListReader);

        await viewModel.EnsureLoadedAsync();
        await viewModel.EnsureLoadedAsync();

        Assert.Same(trades, viewModel.RecentTrades);
        Assert.True(viewModel.HasTrades);
        Assert.Null(viewModel.TradeListErrorMessage);
        Assert.False(viewModel.HasTradeListError);
        Assert.Equal(1, tradeListReader.CallCount);
        Assert.Equal([50], tradeListReader.RequestedLimits);
    }

    [Fact]
    public async Task EnsureLoadedAsyncPreservesAuthoritativeReaderOrder()
    {
        TradeListItem tradeB = CreateTradeListItem(symbol: "ES");
        TradeListItem tradeA = CreateTradeListItem(symbol: "NQ");
        var tradeListReader = new FakeTradeListReader();
        tradeListReader.EnqueueResult([tradeB, tradeA]);
        var viewModel = CreateViewModel(tradeListReader: tradeListReader);

        await viewModel.EnsureLoadedAsync();

        Assert.Equal(
            [tradeB.Id, tradeA.Id],
            viewModel.RecentTrades.Select(trade => trade.Id));
    }

    [Fact]
    public async Task NormalLoadsAlwaysRequestActiveReferencesOnly()
    {
        var reader = new FakeManualTradeReferenceDataReader();
        reader.EnqueueResult(CreateReferenceData());
        reader.EnqueueResult(CreateReferenceData());
        var viewModel = CreateViewModel(reader);

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
        var viewModel = CreateViewModel(reader);
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
        var viewModel = CreateViewModel(reader);
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
        var viewModel = CreateViewModel(reader);
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
        var viewModel = CreateViewModel(reader);
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
        var viewModel = CreateViewModel(reader);
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
        var viewModel = CreateViewModel(reader);
        await viewModel.EnsureLoadedAsync();
        viewModel.SelectedAccount = Assert.Single(initial.Accounts);
        viewModel.SelectedInstrument = Assert.Single(initial.Instruments);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Null(viewModel.SelectedAccount);
        Assert.Null(viewModel.SelectedInstrument);
        Assert.False(viewModel.HasReferenceData);
    }

    [Fact]
    public async Task RefreshReplacesRecentTrades()
    {
        IReadOnlyList<TradeListItem> initial = [CreateTradeListItem(symbol: "NQ")];
        IReadOnlyList<TradeListItem> refreshed = [CreateTradeListItem(symbol: "ES")];
        var tradeListReader = new FakeTradeListReader();
        tradeListReader.EnqueueResult(initial);
        tradeListReader.EnqueueResult(refreshed);
        var viewModel = CreateViewModel(tradeListReader: tradeListReader);
        await viewModel.EnsureLoadedAsync();

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Same(refreshed, viewModel.RecentTrades);
        Assert.Equal([50, 50], tradeListReader.RequestedLimits);
    }

    [Fact]
    public async Task TradeListRefreshFailureRetainsRowsAndShowsSafeError()
    {
        IReadOnlyList<TradeListItem> initial = [CreateTradeListItem()];
        var tradeListReader = new FakeTradeListReader();
        tradeListReader.EnqueueResult(initial);
        tradeListReader.EnqueueException(new InvalidOperationException("Technical details."));
        var viewModel = CreateViewModel(tradeListReader: tradeListReader);
        await viewModel.EnsureLoadedAsync();

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Same(initial, viewModel.RecentTrades);
        Assert.Equal("Trades could not be loaded.", viewModel.TradeListErrorMessage);
        Assert.True(viewModel.HasTradeListError);
    }

    [Fact]
    public async Task SuccessfulTradeListRetryClearsErrorAndReplacesRows()
    {
        IReadOnlyList<TradeListItem> initial = [CreateTradeListItem(symbol: "NQ")];
        IReadOnlyList<TradeListItem> recovered = [CreateTradeListItem(symbol: "ES")];
        var tradeListReader = new FakeTradeListReader();
        tradeListReader.EnqueueResult(initial);
        tradeListReader.EnqueueException(new InvalidOperationException("Read failed."));
        tradeListReader.EnqueueResult(recovered);
        var viewModel = CreateViewModel(tradeListReader: tradeListReader);
        await viewModel.EnsureLoadedAsync();
        await viewModel.RefreshCommand.ExecuteAsync(null);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Same(recovered, viewModel.RecentTrades);
        Assert.Null(viewModel.TradeListErrorMessage);
        Assert.False(viewModel.HasTradeListError);
    }

    [Fact]
    public async Task SuccessfulEmptyTradeListIsNotAnError()
    {
        var tradeListReader = new FakeTradeListReader();
        tradeListReader.EnqueueResult([]);
        var viewModel = CreateViewModel(tradeListReader: tradeListReader);

        await viewModel.EnsureLoadedAsync();

        Assert.Empty(viewModel.RecentTrades);
        Assert.False(viewModel.HasTrades);
        Assert.Null(viewModel.TradeListErrorMessage);
        Assert.False(viewModel.HasTradeListError);
    }

    [Fact]
    public async Task ReferenceRefreshFailureDoesNotDiscardSuccessfulTradeListRefresh()
    {
        ManualTradeReferenceData initialReferences = CreateReferenceData();
        IReadOnlyList<TradeListItem> initialTrades = [CreateTradeListItem(symbol: "NQ")];
        IReadOnlyList<TradeListItem> refreshedTrades = [CreateTradeListItem(symbol: "ES")];
        var referenceReader = new FakeManualTradeReferenceDataReader();
        referenceReader.EnqueueResult(initialReferences);
        referenceReader.EnqueueException(new InvalidOperationException("Reference read failed."));
        var tradeListReader = new FakeTradeListReader();
        tradeListReader.EnqueueResult(initialTrades);
        tradeListReader.EnqueueResult(refreshedTrades);
        var viewModel = CreateViewModel(referenceReader, tradeListReader);
        await viewModel.EnsureLoadedAsync();

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Same(refreshedTrades, viewModel.RecentTrades);
        Assert.Same(initialReferences.Accounts, viewModel.AccountOptions);
        Assert.Same(initialReferences.Instruments, viewModel.InstrumentOptions);
        Assert.Equal("Trade reference data could not be loaded.", viewModel.ErrorMessage);
        Assert.Null(viewModel.TradeListErrorMessage);
    }

    [Fact]
    public async Task TradeListRefreshFailureDoesNotDiscardSuccessfulReferenceRefresh()
    {
        ManualTradeReferenceData initialReferences = CreateReferenceData();
        ManualTradeReferenceData refreshedReferences = CreateReferenceData(
            accountName: "Refreshed Account",
            instrumentSymbol: "ES");
        IReadOnlyList<TradeListItem> initialTrades = [CreateTradeListItem()];
        var referenceReader = new FakeManualTradeReferenceDataReader();
        referenceReader.EnqueueResult(initialReferences);
        referenceReader.EnqueueResult(refreshedReferences);
        var tradeListReader = new FakeTradeListReader();
        tradeListReader.EnqueueResult(initialTrades);
        tradeListReader.EnqueueException(new InvalidOperationException("Trade read failed."));
        var viewModel = CreateViewModel(referenceReader, tradeListReader);
        await viewModel.EnsureLoadedAsync();

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Same(refreshedReferences.Accounts, viewModel.AccountOptions);
        Assert.Same(refreshedReferences.Instruments, viewModel.InstrumentOptions);
        Assert.Same(initialTrades, viewModel.RecentTrades);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal("Trades could not be loaded.", viewModel.TradeListErrorMessage);
    }

    [Fact]
    public async Task ShowTradeDetailLoadsExactAuthoritativeReaderResult()
    {
        TradeListItem listItem = CreateTradeListItem();
        TradeDetail detail = CreateTradeDetail(listItem);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(detail);
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader);

        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);

        Assert.Equal(1, detailReader.CallCount);
        Assert.Equal([listItem.Id], detailReader.RequestedTradeIds);
        Assert.True(viewModel.IsTradeDetailVisible);
        Assert.Same(detail, viewModel.SelectedTradeDetail);
        Assert.False(viewModel.IsTradeDetailNotFound);
        Assert.Null(viewModel.TradeDetailErrorMessage);
        Assert.False(viewModel.IsTradeDetailLoading);
    }

    [Fact]
    public async Task ShowTradeDetailDoesNotExecuteForNullItem()
    {
        var detailReader = new FakeTradeDetailReader();
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader);

        Assert.False(viewModel.ShowTradeDetailCommand.CanExecute(null));

        await viewModel.ShowTradeDetailCommand.ExecuteAsync(null);

        Assert.Equal(0, detailReader.CallCount);
        Assert.False(viewModel.IsTradeDetailVisible);
    }

    [Fact]
    public async Task MissingTradeShowsNotFoundWithoutChangingRecentTrades()
    {
        TradeListItem listItem = CreateTradeListItem();
        IReadOnlyList<TradeListItem> recentTrades = [listItem];
        var listReader = new FakeTradeListReader();
        listReader.EnqueueResult(recentTrades);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(null);
        TradesViewModel viewModel = CreateViewModel(
            tradeListReader: listReader,
            tradeDetailReader: detailReader);
        await viewModel.EnsureLoadedAsync();

        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);

        Assert.True(viewModel.IsTradeDetailVisible);
        Assert.Null(viewModel.SelectedTradeDetail);
        Assert.True(viewModel.IsTradeDetailNotFound);
        Assert.Null(viewModel.TradeDetailErrorMessage);
        Assert.Same(recentTrades, viewModel.RecentTrades);
        Assert.Null(viewModel.TradeListErrorMessage);
    }

    [Fact]
    public async Task TechnicalDetailFailureShowsSafeIndependentError()
    {
        TradeListItem listItem = CreateTradeListItem();
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueException(
            new InvalidOperationException("Database unavailable."));
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader);

        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);

        Assert.True(viewModel.IsTradeDetailVisible);
        Assert.Null(viewModel.SelectedTradeDetail);
        Assert.False(viewModel.IsTradeDetailNotFound);
        Assert.Equal(
            "Trade details could not be loaded.",
            viewModel.TradeDetailErrorMessage);
        Assert.DoesNotContain(
            "Database unavailable.",
            viewModel.TradeDetailErrorMessage,
            StringComparison.Ordinal);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Null(viewModel.TradeListErrorMessage);
        Assert.Null(viewModel.ValidationErrorMessage);
        Assert.Null(viewModel.SaveErrorMessage);
        Assert.Null(viewModel.SuccessMessage);
        Assert.False(viewModel.IsTradeDetailLoading);
    }

    [Fact]
    public async Task SwitchingTradeClearsPreviouslySelectedDetail()
    {
        TradeListItem firstItem = CreateTradeListItem(symbol: "NQ");
        TradeListItem secondItem = CreateTradeListItem(symbol: "ES");
        TradeDetail firstDetail = CreateTradeDetail(firstItem);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(firstDetail);
        detailReader.EnqueueResult(null);
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader);
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(firstItem);
        Assert.Same(firstDetail, viewModel.SelectedTradeDetail);

        await viewModel.ShowTradeDetailCommand.ExecuteAsync(secondItem);

        Assert.Equal(
            [firstItem.Id, secondItem.Id],
            detailReader.RequestedTradeIds);
        Assert.Null(viewModel.SelectedTradeDetail);
        Assert.True(viewModel.IsTradeDetailNotFound);
    }

    [Fact]
    public async Task CloseTradeDetailClearsDetailStateAndPreservesRecentTrades()
    {
        TradeListItem listItem = CreateTradeListItem();
        IReadOnlyList<TradeListItem> recentTrades = [listItem];
        TradeDetail detail = CreateTradeDetail(listItem);
        var listReader = new FakeTradeListReader();
        listReader.EnqueueResult(recentTrades);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(detail);
        TradesViewModel viewModel = CreateViewModel(
            tradeListReader: listReader,
            tradeDetailReader: detailReader);
        await viewModel.EnsureLoadedAsync();
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);

        viewModel.CloseTradeDetailCommand.Execute(null);

        Assert.False(viewModel.IsTradeDetailVisible);
        Assert.Null(viewModel.SelectedTradeDetail);
        Assert.Null(viewModel.TradeDetailErrorMessage);
        Assert.False(viewModel.IsTradeDetailNotFound);
        Assert.Same(recentTrades, viewModel.RecentTrades);
    }

    [Fact]
    public async Task DetailLoadingDisablesOnlyCompetingOperations()
    {
        TradeListItem listItem = CreateTradeListItem();
        TradeDetail detail = CreateTradeDetail(listItem);
        var detailReader = new FakeTradeDetailReader
        {
            HoldRead = true,
        };
        detailReader.EnqueueResult(detail);
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader);

        Task detailTask =
            viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);
        await detailReader.ReadStarted;

        Assert.True(viewModel.IsTradeDetailLoading);
        Assert.False(viewModel.ShowTradeDetailCommand.CanExecute(listItem));
        Assert.False(viewModel.CloseTradeDetailCommand.CanExecute(null));
        Assert.False(viewModel.RefreshCommand.CanExecute(null));
        Assert.True(viewModel.ShowManualEntryCommand.CanExecute(null));
        viewModel.ShowManualEntryCommand.Execute(null);
        Assert.False(viewModel.SaveManualTradeCommand.CanExecute(null));

        detailReader.ReleaseRead();
        await detailTask;

        Assert.False(viewModel.IsTradeDetailLoading);
        Assert.True(viewModel.ShowTradeDetailCommand.CanExecute(listItem));
        Assert.True(viewModel.CloseTradeDetailCommand.CanExecute(null));
        Assert.True(viewModel.RefreshCommand.CanExecute(null));
        Assert.True(viewModel.SaveManualTradeCommand.CanExecute(null));
    }

    [Fact]
    public async Task DetailCancellationPropagatesWithoutMutatingOtherState()
    {
        TradeListItem listItem = CreateTradeListItem();
        IReadOnlyList<TradeListItem> recentTrades = [listItem];
        var listReader = new FakeTradeListReader();
        listReader.EnqueueResult(recentTrades);
        var detailReader = new FakeTradeDetailReader
        {
            HoldRead = true,
        };
        TradesViewModel viewModel = CreateViewModel(
            tradeListReader: listReader,
            tradeDetailReader: detailReader);
        await viewModel.EnsureLoadedAsync();

        Task detailTask =
            viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);
        await detailReader.ReadStarted;
        viewModel.ShowTradeDetailCommand.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await detailTask);
        Assert.True(detailReader.CancellationToken.IsCancellationRequested);
        Assert.True(viewModel.IsTradeDetailVisible);
        Assert.Null(viewModel.SelectedTradeDetail);
        Assert.Null(viewModel.TradeDetailErrorMessage);
        Assert.False(viewModel.IsTradeDetailNotFound);
        Assert.False(viewModel.IsTradeDetailLoading);
        Assert.Same(recentTrades, viewModel.RecentTrades);
        Assert.Null(viewModel.TradeListErrorMessage);
    }

    [Fact]
    public async Task DetailLoadingDoesNotMutateManualEntryDraft()
    {
        ManualTradeReferenceData referenceData = CreateReferenceData();
        var selectedAccount = Assert.Single(referenceData.Accounts);
        var selectedInstrument = Assert.Single(referenceData.Instruments);
        TradeListItem listItem = CreateTradeListItem();
        TradeDetail detail = CreateTradeDetail(listItem);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(detail);
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader);
        viewModel.ShowManualEntryCommand.Execute(null);
        Assert.False(viewModel.TryBuildManualTradeCommand(out _));
        string validationError = Assert.IsType<string>(
            viewModel.ValidationErrorMessage);
        viewModel.SelectedAccount = selectedAccount;
        viewModel.SelectedInstrument = selectedInstrument;
        PopulateRepresentativeTradeFacts(viewModel);

        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);

        Assert.True(viewModel.IsManualEntryVisible);
        Assert.Same(selectedAccount, viewModel.SelectedAccount);
        Assert.Same(selectedInstrument, viewModel.SelectedInstrument);
        AssertRepresentativeTradeFacts(viewModel);
        Assert.Equal(validationError, viewModel.ValidationErrorMessage);
        Assert.Same(detail, viewModel.SelectedTradeDetail);
    }

    [Fact]
    public void CancelClosesShellAndResetsEntireManualEntryForm()
    {
        ManualTradeReferenceData referenceData = CreateReferenceData();
        TradesViewModel viewModel =
            CreateViewModel(new FakeManualTradeReferenceDataReader());
        viewModel.SelectedAccount = Assert.Single(referenceData.Accounts);
        viewModel.SelectedInstrument = Assert.Single(referenceData.Instruments);

        viewModel.ShowManualEntryCommand.Execute(null);
        PopulateRepresentativeTradeFacts(viewModel);
        viewModel.QuantityText = "invalid";
        Assert.False(viewModel.TryBuildManualTradeCommand(out _));
        Assert.True(viewModel.HasValidationError);

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
        Assert.Null(viewModel.ValidationErrorMessage);
        Assert.False(viewModel.HasValidationError);
    }

    [Fact]
    public void ManualEntryCommandsReflectShellVisibility()
    {
        var viewModel = CreateViewModel(new FakeManualTradeReferenceDataReader());

        Assert.True(viewModel.ShowManualEntryCommand.CanExecute(null));
        Assert.False(viewModel.CancelManualEntryCommand.CanExecute(null));

        viewModel.ShowManualEntryCommand.Execute(null);

        Assert.False(viewModel.ShowManualEntryCommand.CanExecute(null));
        Assert.True(viewModel.CancelManualEntryCommand.CanExecute(null));
    }

    [Fact]
    public async Task CloseActionIsAvailableOnlyForSelectedOpenTrade()
    {
        CloseTradeFixture openFixture = await CreateCloseTradeFixtureAsync();

        Assert.True(openFixture.ViewModel.IsSelectedTradeOpen);
        Assert.True(openFixture.ViewModel.ShowCloseTradeCommand.CanExecute(null));

        TradeListItem closedItem = CreateTradeListItem();
        TradeDetail closedDetail = CreateTradeDetail(closedItem);
        var closedReader = new FakeTradeDetailReader();
        closedReader.EnqueueResult(closedDetail);
        TradesViewModel closedViewModel = CreateViewModel(
            tradeDetailReader: closedReader);
        await closedViewModel.ShowTradeDetailCommand.ExecuteAsync(closedItem);

        Assert.False(closedViewModel.IsSelectedTradeOpen);
        Assert.False(closedViewModel.ShowCloseTradeCommand.CanExecute(null));
        closedViewModel.ShowCloseTradeCommand.Execute(null);
        Assert.False(closedViewModel.IsCloseTradeVisible);
    }

    [Fact]
    public async Task ShowAndCancelCloseTradeUseIndependentDraftAndPreserveContext()
    {
        var screenshot = new TradeScreenshotListItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TradeScreenshotType.Entry,
            "entry.png",
            null,
            null,
            null,
            FixedTimeProvider.FixedUtcNow);
        CloseTradeFixture fixture = await CreateCloseTradeFixtureAsync(
            screenshotFactory: tradeId => screenshot with { TradeId = tradeId });
        fixture.ViewModel.QuantityText = "manual draft";

        fixture.ViewModel.ShowCloseTradeCommand.Execute(null);

        Assert.True(fixture.ViewModel.IsCloseTradeVisible);
        Assert.Equal(string.Empty, fixture.ViewModel.CloseTradeExecutedAtUtcText);
        Assert.Equal(string.Empty, fixture.ViewModel.CloseTradePriceText);
        Assert.Equal("0", fixture.ViewModel.CloseTradeCommissionText);
        Assert.Equal("0", fixture.ViewModel.CloseTradeFeesText);
        Assert.Null(fixture.ViewModel.CloseTradeValidationErrorMessage);
        Assert.Null(fixture.ViewModel.CloseTradeSaveErrorMessage);
        Assert.Null(fixture.ViewModel.CloseTradeSuccessMessage);

        fixture.ViewModel.CloseTradeExecutedAtUtcText = "draft close";
        fixture.ViewModel.CloseTradePriceText = "101";
        fixture.ViewModel.CancelCloseTradeCommand.Execute(null);

        Assert.False(fixture.ViewModel.IsCloseTradeVisible);
        Assert.Same(fixture.OpenDetail, fixture.ViewModel.SelectedTradeDetail);
        Assert.Same(fixture.InitialTrades, fixture.ViewModel.RecentTrades);
        Assert.Same(fixture.Screenshots, fixture.ViewModel.TradeScreenshots);
        Assert.Equal("manual draft", fixture.ViewModel.QuantityText);
        Assert.Equal(string.Empty, fixture.ViewModel.CloseTradeExecutedAtUtcText);
        Assert.Equal(string.Empty, fixture.ViewModel.CloseTradePriceText);
    }

    [Fact]
    public async Task CloseCommandContainsNoQuantityAndUsesOpenQuantityAsReadOnlyContext()
    {
        CloseTradeFixture fixture = await CreateCloseTradeFixtureAsync(
            openQuantity: 2.75m);
        ShowValidCloseForm(fixture.ViewModel);

        bool succeeded = fixture.ViewModel.TryBuildCloseManualTradeCommand(
            out CloseManualTradeCommand? command);

        Assert.True(succeeded);
        Assert.NotNull(command);
        Assert.Equal(fixture.OpenDetail.Id, command.TradeId);
        Assert.Equal(2.75m, fixture.ViewModel.SelectedTradeDetail!.OpenQuantity);
        Assert.Null(typeof(CloseManualTradeCommand).GetProperty("Quantity"));
        Assert.Null(typeof(TradesViewModel).GetProperty("CloseTradeQuantityText"));
    }

    [Theory]
    [InlineData("2026-09-08 14:15:00")]
    [InlineData("2026-09-08 14:15")]
    [InlineData("2026-09-08T14:15:00Z")]
    [InlineData("2026-09-08T14:15Z")]
    public async Task CloseCommandUsesStrictSupportedUtcFormats(string timestamp)
    {
        CloseTradeFixture fixture = await CreateCloseTradeFixtureAsync();
        ShowValidCloseForm(fixture.ViewModel);
        fixture.ViewModel.CloseTradeExecutedAtUtcText = timestamp;

        bool succeeded = fixture.ViewModel.TryBuildCloseManualTradeCommand(
            out CloseManualTradeCommand? command);

        Assert.True(succeeded);
        Assert.NotNull(command);
        Assert.Equal(TimeSpan.Zero, command.ExecutedAtUtc.Offset);
        Assert.Equal(101.25m, command.Price);
        Assert.Equal(1.50m, command.Commission);
        Assert.Equal(0.25m, command.Fees);
    }

    [Theory]
    [InlineData("timestamp", "2026-09-08T14:15:00+03:00")]
    [InlineData("price", "not-a-price")]
    [InlineData("commission", "-0.01")]
    [InlineData("fees", "-0.01")]
    public async Task InvalidCloseInputDoesNotCallUseCase(
        string field,
        string value)
    {
        CloseTradeFixture fixture = await CreateCloseTradeFixtureAsync();
        ShowValidCloseForm(fixture.ViewModel);
        switch (field)
        {
            case "timestamp":
                fixture.ViewModel.CloseTradeExecutedAtUtcText = value;
                break;
            case "price":
                fixture.ViewModel.CloseTradePriceText = value;
                break;
            case "commission":
                fixture.ViewModel.CloseTradeCommissionText = value;
                break;
            case "fees":
                fixture.ViewModel.CloseTradeFeesText = value;
                break;
        }

        await fixture.ViewModel.SaveCloseTradeCommand.ExecuteAsync(null);

        Assert.True(fixture.ViewModel.HasCloseTradeValidationError);
        Assert.Equal(0, fixture.MutationStore.GetCallCount);
        Assert.Equal(0, fixture.MutationStore.SaveCallCount);
    }

    [Fact]
    public async Task SuccessfulCloseReloadsAuthoritativeDetailAndListWithoutFabrication()
    {
        CloseTradeFixture fixture = await CreateCloseTradeFixtureAsync(
            screenshotFactory: tradeId => new TradeScreenshotListItem(
                Guid.NewGuid(),
                tradeId,
                TradeScreenshotType.Entry,
                "entry.png",
                null,
                "5m",
                "Entry context",
                FixedTimeProvider.FixedUtcNow));
        fixture.ViewModel.QuantityText = "manual draft";
        ShowValidCloseForm(fixture.ViewModel);
        TradeListItem authoritativeList = fixture.OpenListItem with
        {
            Status = TradeStatus.Closed,
            ClosedAtUtc = CloseExecutedAtUtc,
            OpenQuantity = 0m,
            AverageExitPrice = 101.25m,
            TotalCosts = 3.25m,
            GrossPnL = 62.5m,
            NetPnL = 59.25m,
        };
        TradeDetail authoritativeDetail = fixture.OpenDetail with
        {
            Status = TradeStatus.Closed,
            ClosedAtUtc = CloseExecutedAtUtc,
            OpenQuantity = 0m,
            AverageExitPrice = 101.25m,
            TotalCosts = 3.25m,
            GrossPnL = 62.5m,
            NetPnL = 59.25m,
            Executions =
            [
                .. fixture.OpenDetail.Executions,
                new TradeExecutionDetailItem(
                    Guid.NewGuid(),
                    2,
                    CloseExecutedAtUtc,
                    ExecutionSide.Sell,
                    fixture.OpenDetail.OpenQuantity,
                    101.25m,
                    1.50m,
                    0.25m,
                    1.75m,
                    null,
                    null,
                    null),
            ],
        };
        fixture.DetailReader.EnqueueResult(authoritativeDetail);
        var listReloadStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseListReload = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken reloadToken = new(canceled: true);
        fixture.ListReader.EnqueueBehavior(async cancellationToken =>
        {
            reloadToken = cancellationToken;
            listReloadStarted.TrySetResult(true);
            await releaseListReload.Task;
            return [authoritativeList];
        });

        Task closeTask = fixture.ViewModel.SaveCloseTradeCommand.ExecuteAsync(null);
        await listReloadStarted.Task;

        Assert.Same(fixture.InitialTrades, fixture.ViewModel.RecentTrades);
        Assert.Same(authoritativeDetail, fixture.ViewModel.SelectedTradeDetail);
        Assert.False(reloadToken.CanBeCanceled);
        Assert.False(fixture.DetailReader.CancellationToken.CanBeCanceled);
        releaseListReload.TrySetResult(true);
        await closeTask;

        Assert.Equal(1, fixture.MutationStore.GetCallCount);
        Assert.Equal(1, fixture.MutationStore.SaveCallCount);
        Assert.Equal(fixture.OpenDetail.Id, fixture.MutationStore.RequestedTradeId);
        Assert.Equal(TradeStatus.Closed, fixture.MutationStore.SavedTrade!.Status);
        Assert.Equal(0m, fixture.MutationStore.SavedTrade.OpenQuantity);
        Assert.False(fixture.ViewModel.IsCloseTradeVisible);
        Assert.Equal("Trade closed successfully.", fixture.ViewModel.CloseTradeSuccessMessage);
        Assert.Null(fixture.ViewModel.CloseTradeSaveErrorMessage);
        Assert.Same(authoritativeDetail, fixture.ViewModel.SelectedTradeDetail);
        Assert.Equal([authoritativeList], fixture.ViewModel.RecentTrades);
        Assert.Equal(2, fixture.DetailReader.CallCount);
        Assert.Equal(2, fixture.ListReader.CallCount);
        Assert.Equal(1, fixture.ScreenshotReader.CallCount);
        Assert.Same(fixture.Screenshots, fixture.ViewModel.TradeScreenshots);
        Assert.Equal("manual draft", fixture.ViewModel.QuantityText);
    }

    [Fact]
    public async Task DetailReloadFailureKeepsCommittedCloseSuccessfulAndClearsStaleDetail()
    {
        CloseTradeFixture fixture = await CreateCloseTradeFixtureAsync();
        ShowValidCloseForm(fixture.ViewModel);
        fixture.DetailReader.EnqueueException(
            new InvalidOperationException("sensitive database detail"));
        fixture.ListReader.EnqueueResult(
            [fixture.OpenListItem with { Status = TradeStatus.Closed }]);

        await fixture.ViewModel.SaveCloseTradeCommand.ExecuteAsync(null);

        Assert.Equal("Trade closed successfully.", fixture.ViewModel.CloseTradeSuccessMessage);
        Assert.Null(fixture.ViewModel.CloseTradeSaveErrorMessage);
        Assert.Null(fixture.ViewModel.SelectedTradeDetail);
        Assert.Equal(
            "Trade details could not be loaded.",
            fixture.ViewModel.TradeDetailErrorMessage);
        Assert.DoesNotContain(
            "sensitive",
            fixture.ViewModel.TradeDetailErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListReloadFailureKeepsCommittedCloseSuccessfulAndExistingList()
    {
        CloseTradeFixture fixture = await CreateCloseTradeFixtureAsync();
        ShowValidCloseForm(fixture.ViewModel);
        TradeDetail authoritativeDetail = fixture.OpenDetail with
        {
            Status = TradeStatus.Closed,
            ClosedAtUtc = CloseExecutedAtUtc,
            OpenQuantity = 0m,
        };
        fixture.DetailReader.EnqueueResult(authoritativeDetail);
        fixture.ListReader.EnqueueException(
            new InvalidOperationException("sensitive database detail"));

        await fixture.ViewModel.SaveCloseTradeCommand.ExecuteAsync(null);

        Assert.Equal("Trade closed successfully.", fixture.ViewModel.CloseTradeSuccessMessage);
        Assert.Null(fixture.ViewModel.CloseTradeSaveErrorMessage);
        Assert.Same(authoritativeDetail, fixture.ViewModel.SelectedTradeDetail);
        Assert.Same(fixture.InitialTrades, fixture.ViewModel.RecentTrades);
        Assert.Equal("Trades could not be loaded.", fixture.ViewModel.TradeListErrorMessage);
    }

    [Theory]
    [InlineData("missing", "The selected trade is no longer available.")]
    [InlineData("closed", "The trade is already closed.")]
    [InlineData("chronology", "Exit time cannot be earlier than the latest execution.")]
    [InlineData("technical", "Trade could not be closed.")]
    public async Task CloseFailuresUseSafeSpecificMessages(
        string failure,
        string expectedMessage)
    {
        CloseTradeFixture fixture = await CreateCloseTradeFixtureAsync();
        ShowValidCloseForm(fixture.ViewModel);
        switch (failure)
        {
            case "missing":
                fixture.MutationStore.TradeToReturn = null;
                break;
            case "closed":
                fixture.MutationStore.TradeToReturn = CreateClosedDomainTrade(
                    fixture.OpenListItem.Id,
                    fixture.OpenDetail.OpenQuantity);
                break;
            case "chronology":
                fixture.ViewModel.CloseTradeExecutedAtUtcText =
                    "2026-09-08 09:00:00";
                break;
            case "technical":
                fixture.MutationStore.GetException =
                    new IOException("sensitive database detail");
                break;
        }

        await fixture.ViewModel.SaveCloseTradeCommand.ExecuteAsync(null);

        Assert.Equal(expectedMessage, fixture.ViewModel.CloseTradeSaveErrorMessage);
        Assert.DoesNotContain(
            "sensitive",
            fixture.ViewModel.CloseTradeSaveErrorMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(fixture.ViewModel.IsCloseTradeVisible);
        Assert.Same(fixture.OpenDetail, fixture.ViewModel.SelectedTradeDetail);
        Assert.Equal(0, fixture.DetailReader.CallCount - 1);
        Assert.Equal(1, fixture.ListReader.CallCount);
    }

    [Fact]
    public async Task CloseCancellationPropagatesAndPreservesAllPresentationState()
    {
        CloseTradeFixture fixture = await CreateCloseTradeFixtureAsync(
            screenshotFactory: tradeId => new TradeScreenshotListItem(
                Guid.NewGuid(),
                tradeId,
                TradeScreenshotType.Entry,
                "entry.png",
                null,
                null,
                null,
                FixedTimeProvider.FixedUtcNow));
        fixture.MutationStore.HoldSave = true;
        fixture.ViewModel.QuantityText = "manual draft";
        ShowValidCloseForm(fixture.ViewModel);

        Task closeTask = fixture.ViewModel.SaveCloseTradeCommand.ExecuteAsync(null);
        await fixture.MutationStore.SaveStarted;
        fixture.ViewModel.SaveCloseTradeCommand.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await closeTask);
        Assert.True(fixture.MutationStore.SaveCancellationToken.IsCancellationRequested);
        Assert.True(fixture.ViewModel.IsCloseTradeVisible);
        Assert.Equal("2026-09-08 14:15:00", fixture.ViewModel.CloseTradeExecutedAtUtcText);
        Assert.Same(fixture.OpenDetail, fixture.ViewModel.SelectedTradeDetail);
        Assert.Same(fixture.InitialTrades, fixture.ViewModel.RecentTrades);
        Assert.Same(fixture.Screenshots, fixture.ViewModel.TradeScreenshots);
        Assert.Equal("manual draft", fixture.ViewModel.QuantityText);
        Assert.Null(fixture.ViewModel.CloseTradeSaveErrorMessage);
        Assert.Null(fixture.ViewModel.CloseTradeSuccessMessage);
        Assert.False(fixture.ViewModel.IsClosingTrade);
        Assert.Equal(1, fixture.DetailReader.CallCount);
        Assert.Equal(1, fixture.ListReader.CallCount);
        Assert.Equal(1, fixture.ScreenshotReader.CallCount);
    }

    [Fact]
    public async Task ClosingTradeDisablesCompetingContextAndMutationCommands()
    {
        CloseTradeFixture fixture = await CreateCloseTradeFixtureAsync(
            screenshotFactory: tradeId => new TradeScreenshotListItem(
                Guid.NewGuid(),
                tradeId,
                TradeScreenshotType.Entry,
                "entry.png",
                null,
                null,
                null,
                FixedTimeProvider.FixedUtcNow));
        fixture.MutationStore.HoldSave = true;
        fixture.ViewModel.ShowManualEntryCommand.Execute(null);
        fixture.ViewModel.ShowAddScreenshotCommand.Execute(null);
        ShowValidCloseForm(fixture.ViewModel);
        TradeScreenshotListItem screenshot = Assert.Single(
            fixture.ViewModel.TradeScreenshots);

        Assert.True(fixture.ViewModel.RefreshCommand.CanExecute(null));
        Assert.True(fixture.ViewModel.SaveManualTradeCommand.CanExecute(null));
        Assert.True(fixture.ViewModel.SaveScreenshotCommand.CanExecute(null));
        Assert.True(fixture.ViewModel.DeleteScreenshotCommand.CanExecute(screenshot));
        Task closeTask = fixture.ViewModel.SaveCloseTradeCommand.ExecuteAsync(null);
        await fixture.MutationStore.SaveStarted;

        Assert.True(fixture.ViewModel.IsClosingTrade);
        Assert.False(fixture.ViewModel.SaveCloseTradeCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.CancelCloseTradeCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.RefreshCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.ShowTradeDetailCommand.CanExecute(fixture.OpenListItem));
        Assert.False(fixture.ViewModel.CloseTradeDetailCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.SaveManualTradeCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.ShowAddScreenshotCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.CancelAddScreenshotCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.SaveScreenshotCommand.CanExecute(null));
        Assert.False(fixture.ViewModel.DeleteScreenshotCommand.CanExecute(screenshot));
        Assert.False(fixture.ViewModel.OpenScreenshotPreviewCommand.CanExecute(screenshot));

        fixture.DetailReader.EnqueueResult(fixture.OpenDetail);
        fixture.ListReader.EnqueueResult(fixture.InitialTrades);
        fixture.MutationStore.ReleaseSave();
        await closeTask;
    }

    [Fact]
    public async Task SetupOptionsLoadActiveOnlyAndOptionalSelectionBuildsNull()
    {
        TradingSetup active = CreateTradingSetup("Silver Bullet");
        TradingSetup inactive = CreateTradingSetup("Retired Setup", active: false);
        var setupReader = new FakeTradingSetupReader();
        setupReader.EnqueueResult(
            [CreateTradingSetupListItem(active), CreateTradingSetupListItem(inactive)]);
        TradesViewModel viewModel = CreateViewModel(
            tradingSetupReader: setupReader);

        await viewModel.EnsureLoadedAsync();
        viewModel.SelectedAccount = Assert.Single(CreateReferenceData().Accounts);
        viewModel.SelectedInstrument = Assert.Single(CreateReferenceData().Instruments);
        viewModel.SelectedDirection = TradeDirection.Long;
        viewModel.QuantityText = "1";
        viewModel.EntryExecutedAtUtcText = "2026-09-10 13:30:00";
        viewModel.EntryPriceText = "100";

        TradingSetupListItem option = Assert.Single(viewModel.AvailableTradingSetups);
        Assert.Equal(active.Id, option.Id);
        Assert.DoesNotContain(
            viewModel.AvailableTradingSetups,
            item => item.Id == inactive.Id);
        Assert.True(viewModel.TryBuildManualTradeCommand(out var command));
        Assert.Null(command!.TradingSetupId);
    }

    [Fact]
    public async Task ManualSavePassesExactSetupAndResetsSelection()
    {
        TradingSetup setup = CreateTradingSetup("Silver Bullet");
        ManualTradeSaveFixture fixture = CreateSaveFixture(tradingSetup: setup);
        fixture.ViewModel.SelectedTradingSetup = CreateTradingSetupListItem(setup);

        await fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);

        Assert.Equal(setup.Id, fixture.TradingSetupStore.RequestedId);
        Assert.Equal(setup.Id, fixture.TradeStore.AddedTrade?.TradingSetupId);
        Assert.Null(fixture.ViewModel.SelectedTradingSetup);
        Assert.Equal(1, fixture.TradeStore.AddCallCount);
    }

    [Theory]
    [InlineData(false, "The selected trading setup is no longer available.")]
    [InlineData(true, "The selected trading setup is inactive.")]
    public async Task ManualSaveMapsSetupAuthorityFailuresSafely(
        bool inactive,
        string expectedMessage)
    {
        TradingSetup selected = CreateTradingSetup(
            "Selected Setup",
            active: !inactive);
        ManualTradeSaveFixture fixture = CreateSaveFixture(
            tradingSetup: inactive ? selected : null);
        fixture.ViewModel.SelectedTradingSetup = CreateTradingSetupListItem(selected);

        await fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);

        Assert.Equal(expectedMessage, fixture.ViewModel.SaveErrorMessage);
        Assert.Equal(0, fixture.TradeStore.AddCallCount);
        Assert.Equal(selected.Id, fixture.ViewModel.SelectedTradingSetup?.Id);
    }

    [Fact]
    public async Task SetupLoadFailureIsSafeAndDoesNotDiscardTradeList()
    {
        TradeListItem trade = CreateTradeListItem();
        var listReader = new FakeTradeListReader();
        listReader.EnqueueResult([trade]);
        var setupReader = new FakeTradingSetupReader();
        setupReader.EnqueueException(new InvalidOperationException("details"));
        TradesViewModel viewModel = CreateViewModel(
            tradeListReader: listReader,
            tradingSetupReader: setupReader);

        await viewModel.EnsureLoadedAsync();

        Assert.Equal("Trade reference data could not be loaded.", viewModel.ErrorMessage);
        Assert.Empty(viewModel.AvailableTradingSetups);
        Assert.Same(trade, Assert.Single(viewModel.RecentTrades));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DetailDisplaysAssignedSetupAndRetainsCurrentInactiveOption(
        bool active)
    {
        TradingSetup current = CreateTradingSetup("Current Setup", active);
        TradingSetup otherInactive = CreateTradingSetup("Other Inactive", false);
        TradingSetup replacement = CreateTradingSetup("Replacement");
        var setupReader = new FakeTradingSetupReader();
        setupReader.EnqueueResult(
        [
            CreateTradingSetupListItem(current),
            CreateTradingSetupListItem(otherInactive),
            CreateTradingSetupListItem(replacement),
        ]);
        TradeListItem listItem = CreateTradeListItem();
        TradeDetail detail = CreateTradeDetail(listItem) with
        {
            TradingSetupId = current.Id,
            TradingSetupName = current.Name,
            IsTradingSetupActive = current.IsActive,
        };
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(detail);
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader,
            tradingSetupReader: setupReader);

        await viewModel.EnsureLoadedAsync();
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);

        Assert.Equal(current.Name, viewModel.SelectedTradeDetail?.TradingSetupName);
        Assert.Equal(active, viewModel.SelectedTradeDetail?.IsTradingSetupActive);
        Assert.Contains(
            viewModel.TradeDetailTradingSetupOptions,
            item => item.Id == current.Id);
        Assert.Contains(
            viewModel.TradeDetailTradingSetupOptions,
            item => item.Id == replacement.Id);
        Assert.DoesNotContain(
            viewModel.TradeDetailTradingSetupOptions,
            item => item.Id == otherInactive.Id);
        Assert.Equal(current.Id, viewModel.SelectedTradeDetailTradingSetup?.Id);
    }

    [Fact]
    public async Task DetailWithoutSetupKeepsEmptyClassificationState()
    {
        TradeListItem listItem = CreateTradeListItem();
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(CreateTradeDetail(listItem));
        TradesViewModel viewModel = CreateViewModel(tradeDetailReader: detailReader);

        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);

        Assert.Null(viewModel.SelectedTradeDetail?.TradingSetupId);
        Assert.Null(viewModel.SelectedTradeDetail?.TradingSetupName);
        Assert.Null(viewModel.SelectedTradeDetail?.IsTradingSetupActive);
        Assert.Null(viewModel.SelectedTradeDetailTradingSetup);
    }

    [Fact]
    public async Task SaveSetupSendsExactIdAndReloadsAuthoritativeDetail()
    {
        TradingSetup setup = CreateTradingSetup("Silver Bullet");
        TradeListItem listItem = CreateTradeListItem();
        TradeDetail initial = CreateTradeDetail(listItem);
        TradeDetail authoritative = initial with
        {
            TradingSetupId = setup.Id,
            TradingSetupName = setup.Name,
            IsTradingSetupActive = true,
        };
        var setupReader = new FakeTradingSetupReader();
        setupReader.EnqueueResult([CreateTradingSetupListItem(setup)]);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(initial);
        detailReader.EnqueueResult(authoritative);
        var mutationStore = new FakeTradeMutationStore
        {
            TradeToReturn = CreateOpenDomainTrade(listItem.Id, 1m),
        };
        var setupStore = new FakeTradingSetupStore { Setup = setup };
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader,
            tradeMutationStore: mutationStore,
            tradingSetupReader: setupReader,
            tradingSetupStore: setupStore);
        await viewModel.EnsureLoadedAsync();
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);
        viewModel.SelectedTradeDetailTradingSetup =
            Assert.Single(viewModel.TradeDetailTradingSetupOptions);

        await viewModel.SaveTradingSetupCommand.ExecuteAsync(null);

        Assert.Equal(setup.Id, mutationStore.SavedTrade?.TradingSetupId);
        Assert.Equal(1, mutationStore.SaveCallCount);
        Assert.Equal(2, detailReader.CallCount);
        Assert.Same(authoritative, viewModel.SelectedTradeDetail);
        Assert.Equal("Trading setup updated successfully.", viewModel.TradingSetupSuccessMessage);
        Assert.Null(viewModel.TradingSetupSaveErrorMessage);
    }

    [Fact]
    public async Task ClearSetupSendsNullAndReloadsAuthoritativeDetail()
    {
        TradingSetup setup = CreateTradingSetup("Historical", false);
        TradeListItem listItem = CreateTradeListItem();
        TradeDetail initial = CreateTradeDetail(listItem) with
        {
            TradingSetupId = setup.Id,
            TradingSetupName = setup.Name,
            IsTradingSetupActive = false,
        };
        var setupReader = new FakeTradingSetupReader();
        setupReader.EnqueueResult([CreateTradingSetupListItem(setup)]);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(initial);
        detailReader.EnqueueResult(CreateTradeDetail(listItem));
        Trade domainTrade = CreateOpenDomainTrade(listItem.Id, 1m);
        domainTrade.SetTradingSetup(setup.Id, domainTrade.UpdatedAtUtc.AddMinutes(1));
        var mutationStore = new FakeTradeMutationStore { TradeToReturn = domainTrade };
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader,
            tradeMutationStore: mutationStore,
            tradingSetupReader: setupReader);
        await viewModel.EnsureLoadedAsync();
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);

        await viewModel.ClearTradingSetupCommand.ExecuteAsync(null);

        Assert.Null(mutationStore.SavedTrade?.TradingSetupId);
        Assert.Equal(1, mutationStore.SaveCallCount);
        Assert.Null(viewModel.SelectedTradeDetail?.TradingSetupId);
        Assert.Equal(2, detailReader.CallCount);
    }

    [Theory]
    [InlineData("missing", "The selected trading setup is no longer available.")]
    [InlineData("inactive", "The selected trading setup is inactive.")]
    [InlineData("failure", "Trading setup could not be saved.")]
    public async Task SaveSetupMapsFailuresSafely(
        string failure,
        string expectedMessage)
    {
        TradingSetup setup = CreateTradingSetup(
            "Selected",
            active: failure != "inactive");
        TradeListItem listItem = CreateTradeListItem();
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(CreateTradeDetail(listItem));
        var setupReader = new FakeTradingSetupReader();
        setupReader.EnqueueResult(
            [CreateTradingSetupListItem(setup) with { IsActive = true }]);
        var mutationStore = new FakeTradeMutationStore
        {
            TradeToReturn = CreateOpenDomainTrade(listItem.Id, 1m),
            SaveException = failure == "failure"
                ? new InvalidOperationException("database")
                : null,
        };
        var setupStore = new FakeTradingSetupStore
        {
            Setup = failure == "missing" ? null : setup,
        };
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader,
            tradeMutationStore: mutationStore,
            tradingSetupReader: setupReader,
            tradingSetupStore: setupStore);
        await viewModel.EnsureLoadedAsync();
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);
        viewModel.SelectedTradeDetailTradingSetup =
            Assert.Single(viewModel.TradeDetailTradingSetupOptions);

        await viewModel.SaveTradingSetupCommand.ExecuteAsync(null);

        Assert.Equal(expectedMessage, viewModel.TradingSetupSaveErrorMessage);
        Assert.Null(viewModel.TradingSetupSuccessMessage);
        Assert.Equal(1, detailReader.CallCount);
    }

    [Fact]
    public async Task SuccessfulSetupWriteRetainsSuccessWhenAuthoritativeReloadFails()
    {
        TradingSetup setup = CreateTradingSetup("Silver Bullet");
        TradeListItem listItem = CreateTradeListItem();
        TradeDetail initial = CreateTradeDetail(listItem);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(initial);
        detailReader.EnqueueException(new InvalidOperationException("read"));
        var setupReader = new FakeTradingSetupReader();
        setupReader.EnqueueResult([CreateTradingSetupListItem(setup)]);
        var mutationStore = new FakeTradeMutationStore
        {
            TradeToReturn = CreateOpenDomainTrade(listItem.Id, 1m),
        };
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader,
            tradeMutationStore: mutationStore,
            tradingSetupReader: setupReader,
            tradingSetupStore: new FakeTradingSetupStore { Setup = setup });
        await viewModel.EnsureLoadedAsync();
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);
        viewModel.SelectedTradeDetailTradingSetup =
            Assert.Single(viewModel.TradeDetailTradingSetupOptions);

        await viewModel.SaveTradingSetupCommand.ExecuteAsync(null);

        Assert.Equal(1, mutationStore.SaveCallCount);
        Assert.Same(initial, viewModel.SelectedTradeDetail);
        Assert.Equal("Trading setup updated successfully.", viewModel.TradingSetupSuccessMessage);
        Assert.Equal("Trade details could not be loaded.", viewModel.TradeDetailErrorMessage);
        Assert.Null(viewModel.TradingSetupSaveErrorMessage);
    }

    [Fact]
    public async Task SetupWriteDisablesCompetingCommandsUntilCompletion()
    {
        TradingSetup setup = CreateTradingSetup("Silver Bullet");
        TradeListItem listItem = CreateTradeListItem();
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(CreateTradeDetail(listItem));
        var setupReader = new FakeTradingSetupReader();
        setupReader.EnqueueResult([CreateTradingSetupListItem(setup)]);
        var mutationStore = new FakeTradeMutationStore
        {
            TradeToReturn = CreateOpenDomainTrade(listItem.Id, 1m),
            HoldSave = true,
        };
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader,
            tradeMutationStore: mutationStore,
            tradingSetupReader: setupReader,
            tradingSetupStore: new FakeTradingSetupStore { Setup = setup });
        await viewModel.EnsureLoadedAsync();
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);
        viewModel.SelectedTradeDetailTradingSetup =
            Assert.Single(viewModel.TradeDetailTradingSetupOptions);

        Task saveTask = viewModel.SaveTradingSetupCommand.ExecuteAsync(null);
        await mutationStore.SaveStarted;

        Assert.True(viewModel.IsTradingSetupSaving);
        Assert.False(viewModel.SaveTradingSetupCommand.CanExecute(null));
        Assert.False(viewModel.ClearTradingSetupCommand.CanExecute(null));
        Assert.False(viewModel.RefreshCommand.CanExecute(null));
        Assert.False(viewModel.CloseTradeDetailCommand.CanExecute(null));

        detailReader.EnqueueResult(CreateTradeDetail(listItem));
        mutationStore.ReleaseSave();
        await saveTask;
        Assert.False(viewModel.IsTradingSetupSaving);
    }

    private static async Task<CloseTradeFixture> CreateCloseTradeFixtureAsync(
        decimal openQuantity = 2.5m,
        Func<Guid, TradeScreenshotListItem>? screenshotFactory = null)
    {
        Guid tradeId = Guid.NewGuid();
        TradeListItem openListItem = CreateOpenTradeListItem(
            tradeId,
            openQuantity);
        TradeDetail openDetail = CreateOpenTradeDetail(
            openListItem,
            openQuantity);
        IReadOnlyList<TradeListItem> initialTrades = [openListItem];
        IReadOnlyList<TradeScreenshotListItem> screenshots =
            screenshotFactory is null
                ? []
                : [screenshotFactory(tradeId)];
        var listReader = new FakeTradeListReader();
        listReader.EnqueueResult(initialTrades);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(openDetail);
        var screenshotReader = new FakeTradeScreenshotReader();
        screenshotReader.EnqueueResult(screenshots);
        var mutationStore = new FakeTradeMutationStore
        {
            TradeToReturn = CreateOpenDomainTrade(tradeId, openQuantity),
        };
        TradesViewModel viewModel = CreateViewModel(
            tradeListReader: listReader,
            tradeDetailReader: detailReader,
            tradeScreenshotReader: screenshotReader,
            tradeMutationStore: mutationStore);

        await viewModel.EnsureLoadedAsync();
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(openListItem);

        return new CloseTradeFixture(
            viewModel,
            mutationStore,
            listReader,
            detailReader,
            screenshotReader,
            openListItem,
            openDetail,
            initialTrades,
            screenshots);
    }

    private static void ShowValidCloseForm(TradesViewModel viewModel)
    {
        viewModel.ShowCloseTradeCommand.Execute(null);
        viewModel.CloseTradeExecutedAtUtcText = "2026-09-08 14:15:00";
        viewModel.CloseTradePriceText = "101.25";
        viewModel.CloseTradeCommissionText = "1.50";
        viewModel.CloseTradeFeesText = "0.25";
    }

    private static TradeListItem CreateOpenTradeListItem(
        Guid tradeId,
        decimal openQuantity)
    {
        DateTimeOffset openedAtUtc =
            new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);

        return new TradeListItem(
            tradeId,
            Guid.NewGuid(),
            "Primary Account",
            Guid.NewGuid(),
            "NQ",
            TradeDirection.Long,
            TradeStatus.Open,
            openedAtUtc,
            null,
            openQuantity,
            100m,
            null,
            1.50m,
            null,
            null,
            "USD");
    }

    private static TradeDetail CreateOpenTradeDetail(
        TradeListItem listItem,
        decimal openQuantity)
    {
        return new TradeDetail(
            listItem.Id,
            listItem.TradingAccountId,
            listItem.TradingAccountName,
            listItem.InstrumentId,
            listItem.InstrumentSymbol,
            "Nasdaq-100 E-mini",
            null,
            null,
            null,
            TradeDirection.Long,
            TradeStatus.Open,
            listItem.OpenedAtUtc,
            null,
            openQuantity,
            100m,
            null,
            1.50m,
            null,
            null,
            20m,
            "USD",
            [
                new TradeExecutionDetailItem(
                    Guid.NewGuid(),
                    1,
                    listItem.OpenedAtUtc,
                    ExecutionSide.Buy,
                    openQuantity,
                    100m,
                    1m,
                    0.50m,
                    1.50m,
                    null,
                    null,
                    null),
            ]);
    }

    private static Trade CreateOpenDomainTrade(
        Guid tradeId,
        decimal openQuantity)
    {
        DateTimeOffset openedAtUtc =
            new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);
        var openingExecution = new TradeExecution(
            tradeId,
            1,
            openedAtUtc,
            ExecutionSide.Buy,
            openQuantity,
            100m,
            1m,
            0.50m,
            null,
            null,
            null);

        return Trade.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new TradePricingSnapshot(20m, "USD"),
            openingExecution,
            openedAtUtc);
    }

    private static Trade CreateClosedDomainTrade(
        Guid tradeId,
        decimal quantity)
    {
        Trade trade = CreateOpenDomainTrade(tradeId, quantity);
        trade.AddExecution(
            new TradeExecution(
                tradeId,
                2,
                CloseExecutedAtUtc,
                ExecutionSide.Sell,
                quantity,
                101m,
                1m,
                0.50m,
                null,
                null,
                null),
            FixedTimeProvider.FixedUtcNow);
        return trade;
    }

    private static TradesViewModel CreateValidTradeForm(
        bool hasExit = false,
        TradeDirection direction = TradeDirection.Long,
        AssetClass assetClass = AssetClass.Other,
        string quantityText = "2.5",
        string instrumentSymbol = "NQ")
    {
        ManualTradeReferenceData referenceData = CreateReferenceData(
            instrumentSymbol: instrumentSymbol,
            assetClass: assetClass);
        TradesViewModel viewModel =
            CreateViewModel(new FakeManualTradeReferenceDataReader());
        viewModel.SelectedAccount = Assert.Single(referenceData.Accounts);
        viewModel.SelectedInstrument = Assert.Single(referenceData.Instruments);
        viewModel.SelectedDirection = direction;
        viewModel.QuantityText = quantityText;
        viewModel.EntryExecutedAtUtcText = "2026-09-10 13:30:00";
        viewModel.EntryPriceText = "23950.25";
        viewModel.EntryCommissionText = "1.50";
        viewModel.EntryFeesText = "0.25";
        viewModel.HasExit = hasExit;

        if (hasExit)
        {
            viewModel.ExitExecutedAtUtcText = "2026-09-10 14:15:00";
            viewModel.ExitPriceText = "23900.00";
            viewModel.ExitCommissionText = "1.50";
            viewModel.ExitFeesText = "0.25";
        }

        return viewModel;
    }

    private static TradesViewModel CreateViewModel(
        FakeManualTradeReferenceDataReader? reader = null,
        FakeTradeListReader? tradeListReader = null,
        FakeTradeDetailReader? tradeDetailReader = null,
        FakeTradingAccountStore? accountStore = null,
        FakeInstrumentStore? instrumentStore = null,
        FakeTradeStore? tradeStore = null,
        FakeTradeScreenshotReader? tradeScreenshotReader = null,
        FakeTradeScreenshotFilePicker? tradeScreenshotFilePicker = null,
        FakeTradeExistenceReader? tradeExistenceReader = null,
        FakeTradeScreenshotFileStorage? tradeScreenshotFileStorage = null,
        FakeTradeScreenshotStore? tradeScreenshotStore = null,
        FakeTradeScreenshotContentReader? tradeScreenshotContentReader = null,
        FakeTradeScreenshotImageDecoder? tradeScreenshotImageDecoder = null,
        FakeTradeScreenshotDeletionStore? tradeScreenshotDeletionStore = null,
        FakeTradeScreenshotDeleteConfirmation? tradeScreenshotDeleteConfirmation = null,
        FakeTradeMutationStore? tradeMutationStore = null,
        TimeProvider? timeProvider = null,
        FakeTradingSetupReader? tradingSetupReader = null,
        FakeTradingSetupStore? tradingSetupStore = null)
    {
        reader ??= new FakeManualTradeReferenceDataReader();
        tradeListReader ??= new FakeTradeListReader();
        tradeDetailReader ??= new FakeTradeDetailReader();
        accountStore ??= new FakeTradingAccountStore();
        instrumentStore ??= new FakeInstrumentStore();
        tradeStore ??= new FakeTradeStore();
        tradeScreenshotReader ??= new FakeTradeScreenshotReader();
        tradeScreenshotFilePicker ??= new FakeTradeScreenshotFilePicker();
        tradeExistenceReader ??= new FakeTradeExistenceReader();
        tradeScreenshotFileStorage ??= new FakeTradeScreenshotFileStorage();
        tradeScreenshotStore ??= new FakeTradeScreenshotStore();
        tradeScreenshotContentReader ??= new FakeTradeScreenshotContentReader();
        tradeScreenshotImageDecoder ??= new FakeTradeScreenshotImageDecoder();
        tradeScreenshotDeletionStore ??= new FakeTradeScreenshotDeletionStore();
        tradeScreenshotDeleteConfirmation ??=
            new FakeTradeScreenshotDeleteConfirmation();
        tradeMutationStore ??= new FakeTradeMutationStore();
        timeProvider ??= new FixedTimeProvider();
        tradingSetupReader ??= new FakeTradingSetupReader();
        tradingSetupStore ??= new FakeTradingSetupStore();

        return new TradesViewModel(
            reader,
            tradingSetupReader,
            tradeListReader,
            tradeDetailReader,
            new CreateManualTradeUseCase(
                accountStore,
                instrumentStore,
                tradingSetupStore,
                tradeStore,
                timeProvider),
            new SetTradeTradingSetupUseCase(
                tradeMutationStore,
                tradingSetupStore,
                timeProvider),
            new CloseManualTradeUseCase(
                tradeMutationStore,
                timeProvider),
            tradeScreenshotReader,
            new AddTradeScreenshotUseCase(
                tradeExistenceReader,
                tradeScreenshotFileStorage,
                tradeScreenshotStore,
                timeProvider),
            tradeScreenshotFilePicker,
            tradeScreenshotContentReader,
            tradeScreenshotImageDecoder,
            new DeleteTradeScreenshotUseCase(
                tradeScreenshotDeletionStore,
                tradeScreenshotFileStorage),
            tradeScreenshotDeleteConfirmation);
    }

    private static ManualTradeSaveFixture CreateSaveFixture(
        bool hasExit = false,
        TradeDirection direction = TradeDirection.Long,
        decimal selectorPointValue = 20m,
        string quantityText = "2",
        TradingSetup? tradingSetup = null)
    {
        Guid accountId = Guid.NewGuid();
        Guid instrumentId = Guid.NewGuid();
        ManualTradeReferenceData referenceData = CreateReferenceData(
            accountId,
            instrumentId,
            selectorPointValue: selectorPointValue);
        var accountStore = new FakeTradingAccountStore
        {
            AccountToReturn = CreateTradingAccount(accountId),
        };
        var instrumentStore = new FakeInstrumentStore
        {
            InstrumentToReturn = CreateInstrument(instrumentId),
        };
        var tradeStore = new FakeTradeStore();
        var tradeListReader = new FakeTradeListReader();
        var referenceDataReader = new FakeManualTradeReferenceDataReader();
        referenceDataReader.EnqueueResult(referenceData);
        var tradingSetupReader = new FakeTradingSetupReader();
        if (tradingSetup is not null)
        {
            tradingSetupReader.EnqueueResult(
                [CreateTradingSetupListItem(tradingSetup)]);
        }
        var tradingSetupStore = new FakeTradingSetupStore
        {
            Setup = tradingSetup,
        };
        TradesViewModel viewModel = CreateViewModel(
            reader: referenceDataReader,
            tradeListReader: tradeListReader,
            accountStore: accountStore,
            instrumentStore: instrumentStore,
            tradeStore: tradeStore,
            tradingSetupReader: tradingSetupReader,
            tradingSetupStore: tradingSetupStore);

        viewModel.ShowManualEntryCommand.Execute(null);
        viewModel.SelectedAccount = Assert.Single(referenceData.Accounts);
        viewModel.SelectedInstrument = Assert.Single(referenceData.Instruments);
        viewModel.SelectedDirection = direction;
        viewModel.QuantityText = quantityText;
        viewModel.EntryExecutedAtUtcText = "2026-09-10 13:30:00";
        viewModel.EntryPriceText = "23950.25";
        viewModel.EntryCommissionText = "1.50";
        viewModel.EntryFeesText = "0.25";
        viewModel.HasExit = hasExit;

        if (hasExit)
        {
            viewModel.ExitExecutedAtUtcText = "2026-09-10 14:15:00";
            viewModel.ExitPriceText = "23900.00";
            viewModel.ExitCommissionText = "1.50";
            viewModel.ExitFeesText = "0.25";
        }

        return new ManualTradeSaveFixture(
            viewModel,
            accountStore,
            instrumentStore,
            tradeStore,
            tradeListReader,
            referenceDataReader,
            referenceData,
            tradingSetupReader,
            tradingSetupStore);
    }

    private static ManualTradeReferenceData CreateReferenceData(
        Guid? accountId = null,
        Guid? instrumentId = null,
        string accountName = "Primary Account",
        string instrumentSymbol = "NQ",
        decimal selectorPointValue = 20m,
        AssetClass assetClass = AssetClass.Futures)
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
                    assetClass,
                    "CME",
                    "USD",
                    0.25m,
                    5m,
                    selectorPointValue,
                    true),
            ]);
    }

    private static TradeListItem CreateTradeListItem(
        Guid? id = null,
        string symbol = "NQ")
    {
        DateTimeOffset openedAtUtc =
            new(2026, 9, 10, 13, 30, 0, TimeSpan.Zero);

        return new TradeListItem(
            id ?? Guid.NewGuid(),
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

    private static TradeDetail CreateTradeDetail(TradeListItem listItem)
    {
        return new TradeDetail(
            listItem.Id,
            listItem.TradingAccountId,
            listItem.TradingAccountName,
            listItem.InstrumentId,
            listItem.InstrumentSymbol,
            $"{listItem.InstrumentSymbol} display name",
            null,
            null,
            null,
            listItem.Direction,
            listItem.Status,
            listItem.OpenedAtUtc,
            listItem.ClosedAtUtc,
            listItem.OpenQuantity,
            listItem.AverageEntryPrice,
            listItem.AverageExitPrice,
            listItem.TotalCosts,
            listItem.GrossPnL,
            listItem.NetPnL,
            20m,
            listItem.Currency,
            [
                new TradeExecutionDetailItem(
                    Guid.NewGuid(),
                    1,
                    listItem.OpenedAtUtc,
                    ExecutionSide.Buy,
                    1m,
                    listItem.AverageEntryPrice,
                    0m,
                    0m,
                    0m,
                    null,
                    null,
                    null),
            ]);
    }

    private static TradingAccount CreateTradingAccount(Guid id)
    {
        DateTimeOffset createdAtUtc =
            new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

        return TradingAccount.Rehydrate(
            id,
            "Primary Account",
            TradingAccountType.Personal,
            "Broker",
            "ACCOUNT-1",
            "USD",
            25_000m,
            true,
            createdAtUtc,
            createdAtUtc);
    }

    private static Instrument CreateInstrument(Guid id)
    {
        DateTimeOffset createdAtUtc =
            new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

        return Instrument.Rehydrate(
            id,
            "NQ",
            "Nasdaq-100 E-mini",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5m,
            true,
            createdAtUtc,
            createdAtUtc);
    }

    private static TradingSetup CreateTradingSetup(
        string name,
        bool active = true)
    {
        var setup = new TradingSetup(
            name,
            null,
            new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero));
        if (!active)
        {
            setup.Deactivate(
                new DateTimeOffset(2026, 9, 2, 8, 0, 0, TimeSpan.Zero));
        }

        return setup;
    }

    private static TradingSetupListItem CreateTradingSetupListItem(
        TradingSetup setup) =>
        new(
            setup.Id,
            setup.Name,
            setup.Description,
            setup.IsActive,
            setup.CreatedAtUtc,
            setup.UpdatedAtUtc);

    private static void AssertManualEntryFormReset(TradesViewModel viewModel)
    {
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

    private static void AssertRepresentativeTradeFacts(
        TradesViewModel viewModel,
        string expectedQuantity = "2.5")
    {
        Assert.Equal(TradeDirection.Short, viewModel.SelectedDirection);
        Assert.Equal(expectedQuantity, viewModel.QuantityText);
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

    private sealed record ManualTradeSaveFixture(
        TradesViewModel ViewModel,
        FakeTradingAccountStore AccountStore,
        FakeInstrumentStore InstrumentStore,
        FakeTradeStore TradeStore,
        FakeTradeListReader TradeListReader,
        FakeManualTradeReferenceDataReader ReferenceDataReader,
        ManualTradeReferenceData ReferenceData,
        FakeTradingSetupReader TradingSetupReader,
        FakeTradingSetupStore TradingSetupStore);

    private sealed record CloseTradeFixture(
        TradesViewModel ViewModel,
        FakeTradeMutationStore MutationStore,
        FakeTradeListReader ListReader,
        FakeTradeDetailReader DetailReader,
        FakeTradeScreenshotReader ScreenshotReader,
        TradeListItem OpenListItem,
        TradeDetail OpenDetail,
        IReadOnlyList<TradeListItem> InitialTrades,
        IReadOnlyList<TradeScreenshotListItem> Screenshots);
}
