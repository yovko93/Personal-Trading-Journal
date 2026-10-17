namespace PersonalTradingJournal.Domain.TradingPlans;

/// <summary>Self-contained confirmed preparation. Created only by a successful Domain Ready
/// transition. Its presence alone does not mean that it has been committed to storage.</summary>
public sealed class TradingPlanReadyRevision
{
    internal TradingPlanReadyRevision(TradingPlanRevisionId id, TradingPlanKey key,
        TradingPlanDraft draft, TradingPlanReferenceSnapshot references, DateTimeOffset planCreatedAtUtc,
        DateTimeOffset readyAtUtc, long sourceDraftVersion)
    {
        Id = id; Key = key; Content = draft.Content; References = references;
        PlanCreatedAtUtc = planCreatedAtUtc; DraftStartedAtUtc = draft.StartedAtUtc;
        DraftUpdatedAtUtc = draft.UpdatedAtUtc; ReadyAtUtc = readyAtUtc;
        SourceDraftVersion = sourceDraftVersion; DerivedFromRevision = draft.DerivedFromRevision;
    }
    public const int ContractVersion = 1;
    public TradingPlanRevisionId Id { get; }
    public TradingPlanKey Key { get; }
    public TradingPlanContent Content { get; }
    public TradingPlanReferenceSnapshot References { get; }
    public DateTimeOffset PlanCreatedAtUtc { get; }
    public DateTimeOffset DraftStartedAtUtc { get; }
    public DateTimeOffset DraftUpdatedAtUtc { get; }
    public DateTimeOffset ReadyAtUtc { get; }
    public long SourceDraftVersion { get; }
    public TradingPlanRevisionId? DerivedFromRevision { get; }
}

/// <summary>An immutable value replaced when the aggregate's working preparation changes.</summary>
public sealed class TradingPlanDraft
{
    internal TradingPlanDraft(TradingPlanContent content, TradingPlanRevisionId? derivedFromRevision,
        DateTimeOffset startedAtUtc, DateTimeOffset updatedAtUtc)
    {
        Content = content; DerivedFromRevision = derivedFromRevision;
        StartedAtUtc = startedAtUtc; UpdatedAtUtc = updatedAtUtc;
    }
    public TradingPlanContent Content { get; }
    public TradingPlanRevisionId? DerivedFromRevision { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; }
}
