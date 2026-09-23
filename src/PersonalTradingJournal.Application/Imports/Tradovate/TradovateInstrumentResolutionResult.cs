namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed class TradovateInstrumentResolutionResult
{
    public TradovateInstrumentResolutionResult(
        IEnumerable<TradovateBrokerSymbolMapping> brokerSymbolMappings,
        IEnumerable<TradovateCanonicalInstrumentResolution> canonicalInstrumentResolutions,
        IEnumerable<TradovateInstrumentResolutionDiagnostic> diagnostics,
        TradovateInstrumentResolutionOverallStatus status)
    {
        ArgumentNullException.ThrowIfNull(brokerSymbolMappings);
        ArgumentNullException.ThrowIfNull(canonicalInstrumentResolutions);
        ArgumentNullException.ThrowIfNull(diagnostics);

        BrokerSymbolMappings = Array.AsReadOnly(brokerSymbolMappings.ToArray());
        CanonicalInstrumentResolutions = Array.AsReadOnly(
            canonicalInstrumentResolutions.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        Status = status;
    }

    public IReadOnlyList<TradovateBrokerSymbolMapping> BrokerSymbolMappings { get; }

    public IReadOnlyList<TradovateCanonicalInstrumentResolution> CanonicalInstrumentResolutions { get; }

    public IReadOnlyList<TradovateInstrumentCreationProposal> CreationProposals =>
        CanonicalInstrumentResolutions
            .Where(resolution => resolution.CreationProposal is not null)
            .Select(resolution => resolution.CreationProposal!)
            .ToArray();

    public IReadOnlyList<TradovateInstrumentResolutionDiagnostic> Diagnostics { get; }

    public TradovateInstrumentResolutionOverallStatus Status { get; }

    public bool IsReadyForPreview =>
        Status == TradovateInstrumentResolutionOverallStatus.ReadyForPreview &&
        BrokerSymbolMappings.Count > 0 &&
        BrokerSymbolMappings.All(mapping =>
            mapping.CanonicalSymbol is not null &&
            (mapping.Status is TradovateInstrumentResolutionStatus.ExistingInstrument or
                TradovateInstrumentResolutionStatus.ProposedCreation) &&
            CanonicalInstrumentResolutions.Count(resolution =>
                string.Equals(
                    resolution.CanonicalSymbol,
                    mapping.CanonicalSymbol,
                    StringComparison.Ordinal) &&
                resolution.Status == mapping.Status &&
                (resolution.Status != TradovateInstrumentResolutionStatus.ExistingInstrument ||
                 resolution.ExistingInstrument is not null) &&
                (resolution.Status != TradovateInstrumentResolutionStatus.ProposedCreation ||
                 resolution.CreationProposal is not null)) == 1) &&
        Diagnostics.All(diagnostic =>
            diagnostic.Severity != TradovateReconstructionDiagnosticSeverity.Error);
}
