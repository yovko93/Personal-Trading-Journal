namespace PersonalTradingJournal.Application.Imports.Topstep;

public sealed class TopstepTradeReconstructionResult
{
    public TopstepTradeReconstructionResult(
        TopstepCsvParseResult source,
        IEnumerable<TopstepTradeCandidate> candidates,
        IEnumerable<TopstepReconstructionDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(diagnostics);
        Source = source;
        Candidates = Array.AsReadOnly(candidates.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>Retains parser diagnostics and every accepted normalized row for traceability.</summary>
    public TopstepCsvParseResult Source { get; }
    public IReadOnlyList<TopstepTradeCandidate> Candidates { get; }
    public IReadOnlyList<TopstepReconstructionDiagnostic> Diagnostics { get; }

    /// <summary>Ready for further row-level review, NOT authorization for automatic import.</summary>
    public bool CanUseRowCandidates => Source.IsCompleteInputValid && Candidates.Count > 0 &&
        Diagnostics.All(d => d.Severity != TopstepReconstructionDiagnosticSeverity.Error);

    public bool IsPositionGroupingVerified => false;
}
