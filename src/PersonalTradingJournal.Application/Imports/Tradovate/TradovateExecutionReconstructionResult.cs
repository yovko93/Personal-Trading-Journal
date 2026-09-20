namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed class TradovateExecutionReconstructionResult
{
    public TradovateExecutionReconstructionResult(
        IEnumerable<TradovateReconstructedExecution> executions,
        IEnumerable<TradovateTradeCandidate> candidates,
        IEnumerable<TradovateMatchedPairEvidence> matchedPairs,
        IEnumerable<TradovateSymbolReconciliation> symbolReconciliations,
        IEnumerable<TradovateReconstructionDiagnostic> diagnostics,
        int sourceRecordCount,
        TradovateReconstructionStatus status)
    {
        ArgumentNullException.ThrowIfNull(executions);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(matchedPairs);
        ArgumentNullException.ThrowIfNull(symbolReconciliations);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (sourceRecordCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRecordCount));
        }

        Executions = Array.AsReadOnly(executions.ToArray());
        Candidates = Array.AsReadOnly(candidates.ToArray());
        MatchedPairs = Array.AsReadOnly(matchedPairs.ToArray());
        SymbolReconciliations = Array.AsReadOnly(symbolReconciliations.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        SourceRecordCount = sourceRecordCount;
        Status = status;
    }

    public IReadOnlyList<TradovateReconstructedExecution> Executions { get; }

    public IReadOnlyList<TradovateTradeCandidate> Candidates { get; }

    public IReadOnlyList<TradovateMatchedPairEvidence> MatchedPairs { get; }

    public IReadOnlyList<TradovateSymbolReconciliation> SymbolReconciliations { get; }

    public IReadOnlyList<TradovateReconstructionDiagnostic> Diagnostics { get; }

    public int SourceRecordCount { get; }

    public int ReconciledSourceRecordCount => MatchedPairs.Count;

    public int UniqueBuyFillCount => Executions.Count(execution =>
        execution.Side == Domain.Trades.ExecutionSide.Buy);

    public int UniqueSellFillCount => Executions.Count(execution =>
        execution.Side == Domain.Trades.ExecutionSide.Sell);

    public TradovateReconstructionStatus Status { get; }

    public bool IsEligibleForAutomaticImport =>
        Status == TradovateReconstructionStatus.Reconstructed;

    public bool IsSourceCompletenessIndependentlyVerified => false;
}
