using System.Collections.Immutable;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Application.TradingPlans;

public enum TradingPlanWriteOperation
{
    CreateDraft = 1, SaveDraft = 2, MarkReady = 3, StartDraft = 4, CopyForward = 5,
    ConfirmChecklist = 6, RefreshReferences = 7
}
public enum TradingPlanWriteStatus
{
    Success = 1, Invalid = 2, Conflict = 3, TargetAlreadyExists = 4,
    ReferenceUnavailable = 5, ReferenceChanged = 6, Cancelled = 7, StorageFailure = 8
}

/// <summary>The loaded logical Plan and optimistic save token. The token advances on every
/// committed mutation, independently of immutable Ready revision numbering.</summary>
public sealed record TradingPlanExpectedState
{
    public TradingPlanExpectedState(Guid planId, long version)
    {
        if (planId == Guid.Empty) throw new ArgumentException("A Plan ID is required.", nameof(planId));
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        PlanId = planId;
        Version = version;
    }
    public Guid PlanId { get; }
    public long Version { get; }
}

/// <summary>Minimal transaction-time state for authoring decisions, not a persisted revision payload.</summary>
public sealed record TradingPlanLoadedState
{
    public TradingPlanLoadedState(TradingPlanKey key, TradingPlanExpectedState token, TradingPlanStatus status,
        int latestReadyRevision)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Token = token ?? throw new ArgumentNullException(nameof(token));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (latestReadyRevision < 0 || (status == TradingPlanStatus.Ready && latestReadyRevision == 0))
            throw new ArgumentOutOfRangeException(nameof(latestReadyRevision));
        Status = status;
        LatestReadyRevision = latestReadyRevision;
    }
    public TradingPlanKey Key { get; }
    public TradingPlanExpectedState Token { get; }
    public TradingPlanStatus Status { get; }
    public int LatestReadyRevision { get; }
    /// <summary>Read-only history projection; Ready labels come from frozen references,
    /// Draft labels from the current catalogs (explicitly unavailable if missing).</summary>
    public TradingPlanHistorySummary? Summary { get; init; }

    /// <summary>Expose the Domain lifecycle using the existing Application concurrency token.</summary>
    public static TradingPlanLoadedState FromPlan(TradingPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new(plan.Key, new(plan.Id, plan.Version), plan.Status, plan.LatestReadyRevision?.Id.Revision ?? 0);
    }
}

public sealed record TradingPlanHistorySummary(string AccountName, string Currency, bool AccountAvailable,
    TradingPlanSession? Session, ImmutableArray<string> Instruments, DateTimeOffset SavedAtUtc);

public sealed record TradingPlanHistoryAccount(Guid AccountId, string Name, string Currency, bool IsAvailable, bool IsActive);

/// <summary>Input to transactional use cases. Scope is captured at submission; selected
/// session and navigation never retarget a command.</summary>
public sealed record TradingPlanWriteCommand
{
    public TradingPlanWriteCommand(TradingPlanWriteOperation operation, TradingPlanKey key,
        TradingPlanContent content, TradingPlanExpectedState? expected = null,
        TradingPlanRevisionId? sourceRevision = null, TradingPlanChecklist? explicitConfirmations = null,
        string? expectedAccountCurrency = null)
    {
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Content = content ?? throw new ArgumentNullException(nameof(content));
        bool creates = operation is TradingPlanWriteOperation.CreateDraft or TradingPlanWriteOperation.CopyForward;
        if (creates != (expected is null)) throw new ArgumentException(
            "Creating requires an absent target; changing an existing Plan requires its loaded version.", nameof(expected));
        bool derives = operation is TradingPlanWriteOperation.StartDraft or TradingPlanWriteOperation.CopyForward;
        if (derives != (sourceRevision is not null)) throw new ArgumentException(
            "Choose an exact Ready revision only when starting or copying a Draft.", nameof(sourceRevision));
        if (operation == TradingPlanWriteOperation.StartDraft && sourceRevision!.PlanId != expected!.PlanId)
            throw new ArgumentException("Start Draft must refer to the same logical Plan.", nameof(sourceRevision));
        Operation = operation;
        if (explicitConfirmations.HasValue)
        {
            TradingPlanChecklistPolicy.Validate(explicitConfirmations.Value);
            if (operation != TradingPlanWriteOperation.SaveDraft)
                throw new ArgumentException("Combined checklist submission requires Save Draft.", nameof(explicitConfirmations));
        }
        ExplicitConfirmations = explicitConfirmations;
        if (expectedAccountCurrency is not null && (string.IsNullOrWhiteSpace(expectedAccountCurrency) ||
            expectedAccountCurrency.Length > 8 || expectedAccountCurrency != expectedAccountCurrency.Trim().ToUpperInvariant()))
            throw new ArgumentException("Use the reviewed canonical Account currency.", nameof(expectedAccountCurrency));
        ExpectedAccountCurrency = expectedAccountCurrency;
        Expected = expected;
        SourceRevision = sourceRevision;
    }
    public TradingPlanWriteOperation Operation { get; }
    public TradingPlanKey Key { get; }
    public TradingPlanContent Content { get; }
    public TradingPlanExpectedState? Expected { get; }
    public TradingPlanRevisionId? SourceRevision { get; }
    /// <summary>Explicit staged user confirmations, separate from Content's ignored incoming checkmarks.
    /// Apply existing EditDraft and SetConfirmations in the same transaction. Null preserves prior callers.</summary>
    public TradingPlanChecklist? ExplicitConfirmations { get; }
    /// <summary>Currency reviewed before an explicit reference refresh/derivation; no FX conversion.</summary>
    public string? ExpectedAccountCurrency { get; }

