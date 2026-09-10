using System.Globalization;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Tests.Instruments;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

[Collection(CultureSensitiveCollection.Name)]
public sealed class TradesViewModelTests
{
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
    }

    [Fact]
    public void ManualEntryFactsRemainAsEnteredWithoutParsing()
    {
        var viewModel = CreateViewModel(new FakeManualTradeReferenceDataReader());

        PopulateRepresentativeTradeFacts(viewModel);

        AssertRepresentativeTradeFacts(viewModel);
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
        TradesViewModel viewModel = CreateViewModel(tradeStore: tradeStore);
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
        Assert.Equal(2.5m, trade.OpenQuantity);
        Assert.Equal(20m, trade.Pricing.PointValue);
        Assert.Equal("USD", trade.Pricing.Currency);
        TradeExecution execution = Assert.Single(trade.Executions);
        Assert.Equal(ExecutionSide.Buy, execution.Side);
        Assert.Equal(2.5m, execution.Quantity);
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
                Assert.Equal(2.5m, entry.Quantity);
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
                Assert.Equal(2.5m, exit.Quantity);
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
        AssertRepresentativeTradeFacts(fixture.ViewModel);
        Assert.Equal(
            "Trade could not be saved.",
            fixture.ViewModel.SaveErrorMessage);
        Assert.True(fixture.ViewModel.HasSaveError);
        Assert.Null(fixture.ViewModel.SuccessMessage);
        Assert.False(fixture.ViewModel.IsSaving);
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
        AssertRepresentativeTradeFacts(fixture.ViewModel);
        Assert.Equal(
            "The selected trading account or instrument is no longer available. " +
            "Refresh the reference data and try again.",
            fixture.ViewModel.SaveErrorMessage);
        Assert.Null(fixture.ViewModel.SuccessMessage);
        Assert.False(fixture.ViewModel.IsSaving);
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
        AssertRepresentativeTradeFacts(fixture.ViewModel);
        Assert.Null(fixture.ViewModel.SaveErrorMessage);
        Assert.Null(fixture.ViewModel.SuccessMessage);
        Assert.False(fixture.ViewModel.IsSaving);
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

    private static TradesViewModel CreateValidTradeForm(
        bool hasExit = false,
        TradeDirection direction = TradeDirection.Long)
    {
        ManualTradeReferenceData referenceData = CreateReferenceData();
        TradesViewModel viewModel =
            CreateViewModel(new FakeManualTradeReferenceDataReader());
        viewModel.SelectedAccount = Assert.Single(referenceData.Accounts);
        viewModel.SelectedInstrument = Assert.Single(referenceData.Instruments);
        viewModel.SelectedDirection = direction;
        viewModel.QuantityText = "2.5";
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
        FakeTradingAccountStore? accountStore = null,
        FakeInstrumentStore? instrumentStore = null,
        FakeTradeStore? tradeStore = null,
        TimeProvider? timeProvider = null)
    {
        reader ??= new FakeManualTradeReferenceDataReader();
        accountStore ??= new FakeTradingAccountStore();
        instrumentStore ??= new FakeInstrumentStore();
        tradeStore ??= new FakeTradeStore();
        timeProvider ??= new FixedTimeProvider();

        return new TradesViewModel(
            reader,
            new CreateManualTradeUseCase(
                accountStore,
                instrumentStore,
                tradeStore,
                timeProvider));
    }

    private static ManualTradeSaveFixture CreateSaveFixture(
        bool hasExit = false,
        TradeDirection direction = TradeDirection.Long,
        decimal selectorPointValue = 20m)
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
        TradesViewModel viewModel = CreateViewModel(
            accountStore: accountStore,
            instrumentStore: instrumentStore,
            tradeStore: tradeStore);

        viewModel.ShowManualEntryCommand.Execute(null);
        viewModel.SelectedAccount = Assert.Single(referenceData.Accounts);
        viewModel.SelectedInstrument = Assert.Single(referenceData.Instruments);
        viewModel.SelectedDirection = direction;
        viewModel.QuantityText = "2.5";
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
            referenceData);
    }

    private static ManualTradeReferenceData CreateReferenceData(
        Guid? accountId = null,
        Guid? instrumentId = null,
        string accountName = "Primary Account",
        string instrumentSymbol = "NQ",
        decimal selectorPointValue = 20m)
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
                    selectorPointValue,
                    true),
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

    private sealed record ManualTradeSaveFixture(
        TradesViewModel ViewModel,
        FakeTradingAccountStore AccountStore,
        FakeInstrumentStore InstrumentStore,
        FakeTradeStore TradeStore,
        ManualTradeReferenceData ReferenceData);
}
