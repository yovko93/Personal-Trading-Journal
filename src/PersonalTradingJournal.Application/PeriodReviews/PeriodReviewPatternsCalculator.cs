using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Application.PeriodReviews;

/// <summary>Pure, bounded interpretation of recorded outcomes and mistake assignments. Does not
/// execute checks, infer adherence, recalculate economics, query sources or mutate the snapshot.</summary>
public static class PeriodReviewPatternsCalculator
{
    public const int MaximumTargets = 100_000;

    public static PeriodPatternsResult Calculate(PeriodReviewEvidence evidence,
        CancellationToken cancellationToken = default, int maxTargets = MaximumTargets)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (maxTargets is < 1 or > MaximumTargets) throw new ArgumentOutOfRangeException(nameof(maxTargets));
        cancellationToken.ThrowIfCancellationRequested();
        var limits = new PeriodEvidenceLimits();
        if ((long)evidence.ClosedTrades.Count + evidence.ActivityContext.Count > limits.MaxRowsPerSet ||
            evidence.ApplicableRules.Count > limits.MaxRowsPerSet ||
            (long)evidence.Assessments.Count + evidence.UnattributedTradeAssessments.Count > limits.MaxScopedAssessments ||
            evidence.ClosedTrades.Concat(evidence.ActivityContext).Sum(t => (long)t.AssignedMistakes.Count) > limits.MaxRowsPerSet)
            return Limit("source-rows");

