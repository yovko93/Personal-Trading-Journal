using System.Collections.Immutable;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Application.TradingPlans;

public sealed record TradingPlanWriteResult(TradingPlanWriteStatus Status, TradingPlan? Plan = null,
    ImmutableArray<TradingPlanValidationIssue> Issues = default,
    ImmutableArray<TradingPlanReferenceIssue> References = default);

/// <summary>Reads throw cancellation/storage errors; malformed stored evidence is never repaired.
/// Mutations return sanitized outcomes and the committed aggregate, including its next expected token.</summary>
public interface ITradingPlanStore
{
    Task<TradingPlan?> LoadAsync(TradingPlanKey key, CancellationToken cancellationToken = default);
    Task<TradingPlanReadyRevision?> LoadReadyAsync(TradingPlanRevisionId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TradingPlanLoadedState>> HistoryAsync(TradingPlanHistoryQuery query,
        DateOnly? beforeDate = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TradingPlanReadyRevision>> ReadyHistoryAsync(Guid planId, int? beforeRevision = null,
        int limit = 20, CancellationToken cancellationToken = default);
    Task<TradingPlanWriteResult> WriteAsync(TradingPlanWriteCommand command, CancellationToken cancellationToken = default);
}

public sealed class WriteTradingPlanUseCase(ITradingPlanStore store)
{
    public Task<TradingPlanWriteResult> ExecuteAsync(TradingPlanWriteCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return store.WriteAsync(command, cancellationToken);
    }
}
