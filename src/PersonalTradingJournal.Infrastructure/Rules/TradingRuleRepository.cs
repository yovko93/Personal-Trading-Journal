using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Rules;

public sealed partial class TradingRuleRepository(IDbContextFactory<JournalDbContext> factory, TimeProvider? clock = null)
    : ITradingRuleStore, ITradingRuleAssessmentStore, ITradingRuleReader
{
    public async Task<TradingRuleRevision?> GetLatestAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        ValidateId(ruleId);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var row = await LatestRule(db, ruleId).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToDomain(row);
    }

    public async Task<bool> TryAppendAsync(TradingRuleRevision candidate, int expectedRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (expectedRevision < 0 || expectedRevision == int.MaxValue || candidate.Revision != expectedRevision + 1)
            throw new ArgumentException("A consecutive expected revision is required.", nameof(expectedRevision));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);
        var latest = await LatestRule(db, candidate.RuleId).FirstOrDefaultAsync(cancellationToken);
        if ((latest?.Revision ?? 0) != expectedRevision) return false;
        if (!candidate.Definition.IsGlobal || latest is { IsGlobal: false } || latest is { IsGlobal: true, IsActive: false }) return false;
        if (latest is null && !candidate.IsActive) throw new ArgumentException("A new Rule must be active.", nameof(candidate));
        if (latest is not null)
        {
            if (candidate.RecordedAtUtc < latest.RecordedAtUtc) return false;
            var previous = ToDomain(latest);
            if ((!previous.IsActive && !candidate.IsActive) ||
                (previous.IsActive != candidate.IsActive && previous.Definition != candidate.Definition))
                throw new ArgumentException("Archive/reopen must preserve the definition; edit an active Rule separately.", nameof(candidate));
        }
        string? accountName = null;
        if (candidate.Definition.AccountScope.AccountId is Guid account)
        {
            accountName = await db.TradingAccounts.Where(a => a.Id == account).Select(a => a.Name).SingleOrDefaultAsync(cancellationToken);
            if (accountName is null)
            {
                // Historical exact scope may be archived after Account deletion, never silently reassigned.
                if (candidate.IsActive || latest?.AccountId != account) return false;
                accountName = latest.AccountNameAtRevision;
            }
        }
        db.TradingRuleRevisions.Add(ToRecord(candidate, accountName));
        await db.SaveChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(CancellationToken.None);
        return true;
    }

    public async Task<TradingRuleAssessment?> GetLatestAsync(Guid ruleId, TradingRuleAssessmentTarget target,
        CancellationToken cancellationToken = default)
    {
        ValidateId(ruleId); ArgumentNullException.ThrowIfNull(target);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        // The revision and assessment are immutable and cannot be deleted between these reads.
        var row = await AssessmentRows(db, ruleId, target).OrderByDescending(a => a.Sequence).FirstOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        var revision = await db.TradingRuleRevisions.AsNoTracking().SingleAsync(r => r.RuleId == row.RuleId && r.Revision == row.RuleRevision, cancellationToken);
        return ToDomain(row, ToDomain(revision));
    }

    public async Task<bool> TryAppendAsync(TradingRuleAssessment candidate, int expectedRuleRevision,
        Guid? expectedLatestAssessmentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (expectedRuleRevision < 1 || candidate.RuleRevision.Revision != expectedRuleRevision ||
            candidate.SupersedesAssessmentId != expectedLatestAssessmentId || candidate.EvidenceReferences.Count > 1000)
            throw new ArgumentException("Expected Rule/assessment identities and at most 1000 sources are required.", nameof(candidate));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);
        var rule = await LatestRule(db, candidate.RuleRevision.RuleId).FirstOrDefaultAsync(cancellationToken);
        if (rule is null || !rule.IsGlobal || !rule.IsActive || rule.Revision != expectedRuleRevision) return false;
        var actual = ToDomain(rule);
        if (actual.Definition != candidate.RuleRevision.Definition || actual.RecordedAtUtc != candidate.RuleRevision.RecordedAtUtc)
            return false; // Never accept a fabricated definition carrying a genuine revision ID.
        if (candidate.Source == TradingRuleAssessmentSource.AutomaticCandidate &&
            actual.Definition.EvaluationMode != TradingRuleEvaluationMode.AutomaticCandidate) return false;
        var latest = await AssessmentRows(db, rule.RuleId, candidate.Target).OrderByDescending(a => a.Sequence).FirstOrDefaultAsync(cancellationToken);
        if (latest?.Id != expectedLatestAssessmentId || (latest is not null && candidate.RecordedAtUtc < latest.RecordedAtUtc) ||
            await db.TradingRuleAssessments.AnyAsync(a => a.Id == candidate.Id, cancellationToken)) return false;
        string? accountName = null;
        if (candidate.Target.AccountScope.AccountId is Guid account)
        {
            accountName = await db.TradingAccounts.Where(a => a.Id == account).Select(a => a.Name).SingleOrDefaultAsync(cancellationToken);
            if (accountName is null) return false;
        }
        var references = candidate.EvidenceReferences.ToList();
        TradingRuleEvidenceReference? targetReference = candidate.Target.TradeId is Guid tradeId
            ? new(TradingRuleEvidenceKind.Trade, tradeId) : null;
        if (targetReference is not null) references.Add(targetReference);
        var live = await RuleEvidenceSnapshots.LoadAsync(db, references, cancellationToken);
        RuleEvidenceSnapshot? targetTrade = null;
        if (targetReference is not null && (!live.Items.TryGetValue(targetReference, out targetTrade) ||
            targetTrade.AccountId != candidate.Target.AccountScope.AccountId)) return false;
        var sources = new List<RuleEvidenceSnapshot>();
        foreach (var reference in candidate.EvidenceReferences)
        {
            if (!live.Items.TryGetValue(reference, out var source) || !live.Matches(source, candidate.Target, targetTrade)) return false;
            sources.Add(source);
        }
        db.TradingRuleAssessments.Add(new()
        {
            Id = candidate.Id, RuleId = rule.RuleId, RuleRevision = expectedRuleRevision,
            Sequence = checked((latest?.Sequence ?? 0) + 1), TargetKey = TargetKey(candidate.Target),
            TargetKind = (int)candidate.Target.Kind, TradeId = candidate.Target.TradeId, NewYorkDate = candidate.Target.NewYorkDate,
            ScopeKind = (int)candidate.Target.AccountScope.Kind, AccountId = candidate.Target.AccountScope.AccountId,
            AccountNameAtAssessment = accountName, TargetFingerprint = targetTrade?.Fingerprint,
            Outcome = (int)candidate.Outcome, Source = (int)candidate.Source, Basis = (int)candidate.Basis,
            EvidenceSufficiency = (int)candidate.EvidenceSufficiency, Explanation = candidate.Explanation,
            EvidenceJson = JsonSerializer.Serialize(sources, RuleEvidenceSnapshots.Json),
            SupersedesAssessmentId = candidate.SupersedesAssessmentId, RecordedAtUtc = candidate.RecordedAtUtc,
        });
        await db.SaveChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(CancellationToken.None);
        return true;
    }

    private static IOrderedQueryable<TradingRuleRevisionRecord> LatestRule(JournalDbContext db, Guid id) =>
        db.TradingRuleRevisions.AsNoTracking().Where(r => r.RuleId == id).OrderByDescending(r => r.Revision);
    private static IQueryable<TradingRuleAssessmentRecord> AssessmentRows(JournalDbContext db, Guid ruleId, TradingRuleAssessmentTarget target)
    {
        string key = TargetKey(target);
        return db.TradingRuleAssessments.AsNoTracking().Where(a => a.RuleId == ruleId && a.TargetKey == key);
    }
    internal static string TargetKey(TradingRuleAssessmentTarget target) => target.Kind == TradingRuleTargetKind.Trade
        ? $"trade:{target.TradeId:D}:{target.AccountScope.AccountId:D}"
        : "day:" + target.NewYorkDate!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ":" +
          (target.AccountScope.AccountId?.ToString("D") ?? "all");
    private static void ValidateId(Guid id) { if (id == Guid.Empty) throw new ArgumentException("An identity is required."); }
    private static TradingRuleAccountScope Scope(Guid? id) => id is Guid account ? TradingRuleAccountScope.ForAccount(account) : TradingRuleAccountScope.AllAccounts;
    private static TradingRuleRevision ToDomain(TradingRuleRevisionRecord r)
    {
        if (r.ContractVersion != TradingRuleRevision.ContractVersion) throw new InvalidDataException("Unsupported Rule contract.");
        var criterion = r.AutomaticCriterionJson is null ? null : JsonSerializer.Deserialize<AutomaticRuleCriterion>(r.AutomaticCriterionJson);
        return TradingRuleRevision.Rehydrate(r.RuleId, r.Revision,
            r.IsGlobal ? new(r.Name, r.Criterion, r.RequiredEvidence, criterion) : new(r.Name, r.Criterion, r.RequiredEvidence, (TradingRuleApplicability)r.Applicability,
                Scope(r.AccountId), (TradingRuleEvaluationMode)r.EvaluationMode,
                criterion), r.IsActive, r.RecordedAtUtc);
    }
    private static TradingRuleRevisionRecord ToRecord(TradingRuleRevision r, string? accountName) => new()
    {
        RuleId = r.RuleId, Revision = r.Revision, ContractVersion = TradingRuleRevision.ContractVersion,
        IsGlobal = r.Definition.IsGlobal, CheckCapability = r.Definition.IsGlobal && r.Definition.AutomaticCriterion is { } c ? (int)c.Capability : null,
        Name = r.Definition.Name, Criterion = r.Definition.Criterion, RequiredEvidence = r.Definition.RequiredEvidence,
        Applicability = (int)r.Definition.Applicability, ScopeKind = (int)r.Definition.AccountScope.Kind,
        AccountId = r.Definition.AccountScope.AccountId, AccountNameAtRevision = accountName,
        EvaluationMode = (int)r.Definition.EvaluationMode, IsActive = r.IsActive, RecordedAtUtc = r.RecordedAtUtc,
        AutomaticCriterionJson = r.Definition.AutomaticCriterion is null ? null : JsonSerializer.Serialize(r.Definition.AutomaticCriterion),
    };
    private static TradingRuleAssessmentTarget Target(TradingRuleAssessmentRecord r) => r.TargetKind == (int)TradingRuleTargetKind.Trade
        ? TradingRuleAssessmentTarget.ForTrade(r.TradeId!.Value, r.AccountId!.Value)
        : TradingRuleAssessmentTarget.ForNewYorkDay(r.NewYorkDate!.Value, Scope(r.AccountId));
    private static RuleEvidenceSnapshot[] Sources(TradingRuleAssessmentRecord r) =>
        JsonSerializer.Deserialize<RuleEvidenceSnapshot[]>(r.EvidenceJson, RuleEvidenceSnapshots.Json)
        ?? throw new InvalidDataException("Assessment source metadata is unavailable.");
    private static TradingRuleAssessment ToDomain(TradingRuleAssessmentRecord r, TradingRuleRevision rule) =>
        TradingRuleAssessment.Record(r.Id, rule, Target(r), (TradingRuleAssessmentOutcome)r.Outcome,
            (TradingRuleAssessmentSource)r.Source, (TradingRuleAssessmentBasis)r.Basis,
            (TradingRuleEvidenceSufficiency)r.EvidenceSufficiency, r.Explanation, Sources(r).Select(s => s.Reference),
            r.SupersedesAssessmentId, r.RecordedAtUtc);
}
