using System.Collections.Immutable;
using PersonalTradingJournal.Domain.Common;

namespace PersonalTradingJournal.Domain.TradingPlans;

public enum TradingPlanPreparationState { DraftOnly = 1, Ready = 2, DraftAfterReady = 3 }

public sealed class TradingPlanNotReadyException : InvalidOperationException
{
    internal TradingPlanNotReadyException(ImmutableArray<TradingPlanValidationIssue> issues)
        : base("Complete the required preparation and confirm the readiness checklist.") => Issues = issues;
    public ImmutableArray<TradingPlanValidationIssue> Issues { get; }
}

/// <summary>Owns the current working Draft and latest Ready revision. Each returned Ready revision
/// is independently immutable; the future store must append/retain it, not overwrite history.
/// This aggregate is single-owner in memory. Its Version is the existing optimistic save token.</summary>
public sealed class TradingPlan : AuditableEntity
{
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

    /// <summary>Incoming checkmarks are ignored. A real preparation change invalidates affected
    /// existing confirmations. Re-confirming is a separate explicit user operation.</summary>
    public bool EditDraft(long expectedVersion, TradingPlanContent content, DateTimeOffset changedAtUtc)
    {
        var draft = RequireDraft(expectedVersion, changedAtUtc);
        ArgumentNullException.ThrowIfNull(content);
        if (draft.Content.HasSamePreparation(content)) return false;
        var next = new TradingPlanDraft(content.WithConfirmations(draft.Content.ConfirmationsAfterEdit(content)),
            draft.DerivedFromRevision, draft.StartedAtUtc, changedAtUtc);
        CommitDraft(next, changedAtUtc);
        return true;
    }

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
        var draft = new TradingPlanDraft(source.Content.WithConfirmations(TradingPlanChecklist.None), source.Id,
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