        var trades = evidence.ClosedTrades.Concat(evidence.ActivityContext).ToArray();
        Require(trades.Select(t => t.TradeId).Distinct().Count() == trades.Length);
        Require(evidence.ApplicableRules.Select(r => r.RuleId).Distinct().Count() == evidence.ApplicableRules.Count);
        var allAssessments = evidence.Assessments.Concat(evidence.UnattributedTradeAssessments).ToArray();
        Require(allAssessments.Select(a => a.Assessment.Id).Distinct().Count() == allAssessments.Length);
        foreach (var trade in trades)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Require(trade.TradeId != Guid.Empty && trade.Account.Id != Guid.Empty && InScope(TradingRuleAccountScope.ForAccount(trade.Account.Id)));
            Require(trade.AssignedMistakes.Select(m => m.Mistake.Id).Distinct().Count() == trade.AssignedMistakes.Count);
        }
        Require(trades.SelectMany(t => t.AssignedMistakes).Select(m => m.AssignmentId).Distinct().Count() == trades.Sum(t => t.AssignedMistakes.Count));
        foreach (var a in allAssessments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Require(InScope(a.Assessment.Target.AccountScope) && a.Sequence > 0);
        }
        foreach (var a in evidence.Assessments.Where(a => a.Assessment.Target.Kind == TradingRuleTargetKind.NewYorkDay))
            Require(a.Assessment.Target.NewYorkDate >= evidence.Query.FirstDate && a.Assessment.Target.NewYorkDate <= evidence.Query.LastDate);

        var current = evidence.ApplicableRules.Where(r => r.IsActive && r.Definition.IsGlobal).ToDictionary(r => r.RuleId);
        var liveTargets = trades.Select(t => TradingRuleAssessmentTarget.ForTrade(t.TradeId, t.Account.Id)).ToHashSet();
        int days = evidence.Query.LastDate.DayNumber - evidence.Query.FirstDate.DayNumber + 1;
        // Reject before allocating a potentially enormous Rule × target population. No partial summary.
        if (checked((long)current.Count * (liveTargets.Count + days)) > maxTargets) return Limit("rule-targets");
        var histories = evidence.Assessments.GroupBy(a => new TargetKey(a.Assessment.RuleRevision.RuleId,
            a.Assessment.RuleRevision.Revision, a.Assessment.Target)).ToDictionary(g => g.Key, g => g.OrderBy(a => a.Sequence).ToArray());
        foreach (var family in allAssessments.GroupBy(a => (a.Assessment.RuleRevision.RuleId, a.Assessment.Target)))
            Require(family.Select(a => a.Sequence).Distinct().Count() == family.Count());
        var rows = new Dictionary<TargetKey, Row>();
        foreach (var rule in current.Values.OrderBy(r => r.RuleId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var target in liveTargets) Add(rule, target, PeriodRuleDenominator.CurrentAssessmentOpportunities);
            for (int d = 0; d < days; d++)
                Add(rule, TradingRuleAssessmentTarget.ForNewYorkDay(evidence.Query.FirstDate.AddDays(d), evidence.Query.Scope),
                    PeriodRuleDenominator.CurrentAssessmentOpportunities);
        }
        foreach (var history in histories.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var a = history[^1].Assessment;
            var key = new TargetKey(a.RuleRevision.RuleId, a.RuleRevision.Revision, a.Target);
            if (!rows.ContainsKey(key))
            {
                if (rows.Count == maxTargets) return Limit("rule-targets");
                Add(a.RuleRevision, a.Target, PeriodRuleDenominator.RecordedTargetsOnly);
            }
        }
        var rules = rows.Values.GroupBy(r => (r.Rule.RuleId, r.Rule.Revision, r.State,
                r.Coverage.Target.Kind, r.Coverage.Target.AccountScope, r.Denominator))
            .OrderBy(g => g.Key.RuleId).ThenBy(g => g.Key.Revision).ThenBy(g => g.Key.Kind)
            .ThenBy(g => g.Key.AccountScope.Kind).ThenBy(g => g.Key.AccountScope.AccountId).ThenBy(g => g.Key.Denominator)
            .Select(g => new PeriodRuleSummary(g.First().Rule, g.Key.State, g.Key.Kind, g.Key.AccountScope, g.Key.Denominator,
                Array.AsReadOnly(g.Select(r => r.Coverage).OrderBy(t => t.Target.NewYorkDate).ThenBy(t => t.Target.TradeId).ToArray())))
            .ToArray();

        var mistakes = new List<PeriodMistakeSummary>();
        foreach (var group in trades.SelectMany(t => t.AssignedMistakes.Select(m =>
                     new PeriodMistakeObservation(t.TradeId, t.Account, t.Inclusion, m))).GroupBy(o => o.Assignment.Mistake.Id).OrderBy(g => g.Key))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Require(group.Select(o => o.Assignment.Mistake).Distinct().Count() == 1);
            var accounts = group.GroupBy(o => o.Account.Id).OrderBy(g => g.Key).Select(g =>
            {
                Require(g.Select(o => o.Account).Distinct().Count() == 1);
                return new PeriodMistakeAccountSummary(g.First().Account,
                    Array.AsReadOnly(g.Where(o => o.Inclusion == DailyReviewTradeInclusion.ClosedOnDate).Select(o => o.TradeId).Order().ToArray()),
                    Array.AsReadOnly(g.Where(o => o.Inclusion != DailyReviewTradeInclusion.ClosedOnDate).Select(o => o.TradeId).Order().ToArray()));
            }).ToArray();
            mistakes.Add(new(group.First().Assignment.Mistake,
                Array.AsReadOnly(group.OrderBy(o => o.TradeId).ThenBy(o => o.Assignment.AssignmentId).ToArray()), Array.AsReadOnly(accounts)));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(PeriodPatternsStatus.Ready, new(evidence, Array.AsReadOnly(rules), mistakes.AsReadOnly()), null);

        bool InScope(TradingRuleAccountScope scope) => evidence.Query.Scope.Kind == TradingRuleAccountScopeKind.AllAccounts || scope == evidence.Query.Scope;
        void Add(TradingRuleRevision rule, TradingRuleAssessmentTarget target, PeriodRuleDenominator denominator)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = new TargetKey(rule.RuleId, rule.Revision, target);
            var history = histories.GetValueOrDefault(key) ?? [];
            var state = !rule.Definition.IsGlobal ? PeriodRuleVersionState.LegacyScoped :
                !current.TryGetValue(rule.RuleId, out var active) ? PeriodRuleVersionState.NotInActiveGlobalCatalog :
                active.Revision == rule.Revision ? PeriodRuleVersionState.CurrentGlobal : PeriodRuleVersionState.SupersededGlobal;
            bool automatic = state == PeriodRuleVersionState.CurrentGlobal && rule.Definition.AutomaticCriterion is { } criterion &&
                (target.Kind == TradingRuleTargetKind.Trade ? criterion.Capability == RuleCheckCapability.ClosedTradeLoss : criterion.Capability == RuleCheckCapability.ClosedDayLoss);
            var outcome = history.LastOrDefault()?.Assessment.Outcome switch
            {
                null => RuleCoverageOutcome.NotAssessed,
                TradingRuleAssessmentOutcome.NoViolation => RuleCoverageOutcome.Compliant,
                TradingRuleAssessmentOutcome.ConfirmedViolation => RuleCoverageOutcome.ConfirmedViolation,
                TradingRuleAssessmentOutcome.CannotEvaluate => RuleCoverageOutcome.CannotEvaluate,
                _ => throw new ArgumentException("Not assessed is absence, not a recorded outcome.", nameof(evidence)),
            };
            rows.Add(key, new(rule, state, denominator, new(target, outcome, automatic, Array.AsReadOnly(history))));
        }
    }

    private static void Require(bool valid)
    {
        if (!valid) throw new ArgumentException("Supply a consistent, unique, correctly scoped M19.1 snapshot.");
    }
    private static PeriodPatternsResult Limit(string code) => new(PeriodPatternsStatus.LimitExceeded, null, code);
    private sealed record TargetKey(Guid RuleId, int Revision, TradingRuleAssessmentTarget Target);
    private sealed record Row(TradingRuleRevision Rule, PeriodRuleVersionState State, PeriodRuleDenominator Denominator, PeriodRuleTargetCoverage Coverage);
}
