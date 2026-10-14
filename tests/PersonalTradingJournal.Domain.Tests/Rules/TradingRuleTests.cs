using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Domain.Tests.Rules;

public sealed class TradingRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Account = Guid.NewGuid();
    private static readonly Guid OtherAccount = Guid.NewGuid();
    private static TradingRuleDefinition Definition(TradingRuleApplicability applicability = TradingRuleApplicability.Trade,
        TradingRuleAccountScope? scope = null) =>
        new("NQ planned entry", "Enter only after a confirmed break.",
            "Recorded execution and the user-entered plan review are required.",
            applicability, scope ?? TradingRuleAccountScope.ForAccount(Account), TradingRuleEvaluationMode.ManualOnly);
    private static TradingRuleRevision Rule(TradingRuleApplicability applicability = TradingRuleApplicability.Trade,
        TradingRuleAccountScope? scope = null) =>
        TradingRuleRevision.Create(Guid.NewGuid(), Definition(applicability, scope), Now);
    private static TradingRuleEvidenceReference TradeReference() =>
        new(TradingRuleEvidenceKind.Trade, Guid.NewGuid());

    [Theory]
    [InlineData("", "criterion", "evidence")]
    [InlineData("name", " ", "evidence")]
    [InlineData("name", "criterion", "")]
    public void DefinitionRejectsMissingRequiredMeaning(string name, string criterion, string evidence) =>
        Assert.Throws<ArgumentException>(() => new TradingRuleDefinition(name, criterion, evidence,
            TradingRuleApplicability.Trade, TradingRuleAccountScope.AllAccounts, TradingRuleEvaluationMode.ManualOnly));

    [Fact]
    public void DefinitionRejectsInvalidScopeEnumsAndExcessiveText()
    {
        Assert.Throws<ArgumentException>(() => TradingRuleAccountScope.ForAccount(Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingRuleDefinition("name", "criterion", "evidence",
            (TradingRuleApplicability)99, TradingRuleAccountScope.AllAccounts, TradingRuleEvaluationMode.ManualOnly));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingRuleDefinition("name", "criterion", "evidence",
            TradingRuleApplicability.Trade, TradingRuleAccountScope.AllAccounts, (TradingRuleEvaluationMode)99));
        Assert.Throws<ArgumentException>(() => new TradingRuleDefinition(new string('x', 121), "criterion", "evidence",
            TradingRuleApplicability.Trade, TradingRuleAccountScope.AllAccounts, TradingRuleEvaluationMode.ManualOnly));
    }

    [Fact]
    public void RevisionIdentityAndArchiveAreImmutableAndMonotonic()
    {
        var original = Rule();
        var changed = original.Revise(1, Definition(TradingRuleApplicability.NewYorkDay), Now.AddMinutes(1));
        var archived = changed.Archive(2, Now.AddMinutes(2));
        var reopened = archived.Reopen(3, Now.AddMinutes(3));
        Assert.Equal(new[] { 1, 2, 3, 4 }, new[] { original.Revision, changed.Revision, archived.Revision, reopened.Revision });
        Assert.All(new[] { changed, archived, reopened }, revision => Assert.Equal(original.RuleId, revision.RuleId));
        Assert.True(original.IsActive);
        Assert.False(archived.IsActive);
        Assert.True(reopened.IsActive);
        Assert.Equal(TradingRuleApplicability.Trade, original.Definition.Applicability);
        Assert.Equal(TradingRuleApplicability.NewYorkDay, reopened.Definition.Applicability);
        Assert.Throws<InvalidOperationException>(() => changed.Revise(1, Definition(), Now.AddMinutes(4)));
        Assert.Throws<InvalidOperationException>(() => archived.Revise(3, Definition(), Now.AddMinutes(4)));
        Assert.Throws<ArgumentException>(() => reopened.Archive(4, Now));
    }

    [Fact]
    public void AssessmentFreezesExactRevisionAndAppendsTransitions()
    {
        var firstRule = Rule();
        var target = TradingRuleAssessmentTarget.ForTrade(Guid.NewGuid(), Account);
        var first = TradingRuleAssessment.Record(Guid.NewGuid(), firstRule, target,
            TradingRuleAssessmentOutcome.CannotEvaluate, TradingRuleAssessmentSource.Manual,
            TradingRuleAssessmentBasis.RecordedEvidence, TradingRuleEvidenceSufficiency.Missing,
            "The execution record is absent.", [], null, Now.AddMinutes(1));
        var laterRule = firstRule.Revise(1, Definition(), Now.AddMinutes(2));
        var second = TradingRuleAssessment.Record(Guid.NewGuid(), laterRule, target,
            TradingRuleAssessmentOutcome.NoViolation, TradingRuleAssessmentSource.Manual,
            TradingRuleAssessmentBasis.RecordedEvidence, TradingRuleEvidenceSufficiency.Sufficient,
            "Recorded execution satisfies the stated condition.", [TradeReference()], first.Id, Now.AddMinutes(3));
        Assert.Same(firstRule, first.RuleRevision);
        Assert.Equal(1, first.RuleRevision.Revision);
        Assert.Equal(2, second.RuleRevision.Revision);
        Assert.Equal(first.Id, second.SupersedesAssessmentId);
        Assert.Equal(TradingRuleAssessmentOutcome.CannotEvaluate, first.Outcome);
        Assert.Equal(TradingRuleAssessmentOutcome.NoViolation, second.Outcome);
        Assert.Throws<ArgumentOutOfRangeException>(() => TradingRuleAssessment.Record(Guid.NewGuid(), laterRule, target,
            TradingRuleAssessmentOutcome.NotAssessed, TradingRuleAssessmentSource.Manual,
            TradingRuleAssessmentBasis.RecordedEvidence, TradingRuleEvidenceSufficiency.Missing,
            "No record", [], null, Now.AddMinutes(3)));
    }

    [Fact]
    public void ExactScopeAndApplicabilityCannotLeakAcrossAccountsOrTargets()
    {
        var exact = Rule();
        Assert.Throws<ArgumentException>(() => Assess(exact,
            TradingRuleAssessmentTarget.ForTrade(Guid.NewGuid(), OtherAccount)));
        Assert.Throws<ArgumentException>(() => Assess(exact,
            TradingRuleAssessmentTarget.ForNewYorkDay(new DateOnly(2026, 10, 12), TradingRuleAccountScope.ForAccount(Account))));
        var aggregate = Rule(TradingRuleApplicability.NewYorkDay, TradingRuleAccountScope.AllAccounts);
        var day = TradingRuleAssessmentTarget.ForNewYorkDay(new DateOnly(2026, 10, 12), TradingRuleAccountScope.AllAccounts);
        Assert.Equal(day.NewYorkDate, Assess(aggregate, day).Target.NewYorkDate);
        Assert.Equal(TradingRuleAccountScopeKind.AllAccounts, day.AccountScope.Kind);
        Assert.Throws<ArgumentException>(() => TradingRuleAssessmentTarget.ForNewYorkDay(default, TradingRuleAccountScope.AllAccounts));
    }

    [Fact]
    public void IncompleteEvidenceCannotBeARecordedPassOrViolation()
    {
        var rule = Rule();
        var target = TradingRuleAssessmentTarget.ForTrade(Guid.NewGuid(), Account);
        foreach (var outcome in new[] { TradingRuleAssessmentOutcome.NoViolation, TradingRuleAssessmentOutcome.ConfirmedViolation })
            Assert.Throws<ArgumentException>(() => TradingRuleAssessment.Record(Guid.NewGuid(), rule, target,
                outcome, TradingRuleAssessmentSource.Manual, TradingRuleAssessmentBasis.RecordedEvidence,
                TradingRuleEvidenceSufficiency.Missing, "Unknown commission or missing execution.",
                [TradeReference()], null, Now.AddMinutes(1)));
        Assert.Throws<ArgumentException>(() => TradingRuleAssessment.Record(Guid.NewGuid(), rule, target,
            TradingRuleAssessmentOutcome.NoViolation, TradingRuleAssessmentSource.Manual,
            TradingRuleAssessmentBasis.RecordedEvidence, TradingRuleEvidenceSufficiency.Sufficient,
            "A Setup link alone is not adherence evidence.",
            [new(TradingRuleEvidenceKind.PlaybookCapture, Guid.NewGuid())], null, Now.AddMinutes(1)));
    }

    [Fact]
    public void ManualUserEnteredViolationCanBeRecordedDespiteMissingFactsButAutomaticCannot()
    {
        var rule = Rule();
        var target = TradingRuleAssessmentTarget.ForTrade(Guid.NewGuid(), Account);
        var assessment = TradingRuleAssessment.Record(Guid.NewGuid(), rule, target,
            TradingRuleAssessmentOutcome.ConfirmedViolation, TradingRuleAssessmentSource.Manual,
            TradingRuleAssessmentBasis.UserEnteredViolation, TradingRuleEvidenceSufficiency.Missing,
            "I entered before confirmation.", [], null, Now.AddMinutes(1));
        Assert.Empty(assessment.EvidenceReferences);
        Assert.Throws<ArgumentException>(() => TradingRuleAssessment.Record(Guid.NewGuid(), rule, target,
            TradingRuleAssessmentOutcome.ConfirmedViolation, TradingRuleAssessmentSource.AutomaticCandidate,
            TradingRuleAssessmentBasis.UserEnteredViolation, TradingRuleEvidenceSufficiency.Missing,
            "Not user-entered", [], null, Now.AddMinutes(1)));
        Assert.Throws<ArgumentException>(() => TradingRuleAssessment.Record(Guid.NewGuid(), rule, target,
            TradingRuleAssessmentOutcome.CannotEvaluate, TradingRuleAssessmentSource.Manual,
            TradingRuleAssessmentBasis.RecordedEvidence, TradingRuleEvidenceSufficiency.Sufficient,
            "Sufficient is not missing", [], null, Now.AddMinutes(1)));
    }

    private static TradingRuleAssessment Assess(TradingRuleRevision rule, TradingRuleAssessmentTarget target) =>
        TradingRuleAssessment.Record(Guid.NewGuid(), rule, target,
            TradingRuleAssessmentOutcome.CannotEvaluate, TradingRuleAssessmentSource.Manual,
            TradingRuleAssessmentBasis.RecordedEvidence, TradingRuleEvidenceSufficiency.Ambiguous,
            "Recorded timing is ambiguous.", [], null, Now.AddMinutes(1));
}
