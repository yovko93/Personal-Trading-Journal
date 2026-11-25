using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Imports.Tradovate;

// An operator-supplied baseline is independent of the database being repaired.
public sealed record TradovateTimestampRepairScope(
    Guid AccountId, string ExternalAccountId, string Currency, string FillsSha256,
    IReadOnlyList<TradovateTimestampRepairIdentity> Identities);

public sealed record TradovateTimestampRepairIdentity(
    Guid TradeId, Guid ExecutionId, string FillId, int AllocationIndex,
    string Symbol, ExecutionSide Side, decimal Quantity, decimal SourceQuantity,
    decimal Price, DateTimeOffset DiagnosedUtc);

public sealed record TradovateTimestampFieldChange(
    string Table, string Key, string Field, DateTimeOffset Before, DateTimeOffset After);

public sealed record TradovateTimestampRepairPlan(
    Guid AccountId, string FillsSha256, string StateSha256, string ScopeSha256,
    int MatchedExecutions, int AffectedTrades, int UnchangedExecutions,
    bool AlreadyApplied, IReadOnlyList<TradovateTimestampFieldChange> Changes);

public sealed record TradovateTimestampRepairResult(
    int ChangedExecutions, bool AlreadyApplied, string? BackupPath);
