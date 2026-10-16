using PersonalTradingJournal.Application.DailyReview;

namespace PersonalTradingJournal.Application.PeriodReviews;

public enum PeriodSourceConsistency { IndependentReads, SingleReadTransaction }
public sealed record PeriodComparisonEvidence(PeriodReviewEvidence Previous, PeriodReviewEvidence Current,
    PeriodSourceConsistency Consistency = PeriodSourceConsistency.IndependentReads)
{
    public static PeriodReviewQuery PreviousQuery(PeriodReviewQuery current) =>
        new(current.Kind, current.FirstDate.AddDays(-1), current.Scope.AccountId);
}
public sealed record PeriodComparisonEvidenceResult(PeriodEvidenceStatus Status, PeriodComparisonEvidence? Evidence, string? LimitCode);
public interface IPeriodComparisonEvidenceReader
{
    Task<PeriodComparisonEvidenceResult> ReadComparisonAsync(PeriodReviewQuery query, CancellationToken cancellationToken = default);
}

public enum PeriodChangeStatus { Comparable, IndependentSnapshots, MissingPopulation, IncompleteCoverage }
public enum PeriodPercentStatus { Defined, ChangeUnavailable, ZeroBaseline, NegativeBaseline, AccountOrCurrencyPopulationChanged }
/// <summary>Current minus previous. Percent = change / positive previous * 100, never rounded.
/// Zero/negative baselines have no percentage. No known-subset money delta is exposed.</summary>
public sealed record PeriodValueChange(decimal? Previous, decimal? Current, decimal? Absolute,
    decimal? Percent, PeriodChangeStatus Status, PeriodPercentStatus PercentStatus);

/// <summary>Original groups retain coverage, source IDs, outcomes, costs and excluded activity.
/// Null group means no recorded population, not a zero-money result.</summary>
public sealed record PeriodGroupComparison(string Currency, Guid? AccountId,
    PeriodGroupStatistics? Previous, PeriodGroupStatistics? Current,
    IReadOnlyList<Guid> PreviousAccounts, IReadOnlyList<Guid> CurrentAccounts,
    PeriodValueChange ClosedTrades, PeriodValueChange ActiveDays, PeriodValueChange OpenActivity,
    PeriodValueChange UnavailableLifecycle, PeriodValueChange MissingCommissionTrades,
    PeriodValueChange MissingFeeTrades, PeriodValueChange MissingCostTrades,
    PeriodValueChange Gross, PeriodValueChange Net);

public enum PeriodBreakdownKind { Instrument, Setup, Session, RecordedMistake }
public enum PeriodBreakdownContext { CatalogIdentity, FrozenRevision, LegacyClassification, NoAssociation, UnknownSession, NoRecordedMistake }
/// <summary>Identity is never a label. Frozen Setup revisions do not merge with current classifications.
/// Names/availability and full captures are retained in each side's source evidence.</summary>
public sealed record PeriodBreakdownKey(PeriodBreakdownKind Kind, PeriodBreakdownContext Context, Guid? SourceId = null, int? Revision = null);
public sealed record PeriodBreakdownComparison(PeriodBreakdownKey Key, DailyReviewTradeSet PreviousTrades,
    DailyReviewTradeSet CurrentTrades, IReadOnlyList<PeriodGroupComparison> Groups);

public enum PeriodRuleComparisonStatus { ComparableCounts, IndependentSnapshots, MissingCohort, DifferentTargetCount }
/// <summary>No improvement score or percentage. Only exact revision, version state, target kind,
/// scope and denominator cohorts are paired; correction histories and source states stay attached.</summary>
public sealed record PeriodRuleComparison(PeriodRuleSummary? Previous, PeriodRuleSummary? Current,
    PeriodRuleComparisonStatus Status, int? AssessedChange, int? CompliantChange,
    int? ViolationChange, int? CannotEvaluateChange, int? NotAssessedChange);

public sealed record PeriodReviewComparison(PeriodComparisonEvidence Evidence,
    PeriodReviewStatistics Previous, PeriodReviewStatistics Current,
    PeriodReviewPatterns PreviousPatterns, PeriodReviewPatterns CurrentPatterns,
    PeriodValueChange ClosedTrades, PeriodValueChange ActiveDays,
    IReadOnlyList<PeriodGroupComparison> Groups, IReadOnlyList<PeriodBreakdownComparison> Breakdowns,
    IReadOnlyList<PeriodRuleComparison> Rules)
{
    public const string ContractVersion = "period-comparison.v1";
}
public sealed record PeriodComparisonLimits
{
    public PeriodComparisonLimits(int maxBreakdownGroups = 2_000, int maxMemberships = 80_000, int maxRuleTargetsPerPeriod = 100_000)
    {
        if (maxBreakdownGroups is < 1 or > 2_000 || maxMemberships is < 1 or > 80_000 || maxRuleTargetsPerPeriod is < 1 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(maxBreakdownGroups));
        MaxBreakdownGroups = maxBreakdownGroups; MaxMemberships = maxMemberships; MaxRuleTargetsPerPeriod = maxRuleTargetsPerPeriod;
    }
    public int MaxBreakdownGroups { get; }
    public int MaxMemberships { get; }
    public int MaxRuleTargetsPerPeriod { get; }
}
public sealed record PeriodComparisonResult(PeriodEvidenceStatus Status, PeriodReviewComparison? Comparison, string? LimitCode);
