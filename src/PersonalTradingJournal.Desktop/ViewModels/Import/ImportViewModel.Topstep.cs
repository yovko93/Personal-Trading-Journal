using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Imports;
using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Imports;

namespace PersonalTradingJournal.Desktop.ViewModels.Import;

public sealed partial class ImportViewModel
{
    private readonly IImportCsvFormatDetector _formatDetector;
    private readonly ITopstepCsvParser _topstepParser;
    private readonly ITopstepTradeCandidateReconstructor _topstepReconstructor;
    private readonly TopstepImportPreviewBuilder _topstepPreviewBuilder;
    private readonly ImportTopstepTradesUseCase _topstepImport;
    private ImportCsvFormat _sourceFormat;
    private TopstepCsvParseResult? _topstepParse;
    private TopstepImportPreview? _topstepPreview;
    private Func<Stream>? _topstepOpenRead;
    private bool _topstepReviewStale;
    private CancellationTokenSource? _topstepConfirmationCancellation;

    public IRelayCommand CancelOperationCommand { get; }
    public ImportCsvFormat SourceFormat
    {
        get => _sourceFormat;
        private set
        {
            SetProperty(ref _sourceFormat, value);
            OnPropertyChanged(nameof(IsTopstep));
            OnPropertyChanged(nameof(IsTradovate));
            OnPropertyChanged(nameof(CandidateLabel));
        }
    }
    public bool IsTopstep => SourceFormat == ImportCsvFormat.Topstep;
    public bool IsTradovate => SourceFormat == ImportCsvFormat.Tradovate;
    public string CandidateLabel => IsTopstep ? "CLOSED-ROW CANDIDATES" : "TRADE CANDIDATES";
    public TopstepImportPreview? TopstepPreview => _topstepPreview;
    public IReadOnlyList<TopstepReviewChoice> ReviewChoices { get; private set; } = [];
    public IReadOnlyList<TopstepCandidatePresentation> TopstepCandidates { get; private set; } = [];
    public string TopstepTotals => _topstepPreview?.Summary.ReconciledTotals is { } totals
        ? $"Gross {Money(totals.CalculatedGross, totals.Currency)} · Fees {Money(totals.Fees, totals.Currency)} · Commissions {Money(totals.Commissions, totals.Currency)} · Net {Money(totals.Net, totals.Currency)}"
        : "Verified totals unavailable until all blocking errors are resolved.";

    private async Task AnalyzeTopstepAsync(TradovateCsvFileSelection selection, Stream source, long version, CancellationToken token)
    {
        TopstepCsvParseResult parse = await Task.Run(() => _topstepParser.ParseAsync(source, token), token);
        TopstepTradeReconstructionResult reconstruction = await Task.Run(() => _topstepReconstructor.Reconstruct(parse, token), token);
        token.ThrowIfCancellationRequested();
        if (version != _workflowVersion) return;
        _topstepParse = parse;
        _topstepOpenRead = selection.OpenRead;
        AnalysisSummary = new(parse.SourceRecordCount, parse.ValidRecordCount, parse.RejectedRecordCount, 0, 0,
            reconstruction.Candidates.Count, 0, 0, 0);
        _analysisDiagnostics = parse.Diagnostics.Select(d => new ImportDiagnosticItem("CSV", d.Severity.ToString(), d.Code, d.Message, $"Line {d.SourceLineNumber}"))
            .Concat(reconstruction.Diagnostics.Select(d => new ImportDiagnosticItem("Reconstruction", d.Severity.ToString(), d.Code, d.Message,
                SourceLines(d.SourceReferences)))).ToArray();
        Diagnostics = _analysisDiagnostics;
        if (_topstepOpenRead is null) WorkflowErrorMessage = "Select this CSV again to enable source revalidation at confirmation.";
        Phase = reconstruction.CanUseRowCandidates ? ImportWorkflowPhase.FileAnalyzed : ImportWorkflowPhase.Blocked;
    }

