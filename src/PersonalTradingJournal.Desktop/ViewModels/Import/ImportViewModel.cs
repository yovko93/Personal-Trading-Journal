using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Imports;
using System.Globalization;

namespace PersonalTradingJournal.Desktop.ViewModels.Import;

public sealed class ImportViewModel : ObservableObject
{
    private readonly ITradingAccountReader _accountReader;
    private readonly ITradovateCsvParser _csvParser;
    private readonly ITradovateExecutionReconstructor _executionReconstructor;
    private readonly TradovateInstrumentResolver _instrumentResolver;
    private readonly TradovateImportPreparationService _preparationService;
    private readonly TradovateImportPreviewBuilder _previewBuilder;
    private readonly ITradovateCsvFilePicker _filePicker;
    private readonly ImportTradovateTradesUseCase _importUseCase;
    private readonly IDialogService _dialogService;
    private IReadOnlyList<ImportAccountOption> _accounts = [];
    private ImportAccountOption? _selectedAccount;
    private ImportWorkflowPhase _phase;
    private string? _selectedFileName;
    private string? _accountErrorMessage;
    private string? _workflowErrorMessage;
    private ImportAnalysisSummary? _analysisSummary;
    private TradovateImportPreviewSummary? _previewSummary;
    private IReadOnlyList<ImportInstrumentItem> _instruments = [];
    private IReadOnlyList<ImportTradePreviewItem> _trades = [];
    private IReadOnlyList<ImportDiagnosticItem> _diagnostics = [];
    private IReadOnlyList<ImportDiagnosticItem> _analysisDiagnostics = [];
    private TradovateCsvParseResult? _parseResult;
    private TradovateExecutionReconstructionResult? _reconstruction;
    private TradovateInstrumentResolutionResult? _instrumentResolution;
    private TradovateImportPreview? _preview;
    private string? _importSuccessMessage;
    private string? _importErrorMessage;
    private string? _importResultStatus;
    private int _importedTradeCount;
    private int _skippedDuplicateTradeCount;
    private int _createdInstrumentCount;
    private bool _accountsLoaded;
    private bool _isLoadingAccounts;
    private int _isImportSubmissionInProgress;
    private long _workflowVersion;
    private long _previewVersion;

    public ImportViewModel(
        ITradingAccountReader accountReader,
        ITradovateCsvParser csvParser,
        ITradovateExecutionReconstructor executionReconstructor,
        TradovateInstrumentResolver instrumentResolver,
        TradovateImportPreparationService preparationService,
        TradovateImportPreviewBuilder previewBuilder,
        ITradovateCsvFilePicker filePicker,
        ImportTradovateTradesUseCase importUseCase,
        IDialogService dialogService)
    {
        ArgumentNullException.ThrowIfNull(accountReader);
        ArgumentNullException.ThrowIfNull(csvParser);
        ArgumentNullException.ThrowIfNull(executionReconstructor);
        ArgumentNullException.ThrowIfNull(instrumentResolver);
        ArgumentNullException.ThrowIfNull(preparationService);
        ArgumentNullException.ThrowIfNull(previewBuilder);
        ArgumentNullException.ThrowIfNull(filePicker);
        ArgumentNullException.ThrowIfNull(importUseCase);
        ArgumentNullException.ThrowIfNull(dialogService);
        _accountReader = accountReader;
        _csvParser = csvParser;
        _executionReconstructor = executionReconstructor;
        _instrumentResolver = instrumentResolver;
        _preparationService = preparationService;
        _previewBuilder = previewBuilder;
        _filePicker = filePicker;
        _importUseCase = importUseCase;
        _dialogService = dialogService;
        SelectCsvCommand = new AsyncRelayCommand(SelectCsvAsync, CanSelectCsv);
        BuildPreviewCommand = new AsyncRelayCommand(BuildPreviewAsync, CanBuildPreview);
        ConfirmImportCommand = new AsyncRelayCommand(ConfirmImportAsync, CanConfirmImport);
    }

    public event EventHandler<ImportCommittedEventArgs>? ImportCommitted;

