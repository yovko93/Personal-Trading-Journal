namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed class TradovateInstrumentResolutionDiagnostic
{
    public TradovateInstrumentResolutionDiagnostic(
        TradovateReconstructionDiagnosticSeverity severity,
        string code,
        string? canonicalSymbol,
        IEnumerable<string> sourceBrokerSymbols,
        IEnumerable<Guid> matchingInstrumentIds,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(sourceBrokerSymbols);
        ArgumentNullException.ThrowIfNull(matchingInstrumentIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Severity = severity;
        Code = code;
        CanonicalSymbol = canonicalSymbol;
        SourceBrokerSymbols = Array.AsReadOnly(sourceBrokerSymbols
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray());
        MatchingInstrumentIds = Array.AsReadOnly(matchingInstrumentIds
            .Distinct()
            .Order()
            .ToArray());
        Message = message;
    }

    public TradovateReconstructionDiagnosticSeverity Severity { get; }

    public string Code { get; }

    public string? CanonicalSymbol { get; }

    public IReadOnlyList<string> SourceBrokerSymbols { get; }

    public IReadOnlyList<Guid> MatchingInstrumentIds { get; }

    public string Message { get; }
}
