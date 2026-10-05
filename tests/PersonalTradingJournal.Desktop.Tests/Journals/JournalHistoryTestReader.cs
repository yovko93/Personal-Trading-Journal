using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

internal sealed class JournalHistoryTestReader : IDailyJournalHistoryReader
{
    public static readonly DateTimeOffset Now = new(2026, 10, 5, 16, 0, 0, TimeSpan.Zero);
    public List<JournalHistoryItem> Items { get; } = [];
    public List<DailyJournalRevision> Snapshots { get; } = [];
    public Func<Guid?, int, CancellationToken, Task<JournalHistoryPage<JournalHistoryItem>>>? Browse { get; set; }
    public Func<Guid, int, CancellationToken, Task<JournalHistoryPage<JournalRevisionItem>>>? Revisions { get; set; }
    public Func<Guid, long, CancellationToken, Task<DailyJournalRevision?>>? Snapshot { get; set; }
    public int SnapshotReads { get; private set; }

    public JournalHistoryItem Add(DateOnly date, Guid? account = null, long revision = 1, bool draft = true)
    {
        var item = new JournalHistoryItem(Guid.NewGuid(), date, account, account is null ? null : "Historical Account",
            account is null ? DailyJournalAccountState.AllAccounts : DailyJournalAccountState.Inactive, draft, revision, Now);
        Items.Add(item);
        for (long i = 1; i <= revision; i++) Snapshots.Add(new(item.Id, i, $"  text {i}\r\n", i != 2, Now.AddSeconds(i), new("well", "improve", "next")));
        return item;
    }

    public Task<JournalHistoryPage<JournalHistoryItem>> BrowseAsync(Guid? accountId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (Browse is not null) return Browse(accountId, page, cancellationToken);
        var items = Items.Where(i => i.AccountId == accountId).OrderByDescending(i => i.TradingDate).ThenBy(i => i.Id).ToArray();
        return Task.FromResult(new JournalHistoryPage<JournalHistoryItem>(items.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), items.Length, page, pageSize));
    }
    public Task<JournalHistoryPage<JournalRevisionItem>> BrowseRevisionsAsync(Guid journalId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (Revisions is not null) return Revisions(journalId, page, cancellationToken);
        var items = Snapshots.Where(i => i.JournalId == journalId).OrderByDescending(i => i.Revision)
            .Select(i => new JournalRevisionItem(i.JournalId, i.Revision, i.IsDraft, i.SavedAtUtc)).ToArray();
        return Task.FromResult(new JournalHistoryPage<JournalRevisionItem>(items.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), items.Length, page, pageSize));
    }
    public Task<DailyJournalRevision?> GetRevisionAsync(Guid journalId, long revision, CancellationToken cancellationToken = default)
    {
        SnapshotReads++;
        return Snapshot is not null ? Snapshot(journalId, revision, cancellationToken)
            : Task.FromResult(Snapshots.SingleOrDefault(i => i.JournalId == journalId && i.Revision == revision));
    }
}