    private async Task BuildTopstepPreviewAsync(CancellationToken token)
    {
        long workflow = _workflowVersion, version = _previewVersion;
        Guid account = SelectedAccount!.Id;
        Func<Stream> open = _topstepOpenRead!;
        string file = SelectedFileName!;
        ClearTopstepPreview();
        ClearImportResult();
        WorkflowErrorMessage = null;
        Phase = ImportWorkflowPhase.PreparingPreview;
        try
        {
            // Reopen the source, not rounded presentation values or a silently retained byte copy.
            await using Stream source = open();
            using var snapshot = new MemoryStream();
            await source.CopyToAsync(snapshot, token);
            snapshot.Position = 0;
            ImportCsvFormatResult format = await Task.Run(() => _formatDetector.DetectAsync(snapshot, token), token);
            token.ThrowIfCancellationRequested();
            if (workflow != _workflowVersion || version != _previewVersion) return;
            if (format.Format != ImportCsvFormat.Topstep)
            {
                WorkflowErrorMessage = "CSV_FORMAT_CHANGED: The file no longer has the reviewed Topstep header. Select CSV again to detect the current format.";
                Phase = ImportWorkflowPhase.Blocked;
                return;
            }
            snapshot.Position = 0;
            TopstepImportPreview preview = await Task.Run(() => _topstepPreviewBuilder.BuildAsync(file, snapshot, account,
                TopstepCostInterpretation.SeparateReportedRoundTurnTotalsUsd, cancellationToken: token), token);
            token.ThrowIfCancellationRequested();
            if (workflow != _workflowVersion || version != _previewVersion || SelectedAccount?.Id != account) return;
            _topstepPreview = preview;
            ReviewChoices = preview.ReviewRequirements.Select(r => new TopstepReviewChoice(r, () => ConfirmImportCommand.NotifyCanExecuteChanged(),
                r.Kind == TopstepPreviewReviewKind.InstrumentCreationApproval
                    ? ProposalDescription(preview.CreationProposals.Single(p => p.CanonicalSymbol == r.CanonicalSymbol)) : "")).ToArray();
            TopstepCandidates = preview.Candidates.Select(c => new TopstepCandidatePresentation(c)).ToArray();
            AnalysisSummary = new(preview.Summary.SourceRowCount, preview.Summary.AcceptedRowCount, preview.Summary.RejectedRowCount, 0, 0,
                preview.Summary.ClosedRowCandidateCount, preview.Instruments.Count, preview.Summary.ExistingContractResolutionCount, preview.Summary.ProposedInstrumentCount);
            Instruments = preview.Instruments.Select(i => new ImportInstrumentItem(i.Identity?.CanonicalSymbol ?? i.SourceContract,
                i.SourceContract, i.Status.ToString(), i.ExistingInstrument is { } e ? e.IsActive ? "Active" : "Inactive" : "",
                i.ExistingInstrument?.DisplayName ?? i.CreationProposal?.DisplayName ?? "—",
                (i.ExistingInstrument?.AssetClass ?? i.CreationProposal?.AssetClass)?.ToString() ?? "—",
                i.ExistingInstrument?.Exchange ?? i.CreationProposal?.Exchange ?? "—", i.Pricing?.Currency ?? "—",
                FormatDecimal(i.ExistingInstrument?.TickSize ?? i.CreationProposal?.TickSize),
                FormatDecimal(i.ExistingInstrument?.TickValue ?? i.CreationProposal?.TickValue),
                i.CreationProposal is null ? "" : "Will be created only when the import is confirmed.", i.Status == TopstepInstrumentResolutionStatus.Blocked)).ToArray();
            Diagnostics = preview.Diagnostics.Select(d => new ImportDiagnosticItem(d.Stage.ToString(), d.Severity.ToString(), d.Code,
                $"{d.Message} {d.RecoveryGuidance}", SourceLines(d.SourceReferences))).ToArray();
            NotifyTopstepPresentation();
            Phase = preview.IsEligibleForReview ? ImportWorkflowPhase.PreviewReady : ImportWorkflowPhase.Blocked;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (workflow == _workflowVersion && version == _previewVersion) Phase = ImportWorkflowPhase.FileAnalyzed;
        }
        catch
        {
            if (workflow == _workflowVersion && version == _previewVersion)
            {
                WorkflowErrorMessage = "Topstep preview could not be built. Check file access, select the current CSV and retry.";
                Phase = ImportWorkflowPhase.Failed;
            }
        }
    }

