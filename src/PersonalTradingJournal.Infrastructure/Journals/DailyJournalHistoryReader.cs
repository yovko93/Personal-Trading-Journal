using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Journals;

public sealed class DailyJournalHistoryReader(IDbContextFactory<JournalDbContext> factory) : IDailyJournalHistoryReader
{
    public async Task<JournalHistoryPage<JournalHistoryItem>> BrowseAsync(Guid? accountId, int page = 1,
        int pageSize = 20, CancellationToken cancellationToken = default)
    {
        int offset = JournalHistoryPaging.Offset(page, pageSize);
        if (accountId == Guid.Empty) throw new ArgumentException("Select an explicit Account scope.", nameof(accountId));
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var journals = db.DailyJournals.AsNoTracking();
        if (accountId.HasValue) journals = journals.Where(j => j.TradingAccountId == accountId);
        int count = await journals.CountAsync(cancellationToken);
        var items = await (from j in journals
            join account in db.TradingAccounts.AsNoTracking() on j.TradingAccountId equals (Guid?)account.Id into accounts
            from account in accounts.DefaultIfEmpty()
            orderby j.TradingDate descending, j.Id
            select new JournalHistoryItem(j.Id, j.TradingDate, j.TradingAccountId,
                account == null ? null : account.Name,
                j.TradingAccountId == null ? DailyJournalAccountState.AllAccounts
                    : account == null ? DailyJournalAccountState.Unavailable
                    : account.IsActive ? DailyJournalAccountState.Active : DailyJournalAccountState.Inactive,
                j.IsDraft, j.Revision, j.UpdatedAtUtc))
            .Skip(offset).Take(pageSize).ToArrayAsync(cancellationToken);
        return new(items, count, page, pageSize);
    }

    public async Task<JournalHistoryPage<JournalRevisionItem>> BrowseRevisionsAsync(Guid journalId, int page = 1,
        int pageSize = 20, CancellationToken cancellationToken = default)
    {
        int offset = JournalHistoryPaging.Offset(page, pageSize);
        ValidateId(journalId);
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var revisions = db.DailyJournalRevisions.AsNoTracking().Where(r => r.JournalId == journalId);
        int count = await revisions.CountAsync(cancellationToken);
        var items = await revisions.OrderByDescending(r => r.Revision).Skip(offset).Take(pageSize)
            .Select(r => new JournalRevisionItem(r.JournalId, r.Revision, r.IsDraft, r.SavedAtUtc))
            .ToArrayAsync(cancellationToken);
        return new(items, count, page, pageSize);
    }

    public async Task<DailyJournalRevision?> GetRevisionAsync(Guid journalId, long revision,
        CancellationToken cancellationToken = default)
    {
        ValidateId(journalId);
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.DailyJournalRevisions.AsNoTracking()
            .Where(r => r.JournalId == journalId && r.Revision == revision)
            .Select(r => new DailyJournalRevision(r.JournalId, r.Revision, r.Text, r.IsDraft, r.SavedAtUtc,
                new DailyReviewAnswers(r.WentWell, r.NeedsImprovement, r.NextTradingDay)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static void ValidateId(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("A journal identity is required.", nameof(id));
    }
}
