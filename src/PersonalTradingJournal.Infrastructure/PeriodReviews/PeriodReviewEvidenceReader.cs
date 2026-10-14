using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Infrastructure.DailyReview;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Rules;

namespace PersonalTradingJournal.Infrastructure.PeriodReviews;

public sealed class PeriodReviewEvidenceReader(IDbContextFactory<JournalDbContext> factory,
    PeriodEvidenceLimits? limits = null) : IPeriodReviewEvidenceReader
{
    public async Task<PeriodEvidenceResult> ReadAsync(PeriodReviewQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await using var enlisted = await db.Database.UseTransactionAsync(transaction, cancellationToken);
        var budget = new ReadBudget(limits ?? new());
        try
        {
            Guid? account = query.Scope.AccountId;
            var assessmentsQuery = db.TradingRuleAssessments.AsNoTracking().Where(a => account == null || a.AccountId == account);
            // Scope history is bounded before decoding provenance. Old records did not persist
            // the target Trade's date independently; do not guess from assessment recording time.
            await budget.Check(assessmentsQuery.Select(a => (long)a.Explanation.Length + a.EvidenceJson.Length +
                (a.AccountNameAtAssessment == null ? 0 : a.AccountNameAtAssessment.Length) +
                (a.EvaluationJson == null ? 0 : a.EvaluationJson.Length)), "assessment-history", cancellationToken, scopedAssessments: true);
            var assessmentRows = await assessmentsQuery.OrderBy(a => a.RuleId).ThenBy(a => a.TargetKey).ThenBy(a => a.Sequence).ToArrayAsync(cancellationToken);
            var sources = assessmentRows.SelectMany(TradingRuleRepository.Sources).ToArray();
            budget.Count(sources.Length, "assessment-sources");
            var ids = DailyReviewEvidenceReader.CandidateTradeIds(db, account, query.FromUtc, query.BeforeUtc);
            var journalIds = db.DailyJournals.Where(j => j.TradingDate >= query.FirstDate && j.TradingDate <= query.LastDate &&
                (account == null || j.TradingAccountId == account)).Select(j => j.Id);
            var referencedTrades = sources.Where(s => s.TradeId.HasValue).Select(s => s.TradeId!.Value)
                .Concat(assessmentRows.Where(a => a.TradeId.HasValue).Select(a => a.TradeId!.Value)).Distinct().ToArray();
            var referencedJournals = sources.Where(s => s.Kind == TradingRuleEvidenceKind.Journal).Select(s => s.SourceId).Distinct().ToArray();
            var relatedTrades = db.Trades.Where(t => ids.Contains(t.Id) || referencedTrades.Contains(t.Id)).Select(t => t.Id);
            var relatedJournals = db.DailyJournals.Where(j => journalIds.Contains(j.Id) || referencedJournals.Contains(j.Id));

            // Count and text-length aggregates run inside the SAME read snapshot, before
            // materializing full lifecycles, JSON captures or historical source dependencies.
            await budget.Check(relatedTrades.Select(_ => 0L), "trades", cancellationToken);
            await budget.Check(db.TradeExecutions.Where(e => relatedTrades.Contains(e.TradeId)).Select(e =>
                (long)(e.BrokerSymbol == null ? 0 : e.BrokerSymbol.Length) + (e.ExternalOrderId == null ? 0 : e.ExternalOrderId.Length) +
                (e.ExternalExecutionId == null ? 0 : e.ExternalExecutionId.Length)), "executions", cancellationToken);
            await budget.Check(db.TradeMistakes.Where(m => ids.Contains(m.TradeId)).Select(_ => 0L), "mistakes", cancellationToken);
            await budget.Check(db.TradePlaybookContexts.Where(p => relatedTrades.Contains(p.TradeId)).Select(p => (long)p.DefinitionJson.Length), "playbook-captures", cancellationToken);
            await budget.Check(db.TradePlaybookAssociations.Where(p => relatedTrades.Contains(p.TradeId)).Select(p => (long)(p.DefinitionJson == null ? 0 : p.DefinitionJson.Length)), "playbook-transitions", cancellationToken);
            await budget.Check(relatedJournals.Select(j => (long)j.Text.Length + j.WentWell.Length + j.NeedsImprovement.Length + j.NextTradingDay.Length), "journals", cancellationToken);
            await budget.Check(db.DailyJournalRevisions.Where(j => relatedJournals.Select(x => x.Id).Contains(j.JournalId))
                .Select(j => (long)j.Text.Length + j.WentWell.Length + j.NeedsImprovement.Length + j.NextTradingDay.Length), "journal-revisions", cancellationToken);
            await budget.Check(db.TradingRuleRevisions.Select(r => (long)r.Name.Length + r.Criterion.Length + r.RequiredEvidence.Length +
                (r.AccountNameAtRevision == null ? 0 : r.AccountNameAtRevision.Length) +
                (r.AutomaticCriterionJson == null ? 0 : r.AutomaticCriterionJson.Length)), "rule-revisions", cancellationToken);
            // Reference labels are also bounded; no hidden unbounded string payloads.
            await budget.Check(db.TradingAccounts.Select(a => (long)a.Name.Length), "account-catalog", cancellationToken);
            await budget.Check(db.Instruments.Select(i => (long)i.Symbol.Length), "instrument-catalog", cancellationToken);
            await budget.Check(db.TradingSetups.Select(s => (long)s.Name.Length), "setup-catalog", cancellationToken);
            await budget.Check(db.TradingMistakes.Select(m => (long)m.Name.Length), "mistake-catalog", cancellationToken);

            var daily = await DailyReviewEvidenceReader.ReadSnapshotAsync(db, new(query.FirstDate, account), query.LastDate, query.BeforeUtc, cancellationToken);
            var revisions = await db.DailyJournalRevisions.AsNoTracking().Where(r => journalIds.Contains(r.JournalId))
                .OrderBy(r => r.JournalId).ThenBy(r => r.Revision)
                .Select(r => new DailyJournalRevision(r.JournalId, r.Revision, r.Text, r.IsDraft, r.SavedAtUtc,
                    new DailyReviewAnswers(r.WentWell, r.NeedsImprovement, r.NextTradingDay))).ToArrayAsync(cancellationToken);
            var rules = await db.TradingRuleRevisions.AsNoTracking().Where(r => r.IsGlobal && r.IsActive &&
                !db.TradingRuleRevisions.Any(n => n.RuleId == r.RuleId && n.Revision > r.Revision))
                .OrderBy(r => r.RuleId).ToArrayAsync(cancellationToken);
            var selectedIds = daily.Trades.Select(t => t.TradeId).ToHashSet();
            var liveIds = (await db.Trades.Where(t => referencedTrades.Contains(t.Id)).Select(t => t.Id).ToArrayAsync(cancellationToken)).ToHashSet();
            var dates = assessmentRows.Where(a => a.TradeId.HasValue).GroupBy(a => (a.TradeId, a.AccountId))
                .ToDictionary(g => g.Key, g => g.SelectMany(TradingRuleRepository.Sources)
                    .Where(s => s.TradeId == g.Key.TradeId && s.NewYorkDate.HasValue).Select(s => s.NewYorkDate!.Value).Distinct().ToArray());
            var attributed = new HashSet<Guid>();
            var unknown = new HashSet<Guid>();
            foreach (var a in assessmentRows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (a.TradeId is not Guid trade)
                {
                    if (a.NewYorkDate >= query.FirstDate && a.NewYorkDate <= query.LastDate) attributed.Add(a.Id);
                    continue;
                }
                var recorded = dates[(a.TradeId, a.AccountId)];
                if (recorded.Length == 1)
                {
                    if (recorded[0] >= query.FirstDate && recorded[0] <= query.LastDate) attributed.Add(a.Id);
                }
                else if (recorded.Length > 1 || !liveIds.Contains(trade)) unknown.Add(a.Id);
                else if (selectedIds.Contains(trade)) attributed.Add(a.Id);
            }
            var details = await TradingRuleRepository.AssessmentDetails(db,
                assessmentRows.Where(a => attributed.Contains(a.Id) || unknown.Contains(a.Id)).ToArray(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return new(PeriodEvidenceStatus.Ready, new(query,
                Array.AsReadOnly(daily.Trades.Where(t => t.Inclusion == DailyReviewTradeInclusion.ClosedOnDate).ToArray()),
                Array.AsReadOnly(daily.Trades.Where(t => t.Inclusion != DailyReviewTradeInclusion.ClosedOnDate).ToArray()),
                daily.Journals, Array.AsReadOnly(revisions.Select(r => new PeriodJournalRevision(r.JournalId, r)).ToArray()),
                Array.AsReadOnly(rules.Select(TradingRuleRepository.ToDomain).ToArray()),
                Array.AsReadOnly(details.Where(d => attributed.Contains(d.Assessment.Id)).ToArray()),
                Array.AsReadOnly(details.Where(d => unknown.Contains(d.Assessment.Id)).ToArray())), null);
        }
        catch (EvidenceLimitException ex) { return new(PeriodEvidenceStatus.LimitExceeded, null, ex.Code); }
    }

    private sealed class EvidenceLimitException(string code) : Exception { public string Code { get; } = code; }
    private sealed class ReadBudget(PeriodEvidenceLimits limits)
    {
        private long _characters;
        public void Count(int count, string code)
        { if (count > limits.MaxRowsPerSet) throw new EvidenceLimitException(code); }
        public async Task Check(IQueryable<long> lengths, string code, CancellationToken token, bool scopedAssessments = false)
        {
            var size = await lengths.GroupBy(_ => 1).Select(g => new { Rows = g.Count(), Characters = g.Sum() }).SingleOrDefaultAsync(token);
            if (size is null) return;
            if (size.Rows > (scopedAssessments ? limits.MaxScopedAssessments : limits.MaxRowsPerSet)) throw new EvidenceLimitException(code);
            _characters = checked(_characters + size.Characters);
            if (_characters > limits.MaxTextCharacters) throw new EvidenceLimitException("text-budget");
        }
    }
}
