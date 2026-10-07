using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Journals;

/// <summary>Exact-scope reads and atomic, optimistic journal writes. Never reads Trade data.</summary>
public sealed class DailyJournalRepository : IDailyJournalRepository, IDailyJournalRevisionWriter
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;
    private readonly TimeProvider _clock;

    public DailyJournalRepository(IDbContextFactory<JournalDbContext> contextFactory, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<DailyJournalDetails?> GetAsync(DateOnly tradingDate, Guid? tradingAccountId = null,
        CancellationToken cancellationToken = default)
    {
        ValidateAccountId(tradingAccountId);
        cancellationToken.ThrowIfCancellationRequested();
        await using JournalDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var result = await (
            from journal in context.DailyJournals.AsNoTracking()
            where journal.TradingDate == tradingDate && journal.TradingAccountId == tradingAccountId
            join account in context.TradingAccounts.AsNoTracking()
                on journal.TradingAccountId equals (Guid?)account.Id into accounts
            from account in accounts.DefaultIfEmpty()
            select new { Journal = journal, Account = account })
            .SingleOrDefaultAsync(cancellationToken);
        return result is null ? null : Details(result.Journal, result.Account);
    }

    public async Task<IReadOnlyList<DailyJournalRevision>> GetHistoryAsync(Guid journalId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(journalId);
        cancellationToken.ThrowIfCancellationRequested();
        await using JournalDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.DailyJournalRevisions.AsNoTracking()
            .Where(r => r.JournalId == journalId).OrderBy(r => r.Revision)
            .Select(r => new DailyJournalRevision(r.JournalId, r.Revision, r.Text, r.IsDraft, r.SavedAtUtc,
                new DailyReviewAnswers(r.WentWell, r.NeedsImprovement, r.NextTradingDay)))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<DailyJournalWriteResult> CreateAsync(CreateDailyJournalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        var entry = new DailyJournalEntry(command.TradingDate, command.TradingAccountId,
            command.Text, command.IsDraft, _clock.GetUtcNow(), command.Review ?? DailyReviewAnswers.Empty);
        await using JournalDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        // The SQLite writer transaction starts before reference/key checks. A competing
        // writer waits and then sees the committed revision; no check-then-write gap.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        TradingAccountRecord? account = await Account(context, entry.TradingAccountId, cancellationToken);
        if (entry.TradingAccountId.HasValue && account is null)
            return new(DailyJournalWriteStatus.AccountUnavailable, null);
        DailyJournalRecord? existing = await context.DailyJournals.AsNoTracking().SingleOrDefaultAsync(
            r => r.TradingDate == entry.TradingDate && r.TradingAccountId == entry.TradingAccountId, cancellationToken);
        if (existing is not null)
            return new(DailyJournalWriteStatus.AlreadyExists, Details(existing, account));

        DailyJournalRecord record = DailyJournalPersistenceMapper.ToRecord(entry);
        context.DailyJournals.Add(record);
        context.DailyJournalRevisions.Add(DailyJournalPersistenceMapper.ToRevision(entry));
        await context.SaveChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken);
        return new(DailyJournalWriteStatus.Created, Details(record, account));
    }

    public async Task<DailyJournalWriteResult> UpdateAsync(UpdateDailyJournalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateId(command.JournalId);
        if (command.ExpectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(command.ExpectedRevision));
        cancellationToken.ThrowIfCancellationRequested();
        await using JournalDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        DailyJournalRecord? record = await context.DailyJournals.SingleOrDefaultAsync(
            r => r.Id == command.JournalId, cancellationToken);
        if (record is null) return new(DailyJournalWriteStatus.NotFound, null);
        TradingAccountRecord? account = await Account(context, record.TradingAccountId, cancellationToken);
        if (record.TradingAccountId.HasValue && account is null)
            return new(DailyJournalWriteStatus.AccountUnavailable, Details(record, account));
        // Even an otherwise identical stale save must report the newer revision.
        if (record.Revision != command.ExpectedRevision)
            return new(DailyJournalWriteStatus.Conflict, Details(record, account));
        DailyJournalEntry entry = DailyJournalPersistenceMapper.ToDomain(record);
        if (!entry.UpdateContent(command.Text, command.IsDraft, _clock.GetUtcNow(), command.Review ?? entry.Review, command.ReopenCompleted))
            return new(DailyJournalWriteStatus.Unchanged, Details(record, account));

        record.Text = entry.Text;
        record.WentWell = entry.Review.WentWell;
        record.NeedsImprovement = entry.Review.NeedsImprovement;
        record.NextTradingDay = entry.Review.NextTradingDay;
        record.IsDraft = entry.IsDraft;
        record.Revision = entry.Revision;
        record.UpdatedAtUtc = entry.UpdatedAtUtc;
        context.DailyJournalRevisions.Add(DailyJournalPersistenceMapper.ToRevision(entry));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The concurrency token is a second guard if another persistence provider
            // or write path bypasses the SQLite transaction's serialization.
            return new(DailyJournalWriteStatus.Conflict, null);
        }
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken);
        return new(DailyJournalWriteStatus.Updated, Details(record, account));
    }

    private static Task<TradingAccountRecord?> Account(JournalDbContext context, Guid? accountId, CancellationToken ct) =>
        accountId is null ? Task.FromResult<TradingAccountRecord?>(null) :
            context.TradingAccounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == accountId, ct);

    private static DailyJournalDetails Details(DailyJournalRecord record, TradingAccountRecord? account) => new(
        DailyJournalPersistenceMapper.ToDomain(record),
        record.TradingAccountId is null ? DailyJournalAccountState.AllAccounts :
            account is null ? DailyJournalAccountState.Unavailable :
            account.IsActive ? DailyJournalAccountState.Active : DailyJournalAccountState.Inactive,
        account?.Name);

    public async Task<DailyJournalWriteResult> DeleteAsync(DeleteDailyJournalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateId(command.JournalId);
        if (command.ExpectedRevision < 1) throw new ArgumentOutOfRangeException(nameof(command.ExpectedRevision));
        cancellationToken.ThrowIfCancellationRequested();
        await using JournalDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        DailyJournalRecord? record = await context.DailyJournals.SingleOrDefaultAsync(
            j => j.Id == command.JournalId, cancellationToken);
        if (record is null) return new(DailyJournalWriteStatus.NotFound, null);
        if (record.Revision != command.ExpectedRevision) return new(DailyJournalWriteStatus.Conflict, null);

        // Keep the restrictive FK. Delete snapshots explicitly within the writer transaction;
        // cancellation or a failed parent delete rolls back the entire removal.
        await context.DailyJournalRevisions.Where(r => r.JournalId == record.Id).ExecuteDeleteAsync(cancellationToken);
        context.DailyJournals.Remove(record);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new(DailyJournalWriteStatus.Conflict, null); }
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken);
        return new(DailyJournalWriteStatus.Deleted, null);
    }

    public async Task<DeleteJournalRevisionStatus> DeleteRevisionAsync(DeleteJournalRevisionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateId(command.JournalId);
        if (command.Revision < 1) throw new ArgumentOutOfRangeException(nameof(command.Revision));
        if (command.ExpectedCurrentRevision < 1) throw new ArgumentOutOfRangeException(nameof(command.ExpectedCurrentRevision));
        cancellationToken.ThrowIfCancellationRequested();
        await using JournalDbContext context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var journal = await context.DailyJournals.AsNoTracking().SingleOrDefaultAsync(
            j => j.Id == command.JournalId, cancellationToken);
        if (journal is null) return DeleteJournalRevisionStatus.NotFound;
        if (journal.Revision != command.ExpectedCurrentRevision) return DeleteJournalRevisionStatus.Conflict;
        // Preserve the current-state snapshot and monotonic root revision. Gaps in older history
        // are allowed; future saves increment the root token, never a snapshot count or maximum.
        if (command.Revision == journal.Revision) return DeleteJournalRevisionStatus.CurrentRevisionProtected;
        var snapshot = await context.DailyJournalRevisions.SingleOrDefaultAsync(
            r => r.JournalId == command.JournalId && r.Revision == command.Revision, cancellationToken);
        if (snapshot is null) return DeleteJournalRevisionStatus.NotFound;
        context.DailyJournalRevisions.Remove(snapshot);
        await context.SaveChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken);
        return DeleteJournalRevisionStatus.Deleted;
    }

    private static void ValidateId(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("A journal identifier is required.", nameof(id));
    }

    private static void ValidateAccountId(Guid? id)
    {
        if (id == Guid.Empty) throw new ArgumentException("Use null for All accounts, or a non-empty Account ID.", nameof(id));
    }
}
