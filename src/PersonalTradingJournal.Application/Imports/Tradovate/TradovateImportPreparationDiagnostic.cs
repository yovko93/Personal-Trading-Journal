namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed record TradovateImportPreparationDiagnostic(
    TradovateReconstructionDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? BrokerSymbol = null,
    IReadOnlyList<int>? SourceRecordIndices = null,
    IReadOnlyList<int>? SourceLineNumbers = null);
