namespace PersonalTradingJournal.Application.TradingPlans;

/// <summary>Current global reminders, never Plan/Ready/Review evidence. Text is preserved verbatim.</summary>
public sealed record DailyTradingPrinciple(Guid Id, string Text, long Position, DateTimeOffset CreatedAtUtc);
public sealed record DailyTradingPrinciples(long Version, IReadOnlyList<DailyTradingPrinciple> Entries);
public enum PrincipleWriteStatus { Success, Invalid, Conflict, Cancelled, StorageFailure }
public sealed record PrincipleWriteResult(PrincipleWriteStatus Status, DailyTradingPrinciples? Snapshot = null);
public static class DailyTradingPrincipleLimits
{
    // Same short-text bound as Today's Focus; reminders are not long-form Journal entries.
    public const int MaximumTextLength = 500;
    public const int MaximumEntries = 1000;
    public static bool IsValid(string? text) => !string.IsNullOrWhiteSpace(text) && text.Length <= MaximumTextLength && !text.Contains('\0');
}
public interface IDailyTradingPrincipleStore
{
    Task<DailyTradingPrinciples> ReadAsync(CancellationToken cancellationToken = default);
    Task<PrincipleWriteResult> AddAsync(string text, long expectedVersion, CancellationToken cancellationToken = default);
    Task<PrincipleWriteResult> RemoveAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default);
}
