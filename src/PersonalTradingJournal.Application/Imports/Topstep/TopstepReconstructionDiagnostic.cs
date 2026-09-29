namespace PersonalTradingJournal.Application.Imports.Topstep;

public enum TopstepReconstructionDiagnosticSeverity
{
    Warning,
    Error,
}

public sealed record TopstepSourceReference(int SourceRecordIndex, int SourceLineNumber);

public sealed class TopstepReconstructionDiagnostic
{
    public TopstepReconstructionDiagnostic(
        TopstepReconstructionDiagnosticSeverity severity,
        string code,
        IEnumerable<TopstepSourceReference> sourceReferences,
        string message)
    {
        ArgumentNullException.ThrowIfNull(sourceReferences);
        Severity = severity;
        Code = code;
        SourceReferences = Array.AsReadOnly(sourceReferences.Distinct()
            .OrderBy(reference => reference.SourceRecordIndex).ThenBy(reference => reference.SourceLineNumber).ToArray());
        Message = message;
    }

    public TopstepReconstructionDiagnosticSeverity Severity { get; }
    public string Code { get; }
    public IReadOnlyList<TopstepSourceReference> SourceReferences { get; }
    public string Message { get; }
}
