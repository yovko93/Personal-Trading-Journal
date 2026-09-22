namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed class TradovateCanonicalInstrumentResolution
{
    public TradovateCanonicalInstrumentResolution(
        string canonicalSymbol,
        IEnumerable<string> sourceBrokerSymbols,
        decimal? sourceTickSize,
        TradovateInstrumentResolutionStatus status,
        Guid? existingInstrumentId,
        bool? isExistingInstrumentActive,
        IEnumerable<Guid> matchingInstrumentIds,
        TradovateInstrumentCreationProposal? creationProposal,
        IEnumerable<string> diagnosticCodes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalSymbol);
        ArgumentNullException.ThrowIfNull(sourceBrokerSymbols);
        ArgumentNullException.ThrowIfNull(matchingInstrumentIds);
        ArgumentNullException.ThrowIfNull(diagnosticCodes);

        CanonicalSymbol = canonicalSymbol;
        SourceBrokerSymbols = Array.AsReadOnly(sourceBrokerSymbols
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray());
        SourceTickSize = sourceTickSize;
        Status = status;
        ExistingInstrumentId = existingInstrumentId;
        IsExistingInstrumentActive = isExistingInstrumentActive;
        MatchingInstrumentIds = Array.AsReadOnly(matchingInstrumentIds
            .Distinct()
            .Order()
            .ToArray());
        CreationProposal = creationProposal;
        DiagnosticCodes = Array.AsReadOnly(diagnosticCodes
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray());
    }

    public string CanonicalSymbol { get; }

    public IReadOnlyList<string> SourceBrokerSymbols { get; }

    public decimal? SourceTickSize { get; }

    public TradovateInstrumentResolutionStatus Status { get; }

    public Guid? ExistingInstrumentId { get; }

    public bool? IsExistingInstrumentActive { get; }

    public IReadOnlyList<Guid> MatchingInstrumentIds { get; }

    public TradovateInstrumentCreationProposal? CreationProposal { get; }

    public IReadOnlyList<string> DiagnosticCodes { get; }
}
