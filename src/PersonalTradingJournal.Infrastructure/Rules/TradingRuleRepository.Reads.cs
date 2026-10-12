using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Rules;

public sealed partial class TradingRuleRepository
{
    public async Task<TradingRulePage> ReadCurrentAsync(TradingRuleAccountScope? scope = null, bool? active = null,
        int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        ValidateLimit(pageSize);
        if (page < 1 || (long)(page - 1) * pageSize > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(page));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await ReadSnapshot(db, cancellationToken);
        var query = db.TradingRuleRevisions.AsNoTracking().Where(r =>
            !db.TradingRuleRevisions.Any(next => next.RuleId == r.RuleId && next.Revision > r.Revision));
        if (scope is not null) query = query.Where(r => r.ScopeKind == (int)scope.Kind && r.AccountId == scope.AccountId);
        if (active.HasValue) query = query.Where(r => r.IsActive == active.Value);
        int total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(r => r.RuleId).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return new(await RuleDetails(db, rows, cancellationToken), total, page, pageSize);
    }

    public async Task<IReadOnlyList<TradingRuleDetails>> ReadRevisionsAsync(Guid ruleId, int? beforeRevision = null,
        int limit = 20, CancellationToken cancellationToken = default)
    {
        ValidateId(ruleId); ValidateLimit(limit);
        if (beforeRevision is < 1) throw new ArgumentOutOfRangeException(nameof(beforeRevision));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await ReadSnapshot(db, cancellationToken);
        var rows = await LatestRule(db, ruleId).Where(r => !beforeRevision.HasValue || r.Revision < beforeRevision.Value)
            .OrderByDescending(r => r.Revision).Take(limit).ToArrayAsync(cancellationToken);
        return await RuleDetails(db, rows, cancellationToken);
    }

    public async Task<TradingRuleAssessmentDetails?> ReadAssessmentAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        ValidateId(assessmentId);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await ReadSnapshot(db, cancellationToken);
        var rows = await db.TradingRuleAssessments.AsNoTracking().Where(a => a.Id == assessmentId).ToArrayAsync(cancellationToken);
        return (await AssessmentDetails(db, rows, cancellationToken)).SingleOrDefault();
    }

    public async Task<IReadOnlyList<TradingRuleAssessmentDetails>> ReadAssessmentsAsync(Guid ruleId,
        TradingRuleAssessmentTarget target, int? beforeSequence = null, int limit = 20, CancellationToken cancellationToken = default)
    {
        ValidateId(ruleId); ArgumentNullException.ThrowIfNull(target); ValidateLimit(limit);
        if (beforeSequence is < 1) throw new ArgumentOutOfRangeException(nameof(beforeSequence));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await ReadSnapshot(db, cancellationToken);
        var rows = await AssessmentRows(db, ruleId, target).Where(a => !beforeSequence.HasValue || a.Sequence < beforeSequence.Value)
            .OrderByDescending(a => a.Sequence).Take(limit).ToArrayAsync(cancellationToken);
        return await AssessmentDetails(db, rows, cancellationToken);
    }

    private static async Task<SqliteTransaction> ReadSnapshot(JournalDbContext db, CancellationToken token)
    {
        await db.Database.OpenConnectionAsync(token);
        token.ThrowIfCancellationRequested();
        var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        try { await db.Database.UseTransactionAsync(transaction, token); return transaction; }
        catch { await transaction.DisposeAsync(); throw; }
    }
    private static void ValidateLimit(int limit)
    { if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit), "Read 1–100 rows per page."); }

    private static async Task<IReadOnlyList<TradingRuleDetails>> RuleDetails(JournalDbContext db,
        TradingRuleRevisionRecord[] rows, CancellationToken token)
    {
        Guid[] ids = rows.Where(r => r.AccountId.HasValue).Select(r => r.AccountId!.Value).Distinct().ToArray();
        var available = (await db.TradingAccounts.Where(a => ids.Contains(a.Id)).Select(a => a.Id).ToArrayAsync(token)).ToHashSet();
        return Array.AsReadOnly(rows.Select(r => new TradingRuleDetails(ToDomain(r), r.AccountNameAtRevision,
            r.AccountId is null || available.Contains(r.AccountId.Value))).ToArray());
    }

    private static async Task<IReadOnlyList<TradingRuleAssessmentDetails>> AssessmentDetails(JournalDbContext db,
        TradingRuleAssessmentRecord[] rows, CancellationToken token)
    {
        if (rows.Length == 0) return Array.Empty<TradingRuleAssessmentDetails>();
        Guid[] ruleIds = rows.Select(r => r.RuleId).Distinct().ToArray();
        int[] versions = rows.Select(r => r.RuleRevision).Distinct().ToArray();
        var revisions = await db.TradingRuleRevisions.AsNoTracking().Where(r => ruleIds.Contains(r.RuleId) && versions.Contains(r.Revision))
            .ToDictionaryAsync(r => (r.RuleId, r.Revision), token);
        var sources = rows.ToDictionary(r => r.Id, Sources);
        var current = await RuleEvidenceSnapshots.LoadAsync(db, sources.Values.SelectMany(s => s).Select(s => s.Reference)
            .Concat(rows.Where(r => r.TradeId.HasValue).Select(r => new TradingRuleEvidenceReference(TradingRuleEvidenceKind.Trade, r.TradeId!.Value))), token);
        Guid[] accountIds = rows.Where(r => r.AccountId.HasValue).Select(r => r.AccountId!.Value).ToArray();
        var availableAccounts = (await db.TradingAccounts.Where(a => accountIds.Contains(a.Id)).Select(a => a.Id).ToArrayAsync(token)).ToHashSet();
        return Array.AsReadOnly(rows.Select(row =>
        {
            var state = row.AccountId is null || availableAccounts.Contains(row.AccountId.Value)
                ? TradingRuleSourceState.Available : TradingRuleSourceState.Unavailable;
            if (row.TradeId is Guid trade)
                state = State(new(TradingRuleEvidenceKind.Trade, trade), row.TargetFingerprint!);
            return new TradingRuleAssessmentDetails(ToDomain(row, ToDomain(revisions[(row.RuleId, row.RuleRevision)])),
                row.Sequence, row.AccountNameAtAssessment, state,
                Array.AsReadOnly(sources[row.Id].Select(s => new TradingRuleEvidenceDetails(s.Reference, s.AccountId,
                    s.AccountName, s.TradeId, s.NewYorkDate, s.Currency, State(s.Reference, s.Fingerprint))).ToArray()),
                row.EvaluationJson is null ? null : System.Text.Json.JsonSerializer.Deserialize<RuleEvaluationEvidence>(row.EvaluationJson, RuleEvidenceSnapshots.Json));
        }).ToArray());

        TradingRuleSourceState State(TradingRuleEvidenceReference reference, string hash) =>
            !current.Items.TryGetValue(reference, out var live) ? TradingRuleSourceState.Unavailable :
            live.Fingerprint == hash ? TradingRuleSourceState.Available : TradingRuleSourceState.Changed;
    }
}
