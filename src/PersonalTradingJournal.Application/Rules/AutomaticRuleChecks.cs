using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Application.Rules;

public sealed record RuleTradeFact(Guid TradeId, Guid AccountId, string Currency, bool Closed,
    decimal? Gross, decimal? Net, int MissingCommissions, int MissingFees, bool LifecycleUnavailable);
public sealed record RuleCalculation(TradingRuleAssessmentOutcome Outcome, string Explanation,
    decimal? Value, int ContributingTrades, int ExcludedActivity, int OtherCurrencyTrades,
    int MissingCommissions, int MissingFees);
public sealed record RuleEvaluationEvidence(string EvaluatorVersion, AutomaticRuleCriterion Criterion,
    RuleCalculation Calculation, IReadOnlyList<RuleTradeFact> Facts, IReadOnlyList<Guid> ExecutionIds);

/// <summary>No IO or free-text interpretation. Values use Domain Trade economics, without rounding.</summary>
public static class AutomaticRuleCalculator
{
    public const string Version = "closed-loss/1";
    public static RuleCalculation Calculate(AutomaticRuleCriterion criterion, IReadOnlyList<RuleTradeFact> facts)
    {
        var matching = facts.Where(t => t.Currency == criterion.Currency).ToArray();
        var closed = matching.Where(t => t.Closed).ToArray();
        int commissions = closed.Sum(t => t.MissingCommissions), fees = closed.Sum(t => t.MissingFees);
        int excluded = matching.Count(t => !t.Closed), other = facts.Count - matching.Length;
        string? gap = matching.Any(t => t.LifecycleUnavailable) ? "Recorded activity has an unavailable lifecycle; its realized result cannot be established." :
            closed.Length == 0 ? "No closed Trades in the criterion currency; no realized result to assess." :
            closed.Any(t => t.Gross is null) ? "Closed Trade Gross is unavailable." :
            criterion.Basis == RuleProfitBasis.StrictNet && (commissions > 0 || fees > 0 || closed.Any(t => t.Net is null))
                ? $"Strict Net unavailable: {commissions} executions lack commissions and {fees} lack fees; null costs are not zero." : null;
        if (gap is not null) return new(TradingRuleAssessmentOutcome.CannotEvaluate, gap, null, closed.Length, excluded, other, commissions, fees);
        decimal sum = 0;
        foreach (var trade in closed) sum = checked(sum + (criterion.Basis == RuleProfitBasis.Gross ? trade.Gross!.Value : trade.Net!.Value));
        bool violation = sum < -criterion.MaximumLoss;
        string explanation = FormattableString.Invariant($"Calculated {criterion.Basis} closed P&L {sum} {criterion.Currency}; violation only below {-criterion.MaximumLoss}. {closed.Length} contributing Trades, {excluded} open/partial activities excluded, {other} other-currency Trades excluded. This is not a plan-adherence assessment.");
        return new(violation ? TradingRuleAssessmentOutcome.ConfirmedViolation : TradingRuleAssessmentOutcome.NoViolation,
            explanation, sum, closed.Length, excluded, other, commissions, fees);
    }
}

public enum RuleCheckStatus { Completed, Conflict, UnavailableTarget, LimitExceeded }
public sealed record RuleCheckRun(RuleCheckStatus Status, int Recorded, int Unchanged, IReadOnlyList<Guid> AssessmentIds);
public interface IAutomaticRuleChecks
{
    /// <summary>Explicit invocation only; bounded, atomic batch. Cancellation before commit writes nothing.</summary>
    Task<RuleCheckRun> RunAsync(TradingRuleAssessmentTarget target, CancellationToken cancellationToken = default);
}
