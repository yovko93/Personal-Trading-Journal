namespace PersonalTradingJournal.Domain.TradingPlans;

/// <summary>Self-contained confirmed preparation. Created only by a successful Domain Ready
/// transition. Its presence alone does not mean that it has been committed to storage.</summary>
public sealed class TradingPlanReadyRevision
{
    public static TradingPlanReadyRevision Rehydrate(TradingPlanRevisionId id, TradingPlanKey key,
        TradingPlanDraft draft, TradingPlanReferenceSnapshot references, DateTimeOffset planCreatedAtUtc,
        DateTimeOffset readyAtUtc, long sourceDraftVersion)
    {
        ArgumentNullException.ThrowIfNull(id); ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(draft); ArgumentNullException.ThrowIfNull(references);
        if (planCreatedAtUtc.Offset != TimeSpan.Zero || readyAtUtc.Offset != TimeSpan.Zero ||
            planCreatedAtUtc > draft.StartedAtUtc || draft.UpdatedAtUtc > readyAtUtc || sourceDraftVersion < (long)id.Revision * 2 - 1 ||
            (draft.DerivedFromRevision is not null && (draft.DerivedFromRevision.PlanId != id.PlanId ||
                draft.DerivedFromRevision.Revision >= id.Revision)) ||
            (id.Revision == 1 && (draft.DerivedFromRevision is not null || draft.StartedAtUtc != planCreatedAtUtc)) ||
            (id.Revision > 1 && draft.DerivedFromRevision is null) || !TradingPlanReadiness.Validate(draft.Content).IsEmpty)
            throw new ArgumentException("Invalid persisted Ready revision.");
        references.ValidateFor(key, draft.Content, readyAtUtc);
        return new(id, key, draft, references, planCreatedAtUtc, readyAtUtc, sourceDraftVersion);
    }
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
    public static TradingPlanDraft Rehydrate(TradingPlanContent content, TradingPlanRevisionId? derivedFromRevision,
        DateTimeOffset startedAtUtc, DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (startedAtUtc.Offset != TimeSpan.Zero || updatedAtUtc.Offset != TimeSpan.Zero || updatedAtUtc < startedAtUtc)
            throw new ArgumentException("Invalid persisted Draft timestamps.");
        return new(content, derivedFromRevision, startedAtUtc, updatedAtUtc);
    }
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
