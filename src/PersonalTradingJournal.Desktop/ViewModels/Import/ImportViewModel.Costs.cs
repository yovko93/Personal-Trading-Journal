using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Desktop.Imports;

namespace PersonalTradingJournal.Desktop.ViewModels.Import;

public sealed partial class ImportViewModel
{
    private ITradovateCostCsvParser? _costCsvParser;
    private IReadOnlyList<TradovateCostFill>? _costFills;
    private IReadOnlyList<TradovateCashEntry>? _costCash;
    private TradovateCostSources? _verifiedSources;
    private string? _costSourceError;
    private string? _fillsFileName;
    private string? _cashFileName;
    private string? _verifiedCostSummary;

    public IAsyncRelayCommand SelectFillsCommand { get; private set; } = null!;
    public IAsyncRelayCommand SelectCashHistoryCommand { get; private set; } = null!;
    public IRelayCommand ClearCostSourcesCommand { get; private set; } = null!;
    public string CostSourceFiles => $"Fills: {_fillsFileName ?? "not selected"} · Cash History: {_cashFileName ?? "not selected"}";
    public string? VerifiedCostSummary { get => _verifiedCostSummary; private set => SetProperty(ref _verifiedCostSummary, value); }

    private bool CanSelectCosts() => _costCsvParser is not null && !IsBusy && !IsTopstep &&
        _parseResult is not null && Volatile.Read(ref _isImportSubmissionInProgress) == 0;

    private async Task SelectCostsAsync(bool fills, CancellationToken token)
    {
        if (!CanSelectCosts()) return;
        TradovateCsvFileSelection? selection;
        try { selection = _filePicker.Pick(); }
        catch
        {
            WorkflowErrorMessage = "The supporting CSV picker could not be opened. Existing source selection is unchanged.";
            return;
        }
        if (selection is null) return;
        long version = _workflowVersion;
        _previewVersion++;
        InvalidatePreview();
        // An attempted replacement invalidates that source even if parsing fails.
        // Successfully selecting the other export must never resurrect the old one.
        if (fills) { _costFills = null; _fillsFileName = null; }
        else { _costCash = null; _cashFileName = null; }
        _costSourceError = "Cost source selection is incomplete. Reselect both supporting exports or explicitly use Performance only.";
        Phase = ImportWorkflowPhase.AnalyzingFile;
        try
        {
            await using (selection)
            {
                if (fills)
                {
                    var parsed = await _costCsvParser!.ParseFillsAsync(selection.Content, token);
                    if (version != _workflowVersion) return;
                    _costFills = parsed;
                    _fillsFileName = selection.FileName;
                }
                else
                {
                    var parsed = await _costCsvParser!.ParseCashAsync(selection.Content, token);
                    if (version != _workflowVersion) return;
                    _costCash = parsed;
                    _cashFileName = selection.FileName;
                }
                _costSourceError = null;
                WorkflowErrorMessage = null;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (version != _workflowVersion) return;
            _costSourceError = error is TradovateCostSourceException ? error.Message :
                "Supporting CSV could not be read within the import limits. Reselect a complete valid export.";
            WorkflowErrorMessage = _costSourceError;
        }
        finally
        {
            if (version == _workflowVersion)
            {
                Phase = ImportWorkflowPhase.FileAnalyzed;
                OnPropertyChanged(nameof(CostSourceFiles));
            }
        }
    }

    private void ClearCostSources()
    {
        _costFills = null;
        _costCash = null;
        _costSourceError = null;
        _fillsFileName = null;
        _cashFileName = null;
        _verifiedSources = null;
        VerifiedCostSummary = null;
        OnPropertyChanged(nameof(CostSourceFiles));
    }

    private void VerifyCosts(TradovateImportPreparationResult preparation)
    {
        if (_costSourceError is not null) throw new TradovateCostSourceException(_costSourceError);
        if (_costFills is null && _costCash is null) return;
        if (_costFills is null || _costCash is null)
            throw new TradovateCostSourceException("Select both Fills and Cash History for verified costs, or explicitly use Performance only with unknown Net.");
        var sources = new TradovateCostSources(_costFills, _costCash);
        var costs = TradovateCostReconciler.Reconcile(preparation, _reconstruction!.MatchedPairs, sources);
        _verifiedSources = sources;
        VerifiedCostSummary = FormattableString.Invariant($"Verified USD · {costs.Start:yyyy-MM-dd}–{costs.End:yyyy-MM-dd} · Gross {costs.Gross:N2} · Costs {costs.Costs:N2} · Net {costs.Net:N2}. Existing imports: fill unknown costs only; preserve known costs, block conflicts.");
    }
}
