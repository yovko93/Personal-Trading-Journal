using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Imports.Topstep;

public enum TopstepPreviewDiagnosticStage { Csv, Reconstruction, Account, Instrument, Economics, Preview }
public enum TopstepPreviewSeverity { Warning, Error }
public enum TopstepPreviewState { Blocked, RequiresReview }
public enum TopstepPreviewReviewKind { WarningAcknowledgment, InstrumentCreationApproval }

public sealed record TopstepPreviewSourceIdentity(string FileName, long ByteCount, string ContentSha256);

public sealed record TopstepPreviewDiagnostic(
    TopstepPreviewDiagnosticStage Stage, TopstepPreviewSeverity Severity, string Code,
    string Message, string RecoveryGuidance, IReadOnlyList<TopstepSourceReference> SourceReferences,
    string? ContractName = null, string? FieldName = null, int? SourceLineNumber = null);

public sealed record TopstepPreviewReviewRequirement(
    string Key, TopstepPreviewReviewKind Kind, string Message,
    IReadOnlyList<TopstepSourceReference> SourceReferences, string? CanonicalSymbol = null);

/// <summary>Review decisions apply only to this exact preview. This is not an import command.</summary>
public sealed record TopstepPreviewReview(string SnapshotFingerprint, IReadOnlyCollection<string> AcceptedRequirementKeys);

public sealed record TopstepPreviewTotals(string Currency, decimal ReportedGross, decimal CalculatedGross,
    decimal Fees, decimal Commissions, decimal Net);

public sealed record TopstepPreviewSummary(int SourceRowCount, int AcceptedRowCount, int RejectedRowCount,
    int ClosedRowCandidateCount, int ExistingContractResolutionCount, int ProposedContractResolutionCount,
    int BlockedContractResolutionCount, int ProposedInstrumentCount, int WarningCount, int BlockingErrorCount,
    TopstepPreviewTotals? ReconciledTotals);

/// <summary>Exact row-local economics, not a recovered broker position or a raw execution.</summary>
public sealed class TopstepPreviewCandidate
{
    internal TopstepPreviewCandidate(TopstepReconciledTradeEconomics economics, TopstepInstrumentResolution resolution)
    {
        Economics = economics;
        Instrument = resolution;
    }

    public string RecordKind => "Topstep closed-row record";
    public TopstepReconciledTradeEconomics Economics { get; }
    public TopstepInstrumentResolution Instrument { get; }
    public TopstepSourceRow SourceRow => Economics.Candidate.SourceRow;
    public int SourceRecordIndex => SourceRow.SourceRecordIndex;
    public int SourceLineNumber => SourceRow.SourceLineNumber;
    public string ContractName => SourceRow.ContractName;
    public TradeDirection Direction => Economics.Candidate.Direction;
    public decimal ClosedRowQuantity => SourceRow.Size;
    public DateTimeOffset EnteredAtUtc => SourceRow.EnteredAtUtc;
    public DateTimeOffset ExitedAtUtc => SourceRow.ExitedAtUtc;
    public decimal EntryPrice => SourceRow.EntryPrice;
    public decimal ExitPrice => SourceRow.ExitPrice;
    public decimal ReportedGross => SourceRow.SourceReportedPnL;
    public decimal Fees => SourceRow.SourceReportedFees;
    public decimal Commissions => SourceRow.SourceReportedCommissions;
    public decimal? CalculatedGross => Economics.CalculatedGrossPnL;
    public decimal? CalculatedNet => Economics.NetPnL;
    public string? Currency => Economics.Pricing?.Currency;
    public TopstepInstrumentResolutionStatus ResolutionState => Instrument.Status;
    public bool ArePositionBoundariesVerified => false;
}

public sealed class TopstepImportPreview
{
    public const string PolicyVersion = "topstep-preview-v1";

    internal TopstepImportPreview(TopstepPreviewSourceIdentity sourceIdentity, string fingerprint,
        TopstepReferencePreparationResult preparation, TopstepPreviewSummary summary,
        IEnumerable<TopstepPreviewCandidate> candidates, IEnumerable<TopstepPreviewDiagnostic> diagnostics,
        IEnumerable<TopstepPreviewReviewRequirement> requirements)
    {
        SourceIdentity = sourceIdentity;
        SnapshotFingerprint = fingerprint;
        Preparation = preparation;
        Summary = summary;
        Candidates = Array.AsReadOnly(candidates.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        ReviewRequirements = Array.AsReadOnly(requirements.ToArray());
    }

    public string SourceProvider => "Topstep";
    public string CandidateLabel => "Topstep closed-row records (not verified broker positions)";
    public TopstepPreviewSourceIdentity SourceIdentity { get; }
    public string SnapshotFingerprint { get; }
    public TopstepReferencePreparationResult Preparation { get; }
    public Guid? SelectedAccountId => Preparation.SelectedAccountId;
    public TradingAccountDetails? DestinationAccount => Preparation.Account;
    public TopstepPreviewSummary Summary { get; }
    public IReadOnlyList<TopstepPreviewCandidate> Candidates { get; }
    public IReadOnlyList<TopstepInstrumentResolution> Instruments => Preparation.Instruments;
    public IReadOnlyList<TopstepInstrumentCreationProposal> CreationProposals => Preparation.CreationProposals;
    public IReadOnlyList<TopstepPreviewDiagnostic> Diagnostics { get; }
    public IReadOnlyList<TopstepPreviewReviewRequirement> ReviewRequirements { get; }
    public bool IsEligibleForReview => Preparation.IsReadyForPreview && Summary.BlockingErrorCount == 0;
    public TopstepPreviewState State => IsEligibleForReview ? TopstepPreviewState.RequiresReview : TopstepPreviewState.Blocked;

    /// <summary>
    /// Checks review completeness only. M11.6 must independently rebuild/revalidate source and
    /// references transactionally; this check never establishes freshness or authorizes writes.
    /// </summary>
    public bool MeetsReviewRequirements(TopstepPreviewReview? review) => IsEligibleForReview && review is not null &&
        string.Equals(SnapshotFingerprint, review.SnapshotFingerprint, StringComparison.Ordinal) &&
        review.AcceptedRequirementKeys is not null &&
        ReviewRequirements.Select(r => r.Key).ToHashSet(StringComparer.Ordinal).SetEquals(review.AcceptedRequirementKeys);
}
