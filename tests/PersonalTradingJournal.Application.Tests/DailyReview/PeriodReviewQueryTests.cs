using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Application.Tests.DailyReview;

public sealed class PeriodReviewQueryTests
{
    [Theory]
    [InlineData(2026, 3, 8, "2026-03-02", "2026-03-08", 167)]
    [InlineData(2026, 11, 1, "2026-10-26", "2026-11-01", 169)]
    [InlineData(2027, 1, 1, "2026-12-28", "2027-01-03", 168)]
    public void WeeksAreMondaySundayWithRealNewYorkMidnights(int year, int month, int day, string first, string last, int hours)
    {
        var q = new PeriodReviewQuery(ReviewPeriodKind.Week, new(year, month, day));
        Assert.Equal(DateOnly.Parse(first), q.FirstDate); Assert.Equal(DateOnly.Parse(last), q.LastDate);
        Assert.Equal(hours, (q.BeforeUtc - q.FromUtc).TotalHours);
        Assert.Equal(TradingRuleAccountScope.AllAccounts, q.Scope);
    }

    [Theory]
    [InlineData(2026, 3, 31, 743)]
    [InlineData(2026, 11, 30, 721)]
    [InlineData(2024, 2, 29, 696)]
    [InlineData(2026, 12, 31, 744)]
    public void MonthsAreCalendarMonthsNotRollingDurations(int year, int month, int days, int hours)
    {
        var id = Guid.NewGuid(); var q = new PeriodReviewQuery(ReviewPeriodKind.Month, new(year, month, 15), id);
        Assert.Equal(new DateOnly(year, month, 1), q.FirstDate); Assert.Equal(days, q.LastDate.Day);
        Assert.Equal(hours, (q.BeforeUtc - q.FromUtc).TotalHours); Assert.Equal(id, q.Scope.AccountId);
    }

    [Fact]
    public void InvalidPeriodsScopesAndUnboundedLimitsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeriodReviewQuery((ReviewPeriodKind)20, new(2026, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeriodReviewQuery(ReviewPeriodKind.Week, default));
        Assert.Throws<ArgumentException>(() => new PeriodReviewQuery(ReviewPeriodKind.Week, new(2026, 1, 1), Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeriodReviewQuery(ReviewPeriodKind.Month, DateOnly.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeriodEvidenceLimits(10_001));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeriodEvidenceLimits(maxScopedAssessments: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PeriodEvidenceLimits(maxTextCharacters: 8_000_001));
    }

    [Fact]
    public void AbsenceIsNotComplianceAndCoverageUsesExactRevisionTargetAndLatestSequence()
    {
        var q = new PeriodReviewQuery(ReviewPeriodKind.Week, new(2026, 10, 8));
        var rule = TradingRuleRevision.Create(Guid.NewGuid(), new("Synthetic principle"), DateTimeOffset.UtcNow);
        var target = TradingRuleAssessmentTarget.ForNewYorkDay(q.FirstDate, TradingRuleAccountScope.AllAccounts);
        var empty = new PeriodReviewEvidence(q, [], [], [], [], [rule], [], []);
        Assert.Equal(RuleCoverageOutcome.NotAssessed, empty.Coverage(rule, target).Outcome);
        var recorded = TradingRuleAssessment.Record(Guid.NewGuid(), rule, target, TradingRuleAssessmentOutcome.CannotEvaluate,
            TradingRuleAssessmentSource.Manual, TradingRuleAssessmentBasis.RecordedEvidence, TradingRuleEvidenceSufficiency.Missing,
            "No recorded evidence", [], null, rule.RecordedAtUtc.AddMinutes(1));
        var result = empty with { Assessments = [new(recorded, 1, null, Application.Rules.TradingRuleSourceState.Available, [])] };
        Assert.Equal(RuleCoverageOutcome.CannotEvaluate, result.Coverage(rule, target).Outcome);
        Assert.Equal(RuleCoverageOutcome.NotAssessed, result.Coverage(rule.Revise(1, new("Revised"), rule.RecordedAtUtc.AddMinutes(2)), target).Outcome);
        Assert.Equal(RuleCoverageOutcome.NotAssessed, result.Coverage(rule, TradingRuleAssessmentTarget.ForNewYorkDay(q.FirstDate.AddDays(1), target.AccountScope)).Outcome);
    }
}
