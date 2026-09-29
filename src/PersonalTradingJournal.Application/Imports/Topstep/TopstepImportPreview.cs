using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Imports.Topstep;

public enum TopstepPreviewDiagnosticStage { Csv, Reconstruction, Account, Instrument, Economics, Preview }
public enum TopstepPreviewSeverity { Warning, Error }
public enum TopstepPreviewState { Blocked, ReadyForConfirmation }

public sealed record TopstepPreviewSourceIdentity(string FileName, long ByteCount, string ContentSha256);

public sealed record TopstepPreviewDiagnostic(
    TopstepPreviewDiagnosticStage Stage, TopstepPreviewSeverity Severity, string Code,
    string Message, string RecoveryGuidance, IReadOnlyList<TopstepSourceReference> SourceReferences,
    string? ContractName = null, string? FieldName = null, int? SourceLineNumber = null);

/// <summary>Affirmative confirmation approves only the exact Instrument proposals in this snapshot.</summary>
public sealed record TopstepImportConfirmation(string SnapshotFingerprint, IReadOnlyCollection<string> ApprovedInstrumentSymbols);

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
    public const string PolicyVersion = "topstep-preview-v3";

    internal TopstepImportPreview(TopstepPreviewSourceIdentity sourceIdentity, string fingerprint,
        TopstepReferencePreparationResult preparation, TopstepPreviewSummary summary,
        IEnumerable<TopstepPreviewCandidate> candidates, IEnumerable<TopstepPreviewDiagnostic> diagnostics,
        IEnumerable<TopstepInstrumentVerification> verifications)
    {
        SourceIdentity = sourceIdentity;
        SnapshotFingerprint = fingerprint;
        Preparation = preparation;
        Summary = summary;
        Candidates = Array.AsReadOnly(candidates.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        VerifiedExistingInstruments = Array.AsReadOnly(verifications.ToArray());
    }

    public string SourceProvider => "Topstep";
    public string CandidateLabel => "TopstepX Trade candidates";
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
    public IReadOnlyList<TopstepInstrumentVerification> VerifiedExistingInstruments { get; }
    public bool IsEligibleForReview => Preparation.IsReadyForPreview && Summary.BlockingErrorCount == 0;
    public TopstepPreviewState State => IsEligibleForReview ? TopstepPreviewState.ReadyForConfirmation : TopstepPreviewState.Blocked;

    /// <summary>
    /// Checks snapshot-bound approval only. The store must independently rebuild/revalidate source and
    /// references transactionally; this check never establishes freshness or authorizes writes.
    /// </summary>
    public bool AcceptsConfirmation(TopstepImportConfirmation? confirmation) => IsEligibleForReview && confirmation is not null &&
        string.Equals(SnapshotFingerprint, confirmation.SnapshotFingerprint, StringComparison.Ordinal) &&
        confirmation.ApprovedInstrumentSymbols is not null &&
        CreationProposals.Select(p => p.CanonicalSymbol).ToHashSet(StringComparer.Ordinal).SetEquals(confirmation.ApprovedInstrumentSymbols);
}
