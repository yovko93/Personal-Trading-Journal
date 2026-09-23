using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Imports;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Import;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using System.IO;

namespace PersonalTradingJournal.Desktop.Tests.Import;

public sealed class ImportViewModelTests
{
    [Fact]
    public async Task EnsureLoadedAsyncLoadsAllAccountsOnceWithoutSelectingOne()
    {
        Fixture fixture = CreateFixture(isAccountActive: false);

        await fixture.ViewModel.EnsureLoadedAsync();
        await fixture.ViewModel.EnsureLoadedAsync();

        ImportAccountOption option = Assert.Single(fixture.ViewModel.Accounts);
        Assert.Equal(TradingAccountType.Personal, option.AccountType);
        Assert.Equal("Tradovate", option.ProviderName);
        Assert.Equal("SIM-1", option.ExternalAccountId);
        Assert.Equal("USD", option.Currency);
        Assert.False(option.IsActive);
        Assert.Equal(
            "Tradovate Account · Tradovate · SIM-1 · Personal · USD · Inactive",
            option.DisplayText);
        Assert.Null(fixture.ViewModel.SelectedAccount);
        Assert.Equal(1, fixture.AccountReader.CallCount);
        Assert.Equal(0, fixture.FilePicker.CallCount);
    }

    [Fact]
    public void AccountDisplayTextSkipsMissingOptionalIdentityWithoutEmptySegments()
    {
        var option = new ImportAccountOption(
            Guid.NewGuid(),
            "My Account",
            TradingAccountType.Personal,
            ProviderName: null,
            ExternalAccountId: " ",
            "USD",
            IsActive: true);

        Assert.Equal("My Account · Personal · USD", option.DisplayText);
        Assert.DoesNotContain("· ·", option.DisplayText, StringComparison.Ordinal);
        Assert.False(option.DisplayText.EndsWith('·'));
    }

    [Fact]
    public void DuplicateAccountNamesAreDisambiguatedByExternalAccountId()
    {
        var first = new ImportAccountOption(
            Guid.NewGuid(), "Topstep 50K", TradingAccountType.PropFunded,
            "Topstep", "123456", "USD", true);
        var second = new ImportAccountOption(
            Guid.NewGuid(), "Topstep 50K", TradingAccountType.PropFunded,
            "Topstep", "654321", "USD", true);

        Assert.Equal(
            "Topstep 50K · Topstep · 123456 · PropFunded · USD",
            first.DisplayText);
        Assert.NotEqual(first.DisplayText, second.DisplayText);
        Assert.DoesNotContain(first.Id.ToString(), first.DisplayText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectCsvAnalyzesImmediatelyAndDisposesCallerOwnedStream()
    {
        Fixture fixture = CreateFixture();

        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);

        Assert.Equal(ImportWorkflowPhase.FileAnalyzed, fixture.ViewModel.Phase);
        Assert.Equal("fills.csv", fixture.ViewModel.SelectedFileName);
        Assert.NotNull(fixture.ViewModel.AnalysisSummary);
        Assert.Equal(1, fixture.ViewModel.AnalysisSummary.CandidateCount);
        Assert.Single(fixture.ViewModel.Instruments);
        Assert.True(fixture.FilePicker.Stream.IsDisposed);
        Assert.Equal(0, fixture.AccountReader.DetailCallCount);
        Assert.Null(fixture.ViewModel.SelectedAccount);
    }

    [Fact]
    public async Task BuildPreviewUsesExplicitAccountAndProducesReadOnlyPreview()
    {
        Fixture fixture = CreateFixture();
        await fixture.ViewModel.EnsureLoadedAsync();
        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);
        fixture.ViewModel.SelectedAccount = Assert.Single(fixture.ViewModel.Accounts);

        await fixture.ViewModel.BuildPreviewCommand.ExecuteAsync(null);

        Assert.Equal(ImportWorkflowPhase.PreviewReady, fixture.ViewModel.Phase);
        Assert.NotNull(fixture.ViewModel.PreviewSummary);
        Assert.Single(fixture.ViewModel.Trades);
        Assert.Contains(fixture.ViewModel.Diagnostics, item => item.Code == "COSTS_UNAVAILABLE");
        Assert.Contains(fixture.ViewModel.Diagnostics, item =>
            item.Code == TradovateReconstructionDiagnosticCodes.SourceCompletenessUnverified);
        Assert.Equal(1, fixture.AccountReader.DetailCallCount);
    }

