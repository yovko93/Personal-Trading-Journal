using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Application.PeriodReviews;

public enum ReviewPeriodKind { Week, Month }

/// <summary>Whole Monday–Sunday week or calendar month containing the supplied New York date.</summary>
public sealed record PeriodReviewQuery
{
    public PeriodReviewQuery(ReviewPeriodKind kind, DateOnly containingDate, Guid? accountId = null)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (containingDate == default) throw new ArgumentOutOfRangeException(nameof(containingDate));
        Kind = kind;
        FirstDate = kind == ReviewPeriodKind.Week
            ? containingDate.AddDays(-(((int)containingDate.DayOfWeek + 6) % 7))
            : new(containingDate.Year, containingDate.Month, 1);
        LastDate = kind == ReviewPeriodKind.Week ? FirstDate.AddDays(6) : FirstDate.AddMonths(1).AddDays(-1);
        var bounds = new DashboardAnalyticsQuery(accountId, null, FirstDate, LastDate);
        Scope = accountId is Guid id ? TradingRuleAccountScope.ForAccount(id) : TradingRuleAccountScope.AllAccounts;
        FromUtc = bounds.ClosedFromUtc!.Value; BeforeUtc = bounds.ClosedBeforeUtc!.Value;
    }
    public ReviewPeriodKind Kind { get; }
    public DateOnly FirstDate { get; }
    public DateOnly LastDate { get; }
    public TradingRuleAccountScope Scope { get; }
    public DateTimeOffset FromUtc { get; }
    public DateTimeOffset BeforeUtc { get; }
}

public enum PeriodEvidenceStatus { Ready, LimitExceeded }
public enum RuleCoverageOutcome { NotAssessed, Compliant, ConfirmedViolation, CannotEvaluate }
public sealed record PeriodRuleCoverage(RuleCoverageOutcome Outcome, TradingRuleAssessmentDetails? Latest);
public sealed record PeriodJournalRevision(Guid JournalId, DailyJournalRevision Revision);

/// <summary>A disconnected, current read snapshot, not an historical database reconstruction.</summary>
public sealed record PeriodReviewEvidence(PeriodReviewQuery Query,
    IReadOnlyList<DailyReviewTradeEvidence> ClosedTrades,
    IReadOnlyList<DailyReviewTradeEvidence> ActivityContext,
    IReadOnlyList<DailyReviewJournalEvidence> Journals,
    IReadOnlyList<PeriodJournalRevision> JournalRevisions,
    IReadOnlyList<TradingRuleRevision> ApplicableRules,
    IReadOnlyList<TradingRuleAssessmentDetails> Assessments,
    IReadOnlyList<TradingRuleAssessmentDetails> UnattributedTradeAssessments,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    TradingPlans.ReviewPlanEvidence? Preparation = null)
{
    public const string ContractVersion = "period-evidence.v1";

    /// <summary>Exact revision/target coverage, not a claim that an entire week/month was compliant.
    /// Old-revision passes do not certify a new revision; corrections use their durable sequence.</summary>
    public PeriodRuleCoverage Coverage(TradingRuleRevision rule, TradingRuleAssessmentTarget target)
    {
        var latest = Assessments.Where(a => a.Assessment.RuleRevision.RuleId == rule.RuleId &&
            a.Assessment.RuleRevision.Revision == rule.Revision && a.Assessment.Target == target)
            .OrderByDescending(a => a.Sequence).FirstOrDefault();
        return new(latest?.Assessment.Outcome switch
        {
            TradingRuleAssessmentOutcome.NoViolation => RuleCoverageOutcome.Compliant,
            TradingRuleAssessmentOutcome.ConfirmedViolation => RuleCoverageOutcome.ConfirmedViolation,
            TradingRuleAssessmentOutcome.CannotEvaluate => RuleCoverageOutcome.CannotEvaluate,
            _ => RuleCoverageOutcome.NotAssessed,
        }, latest);
    }
}

/// <summary>Limits apply before materialization; excess returns no partial evidence.</summary>
public sealed record PeriodEvidenceLimits
{
    public PeriodEvidenceLimits(int maxRowsPerSet = 10_000, int maxScopedAssessments = 5_000, long maxTextCharacters = 8_000_000)
    {
        if (maxRowsPerSet is < 1 or > 10_000 || maxScopedAssessments is < 1 or > 5_000 || maxTextCharacters is < 1 or > 8_000_000)
            throw new ArgumentOutOfRangeException(nameof(maxRowsPerSet), "Limits must be positive and no greater than the contract ceilings.");
        MaxRowsPerSet = maxRowsPerSet; MaxScopedAssessments = maxScopedAssessments; MaxTextCharacters = maxTextCharacters;
    }
    public int MaxRowsPerSet { get; }
    public int MaxScopedAssessments { get; }
    public long MaxTextCharacters { get; }
}

public sealed record PeriodEvidenceResult(PeriodEvidenceStatus Status, PeriodReviewEvidence? Evidence, string? LimitCode);
public interface IPeriodReviewEvidenceReader
{
    Task<PeriodEvidenceResult> ReadAsync(PeriodReviewQuery query, CancellationToken cancellationToken = default);
}
