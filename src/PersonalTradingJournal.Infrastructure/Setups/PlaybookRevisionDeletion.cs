using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Setups;

public sealed class PlaybookRevisionDeletion(IDbContextFactory<JournalDbContext> factory) : IPlaybookRevisionDeletion
{
    public async Task<PlaybookRevisionDeletionCheck> CheckAsync(Guid setupId, int revision, CancellationToken token = default)
    {
        Validate(setupId, revision);
        await using var db = await factory.CreateDbContextAsync(token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        return await Check(db, setupId, revision, token);
    }

    public async Task<PlaybookRevisionDeletionStatus> DeleteAsync(Guid setupId, int revision, int expectedCurrentRevision, CancellationToken token = default)
    {
        Validate(setupId, revision);
        if (expectedCurrentRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedCurrentRevision));
        await using var db = await factory.CreateDbContextAsync(token);
        await db.Database.OpenConnectionAsync(token);
        token.ThrowIfCancellationRequested();
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await db.Database.UseTransactionAsync(transaction, token);
        var check = await Check(db, setupId, revision, token);
        if (check.CurrentRevision != expectedCurrentRevision) return PlaybookRevisionDeletionStatus.Conflict;
        if (check.Status != PlaybookRevisionDeletionStatus.Eligible) return check.Status;
        // Metadata only. Never delete physical files: other revisions, Trades or captures may own them.
        await db.PlaybookExamples.Where(e => e.TradingSetupId == setupId && e.Revision == revision).ExecuteDeleteAsync(token);
        await db.PlaybookRevisions.Where(r => r.TradingSetupId == setupId && r.Revision == revision).ExecuteDeleteAsync(token);
        token.ThrowIfCancellationRequested();
        await transaction.CommitAsync(CancellationToken.None);
        return PlaybookRevisionDeletionStatus.Deleted;
    }

    private static async Task<PlaybookRevisionDeletionCheck> Check(JournalDbContext db, Guid setup, int revision, CancellationToken token)
    {
        int current = await db.PlaybookRevisions.Where(r => r.TradingSetupId == setup).MaxAsync(r => (int?)r.Revision, token) ?? 0;
        if (!await db.PlaybookRevisions.AnyAsync(r => r.TradingSetupId == setup && r.Revision == revision, token))
            return new(PlaybookRevisionDeletionStatus.Missing, current);
        if (revision == current) return new(PlaybookRevisionDeletionStatus.CurrentRevision, current);
        bool referenced = await db.TradePlaybookContexts.AnyAsync(r => r.TradingSetupId == setup && r.Revision == revision, token) ||
            await db.TradePlaybookAssociations.AnyAsync(r => r.TradingSetupId == setup && r.Revision == revision, token);
        return new(referenced ? PlaybookRevisionDeletionStatus.Referenced : PlaybookRevisionDeletionStatus.Eligible, current);
    }
    private static void Validate(Guid setup, int revision)
    {
        if (setup == Guid.Empty || revision < 1) throw new ArgumentException("A Setup and positive revision are required.");
    }
}
