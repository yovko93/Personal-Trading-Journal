using PersonalTradingJournal.Application.Imports.Tradovate;

namespace PersonalTradingJournal.Desktop.ViewModels.Import;

public enum ImportWorkflowPhase
{
    Idle = 0,
    AnalyzingFile = 1,
    FileAnalyzed = 2,
    PreparingPreview = 3,
    PreviewReady = 4,
    RequiresUserInput = 5,
    Blocked = 6,
    Failed = 7,
}

public sealed record ImportAccountOption(
    Guid Id,
    string Name,
    string Currency,
    bool IsActive)
{
    public string DisplayText => IsActive
        ? $"{Name} · {Currency}"
        : $"{Name} · {Currency} · Inactive";
}

public sealed record ImportAnalysisSummary(
    int SourceRecordCount,
    int ValidRecordCount,
    int RejectedRecordCount,
    int UniqueBuyFillCount,
    int UniqueSellFillCount,
    int CandidateCount,
    int CanonicalInstrumentCount,
    int ExistingInstrumentCount,
    int ProposedInstrumentCount);

public sealed record ImportInstrumentItem(
    string CanonicalSymbol,
    string SourceSymbols,
    string Resolution,
    string Details,
    bool RequiresUserInput);

public sealed record ImportDiagnosticItem(
    string Stage,
    string Severity,
    string Code,
    string Message,
    string? Context);

public sealed record ImportTradePreviewItem(
    int CandidateIndex,
    string Symbols,
    string AccountName,
    string Direction,
    string OpenedAt,
    string ClosedAt,
    int ExecutionCount,
    string OpeningQuantity,
    string AverageEntry,
    string AverageExit,
    string SourcePnL,
    string Resolution);
