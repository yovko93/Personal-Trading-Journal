using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Application.Rules;

public enum TradingRuleSourceState { Available = 1, Changed = 2, Unavailable = 3 }

public sealed record TradingRuleDetails(TradingRuleRevision Revision, string? AccountNameAtRevision,
    bool AccountAvailable);
public sealed record TradingRulePage(IReadOnlyList<TradingRuleDetails> Items, int TotalCount, int Page, int PageSize);
public sealed record TradingRuleEvidenceDetails(TradingRuleEvidenceReference Reference, Guid? AccountId,
    string? AccountNameAtAssessment, Guid? TradeId, DateOnly? NewYorkDate, string? Currency,
    TradingRuleSourceState State);
public sealed record TradingRuleAssessmentDetails(TradingRuleAssessment Assessment, int Sequence,
    string? AccountNameAtAssessment, TradingRuleSourceState TargetState,
    IReadOnlyList<TradingRuleEvidenceDetails> Evidence, RuleEvaluationEvidence? Evaluation = null);

/// <summary>Scope filters match definitions exactly; null means all definitions, including archived/historical scopes.</summary>
public interface ITradingRuleReader
{
    Task<TradingRulePage> ReadCurrentAsync(TradingRuleAccountScope? scope = null, bool? active = null,
        int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TradingRuleDetails>> ReadRevisionsAsync(Guid ruleId, int? beforeRevision = null,
        int limit = 20, CancellationToken cancellationToken = default);
    Task<TradingRuleAssessmentDetails?> ReadAssessmentAsync(Guid assessmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TradingRuleAssessmentDetails>> ReadAssessmentsAsync(Guid ruleId,
        TradingRuleAssessmentTarget target, int? beforeSequence = null, int limit = 20,
        CancellationToken cancellationToken = default);
}
