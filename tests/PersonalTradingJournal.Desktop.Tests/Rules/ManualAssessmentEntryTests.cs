using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;

namespace PersonalTradingJournal.Desktop.Tests.Rules;

public sealed class ManualAssessmentEntryTests
{
    [Fact]
    public void TradeAndCalendarAssessmentEntriesAreRetiredWhileHistoryReadersRemain()
    {
        Assert.Null(typeof(PersonalTradingJournal.Desktop.ViewModels.Trades.TradesViewModel).GetProperty("AssessRulesCommand"));
        Assert.Null(typeof(CalendarViewModel).GetProperty("AssessDayRulesCommand"));
        Assert.NotNull(typeof(IRuleAssessmentDialog).GetMethod("ShowRuleHistory"));
    }
}
