namespace PersonalTradingJournal.Domain.Rules;

public enum RuleProfitBasis { Gross = 1, StrictNet = 2 }

/// <summary>Version 1: loss strictly greater than the nonnegative limit, in one currency.
/// Applicability on the containing revision selects a closed Trade or a NY closed-day sum.</summary>
public sealed record AutomaticRuleCriterion
{
    public AutomaticRuleCriterion(RuleProfitBasis basis, decimal maximumLoss, string currency, int version = 1)
    {
        if (version != 1 || !Enum.IsDefined(basis) || maximumLoss < 0)
            throw new ArgumentException("A supported basis and nonnegative loss limit are required.");
        if (currency is null || currency.Length != 3 || currency.Any(c => c is < 'A' or > 'Z'))
            throw new ArgumentException("Use a three-letter uppercase currency.", nameof(currency));
        Version = version; Basis = basis; MaximumLoss = maximumLoss; Currency = currency;
    }
    public int Version { get; }
    public RuleProfitBasis Basis { get; }
    public decimal MaximumLoss { get; }
    public string Currency { get; }
}
