using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Application.Rules;

public enum TradingRuleAuthoringAction { Create = 1, Revise = 2, Archive = 3, Reopen = 4 }
public enum TradingRuleWriteStatus { Saved = 1, Conflict = 2, MissingRule = 3, MissingEvidence = 4, InvalidState = 5 }

public sealed record TradingRuleWriteResult<T>(TradingRuleWriteStatus Status, T? Value);

public sealed record WriteTradingRuleCommand(Guid RuleId, int ExpectedRevision,
    TradingRuleAuthoringAction Action, TradingRuleDefinition? Definition);

public sealed record RecordTradingRuleAssessmentCommand(Guid RuleId, int ExpectedRuleRevision,
    Guid? ExpectedLatestAssessmentId, TradingRuleAssessmentTarget Target,
    TradingRuleAssessmentOutcome Outcome, TradingRuleAssessmentSource Source,
    TradingRuleAssessmentBasis Basis, TradingRuleEvidenceSufficiency EvidenceSufficiency,
    string Explanation, IReadOnlyList<TradingRuleEvidenceReference> EvidenceReferences);

/// <summary>
/// M18.2 must atomically compare the latest revision (0 for create) and append the candidate.
/// Deleting or renumbering revisions would invalidate historical assessments.
/// </summary>
public interface ITradingRuleStore
{
    Task<TradingRuleRevision?> GetLatestAsync(Guid ruleId, CancellationToken cancellationToken = default);
    Task<bool> TryAppendAsync(TradingRuleRevision candidate, int expectedRevision,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// M18.2 must atomically compare both the latest assessment for this rule/target and the
/// current active rule revision. A false return means a conflict; no partial write is allowed.
/// </summary>
public interface ITradingRuleAssessmentStore
{
    Task<TradingRuleAssessment?> GetLatestAsync(Guid ruleId, TradingRuleAssessmentTarget target,
        CancellationToken cancellationToken = default);
    Task<bool> TryAppendAsync(TradingRuleAssessment candidate, int expectedRuleRevision,
        Guid? expectedLatestAssessmentId, CancellationToken cancellationToken = default);
}
