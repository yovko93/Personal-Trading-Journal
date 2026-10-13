namespace PersonalTradingJournal.Domain.Rules;

public enum TradingRuleApplicability { Trade = 1, NewYorkDay = 2 }

/// <summary>Eligibility alone never runs a check; a supported typed criterion is also required.</summary>
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

/// <summary>Human text is never executable. Optional typed criteria are explicitly versioned.</summary>
public sealed record TradingRuleDefinition
{
    /// <summary>A global principle. Targets and Accounts belong to assessments, not this definition.</summary>
    public TradingRuleDefinition(string name, string criterion = "", string requiredEvidence = "",
        AutomaticRuleCriterion? automaticCriterion = null)
        : this(name, criterion, requiredEvidence, TradingRuleApplicability.Trade,
            TradingRuleAccountScope.AllAccounts, automaticCriterion is null
                ? TradingRuleEvaluationMode.ManualOnly : TradingRuleEvaluationMode.AutomaticCandidate, automaticCriterion, true)
    { }

    // Compatibility constructor: retains the exact meaning of pre-global immutable revisions.
    public TradingRuleDefinition(string name, string criterion, string requiredEvidence,
        TradingRuleApplicability applicability, TradingRuleAccountScope accountScope,
        TradingRuleEvaluationMode evaluationMode, AutomaticRuleCriterion? automaticCriterion = null)
        : this(name, criterion, requiredEvidence, applicability, accountScope, evaluationMode, automaticCriterion, false)
    { }

    private TradingRuleDefinition(string name, string criterion, string requiredEvidence,
        TradingRuleApplicability applicability, TradingRuleAccountScope accountScope,
        TradingRuleEvaluationMode evaluationMode, AutomaticRuleCriterion? automaticCriterion, bool isGlobal)
    {
        Name = Required(name, 120, nameof(name));
        // Optional supporting prose for global principles. Legacy definitions retain their original contract.
        Criterion = isGlobal && criterion is not null && criterion.Length <= 4_000
            ? criterion.Trim() : Required(criterion!, 4_000, nameof(criterion));
        // Empty means no authored evidence description, not fabricated evidence or a passed check.
        if (isGlobal && requiredEvidence is not null && requiredEvidence.Length <= 2_000)
            RequiredEvidence = requiredEvidence.Trim();
        else
            RequiredEvidence = Required(requiredEvidence!, 2_000, nameof(requiredEvidence));
        IsGlobal = isGlobal;
        if (!Enum.IsDefined(applicability)) throw new ArgumentOutOfRangeException(nameof(applicability));
        if (!Enum.IsDefined(evaluationMode)) throw new ArgumentOutOfRangeException(nameof(evaluationMode));
        AccountScope = accountScope ?? throw new ArgumentNullException(nameof(accountScope));
        Applicability = applicability;
        EvaluationMode = evaluationMode;
        if (automaticCriterion is not null && evaluationMode != TradingRuleEvaluationMode.AutomaticCandidate)
            throw new ArgumentException("Typed checks require automatic eligibility.", nameof(automaticCriterion));
        AutomaticCriterion = automaticCriterion;
    }

    public string Name { get; }
    public bool IsGlobal { get; }
    public string Criterion { get; }
    public string RequiredEvidence { get; }
    public TradingRuleApplicability Applicability { get; }
    public TradingRuleAccountScope AccountScope { get; }
    public TradingRuleEvaluationMode EvaluationMode { get; }
    public AutomaticRuleCriterion? AutomaticCriterion { get; }

    private static string Required(string value, int limit, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > limit)
            throw new ArgumentException($"A non-blank value of at most {limit} characters is required.", parameter);
        return value.Trim();
    }
}
