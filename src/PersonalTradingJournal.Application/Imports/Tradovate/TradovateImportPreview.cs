using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Imports.Tradovate;

public enum TradovateImportPreviewDiagnosticStage
{
    Csv = 1,
    Reconstruction = 2,
    Instrument = 3,
    Preparation = 4,
    Preview = 5,
}

public sealed record TradovateImportPreviewDiagnostic(
    TradovateImportPreviewDiagnosticStage Stage,
    TradovateReconstructionDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? Context = null);

public sealed record TradovateImportPreviewInstrumentItem(
    string CanonicalSymbol,
    IReadOnlyList<string> SourceBrokerSymbols,
    TradovateInstrumentResolutionStatus Status,
    Guid? ExistingInstrumentId,
    bool? IsExistingInstrumentActive,
    string? DisplayName,
    string? AssetClass,
    string? Exchange,
    string? Currency,
    decimal? TickSize,
    decimal? TickValue,
    string? MetadataSource);

public sealed record TradovateImportPreviewTradeItem(
    int CandidateIndex,
    string BrokerSymbol,
    string CanonicalSymbol,
    string AccountName,
    TradeDirection Direction,
    DateTimeOffset OpenedAtNewYork,
    DateTimeOffset? ClosedAtNewYork,
    int ExecutionCount,
    decimal OpeningQuantity,
    decimal WeightedAverageEntryPrice,
    decimal? WeightedAverageExitPrice,
    decimal? SourceReportedPnL,
    TradovateInstrumentResolutionStatus InstrumentResolutionStatus);

public sealed record TradovateImportPreviewSummary(
    string FileName,
    string AccountName,
    string AccountCurrency,
    bool IsAccountActive,
    string SourceTimeZoneId,
    string CanonicalTimeZoneId,
    string PreviewTimeZoneId,
    int SourceRecordCount,
    int ValidRecordCount,
    int RejectedRecordCount,
    int UniqueBuyFillCount,
    int UniqueSellFillCount,
    int CandidateCount,
    int CanonicalInstrumentCount,
    int ExistingInstrumentCount,
    int ProposedInstrumentCount,
    int WarningCount,
    int ErrorCount,
    DateTimeOffset? PeriodStartNewYork,
    DateTimeOffset? PeriodEndNewYork);

public sealed record TradovateImportPreview(
    TradovateImportPreviewSummary Summary,
    IReadOnlyList<TradovateImportPreviewInstrumentItem> Instruments,
    IReadOnlyList<TradovateImportPreviewTradeItem> Trades,
    IReadOnlyList<TradovateImportPreviewDiagnostic> Diagnostics,
    bool IsStructurallyReady,
    bool IsReadyForConfirmation);