    public IReadOnlyList<ImportAccountOption> Accounts
    {
        get => _accounts;
        private set => SetProperty(ref _accounts, value);
    }

    public ImportAccountOption? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (!SetProperty(ref _selectedAccount, value))
            {
                return;
            }

            _previewVersion++;
            BuildPreviewCommand.Cancel();
            ConfirmImportCommand.Cancel();
            InvalidatePreview();
            ClearImportResult();
            BuildPreviewCommand.NotifyCanExecuteChanged();
            ConfirmImportCommand.NotifyCanExecuteChanged();
        }
    }

    public ImportWorkflowPhase Phase
    {
        get => _phase;
        private set
        {
            if (SetProperty(ref _phase, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(HasAnalysis));
                OnPropertyChanged(nameof(HasPreview));
                SelectCsvCommand.NotifyCanExecuteChanged();
                BuildPreviewCommand.NotifyCanExecuteChanged();
                ConfirmImportCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? SelectedFileName
    {
        get => _selectedFileName;
        private set => SetProperty(ref _selectedFileName, value);
    }

    public string? AccountErrorMessage
    {
        get => _accountErrorMessage;
        private set => SetProperty(ref _accountErrorMessage, value);
    }

    public string? WorkflowErrorMessage
    {
        get => _workflowErrorMessage;
        private set => SetProperty(ref _workflowErrorMessage, value);
    }

    public ImportAnalysisSummary? AnalysisSummary
    {
        get => _analysisSummary;
        private set => SetProperty(ref _analysisSummary, value);
    }

    public TradovateImportPreviewSummary? PreviewSummary
    {
        get => _previewSummary;
        private set => SetProperty(ref _previewSummary, value);
    }

    public IReadOnlyList<ImportInstrumentItem> Instruments
    {
        get => _instruments;
        private set => SetProperty(ref _instruments, value);
    }

    public IReadOnlyList<ImportTradePreviewItem> Trades
    {
        get => _trades;
        private set => SetProperty(ref _trades, value);
    }

    public IReadOnlyList<ImportDiagnosticItem> Diagnostics
    {
        get => _diagnostics;
        private set => SetProperty(ref _diagnostics, value);
    }

    public string? ImportSuccessMessage
    {
        get => _importSuccessMessage;
        private set => SetProperty(ref _importSuccessMessage, value);
    }

    public string? ImportErrorMessage
    {
        get => _importErrorMessage;
        private set => SetProperty(ref _importErrorMessage, value);
    }

    public string? ImportResultStatus
    {
        get => _importResultStatus;
        private set
        {
            if (SetProperty(ref _importResultStatus, value))
            {
                OnPropertyChanged(nameof(HasImportResult));
            }
        }
    }

    public int ImportedTradeCount
    {
        get => _importedTradeCount;
        private set => SetProperty(ref _importedTradeCount, value);
    }

    public int SkippedDuplicateTradeCount
    {
        get => _skippedDuplicateTradeCount;
        private set => SetProperty(ref _skippedDuplicateTradeCount, value);
    }

    public int CreatedInstrumentCount
    {
        get => _createdInstrumentCount;
        private set => SetProperty(ref _createdInstrumentCount, value);
    }

    public bool IsBusy =>
        _isLoadingAccounts ||
        Phase is ImportWorkflowPhase.AnalyzingFile or
            ImportWorkflowPhase.PreparingPreview or
            ImportWorkflowPhase.Importing;

    public bool HasAnalysis => AnalysisSummary is not null;

    public bool HasPreview => PreviewSummary is not null;

    public bool HasImportResult => ImportResultStatus is not null;

    public IAsyncRelayCommand SelectCsvCommand { get; }

    public IAsyncRelayCommand BuildPreviewCommand { get; }

    public IAsyncRelayCommand ConfirmImportCommand { get; }

    public async Task EnsureLoadedAsync()
    {
        if (_accountsLoaded || _isLoadingAccounts)
        {
            return;
        }

        _isLoadingAccounts = true;
        OnPropertyChanged(nameof(IsBusy));
        try
        {
            IReadOnlyList<AccountListItem> accounts = await _accountReader.GetAllAsync();
            Accounts = accounts
                .OrderByDescending(account => account.IsActive)
                .ThenBy(account => account.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(account => account.Id)
                .Select(account => new ImportAccountOption(
                    account.Id,
                    account.Name,
                    account.AccountType,
                    account.ProviderName,
                    account.ExternalAccountId,
                    account.Currency,
                    account.IsActive))
                .ToArray();
            AccountErrorMessage = null;
            _accountsLoaded = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            AccountErrorMessage = "Trading accounts could not be loaded. Try opening Import again.";
        }
        finally
        {
            _isLoadingAccounts = false;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    public void ResetTransientState()
    {
        SelectCsvCommand.Cancel();
        BuildPreviewCommand.Cancel();
        ConfirmImportCommand.Cancel();
        _workflowVersion++;
        _previewVersion++;
        SelectedAccount = null;
        ClearAnalysis();
        Phase = ImportWorkflowPhase.Idle;
    }

    private bool CanSelectCsv() => !IsBusy;

    private bool CanBuildPreview() =>
        !IsBusy &&
        SelectedAccount is not null &&
        _parseResult is not null &&
        _reconstruction?.IsEligibleForAutomaticImport == true;

    private bool CanConfirmImport() =>
        !IsBusy &&
        Volatile.Read(ref _isImportSubmissionInProgress) == 0 &&
        Phase == ImportWorkflowPhase.PreviewReady &&
        _preview?.IsReadyForConfirmation == true &&
        SelectedAccount is not null &&
        _reconstruction is not null &&
        _instrumentResolution is not null;

    private async Task SelectCsvAsync(CancellationToken cancellationToken)
    {
        TradovateCsvFileSelection? selection;
        try
        {
            selection = _filePicker.Pick();
        }
        catch
        {
            WorkflowErrorMessage = "The CSV file picker could not be opened.";
            Phase = ImportWorkflowPhase.Failed;
            return;
        }

        if (selection is null)
        {
            return;
        }

        long version = ++_workflowVersion;
        _previewVersion++;
        ConfirmImportCommand.Cancel();
        ClearAnalysis();
        SelectedFileName = selection.FileName;
        Phase = ImportWorkflowPhase.AnalyzingFile;
        try
        {
            await using (selection)
            {
                TradovateCsvParseResult parseResult = await _csvParser.ParseAsync(
                    selection.Content,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                TradovateExecutionReconstructionResult reconstruction =
                    _executionReconstructor.Reconstruct(parseResult, cancellationToken);
                TradovateInstrumentResolutionResult resolution =
                    await _instrumentResolver.ResolveAsync(reconstruction, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (version != _workflowVersion)
                {
                    return;
                }

                _parseResult = parseResult;
                _reconstruction = reconstruction;
                _instrumentResolution = resolution;
                AnalysisSummary = new ImportAnalysisSummary(
                    parseResult.SourceRecordCount,
                    parseResult.ValidRecordCount,
                    parseResult.RejectedRecordCount,
                    reconstruction.UniqueBuyFillCount,
                    reconstruction.UniqueSellFillCount,
                    reconstruction.Candidates.Count,
                    resolution.CanonicalInstrumentResolutions.Count,
                    resolution.CanonicalInstrumentResolutions.Count(item =>
                        item.Status == TradovateInstrumentResolutionStatus.ExistingInstrument),
                    resolution.CanonicalInstrumentResolutions.Count(item =>
                        item.Status == TradovateInstrumentResolutionStatus.ProposedCreation));
                Instruments = resolution.CanonicalInstrumentResolutions
                    .OrderBy(item => item.CanonicalSymbol, StringComparer.Ordinal)
                    .Select(ToInstrumentItem)
                    .ToArray();
                _analysisDiagnostics = BuildAnalysisDiagnostics(
                    parseResult,
                    reconstruction,
                    resolution);
                Diagnostics = _analysisDiagnostics;
                Phase = reconstruction.IsEligibleForAutomaticImport && resolution.IsReadyForPreview
                    ? ImportWorkflowPhase.FileAnalyzed
                    : resolution.Status == TradovateInstrumentResolutionOverallStatus.RequiresUserInput
                        ? ImportWorkflowPhase.RequiresUserInput
                        : ImportWorkflowPhase.Blocked;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (version == _workflowVersion)
            {
                ClearAnalysis();
                Phase = ImportWorkflowPhase.Idle;
            }
        }
        catch
        {
            if (version == _workflowVersion)
            {
                WorkflowErrorMessage = "The selected CSV could not be analyzed.";
                Phase = ImportWorkflowPhase.Failed;
            }
        }
    }

    private async Task BuildPreviewAsync(CancellationToken cancellationToken)
    {
        if (!CanBuildPreview())
        {
            return;
        }

        long version = _workflowVersion;
        long previewVersion = _previewVersion;
        Guid accountId = SelectedAccount!.Id;
        InvalidatePreview();
        ClearImportResult();
        WorkflowErrorMessage = null;
        Phase = ImportWorkflowPhase.PreparingPreview;
        try
        {
            TradovateInstrumentResolutionResult resolution =
                await _instrumentResolver.ResolveAsync(_reconstruction!, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (version != _workflowVersion ||
                previewVersion != _previewVersion ||
                SelectedAccount?.Id != accountId)
            {
                return;
            }

            _instrumentResolution = resolution;
            RefreshAnalysisPresentation(resolution);
            if (!resolution.IsReadyForPreview)
            {
                Phase = resolution.Status ==
                    TradovateInstrumentResolutionOverallStatus.RequiresUserInput
                        ? ImportWorkflowPhase.RequiresUserInput
                        : ImportWorkflowPhase.Blocked;
                return;
            }

            TradovateImportPreparationResult preparation =
                await _preparationService.PrepareAsync(
                    _reconstruction!,
                    resolution,
                    accountId,
                    cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (version != _workflowVersion ||
                previewVersion != _previewVersion ||
                SelectedAccount?.Id != accountId)
            {
                return;
            }

            TradovateImportPreview preview = _previewBuilder.Build(
                SelectedFileName!,
                _parseResult!,
                _reconstruction!,
                preparation);
            _preview = preview;
            PreviewSummary = preview.Summary;
            Instruments = preview.Instruments.Select(ToInstrumentItem).ToArray();
            Trades = preview.Trades.Select(ToTradeItem).ToArray();
            Diagnostics = preview.Diagnostics.Select(ToDiagnosticItem).ToArray();
            Phase = preview.IsReadyForConfirmation
                ? ImportWorkflowPhase.PreviewReady
                : preparation.Status == TradovateImportPreparationStatus.RequiresUserInput
                    ? ImportWorkflowPhase.RequiresUserInput
                    : ImportWorkflowPhase.Blocked;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (version == _workflowVersion)
            {
                Phase = ImportWorkflowPhase.FileAnalyzed;
            }
        }
        catch
        {
            if (version == _workflowVersion)
            {
                WorkflowErrorMessage = "The import preview could not be built.";
                Phase = ImportWorkflowPhase.Failed;
            }
        }
    }

    private async Task ConfirmImportAsync(CancellationToken cancellationToken)
    {
        if (!CanConfirmImport())
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _isImportSubmissionInProgress, 1, 0) != 0)
        {
            return;
        }

        try
        {
            ConfirmImportCommand.NotifyCanExecuteChanged();
            TradovateImportPreview preview = _preview!;
            TradovateExecutionReconstructionResult reconstruction = _reconstruction!;
            TradovateInstrumentResolutionResult resolution = _instrumentResolution!;
            ImportAccountOption account = SelectedAccount!;
            long workflowVersion = _workflowVersion;
            long previewVersion = _previewVersion;

            bool confirmed = _dialogService.Confirm(new ConfirmationDialogRequest(
                "Import Tradovate trades?",
                BuildConfirmationMessage(preview, account),
                "Import Trades",
                "Cancel",
                isDestructive: false));
            if (!confirmed)
            {
                return;
            }

            ClearImportResult();
            Phase = ImportWorkflowPhase.Importing;
            try
            {
                TradovateImportResult result = await _importUseCase.ImportAsync(
                    reconstruction,
                    resolution,
                    account.Id,
                    cancellationToken);

                await HandleImportResultAsync(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (workflowVersion == _workflowVersion &&
                    previewVersion == _previewVersion &&
                    ReferenceEquals(preview, _preview))
                {
                    Phase = ImportWorkflowPhase.PreviewReady;
                }
            }
            catch
            {
                if (workflowVersion == _workflowVersion &&
                    previewVersion == _previewVersion)
                {
                    ImportErrorMessage = "Tradovate import could not be completed.";
                    Phase = ImportWorkflowPhase.PreviewReady;
                }
            }
        }
        finally
        {
            Volatile.Write(ref _isImportSubmissionInProgress, 0);
            ConfirmImportCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task HandleImportResultAsync(TradovateImportResult result)
    {
        switch (result.Status)
        {
            case TradovateImportStatus.Imported:
                ImportResultStatus = "Imported";
                ImportSuccessMessage = "Tradovate import completed successfully.";
                ImportedTradeCount = result.ImportedTradeCount;
                SkippedDuplicateTradeCount = result.SkippedDuplicateTradeCount;
                CreatedInstrumentCount = result.CreatedInstrumentCount;
                Phase = ImportWorkflowPhase.Completed;
                ImportCommitted?.Invoke(
                    this,
                    new ImportCommittedEventArgs(
                        result.ImportedTradeCount,
                        result.CreatedInstrumentCount));
                break;
            case TradovateImportStatus.NoChanges:
                ImportResultStatus = "No changes";
                ImportSuccessMessage =
                    "No new trades were imported. All previewed broker executions were already imported.";
                SkippedDuplicateTradeCount = result.SkippedDuplicateTradeCount;
                Phase = ImportWorkflowPhase.Completed;
                break;
            case TradovateImportStatus.Blocked:
                await HandleBlockedImportAsync(result);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported Tradovate import status: {result.Status}.");
        }
    }

    private async Task HandleBlockedImportAsync(TradovateImportResult result)
    {
        _preview = null;
        string guidance = result.ConflictCode switch
        {
            TradovateImportConflictCodes.ReferenceDataChanged =>
                "Reference data changed after the preview. Rebuild the preview before trying again.",
            TradovateImportConflictCodes.TradingAccountNotFound =>
                "The selected Trading Account no longer exists. Select an Account and rebuild the preview.",
            TradovateImportConflictCodes.DeduplicationConflict =>
                "The import overlaps previously imported broker executions in a way that cannot be safely merged.",
            _ => result.Message ?? "The Tradovate import is blocked.",
        };
        if (result.ConflictCode == TradovateImportConflictCodes.TradingAccountNotFound)
        {
            SelectedAccount = null;
            await RefreshAccountsAfterStaleSelectionAsync();
        }
        else
        {
            Phase = ImportWorkflowPhase.FileAnalyzed;
        }

        ImportErrorMessage = string.IsNullOrWhiteSpace(result.ConflictCode)
            ? guidance
            : $"{result.ConflictCode}: {guidance}";
    }

    private async Task RefreshAccountsAfterStaleSelectionAsync()
    {
        _accountsLoaded = false;
        await EnsureLoadedAsync();
    }

    private static string BuildConfirmationMessage(
        TradovateImportPreview preview,
        ImportAccountOption account) =>
        $"File: {preview.Summary.FileName}{Environment.NewLine}" +
        $"Account: {account.DisplayText}{Environment.NewLine}" +
        $"Trades in preview: {preview.Summary.CandidateCount}{Environment.NewLine}" +
        $"New Instruments: {preview.Summary.ProposedInstrumentCount}{Environment.NewLine}" +
        $"Warnings: {preview.Summary.WarningCount}{Environment.NewLine}{Environment.NewLine}" +
        "Commission and fee values are not present in this Tradovate export. " +
        "Imported costs and Net P&L will remain unknown until supplied." +
        $"{Environment.NewLine}{Environment.NewLine}" +
        "The import is transactionally persisted and duplicate broker fills are detected automatically.";

    private void InvalidatePreview()
    {
        _preview = null;
        PreviewSummary = null;
        Trades = [];
        Diagnostics = _analysisDiagnostics;
        if (_parseResult is not null && Phase != ImportWorkflowPhase.AnalyzingFile)
        {
            Phase = _reconstruction?.IsEligibleForAutomaticImport == true &&
                _instrumentResolution?.IsReadyForPreview == true
                    ? ImportWorkflowPhase.FileAnalyzed
                    : _instrumentResolution?.Status ==
                        TradovateInstrumentResolutionOverallStatus.RequiresUserInput
                        ? ImportWorkflowPhase.RequiresUserInput
                        : ImportWorkflowPhase.Blocked;
        }
    }

    private void ClearAnalysis()
    {
        _parseResult = null;
        _reconstruction = null;
        _instrumentResolution = null;
        _preview = null;
        _analysisDiagnostics = [];
        SelectedFileName = null;
        AnalysisSummary = null;
        PreviewSummary = null;
        Instruments = [];
        Trades = [];
        Diagnostics = [];
        WorkflowErrorMessage = null;
        ClearImportResult();
        BuildPreviewCommand.NotifyCanExecuteChanged();
        ConfirmImportCommand.NotifyCanExecuteChanged();
    }

    private void ClearImportResult()
    {
        ImportSuccessMessage = null;
        ImportErrorMessage = null;
        ImportResultStatus = null;
        ImportedTradeCount = 0;
        SkippedDuplicateTradeCount = 0;
        CreatedInstrumentCount = 0;
    }

    private void RefreshAnalysisPresentation(
        TradovateInstrumentResolutionResult resolution)
    {
        AnalysisSummary = new ImportAnalysisSummary(
            _parseResult!.SourceRecordCount,
            _parseResult.ValidRecordCount,
            _parseResult.RejectedRecordCount,
            _reconstruction!.UniqueBuyFillCount,
            _reconstruction.UniqueSellFillCount,
            _reconstruction.Candidates.Count,
            resolution.CanonicalInstrumentResolutions.Count,
            resolution.CanonicalInstrumentResolutions.Count(item =>
                item.Status == TradovateInstrumentResolutionStatus.ExistingInstrument),
            resolution.CanonicalInstrumentResolutions.Count(item =>
                item.Status == TradovateInstrumentResolutionStatus.ProposedCreation));
        Instruments = resolution.CanonicalInstrumentResolutions
            .OrderBy(item => item.CanonicalSymbol, StringComparer.Ordinal)
            .Select(ToInstrumentItem)
            .ToArray();
        _analysisDiagnostics = BuildAnalysisDiagnostics(
            _parseResult,
            _reconstruction,
            resolution);
        Diagnostics = _analysisDiagnostics;
    }

    private static ImportInstrumentItem ToInstrumentItem(
        TradovateCanonicalInstrumentResolution item)
    {
        TradovateInstrumentCreationProposal? proposal = item.CreationProposal;
        TradovateExistingInstrumentSnapshot? existing = item.ExistingInstrument;
        return new ImportInstrumentItem(
            item.CanonicalSymbol,
            string.Join(", ", item.SourceBrokerSymbols),
            ResolutionText(item.Status),
            item.Status == TradovateInstrumentResolutionStatus.ExistingInstrument
                ? item.IsExistingInstrumentActive == true ? "Active" : "Inactive"
                : string.Empty,
            existing?.DisplayName ?? proposal?.DisplayName ?? "—",
            (existing?.AssetClass ?? proposal?.AssetClass)?.ToString() ?? "—",
            existing?.Exchange ?? proposal?.Exchange ?? "—",
            existing?.Currency ?? proposal?.Currency ?? "—",
            FormatDecimal(existing?.TickSize ?? proposal?.TickSize ?? item.SourceTickSize),
            FormatDecimal(existing?.TickValue ?? proposal?.TickValue),
            item.Status == TradovateInstrumentResolutionStatus.ProposedCreation
                ? "Will be created only when the import is confirmed."
                : string.Empty,
            item.Status == TradovateInstrumentResolutionStatus.RequiresUserInput);
    }

    private static ImportInstrumentItem ToInstrumentItem(
        TradovateImportPreviewInstrumentItem item) =>
        new(
            item.CanonicalSymbol,
            string.Join(", ", item.SourceBrokerSymbols),
            ResolutionText(item.Status),
            item.Status == TradovateInstrumentResolutionStatus.ExistingInstrument
                ? item.IsExistingInstrumentActive == true ? "Active" : "Inactive"
                : string.Empty,
            item.DisplayName ?? "—",
            item.AssetClass ?? "—",
            item.Exchange ?? "—",
            item.Currency ?? "—",
            FormatDecimal(item.TickSize),
            FormatDecimal(item.TickValue),
            item.Status == TradovateInstrumentResolutionStatus.ProposedCreation
                ? "Will be created only when the import is confirmed."
                : string.Empty,
            item.Status == TradovateInstrumentResolutionStatus.RequiresUserInput);

    private static string ResolutionText(TradovateInstrumentResolutionStatus status) =>
        status switch
        {
            TradovateInstrumentResolutionStatus.ExistingInstrument => "Existing Instrument",
            TradovateInstrumentResolutionStatus.ProposedCreation => "New Instrument",
            TradovateInstrumentResolutionStatus.RequiresUserInput => "Requires input",
            _ => "Blocked",
        };

    private static string FormatDecimal(decimal? value) =>
        value?.ToString("G29", CultureInfo.InvariantCulture) ?? "—";

    private static ImportTradePreviewItem ToTradeItem(TradovateImportPreviewTradeItem item) =>
        new(
            item.CandidateIndex,
            $"{item.BrokerSymbol} → {item.CanonicalSymbol}",
            item.AccountName,
            item.Direction.ToString(),
            item.OpenedAtNewYork.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            item.ClosedAtNewYork?.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) ?? "Open",
            item.ExecutionCount,
            item.OpeningQuantity.ToString("G29", CultureInfo.InvariantCulture),
            item.WeightedAverageEntryPrice.ToString("G29", CultureInfo.InvariantCulture),
            item.WeightedAverageExitPrice?.ToString("G29", CultureInfo.InvariantCulture) ?? "—",
            item.SourceReportedPnL?.ToString("G29", CultureInfo.InvariantCulture) ?? "—",
            item.InstrumentResolutionStatus == TradovateInstrumentResolutionStatus.ExistingInstrument
                ? "Existing"
                : "New proposal");

    private static IReadOnlyList<ImportDiagnosticItem> BuildAnalysisDiagnostics(
        TradovateCsvParseResult parse,
        TradovateExecutionReconstructionResult reconstruction,
        TradovateInstrumentResolutionResult resolution)
    {
        IEnumerable<ImportDiagnosticItem> csv = parse.Diagnostics.Select(item => new ImportDiagnosticItem(
            "CSV",
            item.Severity.ToString(),
            item.Code,
            item.Message,
            item.SourceLineNumber.HasValue ? $"Line {item.SourceLineNumber}" : null));
        IEnumerable<ImportDiagnosticItem> reconstructed = reconstruction.Diagnostics.Select(item => new ImportDiagnosticItem(
            "Reconstruction",
            item.Severity.ToString(),
            item.Code,
            item.Message,
            item.BrokerSymbol));
        IEnumerable<ImportDiagnosticItem> instruments = resolution.Diagnostics.Select(item => new ImportDiagnosticItem(
            "Instrument",
            item.Severity.ToString(),
            item.Code,
            item.Message,
            item.CanonicalSymbol ?? string.Join(", ", item.SourceBrokerSymbols)));
        return csv.Concat(reconstructed).Concat(instruments)
            .Distinct()
            .OrderBy(item => item.Stage, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ToArray();
    }

    private static ImportDiagnosticItem ToDiagnosticItem(
        TradovateImportPreviewDiagnostic item) =>
        new(
            item.Stage == TradovateImportPreviewDiagnosticStage.Csv ? "CSV" : item.Stage.ToString(),
            item.Severity.ToString(),
            item.Code,
            item.Message,
            item.Context);
}
