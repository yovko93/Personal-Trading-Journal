using System.Text.Json;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Application.Tests.DailyReview;

public sealed class PeriodReviewPatternsCalculatorTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);
    private static readonly DateTimeOffset Audit = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Account = Guid.NewGuid(), Other = Guid.NewGuid();

    [Fact]
    public void LatestSequenceWinsWithoutLosingOriginsCorrectionsOrRuleRevision()
    {
        var rule = Rule(); var trade = Trade(); var target = Target(trade);
        var pass = Assessment(rule, target, 1, TradingRuleAssessmentOutcome.NoViolation, TradingRuleAssessmentSource.AutomaticCandidate);
        var correction = Assessment(rule, target, 2, TradingRuleAssessmentOutcome.ConfirmedViolation, prior: pass.Assessment.Id);
        var unknown = Assessment(rule, target, 3, TradingRuleAssessmentOutcome.CannotEvaluate,
            TradingRuleAssessmentSource.AutomaticCandidate, correction.Assessment.Id);
        var e = Evidence([trade], [rule], [unknown, pass, correction]);
        var summary = Calculate(e).Rules.Single(r => r.TargetKind == TradingRuleTargetKind.Trade);
        Assert.Equal(1, summary.TargetCount); Assert.Equal(1, summary.CannotEvaluate);
        Assert.Equal(0, summary.Compliant); Assert.Equal(1, summary.LatestAutomatic);
        Assert.Equal(new[] { 1, 2, 3 }, summary.Targets[0].History.Select(a => a.Sequence));
        Assert.Same(unknown, summary.Targets[0].Latest);
        var next = rule.Revise(1, new("Edited"), Audit.AddDays(1));
        var revised = Calculate(e with { ApplicableRules = [next] });
        Assert.Equal(1, revised.Rules.Single(r => r.TargetKind == TradingRuleTargetKind.Trade && r.VersionState == PeriodRuleVersionState.CurrentGlobal).NotAssessed);
        Assert.Equal(PeriodRuleDenominator.RecordedTargetsOnly, revised.Rules.Single(r => r.TargetKind == TradingRuleTargetKind.Trade && r.Rule.Revision == 1).Denominator);
        Assert.Equal(PeriodRuleVersionState.SupersededGlobal, revised.Rules.Single(r => r.Rule.Revision == 1).VersionState);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DayTargetsAreNeverMultipliedAndAggregateDoesNotMergeExactScopes(bool exact)
    {
        var rule = Rule(); var a = Trade(); var b = Trade(Other);
        var allDay = TradingRuleAssessmentTarget.ForNewYorkDay(Day, TradingRuleAccountScope.AllAccounts);
        var exactDay = TradingRuleAssessmentTarget.ForNewYorkDay(Day, TradingRuleAccountScope.ForAccount(Account));
        var e = Evidence(exact ? [a] : [a, b], [rule], exact
            ? [Assessment(rule, exactDay, 1)] : [Assessment(rule, exactDay, 1), Assessment(rule, allDay, 1)]);
        if (exact) e = e with { Query = new(ReviewPeriodKind.Week, Day, Account) };
        var result = Calculate(e);
        var days = result.Rules.Where(r => r.TargetKind == TradingRuleTargetKind.NewYorkDay).ToArray();
        var opportunities = days.Single(r => r.Denominator == PeriodRuleDenominator.CurrentAssessmentOpportunities);
        Assert.Equal(7, opportunities.TargetCount); Assert.Equal(1, opportunities.ConfirmedViolation); Assert.Equal(6, opportunities.NotAssessed);
        Assert.All(opportunities.Targets, t => Assert.False(t.HasAutomaticCapability)); // Manual-only opportunity, not auto evaluation.
        if (!exact)
        {
            var observed = days.Single(r => r.Denominator == PeriodRuleDenominator.RecordedTargetsOnly);
            Assert.Equal(Account, observed.Scope.AccountId); Assert.Equal(1, observed.TargetCount); Assert.Equal(0, observed.NotAssessed);
        }
        Assert.Equal(exact ? 1 : 2, result.Rules.Where(r => r.TargetKind == TradingRuleTargetKind.Trade).Sum(r => r.NotAssessed));
    }

    [Theory]
    [InlineData(2026, 3, 8, 31)] [InlineData(2026, 11, 1, 30)] [InlineData(2028, 2, 29, 29)]
    public void EmptyCalendarDaysAreManualOpportunitiesNotAutomaticPasses(int year, int month, int day, int days)
    {
        var result = Calculate(Evidence([], [Rule()], []) with { Query = new(ReviewPeriodKind.Month, new(year, month, day)) });
        var rule = Assert.Single(result.Rules);
        Assert.Equal(days, rule.NotAssessed); Assert.Equal(0, rule.Assessed);
        Assert.Empty(result.Mistakes);
    }

    [Fact]
    public void TypedCapabilityDoesNotExecuteChecksOrRestrictManualTargets()
    {
        var rule = TradingRuleRevision.Create(Guid.NewGuid(), new("Loss", automaticCriterion:
            new(RuleProfitBasis.StrictNet, 100, "USD", capability: RuleCheckCapability.ClosedDayLoss)), Audit);
        var result = Calculate(Evidence([Trade()], [rule], []));
        Assert.All(result.Rules.SelectMany(r => r.Targets), t => Assert.Equal(RuleCoverageOutcome.NotAssessed, t.Outcome));
        Assert.True(result.Rules.Single(r => r.TargetKind == TradingRuleTargetKind.NewYorkDay).Targets.All(t => t.HasAutomaticCapability));
        Assert.False(result.Rules.Single(r => r.TargetKind == TradingRuleTargetKind.Trade).Targets[0].HasAutomaticCapability);
    }

    [Fact]
    public void LegacyAbsentRulesUnavailableSourcesAndUnattributedHistoryRemainExplicit()
    {
        var trade = Trade(); var active = Rule(); var absent = Rule();
        var legacy = TradingRuleRevision.Create(Guid.NewGuid(), new("Legacy", "Only original scope", "Evidence",
            TradingRuleApplicability.Trade, TradingRuleAccountScope.ForAccount(Account), TradingRuleEvaluationMode.ManualOnly), Audit);
        var gone = Assessment(active, Target(trade), 1) with { TargetState = TradingRuleSourceState.Unavailable };
        var older = Assessment(absent, Target(trade), 1, TradingRuleAssessmentOutcome.NoViolation) with
        { TargetState = TradingRuleSourceState.Changed, Evidence = [new(new(TradingRuleEvidenceKind.Trade, trade.TradeId), Account, "Original", trade.TradeId, Day, "USD", TradingRuleSourceState.Unavailable)] };
        var unknown = Assessment(active, TradingRuleAssessmentTarget.ForTrade(Guid.NewGuid(), Account), 1);
        var e = Evidence([], [active], [gone, older, Assessment(legacy, Target(trade), 1)]) with { UnattributedTradeAssessments = [unknown] };
        var result = Calculate(e);
        Assert.Same(e, result.Evidence); Assert.Same(unknown, Assert.Single(result.UnattributedAssessments));
        var retained = result.Rules.Where(r => r.TargetKind == TradingRuleTargetKind.Trade).ToArray();
        Assert.All(retained, r => Assert.Equal(PeriodRuleDenominator.RecordedTargetsOnly, r.Denominator));
        Assert.Equal(1, retained.Single(r => r.Rule.RuleId == active.RuleId).UnavailableTargets);
        Assert.Equal(PeriodRuleVersionState.LegacyScoped, retained.Single(r => r.Rule.RuleId == legacy.RuleId).VersionState);
        var historical = retained.Single(r => r.Rule.RuleId == absent.RuleId);
        Assert.Equal(PeriodRuleVersionState.NotInActiveGlobalCatalog, historical.VersionState);
        Assert.Equal(1, historical.ChangedTargets); Assert.Equal(1, historical.TargetsWithChangedOrUnavailableEvidence);
        Assert.Equal(1, historical.Compliant); // Recorded result retained, not a fresh certification.
    }

    [Fact]
    public void MistakesCountDistinctTradesAndKeepClosedActivityAccountAndIdentityBoundaries()
    {
        var mistake = new DailyReviewReference(Guid.NewGuid(), "Same name", false);
        var missing = new DailyReviewReference(Guid.NewGuid(), null, null);
        var a = Trade() with { AssignedMistakes = [Assignment(mistake), Assignment(missing)] };
        var b = Trade(Other) with { AssignedMistakes = [Assignment(mistake)] };
        var open = Trade() with { Inclusion = DailyReviewTradeInclusion.OpenActivityOnDate, AssignedMistakes = [Assignment(mistake)] };
        var sameName = Trade() with { AssignedMistakes = [Assignment(mistake with { Id = Guid.NewGuid() })] };
        var e = Evidence([b, sameName, a], [], []) with { ActivityContext = [open] };
        var result = Calculate(e); var pattern = result.Mistakes.Single(m => m.Mistake.Id == mistake.Id);
        Assert.Equal(3, pattern.Frequency); Assert.Equal(2, pattern.ClosedFrequency); Assert.Equal(1, pattern.ActivityFrequency);
        Assert.Equal(2, pattern.Accounts.Count); Assert.False(pattern.Mistake.IsActive);
        Assert.Equal(new[] { a.TradeId, b.TradeId, open.TradeId }.Order(), pattern.Observations.Select(o => o.TradeId));
        Assert.Null(result.Mistakes.Single(m => m.Mistake.Id == missing.Id).Mistake.Name);
        var reordered = Calculate(e with { ClosedTrades = e.ClosedTrades.Reverse().ToArray() });
        Assert.Equal(JsonSerializer.Serialize(result.Mistakes), JsonSerializer.Serialize(reordered.Mistakes));
        Assert.Equal(3, result.Mistakes.Count);
    }

    [Fact]
    public void StableOrderBoundsCancellationAndDuplicateOrWrongScopeRejection()
    {
        var a = Trade(); var b = Trade(Other); var first = Rule(); var second = Rule();
        var e = Evidence([a, b], [first, second], [Assessment(first, Target(a), 1), Assessment(second, Target(b), 1)]);
        var before = JsonSerializer.Serialize(e);
        var one = Calculate(e); var two = Calculate(e with { ClosedTrades = [b, a], ApplicableRules = [second, first], Assessments = e.Assessments.Reverse().ToArray() });
        Assert.Equal(JsonSerializer.Serialize(one.Rules), JsonSerializer.Serialize(two.Rules));
        Assert.Equal(before, JsonSerializer.Serialize(e));
        var limited = PeriodReviewPatternsCalculator.Calculate(e, maxTargets: 17);
        Assert.Equal(PeriodPatternsStatus.LimitExceeded, limited.Status); Assert.Null(limited.Patterns);
        Assert.Equal("rule-targets", limited.LimitCode);
        Assert.Throws<OperationCanceledException>(() => PeriodReviewPatternsCalculator.Calculate(e, new(true)));
        Assert.Throws<ArgumentException>(() => Calculate(e with { ClosedTrades = [a, a] }));
        Assert.Throws<ArgumentException>(() => Calculate(e with { Query = new(ReviewPeriodKind.Week, Day, Account) }));
        Assert.Throws<ArgumentException>(() => Calculate(e with { Assessments = [e.Assessments[0], e.Assessments[0]] }));
        Assert.Empty(Calculate(Evidence([], [], [])).Rules);
    }

    [Theory]
    [InlineData(TradingRuleAssessmentOutcome.NoViolation, RuleCoverageOutcome.Compliant)]
    [InlineData(TradingRuleAssessmentOutcome.ConfirmedViolation, RuleCoverageOutcome.ConfirmedViolation)]
    [InlineData(TradingRuleAssessmentOutcome.CannotEvaluate, RuleCoverageOutcome.CannotEvaluate)]
    public void LatestRecordedOutcomeAndNotAssessedExhaustExactTargetDenominator(TradingRuleAssessmentOutcome outcome, RuleCoverageOutcome expected)
    {
        var rule = Rule(); var one = Trade(); var two = Trade();
        var summary = Calculate(Evidence([one, two], [rule], [Assessment(rule, Target(one), 1, outcome)]))
            .Rules.Single(r => r.TargetKind == TradingRuleTargetKind.Trade);
        Assert.Equal(expected, summary.Targets.Single(t => t.Target.TradeId == one.TradeId).Outcome);
        Assert.Equal(2, summary.TargetCount); Assert.Equal(1, summary.Assessed); Assert.Equal(1, summary.NotAssessed);
        Assert.Equal(summary.TargetCount, summary.Compliant + summary.ConfirmedViolation + summary.CannotEvaluate + summary.NotAssessed);
    }

    [Fact]
    public void RecordedOnlyExpansionAndInputLimitsRejectRatherThanTruncate()
    {
        var rule = Rule(); var assessment = Assessment(rule, Target(Trade()), 1);
        var result = PeriodReviewPatternsCalculator.Calculate(Evidence([], [rule], [assessment]), maxTargets: 7);
        Assert.Equal(PeriodPatternsStatus.LimitExceeded, result.Status); Assert.Null(result.Patterns);
        var oversized = Evidence([], [], Enumerable.Repeat(assessment, 5_001).ToArray());
        Assert.Equal("source-rows", PeriodReviewPatternsCalculator.Calculate(oversized).LimitCode);
        var t = Trade(); var bad = t with { AssignedMistakes = [Assignment(new(Guid.NewGuid(), "A", true))] };
        Assert.Throws<ArgumentException>(() => Calculate(Evidence([bad with { AssignedMistakes = [bad.AssignedMistakes[0], bad.AssignedMistakes[0]] }], [], [])));
    }

    private static DailyReviewAssignedMistake Assignment(DailyReviewReference m) => new(Guid.NewGuid(), m, Audit, Audit);
    private static TradingRuleRevision Rule() => TradingRuleRevision.Create(Guid.NewGuid(), new("Principle"), Audit);
    private static TradingRuleAssessmentTarget Target(DailyReviewTradeEvidence t) => TradingRuleAssessmentTarget.ForTrade(t.TradeId, t.Account.Id);
    private static PeriodReviewEvidence Evidence(DailyReviewTradeEvidence[] trades, TradingRuleRevision[] rules, TradingRuleAssessmentDetails[] assessments)
        => new(new(ReviewPeriodKind.Week, Day), trades, [], [], [], rules, assessments, []);
    private static PeriodReviewPatterns Calculate(PeriodReviewEvidence e)
    {
        var result = PeriodReviewPatternsCalculator.Calculate(e); Assert.Equal(PeriodPatternsStatus.Ready, result.Status); return result.Patterns!;
    }
    private static DailyReviewTradeEvidence Trade(Guid? account = null) => new(Guid.NewGuid(), new(account ?? Account, "Account", true),
        new(Guid.NewGuid(), "NQ", true), null, "USD", 20, Audit, Audit, DailyReviewTradeInclusion.ClosedOnDate, null, [], [], DailyReviewTradeQuality.MissingProjection);
    private static TradingRuleAssessmentDetails Assessment(TradingRuleRevision rule, TradingRuleAssessmentTarget target, int sequence,
        TradingRuleAssessmentOutcome outcome = TradingRuleAssessmentOutcome.ConfirmedViolation,
        TradingRuleAssessmentSource source = TradingRuleAssessmentSource.Manual, Guid? prior = null)
    {
        bool explicitViolation = outcome == TradingRuleAssessmentOutcome.ConfirmedViolation && source == TradingRuleAssessmentSource.Manual;
        bool sufficient = outcome != TradingRuleAssessmentOutcome.CannotEvaluate && !explicitViolation;
        var a = TradingRuleAssessment.Record(Guid.NewGuid(), rule, target, outcome, source,
            explicitViolation ? TradingRuleAssessmentBasis.UserEnteredViolation : TradingRuleAssessmentBasis.RecordedEvidence,
            sufficient ? TradingRuleEvidenceSufficiency.Sufficient : TradingRuleEvidenceSufficiency.Missing,
            "Synthetic evidence only", sufficient ? [new(TradingRuleEvidenceKind.Trade, target.TradeId ?? Guid.NewGuid())] : [], prior,
            Audit.AddDays(10)); // Identical timestamps: durable sequence, not clock order.
        return new(a, sequence, "Original Account", TradingRuleSourceState.Available, []);
    }
}
