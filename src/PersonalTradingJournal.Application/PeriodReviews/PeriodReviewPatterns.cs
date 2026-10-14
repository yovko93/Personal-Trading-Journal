using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Application.PeriodReviews;

public enum PeriodRuleVersionState
{
    CurrentGlobal,
    SupersededGlobal,
    // The current-only M19.1 catalog cannot distinguish archive, deletion or other absence.
    NotInActiveGlobalCatalog,
    LegacyScoped,
}
public enum PeriodRuleDenominator
{
    // Current active global rule × selected Trade, or each calendar date at the requested scope.
    CurrentAssessmentOpportunities,
    // Historical versions, unavailable Trades, or exact-account day targets in an aggregate query.
    RecordedTargetsOnly,
}

public sealed record PeriodRuleTargetCoverage(TradingRuleAssessmentTarget Target,
    RuleCoverageOutcome Outcome, bool HasAutomaticCapability,
    IReadOnlyList<TradingRuleAssessmentDetails> History)
{
    public TradingRuleAssessmentDetails? Latest => History.Count == 0 ? null : History[^1];
}

/// <summary>Counts are exact revision/target pairs, never executions or assessments in a correction chain.
/// No adherence percentage: Cannot evaluate and Not assessed do not prove a pass or a violation.</summary>
public sealed record PeriodRuleSummary(TradingRuleRevision Rule, PeriodRuleVersionState VersionState,
    TradingRuleTargetKind TargetKind, TradingRuleAccountScope Scope, PeriodRuleDenominator Denominator,
    IReadOnlyList<PeriodRuleTargetCoverage> Targets)
{
    public int TargetCount => Targets.Count;
    public int Compliant => Targets.Count(t => t.Outcome == RuleCoverageOutcome.Compliant);
    public int ConfirmedViolation => Targets.Count(t => t.Outcome == RuleCoverageOutcome.ConfirmedViolation);
    public int CannotEvaluate => Targets.Count(t => t.Outcome == RuleCoverageOutcome.CannotEvaluate);
    public int NotAssessed => Targets.Count(t => t.Outcome == RuleCoverageOutcome.NotAssessed);
    public int Assessed => TargetCount - NotAssessed;
    public int LatestManual => Targets.Count(t => t.Latest?.Assessment.Source == TradingRuleAssessmentSource.Manual);
    public int LatestAutomatic => Targets.Count(t => t.Latest?.Assessment.Source == TradingRuleAssessmentSource.AutomaticCandidate);
    public int UnavailableTargets => Targets.Count(t => t.Latest?.TargetState == TradingRuleSourceState.Unavailable);
    public int ChangedTargets => Targets.Count(t => t.Latest?.TargetState == TradingRuleSourceState.Changed);
    public int TargetsWithChangedOrUnavailableEvidence => Targets.Count(t => t.Latest?.Evidence.Any(e => e.State != TradingRuleSourceState.Available) == true);
}

public sealed record PeriodMistakeObservation(Guid TradeId, DailyReviewReference Account,
    DailyReviewTradeInclusion Inclusion, DailyReviewAssignedMistake Assignment);
public sealed record PeriodMistakeAccountSummary(DailyReviewReference Account,
    IReadOnlyList<Guid> ClosedTradeIds, IReadOnlyList<Guid> ActivityTradeIds);
/// <summary>Recorded observations, not proven causes of losses. Frequency is distinct Trades,
/// not executions/assignments; missing observations do not establish mistake-free trading.</summary>
public sealed record PeriodMistakeSummary(DailyReviewReference Mistake,
    IReadOnlyList<PeriodMistakeObservation> Observations, IReadOnlyList<PeriodMistakeAccountSummary> Accounts)
{
    public int Frequency => Observations.Select(o => o.TradeId).Distinct().Count();
    public int ClosedFrequency => Accounts.Sum(a => a.ClosedTradeIds.Count);
    public int ActivityFrequency => Accounts.Sum(a => a.ActivityTradeIds.Count);
}

public sealed record PeriodReviewPatterns(PeriodReviewEvidence Evidence,
    IReadOnlyList<PeriodRuleSummary> Rules, IReadOnlyList<PeriodMistakeSummary> Mistakes)
{
    public const string ContractVersion = "period-patterns.v1";
    // Cannot safely assign these histories to the requested period; never included in Rules counts.
    public IReadOnlyList<TradingRuleAssessmentDetails> UnattributedAssessments => Evidence.UnattributedTradeAssessments;
}

public enum PeriodPatternsStatus { Ready, LimitExceeded }
public sealed record PeriodPatternsResult(PeriodPatternsStatus Status, PeriodReviewPatterns? Patterns, string? LimitCode);
