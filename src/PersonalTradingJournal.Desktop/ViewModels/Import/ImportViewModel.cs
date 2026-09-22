using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Imports.Tradovate;
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
    private bool _accountsLoaded;
    private bool _isLoadingAccounts;
    private long _workflowVersion;
    private long _previewVersion;

    public ImportViewModel(
        ITradingAccountReader accountReader,
        ITradovateCsvParser csvParser,
        ITradovateExecutionReconstructor executionReconstructor,
        TradovateInstrumentResolver instrumentResolver,
        TradovateImportPreparationService preparationService,
        TradovateImportPreviewBuilder previewBuilder,
        ITradovateCsvFilePicker filePicker)
    {
        ArgumentNullException.ThrowIfNull(accountReader);
        ArgumentNullException.ThrowIfNull(csvParser);
        ArgumentNullException.ThrowIfNull(executionReconstructor);
        ArgumentNullException.ThrowIfNull(instrumentResolver);
        ArgumentNullException.ThrowIfNull(preparationService);
        ArgumentNullException.ThrowIfNull(previewBuilder);
        ArgumentNullException.ThrowIfNull(filePicker);
        _accountReader = accountReader;
        _csvParser = csvParser;
        _executionReconstructor = executionReconstructor;
        _instrumentResolver = instrumentResolver;
        _preparationService = preparationService;
        _previewBuilder = previewBuilder;
        _filePicker = filePicker;
        SelectCsvCommand = new AsyncRelayCommand(SelectCsvAsync, CanSelectCsv);
        BuildPreviewCommand = new AsyncRelayCommand(BuildPreviewAsync, CanBuildPreview);
    }

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
            InvalidatePreview();
            BuildPreviewCommand.NotifyCanExecuteChanged();
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

    public bool IsBusy =>
        _isLoadingAccounts ||
        Phase is ImportWorkflowPhase.AnalyzingFile or ImportWorkflowPhase.PreparingPreview;

    public bool HasAnalysis => AnalysisSummary is not null;

    public bool HasPreview => PreviewSummary is not null;

    public IAsyncRelayCommand SelectCsvCommand { get; }

    public IAsyncRelayCommand BuildPreviewCommand { get; }

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
        _reconstruction?.IsEligibleForAutomaticImport == true &&
        _instrumentResolution?.IsReadyForPreview == true;

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
        Phase = ImportWorkflowPhase.PreparingPreview;
        try
        {
            TradovateImportPreparationResult preparation =
                await _preparationService.PrepareAsync(
                    _reconstruction!,
                    _instrumentResolution!,
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
            PreviewSummary = preview.Summary;
            Instruments = preview.Instruments.Select(ToInstrumentItem).ToArray();
            Trades = preview.Trades.Select(ToTradeItem).ToArray();
            Diagnostics = preview.Diagnostics.Select(ToDiagnosticItem).ToArray();
            Phase = preview.IsStructurallyReady
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

    private void InvalidatePreview()
    {
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
        _analysisDiagnostics = [];
        SelectedFileName = null;
        AnalysisSummary = null;
        PreviewSummary = null;
        Instruments = [];
        Trades = [];
        Diagnostics = [];
        WorkflowErrorMessage = null;
        BuildPreviewCommand.NotifyCanExecuteChanged();
    }

    private static ImportInstrumentItem ToInstrumentItem(
        TradovateCanonicalInstrumentResolution item)
    {
        TradovateInstrumentCreationProposal? proposal = item.CreationProposal;
        string details = item.Status switch
        {
            TradovateInstrumentResolutionStatus.ExistingInstrument =>
                $"Existing Instrument · {(item.IsExistingInstrumentActive == true ? "Active" : "Inactive")} · {item.ResolvedCurrency ?? "Currency unavailable"} · Tick {item.SourceTickSize:G29}",
            TradovateInstrumentResolutionStatus.ProposedCreation when proposal is not null =>
                $"New proposal · {proposal.DisplayName} · {proposal.AssetClass} · {proposal.Exchange ?? "Exchange unavailable"} · {proposal.Currency} · Tick {proposal.TickSize:G29} / {proposal.TickValue:G29} · {proposal.MetadataSource}",
            TradovateInstrumentResolutionStatus.RequiresUserInput => "Metadata required",
            _ => "Resolution blocked",
        };
        return new ImportInstrumentItem(
            item.CanonicalSymbol,
            string.Join(", ", item.SourceBrokerSymbols),
            item.Status.ToString(),
            details,
            item.Status == TradovateInstrumentResolutionStatus.RequiresUserInput);
    }

    private static ImportInstrumentItem ToInstrumentItem(
        TradovateImportPreviewInstrumentItem item) =>
        new(
            item.CanonicalSymbol,
            string.Join(", ", item.SourceBrokerSymbols),
            item.Status.ToString(),
            item.Status == TradovateInstrumentResolutionStatus.ExistingInstrument
                ? $"Existing Instrument · {(item.IsExistingInstrumentActive == true ? "Active" : "Inactive")} · {item.Currency ?? "Currency unavailable"} · Tick {item.TickSize:G29}"
                : $"New proposal · {item.DisplayName} · {item.AssetClass} · {item.Exchange ?? "Exchange unavailable"} · {item.Currency} · Tick {item.TickSize:G29} / {item.TickValue:G29} · {item.MetadataSource}",
            item.Status == TradovateInstrumentResolutionStatus.RequiresUserInput);

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
