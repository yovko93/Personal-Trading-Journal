namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed record TradovateCsvDiagnostic(
    TradovateCsvDiagnosticSeverity Severity,
    string Code,
    int? SourceRecordIndex,
    int? SourceLineNumber,
    string? FieldName,
    string Message);
