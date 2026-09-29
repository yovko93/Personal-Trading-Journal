namespace PersonalTradingJournal.Application.Imports.Topstep;

public sealed record TopstepCsvDiagnostic(
    TopstepCsvDiagnosticSeverity Severity,
    string Code,
    int? SourceRecordIndex,
    int? SourceLineNumber,
    string? FieldName,
    string Message);

public enum TopstepCsvDiagnosticSeverity
{
    Warning,
    Error,
}
