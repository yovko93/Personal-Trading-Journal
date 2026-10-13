namespace PersonalTradingJournal.Application.Rules;

public enum RuleMoveStatus { Saved, Conflict, Unavailable }

/// <summary>Move one stable Rule identity before another, or to the end. Never a revision edit.</summary>
public sealed record MoveTradingRuleCommand(Guid RuleId, Guid? BeforeRuleId, long ExpectedCatalogVersion);

public interface ITradingRuleOrderStore
{
    /// <summary>Compare catalog version and move atomically. Cancellation before commit writes nothing.</summary>
    Task<RuleMoveStatus> MoveAsync(MoveTradingRuleCommand command, CancellationToken cancellationToken = default);
}
