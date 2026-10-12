namespace PersonalTradingJournal.Domain.Rules;

public enum TradingRuleApplicability { Trade = 1, NewYorkDay = 2 }

/// <summary>Manual-only today; automatic eligibility is a future capability, not an assessment.</summary>
public enum TradingRuleEvaluationMode { ManualOnly = 1, AutomaticCandidate = 2 }

public enum TradingRuleAccountScopeKind { AllAccounts = 1, ExactAccount = 2 }

/// <summary>All accounts is an explicit aggregate scope, never an implicit Account ID.</summary>
public sealed record TradingRuleAccountScope
{
    private TradingRuleAccountScope(TradingRuleAccountScopeKind kind, Guid? accountId)
    {
        if (!Enum.IsDefined(kind) ||
            (kind == TradingRuleAccountScopeKind.ExactAccount && (accountId is null || accountId == Guid.Empty)) ||
            (kind == TradingRuleAccountScopeKind.AllAccounts && accountId is not null))
            throw new ArgumentException("Choose All accounts or one non-empty Account ID.");
        Kind = kind;
        AccountId = accountId;
    }

    public TradingRuleAccountScopeKind Kind { get; }
    public Guid? AccountId { get; }
    public static TradingRuleAccountScope AllAccounts { get; } = new(TradingRuleAccountScopeKind.AllAccounts, null);
    public static TradingRuleAccountScope ForAccount(Guid accountId) =>
        new(TradingRuleAccountScopeKind.ExactAccount, accountId);
}

/// <summary>Human-authored criteria only. No expression is interpreted or run by M18.1.</summary>
public sealed record TradingRuleDefinition
{
    public TradingRuleDefinition(string name, string criterion, string requiredEvidence,
        TradingRuleApplicability applicability, TradingRuleAccountScope accountScope,
        TradingRuleEvaluationMode evaluationMode)
    {
        Name = Required(name, 120, nameof(name));
        Criterion = Required(criterion, 4_000, nameof(criterion));
        RequiredEvidence = Required(requiredEvidence, 2_000, nameof(requiredEvidence));
        if (!Enum.IsDefined(applicability)) throw new ArgumentOutOfRangeException(nameof(applicability));
        if (!Enum.IsDefined(evaluationMode)) throw new ArgumentOutOfRangeException(nameof(evaluationMode));
        AccountScope = accountScope ?? throw new ArgumentNullException(nameof(accountScope));
        Applicability = applicability;
        EvaluationMode = evaluationMode;
    }

    public string Name { get; }
    public string Criterion { get; }
    public string RequiredEvidence { get; }
    public TradingRuleApplicability Applicability { get; }
    public TradingRuleAccountScope AccountScope { get; }
    public TradingRuleEvaluationMode EvaluationMode { get; }

    private static string Required(string value, int limit, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > limit)
            throw new ArgumentException($"A non-blank value of at most {limit} characters is required.", parameter);
        return value.Trim();
    }
}