    private TopstepPreviewReview CurrentTopstepReview() => new(_topstepPreview?.SnapshotFingerprint ?? "",
        ReviewChoices.Where(c => c.IsAccepted).Select(c => c.Requirement.Key).ToArray());

    private async Task ConfirmTopstepImportAsync(CancellationToken token)
    {
        if (!CanConfirmImport() || Interlocked.CompareExchange(ref _isImportSubmissionInProgress, 1, 0) != 0) return;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        _topstepConfirmationCancellation = cancellation;
        token = cancellation.Token;
        long workflow = _workflowVersion, version = _previewVersion;
        TopstepImportPreview preview = _topstepPreview!;
        TopstepPreviewReview review = CurrentTopstepReview();
        Func<Stream> open = _topstepOpenRead!;
        try
        {
            ConfirmImportCommand.NotifyCanExecuteChanged();
            if (!_dialogService.Confirm(new ConfirmationDialogRequest("Import Topstep closed-row records?",
                $"Account: {preview.DestinationAccount!.Name}\nFile: {preview.SourceIdentity.FileName}\nClosed-row records: {preview.Candidates.Count}\n{TopstepTotals}\n\nThese are reported closed rows, not verified broker positions. Reviewed Instruments will be created only in the atomic import. Exact duplicates are skipped.",
                "Import Trades", "Cancel", isDestructive: false))) return;
            token.ThrowIfCancellationRequested();
            if (workflow != _workflowVersion || version != _previewVersion) return;
            ClearImportResult();
            Phase = ImportWorkflowPhase.Importing;
            await using Stream source = open();
            TopstepImportResult result = await Task.Run(() => _topstepImport.ImportAsync(preview, review, preview.SourceIdentity.FileName, source, token), token);
            // The use case invalidates retained data after commit even if navigation discarded this presentation.
            if (workflow != _workflowVersion || version != _previewVersion) return;
            if (result.Status == TopstepImportStatus.Blocked)
            {
                _topstepReviewStale = true;
                foreach (TopstepReviewChoice choice in ReviewChoices) choice.IsAccepted = false;
                ImportErrorMessage = $"{result.ConflictCode}: {result.Message} Rebuild Preview and review again; select another file/account if needed.";
                Phase = ImportWorkflowPhase.Blocked;
                return;
            }
            ImportResultStatus = result.Status == TopstepImportStatus.Imported ? "Imported" : "NoChanges";
            ImportSuccessMessage = result.Status == TopstepImportStatus.Imported
                ? "Topstep closed-row import completed successfully."
                : "No new Trades: every reviewed closed-row record was already imported in this account.";
            ImportedTradeCount = result.ImportedTradeCount;
            SkippedDuplicateTradeCount = result.SkippedDuplicateTradeCount;
            CreatedInstrumentCount = result.CreatedInstrumentCount;
            ClearCompletedPreviewState();
            Phase = ImportWorkflowPhase.Completed;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (workflow == _workflowVersion && version == _previewVersion)
            {
                ImportErrorMessage = "Import cancelled. The reviewed preview is retained; no partial import was saved.";
                Phase = ImportWorkflowPhase.PreviewReady;
            }
        }
        catch
        {
            if (workflow == _workflowVersion && version == _previewVersion)
            {
                ImportErrorMessage = "Topstep import could not be completed. Check file access and retry or rebuild preview. No source contents are included in this message.";
                Phase = ImportWorkflowPhase.PreviewReady;
            }
        }
        finally
        {
            _topstepConfirmationCancellation = null;
            Volatile.Write(ref _isImportSubmissionInProgress, 0);
            ConfirmImportCommand.NotifyCanExecuteChanged();
        }
    }

