namespace PersonalTradingJournal.Application.Imports.Topstep;

public sealed class TopstepCsvParseResult
{
    public TopstepCsvParseResult(
        IEnumerable<TopstepSourceRow> rows,
        IEnumerable<TopstepCsvDiagnostic> diagnostics,
        int sourceRecordCount,
        int rejectedRecordCount,
        bool isHeaderUsable)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(diagnostics);
        TopstepSourceRow[] rowSnapshot = rows.ToArray();
        if (sourceRecordCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRecordCount));
        }

        if (rejectedRecordCount < 0 || rowSnapshot.Length + rejectedRecordCount != sourceRecordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rejectedRecordCount),
                "Valid and rejected records must equal the source record count.");
        }

        Rows = Array.AsReadOnly(rowSnapshot);
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        SourceRecordCount = sourceRecordCount;
        RejectedRecordCount = rejectedRecordCount;
        IsHeaderUsable = isHeaderUsable;
    }

    public IReadOnlyList<TopstepSourceRow> Rows { get; }
    public IReadOnlyList<TopstepCsvDiagnostic> Diagnostics { get; }
    public int SourceRecordCount { get; }
    public int ValidRecordCount => Rows.Count;
    public int RejectedRecordCount { get; }
    public bool IsHeaderUsable { get; }
    public bool IsCompleteInputValid => IsHeaderUsable && SourceRecordCount > 0 &&
        RejectedRecordCount == 0 &&
        Diagnostics.All(diagnostic => diagnostic.Severity != TopstepCsvDiagnosticSeverity.Error);
}
