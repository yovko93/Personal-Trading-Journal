using System.Collections.Immutable;
using PersonalTradingJournal.Domain.Common;

namespace PersonalTradingJournal.Domain.TradingPlans;

public enum TradingPlanPreparationState { DraftOnly = 1, Ready = 2, DraftAfterReady = 3 }

public sealed class TradingPlanNotReadyException : InvalidOperationException
{
    internal TradingPlanNotReadyException(ImmutableArray<TradingPlanValidationIssue> issues)
        : base("Correct the required preparation before marking Ready.") => Issues = issues;
    public ImmutableArray<TradingPlanValidationIssue> Issues { get; }
}

/// <summary>Owns the current working Draft and latest Ready revision. Each returned Ready revision
/// is independently immutable; a store must append/retain it, not overwrite history.
/// This aggregate is single-owner in memory. Its Version is the existing optimistic save token.</summary>
public sealed class TradingPlan : AuditableEntity
{
    private TradingPlan(Guid id, TradingPlanKey key, long version, TradingPlanDraft? draft,
        TradingPlanReadyRevision? latest, DateTimeOffset created, DateTimeOffset updated) : base(id, created, updated)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (version < 1 || (draft is null && latest is null)) throw new ArgumentException("Invalid persisted Plan state.");
        if (latest is not null && (latest.Id.PlanId != id || latest.Key != key || latest.PlanCreatedAtUtc != created ||
            latest.ReadyAtUtc > updated || latest.SourceDraftVersion >= version ||
            (draft is null && (latest.ReadyAtUtc != updated || latest.SourceDraftVersion != version - 1))))
            throw new ArgumentException("Invalid persisted Ready owner or version.");
        if (draft is not null && (draft.StartedAtUtc < created || draft.UpdatedAtUtc != updated ||
            (version == 1 && draft.Content.Confirmed != TradingPlanChecklist.None) ||
            (latest is null && (draft.DerivedFromRevision is not null || draft.StartedAtUtc != created)) ||
            (latest is not null && (version - latest.SourceDraftVersion < 2 || draft.StartedAtUtc < latest.ReadyAtUtc || draft.DerivedFromRevision is null ||
                draft.DerivedFromRevision.PlanId != id || draft.DerivedFromRevision.Revision > latest.Id.Revision))))
            throw new ArgumentException("Invalid persisted Draft derivation or time.");
        Key = key; Version = version; WorkingDraft = draft; LatestReadyRevision = latest;
    }

    /// <summary>Validates stored state without performing an authoring transition or consulting catalogs.</summary>
    public static TradingPlan Rehydrate(Guid id, TradingPlanKey key, long version, TradingPlanDraft? draft,
        TradingPlanReadyRevision? latest, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc) =>
        new(id, key, version, draft, latest, createdAtUtc, updatedAtUtc);

    public TradingPlan(TradingPlanKey key, TradingPlanContent content, DateTimeOffset createdAtUtc)
        : base(createdAtUtc)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        ArgumentNullException.ThrowIfNull(content);
        WorkingDraft = new(content.WithConfirmations(TradingPlanChecklist.None), null, createdAtUtc, createdAtUtc);
        Version = 1;
    }

    public TradingPlanKey Key { get; }
    public long Version { get; private set; }
    public TradingPlanDraft? WorkingDraft { get; private set; }
    public TradingPlanReadyRevision? LatestReadyRevision { get; private set; }
    public TradingPlanStatus Status => WorkingDraft is null ? TradingPlanStatus.Ready : TradingPlanStatus.Draft;
    public TradingPlanPreparationState PreparationState => WorkingDraft is null ? TradingPlanPreparationState.Ready :
        LatestReadyRevision is null ? TradingPlanPreparationState.DraftOnly : TradingPlanPreparationState.DraftAfterReady;

    /// <summary>Incoming legacy flags never assert new confirmations. Edited Drafts use None;
    /// preserved Ready revisions are not changed.</summary>
    public bool EditDraft(long expectedVersion, TradingPlanContent content, DateTimeOffset changedAtUtc)
    {
        var draft = RequireDraft(expectedVersion, changedAtUtc);
        ArgumentNullException.ThrowIfNull(content);
        if (draft.Content.HasSamePreparation(content) && draft.Content.Confirmed == TradingPlanChecklist.None) return false;
        var next = new TradingPlanDraft(content.WithConfirmations(TradingPlanChecklist.None),
            draft.DerivedFromRevision, draft.StartedAtUtc, changedAtUtc);
        CommitDraft(next, changedAtUtc);
        return true;
    }

    /// <summary>Legacy compatibility only. Current application writes reject confirmation commands.</summary>
    public bool SetConfirmations(long expectedVersion, TradingPlanChecklist confirmed, DateTimeOffset confirmedAtUtc)
    {
        var draft = RequireDraft(expectedVersion, confirmedAtUtc);
        TradingPlanChecklistPolicy.Validate(confirmed);
        if (draft.Content.Confirmed == confirmed) return false;
        var next = new TradingPlanDraft(draft.Content.WithConfirmations(confirmed), draft.DerivedFromRevision,
            draft.StartedAtUtc, confirmedAtUtc);
        CommitDraft(next, confirmedAtUtc);
        return true;
    }

    /// <summary>Persist refreshed reference interpretation; legacy Draft flags are not active input.</summary>
    public void RefreshReferences(long expectedVersion, DateTimeOffset atUtc)
    {
        var draft = RequireDraft(expectedVersion, atUtc);
        CommitDraft(new(draft.Content.WithConfirmations(TradingPlanChecklist.None), draft.DerivedFromRevision,
            draft.StartedAtUtc, atUtc), atUtc);
    }

    public TradingPlanReadyRevision MarkReady(long expectedVersion, TradingPlanReferenceSnapshot references, DateTimeOffset readyAtUtc)
    {
        var draft = RequireDraft(expectedVersion, readyAtUtc);
        var issues = TradingPlanReadiness.Validate(draft.Content);
        if (!issues.IsEmpty) throw new TradingPlanNotReadyException(issues);
        ArgumentNullException.ThrowIfNull(references);
        references.ValidateFor(Key, draft.Content, readyAtUtc);
        long nextVersion = checked(Version + 1);
        int nextRevision = checked((LatestReadyRevision?.Id.Revision ?? 0) + 1);
        var revision = new TradingPlanReadyRevision(new(Id, nextRevision), Key, draft, references,
            CreatedAtUtc, readyAtUtc, Version);
        // All validation/allocation/overflow work finishes before any observable state change.
        SetUpdatedAtUtc(readyAtUtc);
        LatestReadyRevision = revision;
        WorkingDraft = null;
        Version = nextVersion;
        return revision;
    }

    public void StartDraft(long expectedVersion, TradingPlanReadyRevision source, DateTimeOffset startedAtUtc)
    {
        CheckMutation(expectedVersion, startedAtUtc);
        ArgumentNullException.ThrowIfNull(source);
        if (WorkingDraft is not null) throw new InvalidOperationException("A working Draft already exists.");
        if (source.Id.PlanId != Id || source.Key != Key || source.PlanCreatedAtUtc != CreatedAtUtc ||
            source.Id.Revision > LatestReadyRevision!.Id.Revision || source.ReadyAtUtc > LatestReadyRevision.ReadyAtUtc)
            throw new InvalidOperationException("Select a Ready revision belonging to this Plan.");
        var draft = new TradingPlanDraft(source.Content.WithPlanCurrency(source.References.Account.Currency).WithConfirmations(TradingPlanChecklist.None), source.Id,
            startedAtUtc, startedAtUtc);
        CommitDraft(draft, startedAtUtc);
    }

    private TradingPlanDraft RequireDraft(long expectedVersion, DateTimeOffset atUtc)
    {
        CheckMutation(expectedVersion, atUtc);
        return WorkingDraft ?? throw new InvalidOperationException("Start a working Draft before editing or marking Ready again.");
    }

    private void CheckMutation(long expectedVersion, DateTimeOffset atUtc)
    {
        if (Version != expectedVersion) throw new InvalidOperationException("The Plan changed; reload before applying this operation.");
        if (atUtc.Offset != TimeSpan.Zero || atUtc < UpdatedAtUtc)
            throw new ArgumentException("Plan operations require UTC and cannot move audit time backwards.", nameof(atUtc));
    }

    private void CommitDraft(TradingPlanDraft draft, DateTimeOffset atUtc)
    {
        long nextVersion = checked(Version + 1);
        SetUpdatedAtUtc(atUtc);
        WorkingDraft = draft;
        Version = nextVersion;
    }
}
