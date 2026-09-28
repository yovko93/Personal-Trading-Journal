namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed class TradovateImportPreparationResult
{
    public TradovateImportPreparationResult(
        TradovateImportAccountSnapshot? accountSnapshot,
        TradovateInstrumentResolutionResult instrumentResolution,
        IEnumerable<TradovatePreparedExecution> preparedExecutions,
        IEnumerable<TradovatePreparedTradeCandidate> preparedCandidates,
        IEnumerable<TradovateImportPreparationDiagnostic> diagnostics,
        TradovateImportPreparationStatus status)
    {
        ArgumentNullException.ThrowIfNull(instrumentResolution);
        ArgumentNullException.ThrowIfNull(preparedExecutions);
        ArgumentNullException.ThrowIfNull(preparedCandidates);
        ArgumentNullException.ThrowIfNull(diagnostics);

        AccountSnapshot = accountSnapshot;
        InstrumentResolution = instrumentResolution;
        PreparedExecutions = Array.AsReadOnly(preparedExecutions.ToArray());
        PreparedCandidates = Array.AsReadOnly(preparedCandidates.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        Status = status;
    }

    public TradovateImportAccountSnapshot? AccountSnapshot { get; }

    public string SourceTimeZoneId => Common.Time.TradingTimePolicy.TradovateSourceTimeZoneId;

    public string TradingTimeZoneId => Common.Time.TradingTimePolicy.TradingTimeZoneId;

    public TradovateInstrumentResolutionResult InstrumentResolution { get; }

    public IReadOnlyList<TradovatePreparedExecution> PreparedExecutions { get; }

    public IReadOnlyList<TradovatePreparedTradeCandidate> PreparedCandidates { get; }

    public IReadOnlyList<TradovateImportPreparationDiagnostic> Diagnostics { get; }

    public TradovateImportPreparationStatus Status { get; }

    public bool IsReadyForPreview =>
        Status == TradovateImportPreparationStatus.ReadyForPreview &&
        AccountSnapshot is not null &&
        InstrumentResolution.IsReadyForPreview &&
        PreparedExecutions.Count > 0 &&
        PreparedCandidates.Count > 0 &&
        PreparedCandidates.All(candidate =>
            candidate.TradingAccountId == AccountSnapshot.TradingAccountId &&
            candidate.OrderedExecutions.Count > 0 &&
            (candidate.ExistingInstrumentId.HasValue ^
             candidate.InstrumentCreationProposal is not null)) &&
        Diagnostics.All(diagnostic =>
            diagnostic.Severity != TradovateReconstructionDiagnosticSeverity.Error);
}
