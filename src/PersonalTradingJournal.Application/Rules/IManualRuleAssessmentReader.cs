using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Application.Rules;

public sealed record RuleAssessmentTargetInfo(string Label, bool Available);
public sealed record RuleEvidenceOption(TradingRuleEvidenceReference Reference, string Label, string Description);

/// <summary>Bounded discovery for manual work. Discovery does not certify evidence sufficiency.</summary>
public interface IManualRuleAssessmentReader
{
    Task<RuleAssessmentTargetInfo> ReadTargetAsync(TradingRuleAssessmentTarget target, CancellationToken token = default);
    Task<TradingRulePage> ReadRulesAsync(TradingRuleAssessmentTarget target, int page = 1, int pageSize = 20, CancellationToken token = default);
    // History/evidence return up to pageSize + 1 rows; the last is lookahead only.
    Task<IReadOnlyList<TradingRuleAssessmentDetails>> ReadRuleHistoryAsync(Guid ruleId, int page = 1, int pageSize = 10, CancellationToken token = default);
    Task<IReadOnlyList<RuleEvidenceOption>> ReadEvidenceAsync(TradingRuleAssessmentTarget target,
        TradingRuleEvidenceKind kind, int page = 1, int pageSize = 20, CancellationToken token = default);
}
