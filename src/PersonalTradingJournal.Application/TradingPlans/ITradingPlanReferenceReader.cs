using System.Collections.Immutable;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Application.TradingPlans;

/// <summary>A read-only preflight at one SQLite snapshot. The committing operation must recheck.
/// CurrentAccount is informational when AccountMatchesBaseline is false; never relabel old amounts with it.</summary>
public sealed record TradingPlanReferenceCheck(TradingPlanWriteStatus Status, TradingPlanKey Key,
    TradingPlanExpectedState Expected, TradingPlanAccountSnapshot? CurrentAccount = null,
    bool AccountMatchesBaseline = false, TradingPlanReferenceSnapshot? VerifiedReferences = null,
    ImmutableArray<TradingPlanReferenceIssue> Issues = default);

public interface ITradingPlanReferenceReader
{
    Task<TradingPlanReferenceCheck> CheckAsync(TradingPlanKey key, TradingPlanExpectedState expected,
        TradingPlanContent proposedContent, CancellationToken cancellationToken = default);
}
