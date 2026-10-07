using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

public sealed class FakeDailyJournalRepository : IDailyJournalRepository, IDailyJournalStatusReader
{
    private readonly Dictionary<(DateOnly, Guid?), DailyJournalDetails> _entries = [];
    public Func<DateOnly, Guid?, CancellationToken, Task<DailyJournalDetails?>>? Read { get; set; }
    public int Writes { get; private set; }
    public Task<DailyJournalDetails?> GetAsync(DateOnly date, Guid? account = null, CancellationToken cancellationToken = default) =>
        Read?.Invoke(date, account, cancellationToken) ?? Task.FromResult(_entries.GetValueOrDefault((date, account)));
    public Task<IReadOnlyList<DailyJournalStatus>> GetAsync(DateOnly from, DateOnly through, Guid? tradingAccountId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DailyJournalStatus>>(_entries.Values.Where(j => (tradingAccountId == null || j.Entry.TradingAccountId == tradingAccountId) && j.Entry.TradingDate >= from && j.Entry.TradingDate <= through)
            .Select(j => new DailyJournalStatus(j.Entry.Id, j.Entry.TradingDate, j.Entry.IsDraft, j.Entry.Revision, j.Entry.TradingAccountId)).ToArray());
    public Task<DailyJournalWriteResult> CreateAsync(CreateDailyJournalCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = new DailyJournalEntry(command.TradingDate, command.TradingAccountId, command.Text, command.IsDraft, DateTimeOffset.UtcNow, command.Review ?? DailyReviewAnswers.Empty);
        var details = new DailyJournalDetails(entry, command.TradingAccountId.HasValue ? DailyJournalAccountState.Active : DailyJournalAccountState.AllAccounts, null);
        if (!_entries.TryAdd((command.TradingDate, command.TradingAccountId), details)) return Task.FromResult(new DailyJournalWriteResult(DailyJournalWriteStatus.AlreadyExists, null));
        Writes++;
        return Task.FromResult(new DailyJournalWriteResult(DailyJournalWriteStatus.Created, details));
    }
    public Task<DailyJournalWriteResult> UpdateAsync(UpdateDailyJournalCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var original = _entries.Values.Single(j => j.Entry.Id == command.JournalId);
        if (original.Entry.Revision != command.ExpectedRevision) return Task.FromResult(new DailyJournalWriteResult(DailyJournalWriteStatus.Conflict, null));
        var e = original.Entry;
        var updated = original with { Entry = DailyJournalEntry.Rehydrate(e.Id, e.TradingDate, e.TradingAccountId, command.Text,
            command.IsDraft, e.Revision + 1, e.CreatedAtUtc, DateTimeOffset.UtcNow, command.Review) };
        _entries[(e.TradingDate, e.TradingAccountId)] = updated;
        Writes++;
        return Task.FromResult(new DailyJournalWriteResult(DailyJournalWriteStatus.Updated, updated));
    }
    public Task<DailyJournalWriteResult> DeleteAsync(DeleteDailyJournalCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = _entries.Values.Single(j => j.Entry.Id == command.JournalId).Entry;
        if (entry.Revision != command.ExpectedRevision) return Task.FromResult(new DailyJournalWriteResult(DailyJournalWriteStatus.Conflict, null));
        _entries.Remove((entry.TradingDate, entry.TradingAccountId));
        Writes++;
        return Task.FromResult(new DailyJournalWriteResult(DailyJournalWriteStatus.Deleted, null));
    }
    public Task<IReadOnlyList<DailyJournalRevision>> GetHistoryAsync(Guid journalId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Inline Journal must not load review history.");
}
