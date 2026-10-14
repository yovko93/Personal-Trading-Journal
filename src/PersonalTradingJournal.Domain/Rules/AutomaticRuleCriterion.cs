namespace PersonalTradingJournal.Domain.Rules;

public enum RuleProfitBasis { Gross = 1, StrictNet = 2 }
public enum RuleCheckCapability { ClosedTradeLoss = 1, ClosedDayLoss = 2 }

/// <summary>Version 1: loss strictly greater than the nonnegative limit, in one currency.
/// Capability selects a closed-Trade or NY closed-day metric, never ownership of the global principle.
/// Legacy revisions retain their original applicability; they are not eligible for new checks.</summary>
public sealed record AutomaticRuleCriterion
{
    public AutomaticRuleCriterion(RuleProfitBasis basis, decimal maximumLoss, string currency, int version = 1,
        RuleCheckCapability capability = RuleCheckCapability.ClosedTradeLoss)
    {
        if (version != 1 || !Enum.IsDefined(basis) || !Enum.IsDefined(capability) || maximumLoss < 0)
            throw new ArgumentException("A supported basis and nonnegative loss limit are required.");
        if (currency is null || currency.Length != 3 || currency.Any(c => c is < 'A' or > 'Z'))
            throw new ArgumentException("Use a three-letter uppercase currency.", nameof(currency));
        Version = version; Basis = basis; MaximumLoss = maximumLoss; Currency = currency; Capability = capability;
    }
    public int Version { get; }
    public RuleProfitBasis Basis { get; }
    public decimal MaximumLoss { get; }
    public string Currency { get; }
    public RuleCheckCapability Capability { get; }
}
