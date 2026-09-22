using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Application.Instruments;
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
        Assert.False(option.IsActive);
        Assert.Contains("Inactive", option.DisplayText, StringComparison.Ordinal);
        Assert.Null(fixture.ViewModel.SelectedAccount);
        Assert.Equal(1, fixture.AccountReader.CallCount);
        Assert.Equal(0, fixture.FilePicker.CallCount);
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
        Assert.Equal("ExistingInstrument", Assert.Single(fixture.ViewModel.Instruments).Resolution);
        Assert.Equal("Existing", Assert.Single(fixture.ViewModel.Trades).Resolution);
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

    private static Fixture CreateFixture(
        bool isAccountActive = true,
        bool accountExistsForPreparation = true,
        string brokerSymbol = "MNQU6",
        DateTime? sourceTimestamp = null,
        bool invalidCsv = false,
        bool useExistingInstrument = false)
    {
        Guid accountId = Guid.NewGuid();
        var accountReader = new FakeTradingAccountReader();
        accountReader.EnqueueResult(
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
        ]);
        accountReader.EnqueueDetailResult(accountExistsForPreparation
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
            : null);
        var instrumentReader = new FakeInstrumentReader();
        instrumentReader.EnqueueResult(useExistingInstrument
            ?
            [
                new InstrumentListItem(
                    Guid.NewGuid(),
                    "MNQ",
                    "Micro E-mini Nasdaq-100",
                    AssetClass.Futures,
                    "CME",
                    "USD",
                    0.25m,
                    0.50m,
                    2m,
                    true),
            ]
            : []);
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
        var viewModel = new ImportViewModel(
            accountReader,
            new FixedParser(parse),
            new FixedReconstructor(reconstruction),
            new TradovateInstrumentResolver(instrumentReader),
            new TradovateImportPreparationService(accountReader),
            new TradovateImportPreviewBuilder(),
            picker);
        return new Fixture(viewModel, accountReader, picker);
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

    private sealed record Fixture(
        ImportViewModel ViewModel,
        FakeTradingAccountReader AccountReader,
        FakeCsvFilePicker FilePicker);
}
