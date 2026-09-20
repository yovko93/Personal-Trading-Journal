namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed class TradovateCsvParseResult
{
    public TradovateCsvParseResult(
        IEnumerable<TradovateMatchedFillRow> rows,
        IEnumerable<TradovateCsvDiagnostic> diagnostics,
        int sourceRecordCount,
        int rejectedRecordCount,
        bool isHeaderUsable)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(diagnostics);

        TradovateMatchedFillRow[] rowSnapshot = rows.ToArray();
        TradovateCsvDiagnostic[] diagnosticSnapshot = diagnostics.ToArray();
        if (sourceRecordCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRecordCount));
        }

        if (rejectedRecordCount < 0 ||
            rowSnapshot.Length + rejectedRecordCount != sourceRecordCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rejectedRecordCount),
                rejectedRecordCount,
                "Valid and rejected records must equal the source record count.");
        }

        Rows = Array.AsReadOnly(rowSnapshot);
        Diagnostics = Array.AsReadOnly(diagnosticSnapshot);
        SourceRecordCount = sourceRecordCount;
        RejectedRecordCount = rejectedRecordCount;
        IsHeaderUsable = isHeaderUsable;
    }

    public IReadOnlyList<TradovateMatchedFillRow> Rows { get; }

    public IReadOnlyList<TradovateCsvDiagnostic> Diagnostics { get; }

    public int SourceRecordCount { get; }

    public int ValidRecordCount => Rows.Count;

    public int RejectedRecordCount { get; }

    public bool IsHeaderUsable { get; }

    public bool IsCompleteInputValid =>
        IsHeaderUsable &&
        RejectedRecordCount == 0 &&
        Diagnostics.All(diagnostic =>
            diagnostic.Severity != TradovateCsvDiagnosticSeverity.Error);
}