    private void ClearTopstepPreview()
    {
        _topstepPreview = null;
        _topstepReviewStale = false;
        ReviewChoices = [];
        TopstepCandidates = [];
        if (IsTopstep)
        {
            Instruments = [];
            Diagnostics = _analysisDiagnostics;
            if (Phase != ImportWorkflowPhase.AnalyzingFile) Phase = ImportWorkflowPhase.FileAnalyzed;
        }
        NotifyTopstepPresentation();
    }

    private void ClearTopstepState()
    {
        ClearTopstepPreview();
        _topstepParse = null;
        _topstepOpenRead = null;
        SourceFormat = ImportCsvFormat.Unknown;
    }

    private void NotifyTopstepPresentation()
    {
        OnPropertyChanged(nameof(TopstepPreview));
        OnPropertyChanged(nameof(TopstepCandidates));
        OnPropertyChanged(nameof(TopstepTotals));
        OnPropertyChanged(nameof(ReviewChoices));
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(ShowConfirmationSection));
    }

    internal static string SourceLines(IReadOnlyList<TopstepSourceReference> rows) =>
        rows.Count == 0 ? "" : "Source lines: " + string.Join(", ", rows.Select(r => r.SourceLineNumber).Distinct());
    private static string Money(decimal value, string currency) => $"{value.ToString("G29", CultureInfo.CurrentCulture)} {currency}";
    private static string ProposalDescription(TopstepInstrumentCreationProposal p) =>
        $"{p.CanonicalSymbol} · {p.DisplayName} · {p.AssetClass} · {p.Exchange} · {p.Currency} · Tick {p.TickSize:G29} / value {p.TickValue:G29} · Point value {p.PointValue:G29}. Evidence: {p.MetadataSource}";
}

public sealed class TopstepReviewChoice(TopstepPreviewReviewRequirement requirement, Action changed, string specifications = "") : ObservableObject
{
    private bool _isAccepted;
    public TopstepPreviewReviewRequirement Requirement { get; } = requirement;
    public string Description => $"{Requirement.Kind}: {Requirement.Message} {specifications} {ImportViewModel.SourceLines(Requirement.SourceReferences)}";
    public bool IsAccepted
    {
        get => _isAccepted;
        set { if (SetProperty(ref _isAccepted, value)) changed(); }
    }
}

public sealed class TopstepCandidatePresentation(TopstepPreviewCandidate candidate)
{
    public TopstepPreviewCandidate Candidate { get; } = candidate;
    public string Identity => $"Source line {Candidate.SourceLineNumber} · {Candidate.ContractName} · {Candidate.Direction} · {Candidate.ResolutionState}";
    public string Quantity => $"Closed-row quantity: {Candidate.ClosedRowQuantity.ToString("G29", CultureInfo.CurrentCulture)} (not broker peak exposure)";
    public string Times => $"Entry UTC: {Candidate.EnteredAtUtc:yyyy-MM-dd HH:mm:ss} · Exit UTC: {Candidate.ExitedAtUtc:yyyy-MM-dd HH:mm:ss}";
    public string Prices => $"Entry: {Candidate.EntryPrice.ToString("F2", CultureInfo.CurrentCulture)} · Exit: {Candidate.ExitPrice.ToString("F2", CultureInfo.CurrentCulture)}";
    public string Economics => $"Reported Gross: {Candidate.ReportedGross:G29} · Fees: {Candidate.Fees:G29} · Commissions: {Candidate.Commissions:G29} · Verified Net: {Candidate.CalculatedNet?.ToString("G29", CultureInfo.CurrentCulture) ?? "—"} {Candidate.Currency ?? "(currency unverified)"}";
}
