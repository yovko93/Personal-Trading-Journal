namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed record TradovateImportResult(
    TradovateImportStatus Status,
    int ImportedTradeCount,
    int SkippedDuplicateTradeCount,
    int CreatedInstrumentCount,
    IReadOnlyList<Guid> ImportedTradeIds,
    IReadOnlyList<Guid> CreatedInstrumentIds,
    IReadOnlyList<Guid> DuplicateTradeIds,
    string? ConflictCode = null,
    string? Message = null)
{
    public static TradovateImportResult Blocked(string code, string message) =>
        new(
            TradovateImportStatus.Blocked,
            0,
            0,
            0,
            [],
            [],
            [],
            code,
            message);
}
