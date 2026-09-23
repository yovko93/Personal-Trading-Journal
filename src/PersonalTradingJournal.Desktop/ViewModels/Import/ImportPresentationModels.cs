using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Domain.Accounts;

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
    Importing = 8,
    Completed = 9,
}

public sealed class ImportCommittedEventArgs(
    int importedTradeCount,
    int createdInstrumentCount) : EventArgs
{
    public int ImportedTradeCount { get; } = importedTradeCount;

    public int CreatedInstrumentCount { get; } = createdInstrumentCount;
}

public sealed record ImportAccountOption(
    Guid Id,
    string Name,
    TradingAccountType AccountType,
    string? ProviderName,
    string? ExternalAccountId,
    string Currency,
    bool IsActive)
{
    public string DisplayText
    {
        get
        {
            string[] identitySegments =
            [
                Name,
                ProviderName ?? string.Empty,
                ExternalAccountId ?? string.Empty,
                AccountType.ToString(),
                Currency,
                IsActive ? string.Empty : "Inactive",
            ];
            return string.Join(
                " · ",
                identitySegments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
        }
    }
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
    string Activity,
    string DisplayName,
    string AssetClass,
    string Exchange,
    string Currency,
    string TickSize,
    string TickValue,
    string CreationNotice,
    bool RequiresUserInput)
{
    public string StatusText => string.IsNullOrEmpty(Activity)
        ? Resolution
        : $"{Resolution} · {Activity}";
}

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
