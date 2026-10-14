using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Rules;

public sealed partial class TradingRuleRepository : IAutomaticRuleChecks
{
    private sealed record CheckSnapshot(TradingRuleRevisionRecord[] Rules, RuleTradeFact[] Facts,
        RuleEvidenceSnapshot[] Sources, Guid[] ExecutionIds, string? AccountName, string Fingerprint);

    public async Task<RuleCheckRun> RunAsync(TradingRuleAssessmentTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        var token = cancellationToken;
        CheckSnapshot? prepared;
        try
        {
            await using (var read = await factory.CreateDbContextAsync(token))
            await using (var snapshot = await ReadSnapshot(read, token))
                prepared = await CheckInput(read, target, token);
            if (prepared is null) return new(RuleCheckStatus.UnavailableTarget, 0, 0, []);
            await using var db = await factory.CreateDbContextAsync(token);
            await db.Database.OpenConnectionAsync(token);
            token.ThrowIfCancellationRequested();
            await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
            await db.Database.UseTransactionAsync(transaction, token);
            // Requery the entire bounded source set, not merely known IDs: new imports/deletions
            // and revised/archived/new rules invalidate the prepared batch too.
            var current = await CheckInput(db, target, token);
            if (current is null || prepared.Fingerprint != current.Fingerprint)
                return new(RuleCheckStatus.Conflict, 0, 0, []);
            int added = 0, unchanged = 0;
            var ids = new List<Guid>();
            foreach (var row in current.Rules)
            {
                token.ThrowIfCancellationRequested();
                var rule = ToDomain(row);
                var criterion = rule.Definition.AutomaticCriterion!;
                string fingerprint = RuleEvidenceSnapshots.Hash(new { AutomaticRuleCalculator.Version, target, Rule = row, current.Facts, current.Sources, current.ExecutionIds });
                var existing = await AssessmentRows(db, row.RuleId, target).Where(a => a.RuleRevision == row.Revision && a.EvaluationFingerprint == fingerprint)
                    .Select(a => (Guid?)a.Id).SingleOrDefaultAsync(token);
                if (existing.HasValue) { unchanged++; ids.Add(existing.Value); continue; }
                var calculation = AutomaticRuleCalculator.Calculate(criterion, current.Facts);
                var prior = await AssessmentRows(db, row.RuleId, target).OrderByDescending(a => a.Sequence).FirstOrDefaultAsync(token);
                var now = (clock ?? TimeProvider.System).GetUtcNow();
                if (now < rule.RecordedAtUtc || (prior is not null && now < prior.RecordedAtUtc))
                    throw new InvalidOperationException("Evaluation clock precedes retained history.");
                var id = Guid.NewGuid();
                // Domain outcome validation still applies. An empty day is Cannot evaluate, not
                // a fabricated evidence-backed pass. All source Account/currency identities remain.
                var assessment = TradingRuleAssessment.Record(id, rule, target, calculation.Outcome,
                    TradingRuleAssessmentSource.AutomaticCandidate, TradingRuleAssessmentBasis.RecordedEvidence,
                    calculation.Outcome == TradingRuleAssessmentOutcome.CannotEvaluate ? TradingRuleEvidenceSufficiency.Missing : TradingRuleEvidenceSufficiency.Sufficient,
                    calculation.Explanation, current.Sources.Select(s => s.Reference), prior?.Id, now);
                db.TradingRuleAssessments.Add(new()
                {
                    Id = id, RuleId = row.RuleId, RuleRevision = row.Revision, Sequence = checked((prior?.Sequence ?? 0) + 1),
                    TargetKey = TargetKey(target), TargetKind = (int)target.Kind, TradeId = target.TradeId, NewYorkDate = target.NewYorkDate,
                    ScopeKind = (int)target.AccountScope.Kind, AccountId = target.AccountScope.AccountId, AccountNameAtAssessment = current.AccountName,
                    TargetFingerprint = target.TradeId is Guid trade ? current.Sources.Single(s => s.SourceId == trade).Fingerprint : null,
                    Outcome = (int)assessment.Outcome, Source = (int)assessment.Source, Basis = (int)assessment.Basis,
                    EvidenceSufficiency = (int)assessment.EvidenceSufficiency, Explanation = assessment.Explanation,
                    EvidenceJson = JsonSerializer.Serialize(current.Sources, RuleEvidenceSnapshots.Json),
                    EvaluationFingerprint = fingerprint,
                    EvaluationJson = JsonSerializer.Serialize(new { EvaluatorVersion = AutomaticRuleCalculator.Version, Criterion = criterion,
                        Calculation = calculation, Facts = current.Facts, ExecutionIds = current.ExecutionIds }, RuleEvidenceSnapshots.Json),
                    SupersedesAssessmentId = prior?.Id, RecordedAtUtc = now,
                });
                added++; ids.Add(id);
            }
            await db.SaveChangesAsync(token);
            token.ThrowIfCancellationRequested();
            await transaction.CommitAsync(CancellationToken.None);
            return new(RuleCheckStatus.Completed, added, unchanged, ids.AsReadOnly());
        }
        catch (CheckLimitException) { return new(RuleCheckStatus.LimitExceeded, 0, 0, []); }
    }

