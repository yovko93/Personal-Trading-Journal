namespace PersonalTradingJournal.Application.Imports.Topstep;

public sealed class TopstepEconomicsReconciliationResult
{
    internal TopstepEconomicsReconciliationResult(
        TopstepTradeReconstructionResult source,
        IEnumerable<TopstepReconciledTradeEconomics> rows,
        IEnumerable<TopstepEconomicsDiagnostic> diagnostics)
    {
        Source = source;
        Rows = Array.AsReadOnly(rows.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>Includes the unchanged M11.2 candidates, provenance and boundary warnings.</summary>
    public TopstepTradeReconstructionResult Source { get; }
    public IReadOnlyList<TopstepReconciledTradeEconomics> Rows { get; }
    public IReadOnlyList<TopstepEconomicsDiagnostic> Diagnostics { get; }

    /// <summary>Economics gate only; not import authorization or proof of broker-position grouping.</summary>
    public bool IsEconomicallyReconciled => Source.CanUseRowCandidates &&
        Rows.Count == Source.Candidates.Count && Rows.Count > 0 &&
        Diagnostics.Count == 0 && Rows.All(row => row.IsReconciled);
}
