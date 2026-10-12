using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Domain.Tests.Rules;

public sealed class GlobalTradingRuleTests
{
    [Fact]
    public void GlobalEvidenceDescriptionMayBeAbsentWithoutInventingAssessmentEvidence()
    {
        var definition = new TradingRuleDefinition("Be patient", "Wait for confirmation");
        Assert.Empty(definition.RequiredEvidence);
        Assert.Null(definition.AutomaticCriterion);
        Assert.Equal(TradingRuleEvaluationMode.ManualOnly, definition.EvaluationMode);
        Assert.Throws<ArgumentException>(() => new TradingRuleDefinition("Legacy", "Statement", "",
            TradingRuleApplicability.Trade, TradingRuleAccountScope.AllAccounts, TradingRuleEvaluationMode.ManualOnly));
        Assert.Throws<ArgumentException>(() => new TradingRuleDefinition("Name", "Statement", new string('x', 2001)));
        var revision = TradingRuleRevision.Create(Guid.NewGuid(), definition, DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() => TradingRuleAssessment.Record(Guid.NewGuid(), revision,
            TradingRuleAssessmentTarget.ForTrade(Guid.NewGuid(), Guid.NewGuid()), TradingRuleAssessmentOutcome.NoViolation,
            TradingRuleAssessmentSource.Manual, TradingRuleAssessmentBasis.RecordedEvidence,
            TradingRuleEvidenceSufficiency.Sufficient, "No recorded supporting facts", [], null, revision.RecordedAtUtc));
    }

    [Fact]
    public void GlobalPrincipleHasNoAccountIdentityAndSupportsIndependentTradeAndDayAssessments()
    {
        var now = DateTimeOffset.UtcNow;
        var rule = TradingRuleRevision.Create(Guid.NewGuid(), new("Be patient", "Wait for confirmation", "Recorded observations"), now);
        Assert.True(rule.Definition.IsGlobal); Assert.Null(rule.Definition.AccountScope.AccountId);
        foreach (var target in new[] {
            TradingRuleAssessmentTarget.ForTrade(Guid.NewGuid(), Guid.NewGuid()),
            TradingRuleAssessmentTarget.ForNewYorkDay(new(2026, 11, 1), TradingRuleAccountScope.AllAccounts),
            TradingRuleAssessmentTarget.ForNewYorkDay(new(2026, 11, 1), TradingRuleAccountScope.ForAccount(Guid.NewGuid())) })
        {
            var assessment = TradingRuleAssessment.Record(Guid.NewGuid(), rule, target,
                TradingRuleAssessmentOutcome.CannotEvaluate, TradingRuleAssessmentSource.Manual,
                TradingRuleAssessmentBasis.RecordedEvidence, TradingRuleEvidenceSufficiency.Missing,
                "Required observation was not recorded", [], null, now);
            Assert.Equal(target, assessment.Target); Assert.Same(rule, assessment.RuleRevision);
        }
    }

    [Fact]
    public void LegacyDefinitionsRemainDistinguishableAndTypedCapabilityIsValidated()
    {
        var legacy = new TradingRuleDefinition("Legacy", "Scoped principle", "Evidence", TradingRuleApplicability.NewYorkDay,
            TradingRuleAccountScope.ForAccount(Guid.NewGuid()), TradingRuleEvaluationMode.ManualOnly);
        Assert.False(legacy.IsGlobal);
        var global = new TradingRuleDefinition(legacy.Name, legacy.Criterion, legacy.RequiredEvidence,
            new(RuleProfitBasis.StrictNet, 100, "USD", capability: RuleCheckCapability.ClosedDayLoss));
        Assert.NotEqual(legacy, global); Assert.True(global.IsGlobal);
        Assert.Equal(RuleCheckCapability.ClosedDayLoss, global.AutomaticCriterion!.Capability);
        Assert.Throws<ArgumentException>(() => new AutomaticRuleCriterion(RuleProfitBasis.Gross, 1, "USD", capability: (RuleCheckCapability)99));
    }
}
