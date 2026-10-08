using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.DailyReview.Coaching;

public sealed class CoachingAnalysisRepository(IDbContextFactory<JournalDbContext> factory) : ICoachingAnalysisRepository
{
    public async Task<SavedCoachingAnalysis> SaveAsync(CoachingAnalysisSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        var a = snapshot.Analysis;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.CoachingAnalyses.Add(new()
        {
            Id = a.Summary.Id, ReviewDate = a.Summary.ReviewDate, ScopeKind = (int)a.Summary.Scope.Kind,
            AccountId = a.Summary.Scope.AccountId, AccountDisplayName = a.Summary.AccountDisplayName,
            GeneratedAtUtc = a.Summary.GeneratedAtUtc, Provider = a.Summary.Provider, Model = a.Summary.Model,
            EvidenceContractVersion = a.EvidenceContractVersion, ResponseContractVersion = a.ResponseContractVersion,
            PacketId = a.PacketId, EvidenceJson = a.EvidenceJson, ResponseJson = a.ResponseJson,
            MetadataJson = JsonSerializer.Serialize(a.Metadata),
        });
        await db.SaveChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken);
        return a;
    }

    public async Task<SavedCoachingAnalysis?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ValidateId(id);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var row = await db.CoachingAnalyses.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        return row is null ? null : new(Summary(row), row.EvidenceContractVersion, row.ResponseContractVersion,
            row.PacketId, row.EvidenceJson, row.ResponseJson,
            JsonSerializer.Deserialize<SavedCoachingProviderMetadata>(row.MetadataJson)
                ?? throw new InvalidDataException("Saved analysis metadata is unavailable."));
    }

    public async Task<CoachingAnalysisHistoryPage> BrowseAsync(CoachingAnalysisHistoryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        // Count and page share one SQLite read snapshot. No joins or payload columns in the page.
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await using var enlisted = await db.Database.UseTransactionAsync(transaction, cancellationToken);
        int kind = (int)query.Scope.Kind;
        var rows = db.CoachingAnalyses.AsNoTracking().Where(r => r.ReviewDate == query.ReviewDate &&
            r.ScopeKind == kind && r.AccountId == query.Scope.AccountId);
        int total = await rows.CountAsync(cancellationToken);
        var summaries = await rows.OrderByDescending(r => r.GeneratedAtUtc).ThenBy(r => r.Id)
            .Skip(query.Offset).Take(query.PageSize)
            .Select(r => new CoachingAnalysisSummary(r.Id, r.ReviewDate, new CoachingAnalysisScope(r.AccountId),
                r.AccountDisplayName, r.GeneratedAtUtc, r.Provider, r.Model)).ToArrayAsync(cancellationToken);
        return new(Array.AsReadOnly(summaries), total, query.Page, query.PageSize);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ValidateId(id);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.CoachingAnalyses.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken) == 1;
    }

    private static CoachingAnalysisSummary Summary(CoachingAnalysisRecord row) => new(row.Id, row.ReviewDate,
        new(row.AccountId), row.AccountDisplayName, row.GeneratedAtUtc, row.Provider, row.Model);
    private static void ValidateId(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("A saved analysis ID is required.", nameof(id));
    }
}
