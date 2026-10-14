using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Tests.Trades;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Domain.Rules;

namespace PersonalTradingJournal.Desktop.Tests.Rules;

public sealed class ManualAssessmentEntryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CalendarEntryCapturesExactOrExplicitAggregateDayWithoutChangingSelection(bool exact)
    {
        var dialogs = new RecordingDialog(); var date = new DateOnly(2026, 11, 1); Guid account = Guid.NewGuid();
        var accounts = new FakeTradingAccountReader();
        accounts.EnqueueResult([new(account, "Synthetic account", Domain.Accounts.TradingAccountType.Personal, null, null, "USD", 0, true)]);
        var calendar = new CalendarViewModel(new EmptyCalendar(), new Clock(), new FakeTradingCalendarDayReader(), accounts, ruleAssessments: dialogs);
        await calendar.ActivateAsync();
        if (exact) { calendar.SelectedAccount = calendar.Accounts.Single(a => a.Id == account); await calendar.LoadTask; }
        await calendar.SelectDayCommand.ExecuteAsync(CalendarDayDetailsTests.Cell(calendar, date));
        var selection = calendar.SelectedAccount; var month = calendar.SelectedMonth; var currency = calendar.SelectedCurrency;
        calendar.AssessDayRulesCommand.Execute(null);
        var target = Assert.Single(dialogs.Targets);
        Assert.Equal(date, target.NewYorkDate); Assert.Null(target.TradeId);
        Assert.Equal(exact ? TradingRuleAccountScopeKind.ExactAccount : TradingRuleAccountScopeKind.AllAccounts, target.AccountScope.Kind);
        Assert.Equal(exact ? account : null, target.AccountScope.AccountId);
        Assert.Equal(date, calendar.SelectedDate); Assert.Equal(month, calendar.SelectedMonth); Assert.Same(selection, calendar.SelectedAccount); Assert.Equal(currency, calendar.SelectedCurrency);
        calendar.Deactivate();
    }

    [Fact]
    public async Task TradeEntryUsesAuthoritativeLoadedTradeAndDoesNotDiscardUnfinishedEditor()
    {
        var row = CalendarDayDetailsTests.Row(new(2026, 9, 5), 20, 18);
        var detail = TradesViewModelTests.CreateEditableTradeDetail(row, null);
        var reader = new FakeTradeDetailReader(); reader.EnqueueResult(detail); reader.EnqueueResult(detail);
        var dialogs = new RecordingDialog();
        var vm = TradesViewModelTests.CreateViewModel(tradeDetailReader: reader, reader: TradesViewModelTests.CreateEditReferenceReader(detail), ruleAssessments: dialogs);
        vm.AssessRulesCommand.Execute(null); Assert.Empty(dialogs.Targets);
        await vm.ShowTradeDetailCommand.ExecuteAsync(row); await vm.ShowSelectedTradeEditCommand.ExecuteAsync(null);
        vm.QuantityText = "unsaved invalid quantity";
        vm.AssessRulesCommand.Execute(null);
        Assert.Equal(TradingRuleAssessmentTarget.ForTrade(detail.Id, detail.TradingAccountId), Assert.Single(dialogs.Targets));
        Assert.True(vm.IsTradeEditVisible); Assert.Equal("unsaved invalid quantity", vm.QuantityText); Assert.Same(detail, vm.SelectedTradeDetail);
    }

    internal sealed class RecordingDialog : IRuleAssessmentDialog
    {
        internal List<TradingRuleAssessmentTarget> Targets { get; } = [];
        public void ShowTarget(TradingRuleAssessmentTarget target) => Targets.Add(target);
        public void ShowRuleHistory(Guid ruleId) { }
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 11, 1, 15, 0, 0, TimeSpan.Zero); }
    private sealed class EmptyCalendar : ITradingCalendarReader
    {
        public Task<TradingCalendarMonth> GetAsync(TradingCalendarQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new TradingCalendarMonth(query.MonthStart, query.GridStart, query.GridEnd,
            Enumerable.Range(0, query.GridEnd.DayNumber - query.GridStart.DayNumber + 1).Select(i => query.GridStart.AddDays(i)).ToArray(), []));
    }
}
