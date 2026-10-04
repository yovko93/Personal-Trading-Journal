using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Journals;

/// <summary>Exact-scope reads and atomic, optimistic journal writes. Never reads Trade data.</summary>
public sealed class DailyJournalRepository : IDailyJournalRepository
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
            .Select(r => new DailyJournalRevision(r.JournalId, r.Revision, r.Text, r.IsDraft, r.SavedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<DailyJournalWriteResult> CreateAsync(CreateDailyJournalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        var entry = new DailyJournalEntry(command.TradingDate, command.TradingAccountId,
            command.Text, command.IsDraft, _clock.GetUtcNow());
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
        if (!entry.UpdateContent(command.Text, command.IsDraft, _clock.GetUtcNow()))
            return new(DailyJournalWriteStatus.Unchanged, Details(record, account));

        record.Text = entry.Text;
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

    private static void ValidateId(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("A journal identifier is required.", nameof(id));
    }

    private static void ValidateAccountId(Guid? id)
    {
        if (id == Guid.Empty) throw new ArgumentException("Use null for All accounts, or a non-empty Account ID.", nameof(id));
    }
}