    public ImmutableArray<TradingPlanValidationIssue> ValidateContent() =>
        Operation == TradingPlanWriteOperation.MarkReady ? TradingPlanReadiness.Validate(Content) : [];

    /// <summary>Run against freshly read state under the store's writer reservation.
    /// Null permits the transition; it does not imply that references validated or a write committed.</summary>
    public TradingPlanWriteStatus? CheckTransition(TradingPlanLoadedState? current)
    {
        if (Expected is null) return current is null ? null : TradingPlanWriteStatus.TargetAlreadyExists;
        if (current is null || current.Key != Key || current.Token != Expected)
            return TradingPlanWriteStatus.Conflict;
        if (Operation == TradingPlanWriteOperation.StartDraft)
            return current.Status == TradingPlanStatus.Ready && SourceRevision!.Revision <= current.LatestReadyRevision
                ? null : TradingPlanWriteStatus.Conflict;
        // Ready content is never edited in place, and a stale double submission cannot publish twice.
        return current.Status == TradingPlanStatus.Draft ? null : TradingPlanWriteStatus.Conflict;
    }
}

public static class TradingPlanDatePolicy
{
    /// <summary>Defaults only. An explicitly selected date is never recomputed on save.</summary>
    public static TradingPlanKey Today(Guid accountId, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return new(accountId, DateOnly.FromDateTime(
            TradingTimePolicy.ConvertUtcToTradingTime(timeProvider.GetUtcNow()).DateTime));
    }

    /// <summary>A Ready timestamp strictly before entry proves only that this record existed.
    /// Equality, a later time or a missing entry cannot prove preparation was available before entry.</summary>
    public static bool WasRecordedBeforeEntry(DateTimeOffset readyAtUtc, DateTimeOffset? openedAtUtc)
    {
        if (readyAtUtc.Offset != TimeSpan.Zero || (openedAtUtc.HasValue && openedAtUtc.Value.Offset != TimeSpan.Zero))
            throw new ArgumentException("Canonical UTC instants are required.");
        return openedAtUtc.HasValue && readyAtUtc < openedAtUtc.Value;
    }
}

public enum TradingPlanReferenceKind { Account = 1, Instrument = 2, Playbook = 3, Rule = 4 }
public enum TradingPlanReferenceState { Current = 1, Changed = 2, Inactive = 3, Unavailable = 4, LegacyScoped = 5 }

/// <summary>Per-reference outcome from the committing store's fresh validation. Historical
/// readers retain captured content and report availability separately; never resolve by name.</summary>
public sealed record TradingPlanReferenceIssue(TradingPlanReferenceKind Kind, Guid SourceId,
    TradingPlanReferenceState State, string Message);

/// <summary>Logical Plan history is keyset paged by New York date descending within an exact
/// Account (one Plan per date). Ready revision history is a separate revision-descending read.
/// Status filters the current lifecycle, not the existence of older Ready revisions.</summary>
public sealed record TradingPlanHistoryQuery
{
    public TradingPlanHistoryQuery(Guid accountId, DateOnly firstDate, DateOnly lastDate,
        TradingPlanStatus? status = null, int limit = 20)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("An exact Account is required.", nameof(accountId));
        if (firstDate > lastDate) throw new ArgumentException("The date range is reversed.", nameof(lastDate));
        if (status.HasValue && !Enum.IsDefined(status.Value)) throw new ArgumentOutOfRangeException(nameof(status));
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        AccountId = accountId;
        FirstDate = firstDate;
        LastDate = lastDate;
        Status = status;
        Limit = limit;
    }
    public Guid AccountId { get; }
    public DateOnly FirstDate { get; }
    public DateOnly LastDate { get; }
    public TradingPlanStatus? Status { get; }
    public int Limit { get; }
}
