using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.PeriodReviews;

public sealed class PeriodReviewRepository(IDbContextFactory<JournalDbContext> factory) : IPeriodReviewRepository
{
    public async Task<SavedPeriodReview> SaveAsync(PeriodReviewSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot); cancellationToken.ThrowIfCancellationRequested();
        var a = snapshot.Review; var s = a.Summary;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.PeriodReviews.Add(new()
        {
            Id = s.Id, Kind = (int)s.Kind, FirstDate = s.FirstDate, LastDate = s.LastDate, FromUtc = s.FromUtc, BeforeUtc = s.BeforeUtc,
            ScopeKind = (int)s.Scope.Kind, AccountId = s.Scope.AccountId, AccountDisplayName = s.AccountDisplayName,
            GeneratedAtUtc = s.GeneratedAtUtc, Provider = s.Provider, Model = s.Model, EvidenceContractVersion = a.EvidenceContractVersion,
            ResponseContractVersion = a.ResponseContractVersion, PacketId = a.PacketId, EvidenceJson = a.EvidenceJson,
            ResponseJson = a.ResponseJson, MetadataJson = JsonSerializer.Serialize(a.Metadata),
        });
        await db.SaveChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // Cancellation fence: after commit begins, report its outcome, not cancellation of a durable row.
        await transaction.CommitAsync(CancellationToken.None);
        return a;
    }
    public async Task<SavedPeriodReview?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ValidateId(id); await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var r = await db.PeriodReviews.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        return r is null ? null : new(Summary(r), r.EvidenceContractVersion, r.ResponseContractVersion, r.PacketId, r.EvidenceJson, r.ResponseJson,
            JsonSerializer.Deserialize<SavedCoachingProviderMetadata>(r.MetadataJson) ?? throw new InvalidDataException("Saved period metadata is unavailable."));
    }
    public async Task<PeriodReviewHistoryPage> BrowseAsync(PeriodReviewHistoryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query); await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await using var enlisted = await db.Database.UseTransactionAsync(transaction, cancellationToken);
        int scope = query.Period.Scope.AccountId.HasValue ? 1 : 0;
        var rows = PeriodRows(db, query.Period).Where(r => r.ScopeKind == scope && r.AccountId == query.Period.Scope.AccountId);
        int count = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(r => r.GeneratedAtUtc).ThenBy(r => r.Id).Skip(query.Offset).Take(query.PageSize)
            .Select(r => new PeriodReviewSummary(r.Id, (ReviewPeriodKind)r.Kind, r.FirstDate, r.LastDate, r.FromUtc, r.BeforeUtc,
                new CoachingAnalysisScope(r.AccountId), r.AccountDisplayName, r.GeneratedAtUtc, r.Provider, r.Model)).ToArrayAsync(cancellationToken);
        return new(Array.AsReadOnly(items), count, query.Page, query.PageSize);
    }
    public async Task<SavedPeriodScopePage> BrowseScopesAsync(PeriodReviewHistoryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Period.Scope.AccountId.HasValue) throw new ArgumentException("Scope discovery requires an aggregate period query.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await using var enlisted = await db.Database.UseTransactionAsync(transaction, cancellationToken);
        var groups = PeriodRows(db, query.Period).GroupBy(r => new { r.ScopeKind, r.AccountId });
        int count = await groups.CountAsync(cancellationToken);
        var items = await groups.OrderBy(g => g.Key.ScopeKind).ThenBy(g => g.Key.AccountId).Skip(query.Offset).Take(query.PageSize)
            .Select(g => new SavedPeriodScope(new CoachingAnalysisScope(g.Key.AccountId),
                g.OrderByDescending(r => r.GeneratedAtUtc).ThenBy(r => r.Id).Select(r => r.AccountDisplayName).FirstOrDefault())).ToArrayAsync(cancellationToken);
        return new(Array.AsReadOnly(items), count, query.Page, query.PageSize);
    }
    public async Task<PeriodReviewDeleteStatus> DeleteAsync(Guid id, bool confirmed, CancellationToken cancellationToken = default)
    {
        ValidateId(id); cancellationToken.ThrowIfCancellationRequested();
        if (!confirmed) return PeriodReviewDeleteStatus.NotConfirmed;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        int changed = await db.PeriodReviews.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested(); await transaction.CommitAsync(CancellationToken.None);
        return changed == 1 ? PeriodReviewDeleteStatus.Deleted : PeriodReviewDeleteStatus.NotFound;
    }
    private static IQueryable<PeriodReviewRecord> PeriodRows(JournalDbContext db, PeriodReviewQuery q) => db.PeriodReviews.AsNoTracking()
        .Where(r => r.Kind == (int)q.Kind && r.FirstDate == q.FirstDate && r.LastDate == q.LastDate);
    private static PeriodReviewSummary Summary(PeriodReviewRecord r) => new(r.Id, (ReviewPeriodKind)r.Kind, r.FirstDate, r.LastDate,
        r.FromUtc, r.BeforeUtc, new(r.AccountId), r.AccountDisplayName, r.GeneratedAtUtc, r.Provider, r.Model);
    private static void ValidateId(Guid id) { if (id == Guid.Empty) throw new ArgumentException("A saved period review ID is required."); }
}
