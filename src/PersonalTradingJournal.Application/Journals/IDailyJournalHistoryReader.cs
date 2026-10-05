namespace PersonalTradingJournal.Application.Journals;

/// <summary>Bounded, read-only browsing. Null Account means only the independent All accounts scope.</summary>
public interface IDailyJournalHistoryReader
{
    Task<JournalHistoryPage<JournalHistoryItem>> BrowseAsync(Guid? accountId, int page = 1, int pageSize = 20,
        CancellationToken cancellationToken = default);
    Task<JournalHistoryPage<JournalRevisionItem>> BrowseRevisionsAsync(Guid journalId, int page = 1, int pageSize = 20,
        CancellationToken cancellationToken = default);
    Task<DailyJournalRevision?> GetRevisionAsync(Guid journalId, long revision, CancellationToken cancellationToken = default);
}

public sealed record JournalHistoryItem(Guid Id, DateOnly TradingDate, Guid? AccountId,
    string? AccountName, DailyJournalAccountState AccountState, bool IsDraft, long Revision, DateTimeOffset UpdatedAtUtc);
public sealed record JournalRevisionItem(Guid JournalId, long Revision, bool IsDraft, DateTimeOffset SavedAtUtc);
public sealed record JournalHistoryPage<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public bool HasNext => (long)Page * PageSize < TotalCount;
}

public static class JournalHistoryPaging
{
    public const int MaximumPageSize = 50;
    public static int Offset(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > MaximumPageSize) throw new ArgumentOutOfRangeException(nameof(pageSize));
        long offset = ((long)page - 1) * pageSize;
        if (offset > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(page));
        return (int)offset;
    }
}
