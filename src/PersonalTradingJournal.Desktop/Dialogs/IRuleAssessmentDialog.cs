using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Desktop.Dialogs;

public interface IRuleAssessmentDialog
{
    void ShowTarget(TradingRuleAssessmentTarget target);
    void ShowRuleHistory(Guid ruleId);
}
