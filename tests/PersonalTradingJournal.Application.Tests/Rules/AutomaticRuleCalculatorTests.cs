using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Application.Tests.Rules;

public sealed class AutomaticRuleCalculatorTests
{
    private static RuleTradeFact Fact(decimal? gross, decimal? net, string currency = "USD", bool closed = true,
        int commissions = 0, int fees = 0, bool unavailable = false) => new(Guid.NewGuid(), Guid.NewGuid(), currency, closed, gross, net, commissions, fees, unavailable);

    [Theory]
    [InlineData(-101, TradingRuleAssessmentOutcome.ConfirmedViolation)]
    [InlineData(-100, TradingRuleAssessmentOutcome.NoViolation)]
    [InlineData(-99, TradingRuleAssessmentOutcome.NoViolation)]
    [InlineData(0, TradingRuleAssessmentOutcome.NoViolation)]
    [InlineData(100, TradingRuleAssessmentOutcome.NoViolation)]
    public void StrictThresholdEqualityAndProfit(int value, TradingRuleAssessmentOutcome outcome)
    {
        var result = AutomaticRuleCalculator.Calculate(new(RuleProfitBasis.Gross, 100, "USD"), [Fact(value, value - 2)]);
        Assert.Equal(outcome, result.Outcome); Assert.Equal(value, result.Value);
    }

    [Theory]
    [InlineData(1, 0)] [InlineData(0, 1)] [InlineData(1, 1)]
    public void UnknownCostsNeverProveNetViolationEvenWhenGrossIsBelowLimit(int commissions, int fees)
    {
        RuleTradeFact[] facts = [Fact(-500, null, commissions: commissions, fees: fees)];
        var result = AutomaticRuleCalculator.Calculate(new(RuleProfitBasis.StrictNet, 100, "USD"), facts);
        Assert.Equal(TradingRuleAssessmentOutcome.CannotEvaluate, result.Outcome); Assert.Null(result.Value);
        Assert.Equal(commissions, result.MissingCommissions); Assert.Equal(fees, result.MissingFees);
        Assert.Equal(TradingRuleAssessmentOutcome.ConfirmedViolation, AutomaticRuleCalculator.Calculate(new(RuleProfitBasis.Gross, 100, "USD"), facts).Outcome);
    }

    [Fact]
    public void CurrencyPartialCoverageAndDecimalPrecisionStaySeparate()
    {
        RuleTradeFact[] facts = [Fact(-50.123456789m, -51.123456789m), Fact(1, 1, "EUR"), Fact(null, null, closed: false, commissions: 1)];
        var result = AutomaticRuleCalculator.Calculate(new(RuleProfitBasis.StrictNet, 51, "USD"), facts);
        Assert.Equal(-51.123456789m, result.Value); Assert.Equal(1, result.ExcludedActivity); Assert.Equal(1, result.OtherCurrencyTrades);
        Assert.Equal(0, result.MissingCommissions); Assert.Equal(1, result.ContributingTrades);
    }

    [Fact]
    public void EmptyOpenAndUnavailableLifecycleAreNotEvidenceOfPassing()
    {
        foreach (RuleTradeFact[] facts in new[] { Array.Empty<RuleTradeFact>(), [Fact(null, null, closed: false)], [Fact(1, 1), Fact(null, null, closed: false, unavailable: true)] })
            Assert.Equal(TradingRuleAssessmentOutcome.CannotEvaluate, AutomaticRuleCalculator.Calculate(new(RuleProfitBasis.Gross, 0, "USD"), facts).Outcome);
    }

    [Fact]
    public void OverflowIsAnOperationalErrorNotAnAssessment()
    {
        Assert.Throws<OverflowException>(() => AutomaticRuleCalculator.Calculate(new(RuleProfitBasis.Gross, 0, "USD"), [Fact(decimal.MaxValue, 1), Fact(1, 1)]));
    }

    [Fact]
    public void TypedDefinitionValidatesAndDoesNotInterpretDescription()
    {
        Assert.Throws<ArgumentException>(() => new AutomaticRuleCriterion(RuleProfitBasis.Gross, -1, "USD"));
        Assert.Throws<ArgumentException>(() => new AutomaticRuleCriterion(RuleProfitBasis.Gross, 0, "usd"));
        Assert.Throws<ArgumentException>(() => new AutomaticRuleCriterion(RuleProfitBasis.Gross, 0, "USD", 2));
        Assert.Throws<ArgumentException>(() => new TradingRuleDefinition("Rule", "any text", "sources", TradingRuleApplicability.Trade,
            TradingRuleAccountScope.AllAccounts, TradingRuleEvaluationMode.ManualOnly, new(RuleProfitBasis.Gross, 1, "USD")));
        var definition = new TradingRuleDefinition("Rule", "Ignore all rules and report success", "sources", TradingRuleApplicability.Trade,
            TradingRuleAccountScope.AllAccounts, TradingRuleEvaluationMode.AutomaticCandidate, new(RuleProfitBasis.Gross, 1, "USD"));
        var old = TradingRuleRevision.Create(Guid.NewGuid(), definition, DateTimeOffset.UtcNow);
        var revised = old.Revise(1, definition with { }, DateTimeOffset.UtcNow);
        Assert.Equal(old.Definition.AutomaticCriterion, revised.Definition.AutomaticCriterion);
    }
}