    private sealed class CheckLimitException : Exception;
    private static async Task<CheckSnapshot?> CheckInput(JournalDbContext db, TradingRuleAssessmentTarget target, CancellationToken token)
    {
        string? accountName = null;
        if (target.AccountScope.AccountId is Guid account)
        {
            accountName = await db.TradingAccounts.Where(a => a.Id == account).Select(a => a.Name).SingleOrDefaultAsync(token);
            if (accountName is null) return null;
        }
        int applicability = target.Kind == TradingRuleTargetKind.Trade ? 1 : 2;
        var rules = await db.TradingRuleRevisions.AsNoTracking().Where(r => r.IsActive && r.AutomaticCriterionJson != null &&
            r.IsGlobal && r.CheckCapability == applicability &&
            !db.TradingRuleRevisions.Any(n => n.RuleId == r.RuleId && n.Revision > r.Revision))
            .OrderBy(r => r.RuleId).Take(101).ToArrayAsync(token);
        if (rules.Length > 100) throw new CheckLimitException();
        var query = db.Trades.AsNoTracking().Where(t => target.AccountScope.AccountId == null || t.TradingAccountId == target.AccountScope.AccountId);
        if (target.TradeId is Guid tradeId) query = query.Where(t => t.Id == tradeId);
        else
        {
            var day = new TradingCalendarDayQuery(target.NewYorkDate!.Value);
            // Any activity can identify a candidate; reconstructed closure decides inclusion.
            // Missing executions cannot establish a date and conservatively block a complete result.
            query = query.Where(t => !db.TradeExecutions.Any(e => e.TradeId == t.Id) ||
                db.TradeExecutions.Any(e => e.TradeId == t.Id && e.ExecutedAtUtc >= day.ClosedFromUtc && e.ExecutedAtUtc < day.ClosedBeforeUtc));
        }
        var trades = await query.OrderBy(t => t.Id).Take(1001).ToArrayAsync(token);
        if (trades.Length > 1000) throw new CheckLimitException();
        if (target.TradeId.HasValue && trades.Length != 1) return null;
        Guid[] ids = trades.Select(t => t.Id).ToArray();
        var executions = await db.TradeExecutions.AsNoTracking().Where(e => ids.Contains(e.TradeId))
            .OrderBy(e => e.TradeId).ThenBy(e => e.Sequence).ThenBy(e => e.Id).Take(10001).ToArrayAsync(token);
        if (executions.Length > 10000) throw new CheckLimitException();
        var byTrade = executions.ToLookup(e => e.TradeId);
        var facts = new List<RuleTradeFact>();
        foreach (var trade in trades)
        {
            token.ThrowIfCancellationRequested();
            var rows = byTrade[trade.Id].ToArray();
            var domain = rows.Length == 0 ? null : TradePersistenceMapper.ToDomain(trade, rows);
            if (target.NewYorkDate is DateOnly selected && domain?.ClosedAtUtc is DateTimeOffset closed &&
                PersonalTradingJournal.Application.Analytics.DashboardMetricCalculator.GetNewYorkCloseDate(closed) != selected) continue;
            facts.Add(new(trade.Id, trade.TradingAccountId, trade.PricingCurrency, domain?.Status == TradeStatus.Closed,
                domain?.GrossPnL, domain?.NetPnL, rows.Count(e => e.Commission is null), rows.Count(e => e.Fees is null), domain is null));
        }
        var references = facts.Select(f => new TradingRuleEvidenceReference(TradingRuleEvidenceKind.Trade, f.TradeId)).ToArray();
        var sources = (await RuleEvidenceSnapshots.LoadAsync(db, references, token)).Items.Values.OrderBy(s => s.SourceId).ToArray();
        var factArray = facts.ToArray();
        var included = facts.Select(f => f.TradeId).ToHashSet();
        var executionIds = executions.Where(e => included.Contains(e.TradeId)).Select(e => e.Id).ToArray();
        return new(rules, factArray, sources, executionIds, accountName,
            RuleEvidenceSnapshots.Hash(new { target, rules, Facts = factArray, sources, executionIds, accountName }));
    }
}