    [Fact]
    public async Task ExistingInstrumentIsPresentedWithoutCreationProposal()
    {
        Fixture fixture = CreateFixture(useExistingInstrument: true);
        await fixture.ViewModel.EnsureLoadedAsync();
        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);
        fixture.ViewModel.SelectedAccount = Assert.Single(fixture.ViewModel.Accounts);

        await fixture.ViewModel.BuildPreviewCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.ViewModel.AnalysisSummary!.ExistingInstrumentCount);
        Assert.Equal(0, fixture.ViewModel.AnalysisSummary.ProposedInstrumentCount);
        ImportInstrumentItem instrument = Assert.Single(fixture.ViewModel.Instruments);
        Assert.Equal("Existing Instrument", instrument.Resolution);
        Assert.Equal("Active", instrument.Activity);
        Assert.Equal("User MNQ", instrument.DisplayName);
        Assert.Equal("Futures", instrument.AssetClass);
        Assert.Equal("User Exchange", instrument.Exchange);
        Assert.Equal("USD", instrument.Currency);
        Assert.Equal("0.25", instrument.TickSize);
        Assert.Equal("0.5", instrument.TickValue);
        Assert.Equal(string.Empty, instrument.CreationNotice);
        Assert.Equal("Existing", Assert.Single(fixture.ViewModel.Trades).Resolution);
    }

    [Fact]
    public async Task ProposedInstrumentShowsEconomicsAndFutureConfirmationNotice()
    {
        Fixture fixture = CreateFixture();

        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);

        ImportInstrumentItem instrument = Assert.Single(fixture.ViewModel.Instruments);
        Assert.Equal("New Instrument", instrument.Resolution);
        Assert.Equal("Micro E-mini Nasdaq-100", instrument.DisplayName);
        Assert.Equal("Futures", instrument.AssetClass);
        Assert.Equal("CME", instrument.Exchange);
        Assert.Equal("USD", instrument.Currency);
        Assert.Equal("0.25", instrument.TickSize);
        Assert.Equal("0.5", instrument.TickValue);
        Assert.Equal(
            "Will be created only when the import is confirmed.",
            instrument.CreationNotice);
    }

    [Fact]
    public async Task InactiveExistingInstrumentRemainsExistingAndClearlyLabeled()
    {
        Fixture fixture = CreateFixture(
            useExistingInstrument: true,
            isInstrumentActive: false);

        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);

        ImportInstrumentItem instrument = Assert.Single(fixture.ViewModel.Instruments);
        Assert.Equal("Existing Instrument · Inactive", instrument.StatusText);
        Assert.Equal(string.Empty, instrument.CreationNotice);
        Assert.Contains(fixture.ViewModel.Diagnostics, item =>
            item.Code == TradovateInstrumentResolutionDiagnosticCodes.ExistingInstrumentInactive);
    }

    [Fact]
    public async Task AccountChangeInvalidatesPreviewButPreservesAnalysis()
    {
        Fixture fixture = CreateFixture();
        await fixture.ViewModel.EnsureLoadedAsync();
        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);
        fixture.ViewModel.SelectedAccount = Assert.Single(fixture.ViewModel.Accounts);
        await fixture.ViewModel.BuildPreviewCommand.ExecuteAsync(null);

        fixture.ViewModel.SelectedAccount = null;

        Assert.Equal(ImportWorkflowPhase.FileAnalyzed, fixture.ViewModel.Phase);
        Assert.NotNull(fixture.ViewModel.AnalysisSummary);
        Assert.Null(fixture.ViewModel.PreviewSummary);
        Assert.Empty(fixture.ViewModel.Trades);
    }

    [Fact]
    public async Task MissingSelectedAccountBlocksPreviewWithSafeDiagnostic()
    {
        Fixture fixture = CreateFixture(accountExistsForPreparation: false);
        await fixture.ViewModel.EnsureLoadedAsync();
        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);
        fixture.ViewModel.SelectedAccount = Assert.Single(fixture.ViewModel.Accounts);

        await fixture.ViewModel.BuildPreviewCommand.ExecuteAsync(null);

        Assert.Equal(ImportWorkflowPhase.Blocked, fixture.ViewModel.Phase);
        Assert.Contains(fixture.ViewModel.Diagnostics, item =>
            item.Code == TradovateImportPreparationDiagnosticCodes.TradingAccountNotFound);
    }

    [Fact]
    public async Task InactiveSelectedAccountRemainsValidAndProducesWarning()
    {
        Fixture fixture = CreateFixture(isAccountActive: false);
        await fixture.ViewModel.EnsureLoadedAsync();
        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);
        fixture.ViewModel.SelectedAccount = Assert.Single(fixture.ViewModel.Accounts);

        await fixture.ViewModel.BuildPreviewCommand.ExecuteAsync(null);

        Assert.Equal(ImportWorkflowPhase.PreviewReady, fixture.ViewModel.Phase);
        Assert.False(fixture.ViewModel.PreviewSummary!.IsAccountActive);
        Assert.Contains(fixture.ViewModel.Diagnostics, item =>
            item.Code == TradovateImportPreparationDiagnosticCodes.SelectedAccountInactive);
    }

    [Fact]
    public async Task UnknownInstrumentRequiresInputAndCannotBuildPreview()
    {
        Fixture fixture = CreateFixture(brokerSymbol: "UNKNOWNU6");

        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);

        Assert.Equal(ImportWorkflowPhase.RequiresUserInput, fixture.ViewModel.Phase);
        Assert.Contains(fixture.ViewModel.Diagnostics, item =>
            item.Code == TradovateInstrumentResolutionDiagnosticCodes.InstrumentMetadataRequired);
        Assert.False(fixture.ViewModel.BuildPreviewCommand.CanExecute(null));
    }

    [Fact]
    public async Task InvalidCsvIsBlockedAndSurfacesCsvDiagnostic()
    {
        Fixture fixture = CreateFixture(invalidCsv: true);

        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);

        Assert.Equal(ImportWorkflowPhase.Blocked, fixture.ViewModel.Phase);
        Assert.Contains(fixture.ViewModel.Diagnostics, item =>
            item.Stage == "CSV" && item.Code == "INVALID_TEST_CSV");
        Assert.False(fixture.ViewModel.BuildPreviewCommand.CanExecute(null));
    }

    [Fact]
    public async Task PickerCancellationPreservesCurrentAnalysis()
    {
        Fixture fixture = CreateFixture();
        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);
        ImportAnalysisSummary summary = fixture.ViewModel.AnalysisSummary!;
        fixture.FilePicker.ReturnNull = true;

        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);

        Assert.Same(summary, fixture.ViewModel.AnalysisSummary);
        Assert.Equal("fills.csv", fixture.ViewModel.SelectedFileName);
        Assert.Equal(ImportWorkflowPhase.FileAnalyzed, fixture.ViewModel.Phase);
    }

    [Fact]
    public async Task AmbiguousSofiaTimestampRequiresInputDuringPreparation()
    {
        Fixture fixture = CreateFixture(
            sourceTimestamp: new DateTime(2026, 10, 25, 3, 30, 0, DateTimeKind.Unspecified));
        await fixture.ViewModel.EnsureLoadedAsync();
        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);
        fixture.ViewModel.SelectedAccount = Assert.Single(fixture.ViewModel.Accounts);

        await fixture.ViewModel.BuildPreviewCommand.ExecuteAsync(null);

        Assert.Equal(ImportWorkflowPhase.RequiresUserInput, fixture.ViewModel.Phase);
        Assert.Contains(fixture.ViewModel.Diagnostics, item =>
            item.Code == TradovateImportPreparationDiagnosticCodes.AmbiguousSourceLocalTime);
    }

    [Fact]
    public async Task ConfirmImportRequiresAConfirmationReadyPreviewAndExplicitAccount()
    {
        Fixture fixture = CreateFixture();

        Assert.False(fixture.ViewModel.ConfirmImportCommand.CanExecute(null));
        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);
        Assert.False(fixture.ViewModel.ConfirmImportCommand.CanExecute(null));
        await fixture.ViewModel.EnsureLoadedAsync();
        fixture.ViewModel.SelectedAccount = Assert.Single(fixture.ViewModel.Accounts);
        Assert.False(fixture.ViewModel.ConfirmImportCommand.CanExecute(null));

        await fixture.ViewModel.BuildPreviewCommand.ExecuteAsync(null);

        Assert.True(fixture.ViewModel.ConfirmImportCommand.CanExecute(null));
    }

    [Fact]
    public async Task BuildPreviewRefreshesInstrumentResolutionAndPresentation()
    {
        Fixture fixture = CreateFixture(instrumentAppearsOnPreview: true);
        await fixture.ViewModel.EnsureLoadedAsync();
        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);
        Assert.Equal("New Instrument", Assert.Single(fixture.ViewModel.Instruments).Resolution);
        fixture.ViewModel.SelectedAccount = Assert.Single(fixture.ViewModel.Accounts);

        await fixture.ViewModel.BuildPreviewCommand.ExecuteAsync(null);

        Assert.Equal(2, fixture.InstrumentReader.CallCount);
        Assert.Equal(1, fixture.ViewModel.AnalysisSummary!.ExistingInstrumentCount);
        Assert.Equal(0, fixture.ViewModel.AnalysisSummary.ProposedInstrumentCount);
        Assert.Equal("Existing Instrument", Assert.Single(fixture.ViewModel.Instruments).Resolution);
        Assert.Equal("Existing", Assert.Single(fixture.ViewModel.Trades).Resolution);
    }

    [Fact]
    public async Task ConfirmImportRequiresDialogApprovalAndPreservesPreviewWhenCancelled()
    {
        Fixture fixture = await CreateReadyFixtureAsync();
        fixture.Dialog.ConfirmationResult = false;

        await fixture.ViewModel.ConfirmImportCommand.ExecuteAsync(null);

        Assert.Equal(0, fixture.ImportStore.CallCount);
        Assert.Equal(ImportWorkflowPhase.PreviewReady, fixture.ViewModel.Phase);
        Assert.NotNull(fixture.ViewModel.PreviewSummary);
        Assert.Null(fixture.ViewModel.ImportErrorMessage);
    }

    [Fact]
    public async Task ConfirmImportShowsCommittedCountsAndRaisesOneCompletionEvent()
    {
        Fixture fixture = await CreateReadyFixtureAsync();
        fixture.Dialog.ConfirmationResult = true;
        fixture.ImportStore.Result = new TradovateImportResult(
            TradovateImportStatus.Imported,
            3,
            2,
            1,
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            [Guid.NewGuid()],
            [Guid.NewGuid(), Guid.NewGuid()]);
        ImportCommittedEventArgs? committed = null;
        int eventCount = 0;
        fixture.ViewModel.ImportCommitted += (_, args) =>
        {
            eventCount++;
            committed = args;
        };

        await fixture.ViewModel.ConfirmImportCommand.ExecuteAsync(null);

        Assert.Equal(ImportWorkflowPhase.Completed, fixture.ViewModel.Phase);
        Assert.Equal("Imported", fixture.ViewModel.ImportResultStatus);
        Assert.Equal(3, fixture.ViewModel.ImportedTradeCount);
        Assert.Equal(2, fixture.ViewModel.SkippedDuplicateTradeCount);
        Assert.Equal(1, fixture.ViewModel.CreatedInstrumentCount);
        Assert.True(fixture.ViewModel.HasImportResult);
        Assert.NotNull(fixture.ViewModel.PreviewSummary);
        Assert.False(fixture.ViewModel.ConfirmImportCommand.CanExecute(null));
        Assert.Equal(1, eventCount);
        Assert.Equal(3, committed!.ImportedTradeCount);
        Assert.Equal(1, committed.CreatedInstrumentCount);
        Assert.Equal(1, fixture.ImportStore.CallCount);
        ConfirmationDialogRequest request = Assert.IsType<ConfirmationDialogRequest>(
            fixture.Dialog.ConfirmationRequest);
        Assert.Equal("Import Tradovate trades?", request.Title);
        Assert.Equal("Import Trades", request.ConfirmButtonText);
        Assert.False(request.IsDestructive);
        Assert.Contains("fills.csv", request.Message, StringComparison.Ordinal);
        Assert.Contains("Tradovate Account", request.Message, StringComparison.Ordinal);
        Assert.Contains("Trades in preview: 1", request.Message, StringComparison.Ordinal);
        Assert.Contains("New Instruments: 1", request.Message, StringComparison.Ordinal);
        Assert.Contains("remain unknown", request.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoChangesIsSuccessfulAndDoesNotRaiseCompletionEvent()
    {
        Fixture fixture = await CreateReadyFixtureAsync();
        fixture.Dialog.ConfirmationResult = true;
        fixture.ImportStore.Result = new TradovateImportResult(
            TradovateImportStatus.NoChanges,
            0,
            1,
            0,
            [],
            [],
            [Guid.NewGuid()]);
        int eventCount = 0;
        fixture.ViewModel.ImportCommitted += (_, _) => eventCount++;

        await fixture.ViewModel.ConfirmImportCommand.ExecuteAsync(null);

        Assert.Equal(ImportWorkflowPhase.Completed, fixture.ViewModel.Phase);
        Assert.Equal("No changes", fixture.ViewModel.ImportResultStatus);
        Assert.Equal(1, fixture.ViewModel.SkippedDuplicateTradeCount);
        Assert.NotNull(fixture.ViewModel.ImportSuccessMessage);
        Assert.Null(fixture.ViewModel.ImportErrorMessage);
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public async Task ReferenceDataChangedDisablesConfirmationAndRequiresPreviewRebuild()
    {
        Fixture fixture = await CreateReadyFixtureAsync();
        fixture.Dialog.ConfirmationResult = true;
        fixture.ImportStore.Result = TradovateImportResult.Blocked(
            TradovateImportConflictCodes.ReferenceDataChanged,
            "Unsafe implementation detail must not be shown.");

        await fixture.ViewModel.ConfirmImportCommand.ExecuteAsync(null);

        Assert.Equal(ImportWorkflowPhase.FileAnalyzed, fixture.ViewModel.Phase);
        Assert.False(fixture.ViewModel.ConfirmImportCommand.CanExecute(null));
        Assert.Contains(
            TradovateImportConflictCodes.ReferenceDataChanged,
            fixture.ViewModel.ImportErrorMessage,
            StringComparison.Ordinal);
        Assert.Contains("Rebuild the preview", fixture.ViewModel.ImportErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Unsafe implementation", fixture.ViewModel.ImportErrorMessage, StringComparison.Ordinal);

        await fixture.ViewModel.BuildPreviewCommand.ExecuteAsync(null);

        Assert.Equal(ImportWorkflowPhase.PreviewReady, fixture.ViewModel.Phase);
        Assert.True(fixture.ViewModel.ConfirmImportCommand.CanExecute(null));
        Assert.Null(fixture.ViewModel.ImportErrorMessage);
    }

    [Fact]
    public async Task MissingAccountResultClearsSelectionAndRefreshesAccountOptions()
    {
        Fixture fixture = await CreateReadyFixtureAsync();
        fixture.Dialog.ConfirmationResult = true;
        fixture.ImportStore.Result = TradovateImportResult.Blocked(
            TradovateImportConflictCodes.TradingAccountNotFound,
            "Trading Account internal identity no longer exists.");

        await fixture.ViewModel.ConfirmImportCommand.ExecuteAsync(null);

        Assert.Null(fixture.ViewModel.SelectedAccount);
        Assert.Equal(2, fixture.AccountReader.CallCount);
        Assert.Equal(ImportWorkflowPhase.FileAnalyzed, fixture.ViewModel.Phase);
        Assert.Contains(
            "Select an Account and rebuild the preview",
            fixture.ViewModel.ImportErrorMessage,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "internal identity",
            fixture.ViewModel.ImportErrorMessage,
            StringComparison.Ordinal);
        Assert.False(fixture.ViewModel.ConfirmImportCommand.CanExecute(null));
    }

    [Fact]
    public async Task ConfirmImportPreventsConcurrentDuplicateSubmission()
    {
        Fixture fixture = await CreateReadyFixtureAsync();
        fixture.Dialog.ConfirmationResult = true;
        fixture.ImportStore.PendingResult = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Task first = fixture.ViewModel.ConfirmImportCommand.ExecuteAsync(null);
        await fixture.ImportStore.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(fixture.ViewModel.ConfirmImportCommand.CanExecute(null));
        Assert.Equal(1, fixture.ImportStore.CallCount);

        fixture.ImportStore.PendingResult.SetResult(fixture.ImportStore.Result);
        await first;
        Assert.Equal(1, fixture.ImportStore.CallCount);
        Assert.False(fixture.ViewModel.ConfirmImportCommand.CanExecute(null));
    }

    [Fact]
    public async Task UnexpectedImportFailureUsesSafeMessageAndKeepsPreviewUsable()
    {
        Fixture fixture = await CreateReadyFixtureAsync();
        fixture.Dialog.ConfirmationResult = true;
        fixture.ImportStore.Exception = new InvalidOperationException(
            "journal.db at C:\\sensitive\\path failed");

        await fixture.ViewModel.ConfirmImportCommand.ExecuteAsync(null);

        Assert.Equal(ImportWorkflowPhase.PreviewReady, fixture.ViewModel.Phase);
        Assert.Equal(
            "Tradovate import could not be completed.",
            fixture.ViewModel.ImportErrorMessage);
        Assert.DoesNotContain("journal.db", fixture.ViewModel.ImportErrorMessage, StringComparison.Ordinal);
        Assert.True(fixture.ViewModel.ConfirmImportCommand.CanExecute(null));
    }

    [Fact]
    public async Task ResetClearsTransientWorkflowButKeepsLoadedAccounts()
    {
        Fixture fixture = CreateFixture();
        await fixture.ViewModel.EnsureLoadedAsync();
        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);

        fixture.ViewModel.ResetTransientState();
        await fixture.ViewModel.EnsureLoadedAsync();

        Assert.Equal(ImportWorkflowPhase.Idle, fixture.ViewModel.Phase);
        Assert.Null(fixture.ViewModel.SelectedFileName);
        Assert.Null(fixture.ViewModel.AnalysisSummary);
        Assert.Single(fixture.ViewModel.Accounts);
        Assert.Equal(1, fixture.AccountReader.CallCount);
    }

    private static async Task<Fixture> CreateReadyFixtureAsync()
    {
        Fixture fixture = CreateFixture();
        await fixture.ViewModel.EnsureLoadedAsync();
        await fixture.ViewModel.SelectCsvCommand.ExecuteAsync(null);
        fixture.ViewModel.SelectedAccount = Assert.Single(fixture.ViewModel.Accounts);
        await fixture.ViewModel.BuildPreviewCommand.ExecuteAsync(null);
        Assert.Equal(ImportWorkflowPhase.PreviewReady, fixture.ViewModel.Phase);
        return fixture;
    }

    private static Fixture CreateFixture(
        bool isAccountActive = true,
        bool accountExistsForPreparation = true,
        string brokerSymbol = "MNQU6",
        DateTime? sourceTimestamp = null,
        bool invalidCsv = false,
        bool useExistingInstrument = false,
        bool isInstrumentActive = true,
        bool instrumentAppearsOnPreview = false)
    {
        Guid accountId = Guid.NewGuid();
        var accountReader = new FakeTradingAccountReader();
        AccountListItem[] accountList =
        [
            new AccountListItem(
                accountId,
                "Tradovate Account",
                TradingAccountType.Personal,
                "Tradovate",
                "SIM-1",
                "USD",
                50000m,
                isAccountActive),
        ];
        accountReader.EnqueueResult(accountList);
        accountReader.EnqueueResult(accountList);
        TradingAccountDetails? accountDetails = accountExistsForPreparation
            ? new TradingAccountDetails(
                accountId,
                "Tradovate Account",
                TradingAccountType.Personal,
                "Tradovate",
                "SIM-1",
                "USD",
                50000m,
                isAccountActive,
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"))
            : null;
        for (int index = 0; index < 5; index++)
        {
            accountReader.EnqueueDetailResult(accountDetails);
        }
        var instrumentReader = new FakeInstrumentReader();
        var existingInstrument = new InstrumentListItem(
                    Guid.NewGuid(),
                    "MNQ",
                    "User MNQ",
                    AssetClass.Futures,
                    "User Exchange",
                    "USD",
                    0.25m,
                    0.50m,
                    2m,
                    isInstrumentActive);
        IReadOnlyList<InstrumentListItem> initialInstruments = useExistingInstrument
            ? [existingInstrument]
            : [];
        IReadOnlyList<InstrumentListItem> previewInstruments =
            useExistingInstrument || instrumentAppearsOnPreview
                ? [existingInstrument]
                : [];
        instrumentReader.EnqueueResult(initialInstruments);
        for (int index = 0; index < 4; index++)
        {
            instrumentReader.EnqueueResult(previewInstruments);
        }
        DateTime timestamp = sourceTimestamp ??
            new DateTime(2026, 9, 14, 16, 30, 0, DateTimeKind.Unspecified);
        TradovateCsvParseResult parse = invalidCsv
            ? new TradovateCsvParseResult(
                rows: [],
                diagnostics:
                [
                    new TradovateCsvDiagnostic(
                        TradovateCsvDiagnosticSeverity.Error,
                        "INVALID_TEST_CSV",
                        1,
                        2,
                        "qty",
                        "The source row is invalid."),
                ],
                sourceRecordCount: 1,
                rejectedRecordCount: 1,
                isHeaderUsable: true)
            : CreateParseResult(brokerSymbol, timestamp);
        TradovateExecutionReconstructionResult reconstruction =
            CreateReconstruction(
                brokerSymbol,
                timestamp,
                invalidCsv
                    ? TradovateReconstructionStatus.Blocked
                    : TradovateReconstructionStatus.Reconstructed);
        var picker = new FakeCsvFilePicker();
        var importStore = new FakeImportStore();
        var dialog = new FakeDialogService();
        var preparationService = new TradovateImportPreparationService(accountReader);
        var viewModel = new ImportViewModel(
            accountReader,
            new FixedParser(parse),
            new FixedReconstructor(reconstruction),
            new TradovateInstrumentResolver(instrumentReader),
            preparationService,
            new TradovateImportPreviewBuilder(),
            picker,
            new ImportTradovateTradesUseCase(
                preparationService,
                importStore,
                new FixedTimeProvider()),
            dialog);
        return new Fixture(
            viewModel,
            accountReader,
            instrumentReader,
            picker,
            importStore,
            dialog);
    }

    private static TradovateCsvParseResult CreateParseResult(
        string symbol,
        DateTime timestamp) =>
        new(
            [new TradovateMatchedFillRow(
                1, 2, symbol, "0.00", "Decimal", 0.25m,
                "buy-1", "sell-1", 1m, 24000m, 24010m, 10m,
                timestamp, timestamp.AddMinutes(10), "00:10:00")],
            diagnostics: [],
            sourceRecordCount: 1,
            rejectedRecordCount: 0,
            isHeaderUsable: true);

    private static TradovateExecutionReconstructionResult CreateReconstruction(
        string symbol,
        DateTime timestamp,
        TradovateReconstructionStatus status)
    {
        var entry = new TradovateReconstructedExecution(
            symbol, ExecutionSide.Buy, "buy-1", 1m, 24000m,
            timestamp, 0.25m, [1], [2]);
        var exit = new TradovateReconstructedExecution(
            symbol, ExecutionSide.Sell, "sell-1", 1m, 24010m,
            timestamp.AddMinutes(10), 0.25m, [1], [2]);
        var candidate = new TradovateTradeCandidate(
            symbol,
            TradeDirection.Long,
            [entry, exit],
            timestamp,
            timestamp.AddMinutes(10),
            1m,
            1m,
            0m,
            TradovateReconstructionStatus.Reconstructed,
            [1],
            []);
        return new TradovateExecutionReconstructionResult(
            [entry, exit],
            [candidate],
            matchedPairs: [],
            symbolReconciliations: [],
            diagnostics: [],
            sourceRecordCount: 1,
            status);
    }

    private sealed class FixedParser(TradovateCsvParseResult result) : ITradovateCsvParser
    {
        public Task<TradovateCsvParseResult> ParseAsync(
            Stream source,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }

    private sealed class FixedReconstructor(TradovateExecutionReconstructionResult result)
        : ITradovateExecutionReconstructor
    {
        public TradovateExecutionReconstructionResult Reconstruct(
            TradovateCsvParseResult parseResult,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
    }

    private sealed class FakeCsvFilePicker : ITradovateCsvFilePicker
    {
        public TrackingMemoryStream Stream { get; } = new();

        public int CallCount { get; private set; }

        public bool ReturnNull { get; set; }

        public TradovateCsvFileSelection? Pick()
        {
            CallCount++;
            return ReturnNull
                ? null
                : new TradovateCsvFileSelection("fills.csv", Stream);
        }
    }

    private sealed class TrackingMemoryStream : MemoryStream
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class FakeImportStore : ITradovateImportStore
    {
        public int CallCount { get; private set; }

        public Exception? Exception { get; set; }

        public TradovateImportResult Result { get; set; } = new(
            TradovateImportStatus.Imported,
            1,
            0,
            1,
            [Guid.NewGuid()],
            [Guid.NewGuid()],
            []);

        public TaskCompletionSource<bool> Called { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<TradovateImportResult>? PendingResult { get; set; }

        public async Task<TradovateImportResult> ImportAsync(
            TradovateImportRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Called.TrySetResult(true);
            cancellationToken.ThrowIfCancellationRequested();
            if (Exception is not null)
            {
                throw Exception;
            }

            return PendingResult is null
                ? Result
                : await PendingResult.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed record Fixture(
        ImportViewModel ViewModel,
        FakeTradingAccountReader AccountReader,
        FakeInstrumentReader InstrumentReader,
        FakeCsvFilePicker FilePicker,
        FakeImportStore ImportStore,
        FakeDialogService Dialog);
}
