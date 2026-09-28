using PersonalTradingJournal.Application.Accounts;

namespace PersonalTradingJournal.Application.Imports.Topstep;

public sealed record TopstepMappedCandidate(
    TopstepReconciledTradeEconomics Economics,
    TradingAccountDetails DestinationAccount,
    TopstepInstrumentResolution Instrument)
{
    public string SourceProvider => "Topstep";
    public string SourceId => Economics.Candidate.SourceRow.Id;
    public Guid DestinationTradingAccountId => DestinationAccount.Id;
    // No journal Trade ID, execution ID or deduplication key is generated here.
}

public sealed class TopstepReferencePreparationResult
{
    internal TopstepReferencePreparationResult(Guid? selectedAccountId, TradingAccountDetails? account,
        IEnumerable<TopstepInstrumentResolution> instruments, TopstepEconomicsReconciliationResult economics,
        IEnumerable<TopstepMappedCandidate> rows, IEnumerable<TopstepReferenceDiagnostic> diagnostics)
    {
        SelectedAccountId = selectedAccountId;
        Account = account;
        Instruments = Array.AsReadOnly(instruments.ToArray());
        CreationProposals = Array.AsReadOnly(Instruments.Where(i => i.CreationProposal is not null)
            .Select(i => i.CreationProposal!).Distinct().ToArray());
        Economics = economics;
        Rows = Array.AsReadOnly(rows.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public string PolicyVersion => "topstep-reference-v1";
    public string SourceProvider => "Topstep";
    public Guid? SelectedAccountId { get; }
    public TradingAccountDetails? Account { get; }
    public IReadOnlyList<TopstepInstrumentResolution> Instruments { get; }
    public IReadOnlyList<TopstepInstrumentCreationProposal> CreationProposals { get; }
    public TopstepEconomicsReconciliationResult Economics { get; }
    public IReadOnlyList<TopstepMappedCandidate> Rows { get; }
    public IReadOnlyList<TopstepReferenceDiagnostic> Diagnostics { get; }
    public bool RequiresInstrumentCreationApproval => Instruments.Any(i => i.Status == TopstepInstrumentResolutionStatus.ProposedCreation);
    /// <summary>Read-only preview readiness, never permission to create reference data or import.</summary>
    public bool IsReadyForPreview => Account is not null && Rows.Count > 0 &&
        Rows.Count == Economics.Source.Candidates.Count && Economics.IsEconomicallyReconciled &&
        Instruments.All(i => i.Status != TopstepInstrumentResolutionStatus.Blocked) &&
        Diagnostics.All(d => d.Severity != TopstepReferenceDiagnosticSeverity.Error);
}
