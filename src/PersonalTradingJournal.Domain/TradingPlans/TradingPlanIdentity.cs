namespace PersonalTradingJournal.Domain.TradingPlans;

public enum TradingPlanScope { AllAccounts = 1, Account = 2 }

/// <summary>One shared or exact-Account plan per New York calendar date.
/// Session and Instrument are deliberately excluded from this key; null is shared applicability, not a fabricated Account.</summary>
public sealed record TradingPlanKey
{
    public TradingPlanKey(Guid? accountId, DateOnly newYorkDate)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("Select an exact Account.", nameof(accountId));
        AccountId = accountId;
        NewYorkDate = newYorkDate;
    }

    public Guid? AccountId { get; }
    [System.Text.Json.Serialization.JsonIgnore]
    public TradingPlanScope Scope => AccountId.HasValue ? TradingPlanScope.Account : TradingPlanScope.AllAccounts;
    public static TradingPlanKey AllAccounts(DateOnly date) => new(null, date);
    public DateOnly NewYorkDate { get; }
}

/// <summary>A permanent source address. Draft save versions are not Ready revision numbers.</summary>
public sealed record TradingPlanRevisionId
{
    public TradingPlanRevisionId(Guid planId, int revision)
    {
        if (planId == Guid.Empty) throw new ArgumentException("A Plan ID is required.", nameof(planId));
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
        PlanId = planId;
        Revision = revision;
    }

    public Guid PlanId { get; }
    public int Revision { get; }
}

// Labels describe manual preparation, never execution/session attribution rules.
public enum TradingPlanSession { Asia = 1, London = 2, NewYork = 3, Other = 4 }
public enum TradingPlanStatus { Draft = 1, Ready = 2 }
public enum TradingPlanIntent { Opportunities = 1, StandAside = 2 }
public enum TradingPlanBias { Long = 1, Short = 2, Neutral = 3, Conditional = 4 }
public enum TradingPlanScenarioRole { Primary = 1, Alternative = 2 }

[Flags]
public enum TradingPlanChecklist
{
    None = 0,
    ContextAndLevels = 1,
    ScenariosOrStandAside = 2,
    RiskLimits = 4,
    News = 8,
    MindsetAndFocus = 16,
    All = ContextAndLevels | ScenariosOrStandAside | RiskLimits | News | MindsetAndFocus
}

public enum TradingPlanEditArea
{
    Session = 1, Focus = 2, Instruments = 3, ContextAndLevels = 4,
    ScenariosAndSetups = 5, StandAside = 6, RiskLimitsAndRules = 7, News = 8
}

public static class TradingPlanChecklistPolicy
{
    /// <summary>Apply after an actual semantic edit, not focus, navigation or an unchanged assignment.
    /// No operation automatically confirms a check.</summary>
    public static TradingPlanChecklist AfterEdit(TradingPlanChecklist confirmed, TradingPlanEditArea area)
    {
        Validate(confirmed);
        var invalidated = area switch
        {
            TradingPlanEditArea.Session or TradingPlanEditArea.Instruments => TradingPlanChecklist.All,
            TradingPlanEditArea.Focus => TradingPlanChecklist.MindsetAndFocus,
            TradingPlanEditArea.ContextAndLevels => TradingPlanChecklist.ContextAndLevels | TradingPlanChecklist.ScenariosOrStandAside,
            TradingPlanEditArea.ScenariosAndSetups or TradingPlanEditArea.StandAside => TradingPlanChecklist.ScenariosOrStandAside,
            TradingPlanEditArea.RiskLimitsAndRules => TradingPlanChecklist.RiskLimits,
            TradingPlanEditArea.News => TradingPlanChecklist.News | TradingPlanChecklist.ScenariosOrStandAside,
            _ => throw new ArgumentOutOfRangeException(nameof(area))
        };
        return confirmed & ~invalidated;
    }

    public static int CompletedCount(TradingPlanChecklist confirmed)
    {
        Validate(confirmed);
        return System.Numerics.BitOperations.PopCount((uint)confirmed);
    }

    public static void Validate(TradingPlanChecklist confirmed)
    {
        if ((confirmed & ~TradingPlanChecklist.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(confirmed), "Unknown readiness confirmation.");
    }
}
