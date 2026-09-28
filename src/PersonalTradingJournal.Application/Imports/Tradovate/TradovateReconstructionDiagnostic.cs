namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed class TradovateReconstructionDiagnostic
{
    public TradovateReconstructionDiagnostic(
        TradovateReconstructionDiagnosticSeverity severity,
        string code,
        string? brokerSymbol,
        IEnumerable<int> sourceRecordIndices,
        IEnumerable<string> externalFillIds,
        string message)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(sourceRecordIndices);
        ArgumentNullException.ThrowIfNull(externalFillIds);
        ArgumentNullException.ThrowIfNull(message);

        Severity = severity;
        Code = code;
        BrokerSymbol = brokerSymbol;
        SourceRecordIndices = Array.AsReadOnly(sourceRecordIndices.Distinct().Order().ToArray());
        ExternalFillIds = Array.AsReadOnly(
            externalFillIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        Message = message;
    }

    public TradovateReconstructionDiagnosticSeverity Severity { get; }

    public string Code { get; }

    public string? BrokerSymbol { get; }

    public IReadOnlyList<int> SourceRecordIndices { get; }

    public IReadOnlyList<string> ExternalFillIds { get; }

    public string Message { get; }
}
