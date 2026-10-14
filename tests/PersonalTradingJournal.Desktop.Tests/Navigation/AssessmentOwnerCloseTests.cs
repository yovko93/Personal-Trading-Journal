using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.Rules;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Tests.Trades;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.ViewModels.Rules;
using PersonalTradingJournal.Desktop.Views.Calendar;
using PersonalTradingJournal.Desktop.Views.Rules;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    [Theory]
    [InlineData("main", "journal")]
    [InlineData("day", "journal")]
    [InlineData("main", "trade")]
    [InlineData("day", "trade")]
    [InlineData("main", "assessment")]
    [InlineData("day", "assessment")]
    [InlineData("main", "subscriber")]
    [InlineData("day", "subscriber")]
    [InlineData("assessment", "assessment")]
    [InlineData("command", "assessment")]
    public async Task CalendarOwnerCloseWaitsForEveryGuardBeforeEndingAssessmentModal(string target, string veto)
    {
        if (Environment.GetEnvironmentVariable(OwnerCloseHostVariable) != "1") { await OwnerCloseHost.Value; return; }
        var date = new DateOnly(2026, 9, 5);
        var row = CalendarDayDetailsTests.Row(date, 10, 9);
        var detail = TradesViewModelTests.CreateEditableTradeDetail(row, null);
        var details = new FakeTradeDetailReader(); details.EnqueueResult(detail); details.EnqueueResult(detail);
        var editor = TradesViewModelTests.CreateViewModel(tradeDetailReader: details,
            reader: TradesViewModelTests.CreateEditReferenceReader(detail));
        var journalDialogs = new CountingOwnerCloseDialogs { Accept = veto != "journal" };
        var repository = new FakeDailyJournalRepository();
        var fixture = CreateFixture(calendarEditor: editor,
            calendarDayReader: new FakeTradingCalendarDayReader { Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [row], ct)) },
            calendarJournalRepository: repository, calendarJournalDialogs: journalDialogs);
        var main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        var calendar = Assert.IsType<CalendarViewModel>(main.CurrentContentViewModel);
        await calendar.LoadTask;
        var selected = calendar.Weeks.SelectMany(w => w.Days).Single(d => d.Date == date);
        await calendar.SelectDayCommand.ExecuteAsync(selected);
        await calendar.AddDayJournalCommand.ExecuteAsync(null);
        var journal = calendar.InlineJournal!; journal.Text = "Synthetic unsaved journal";
        if (veto == "trade")
        {
            await calendar.ViewTradeCommand.ExecuteAsync(row);
            await editor.ShowSelectedTradeEditCommand.ExecuteAsync(null);
            Assert.True(calendar.HasInlineWork);
        }
        var assessmentDialogs = new CountingOwnerCloseDialogs { Accept = veto != "assessment" };
        var data = new AssessmentData();
        var vm = new RuleAssessmentViewModel(data, data, data.Rules, data,
            new(data.Rules, data, TimeProvider.System), assessmentDialogs);
        await RunOwnerCloseSta(() =>
        {
            var owner = CreateCloseTestWindow(main);
            var day = new CalendarDayDialogWindow { DataContext = calendar };
            bool ownerClosed = false, dayClosed = false, assessmentReturned = false, lateVeto = veto == "subscriber";
            owner.Closed += (_, _) => ownerClosed = true;
            day.Closed += (_, _) => dayClosed = true;
            Window closing = target == "main" ? owner : day;
            Exception? failure = null;
            try
            {
                owner.Show(); day.Owner = owner;
                InNextModal(() => day.ShowDialog(), () =>
                {
                    System.Windows.Application.Current.MainWindow = day;
                    InNextModal(() => { new WpfRuleAssessmentDialog(() => vm).ShowTarget(data.Target); assessmentReturned = true; }, () =>
                    {
                        var assessment = System.Windows.Application.Current.Windows.OfType<Window>().Single(w => w.Content is RuleAssessmentView);
                        try
                        {
                            Assert.Same(day, assessment.Owner);
                            bool direct = target is "assessment" or "command";
                            if (direct) closing = assessment;
                            void CloseTarget()
                            {
                                if (target == "command") vm.CloseCommand.Execute(null);
                                else closing.Close();
                            }
                            vm.Explanation = "Synthetic unsaved assessment"; vm.Evidence[0].Selected = true;
                            var rule = vm.SelectedRule;
                            var explanation = (TextBox)((RuleAssessmentView)assessment.Content).FindName("Explanation");
                            assessment.Activate(); explanation.Focus();
                            // Registered AFTER the assessment subscriber: its approval is not final consent.
                            closing.Closing += (_, e) => { if (lateVeto) e.Cancel = true; };
                            WpfStepDiagnostics.Write($"assessment close attempt: owner={target}; veto={veto}; draft={vm.HasUnsavedChanges}; tradeWork={calendar.HasInlineWork}");
                            CloseTarget();
                            WpfStepDiagnostics.Write($"veto returned: main={owner.IsVisible}; day={day.IsVisible}; assessment={assessment.IsVisible}; journalRetained={ReferenceEquals(journal, calendar.InlineJournal)}");
                            FlushOwnerCloseDispatcher(); // Deferred work must not consume a vetoed draft either.
                            Assert.False(ownerClosed); Assert.False(dayClosed); Assert.False(assessmentReturned);
                            Assert.True(assessment.IsVisible); Assert.True(assessment.IsEnabled);
                            Assert.True(day.IsVisible); Assert.True(owner.IsVisible);
                            Assert.Same(journal, calendar.InlineJournal); Assert.True(journal.IsEditorOpen);
                            Assert.Equal("Synthetic unsaved journal", journal.Text);
                            Assert.Equal("Synthetic unsaved assessment", vm.Explanation);
                            Assert.True(vm.Evidence[0].Selected); Assert.Same(rule, vm.SelectedRule);
                            Assert.Equal(data.Target, vm.Target); Assert.True(vm.CanRecord);
                            Assert.Equal(date, calendar.SelectedDate); Assert.True(selected.IsSelected);
                            Assert.Equal(NavigationDestination.Calendar, main.CurrentDestination);
                            Assert.True(explanation.Focusable); Assert.True(explanation.IsEnabled);
                            explanation.Focus(); Assert.Same(explanation, FocusManager.GetFocusedElement(assessment));
                            Assert.Equal(veto == "journal" || (veto == "subscriber" && target == "main") ? 1 : 0, journalDialogs.Count);
                            Assert.Equal(1, assessmentDialogs.Count);
                            Assert.Empty(data.Saved); Assert.Equal(0, repository.Writes);

                            lateVeto = false; assessmentDialogs.Accept = true; journalDialogs.Accept = true;
                            if (veto == "trade") editor.CancelTradeEditCommand.Execute(null);
                            int journalPrompts = journalDialogs.Count, assessmentPrompts = assessmentDialogs.Count;
                            CloseTarget();
                            WpfStepDiagnostics.Write($"accepted returned: main={owner.IsVisible}; day={day.IsVisible}; assessment={assessment.IsVisible}");
                            Assert.False(assessment.IsVisible); Assert.Equal(!direct, dayClosed);
                            Assert.Equal(target == "main", ownerClosed);
                            if (direct) Assert.Same(journal, calendar.InlineJournal);
                            else Assert.Null(calendar.InlineJournal);
                            Assert.Equal(journalPrompts + (direct ? 0 : 1), journalDialogs.Count);
                            Assert.Equal(assessmentPrompts + 1, assessmentDialogs.Count);
                            Assert.Empty(data.Saved); Assert.Equal(0, repository.Writes);
                        }
                        catch (Exception ex) { failure = ex; }
                        finally
                        {
                            lateVeto = false; assessmentDialogs.Accept = true; journalDialogs.Accept = true;
                            if (editor.IsTradeEditVisible) editor.CancelTradeEditCommand.Execute(null);
                            assessment.Close(); if (!dayClosed) day.Close();
                        }
                    });
                    Assert.True(assessmentReturned); // ShowDialog's nested frame actually ended.
                });
                if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
            finally { journalDialogs.Accept = true; if (!ownerClosed) owner.Close(); main.Dispose(); }
        });
    }

    private static void InNextModal(Action show, Action interact)
    {
        // EnterThreadModal fires only for the outermost modal frame, not a nested
        // ShowDialog. The outer callback already runs on its active dispatcher.
        if (ComponentDispatcher.IsThreadModal)
        {
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, interact);
            show();
            return;
        }
        EventHandler? entered = null;
        entered = (_, _) =>
        {
            ComponentDispatcher.EnterThreadModal -= entered;
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, interact);
        };
        ComponentDispatcher.EnterThreadModal += entered;
        try { show(); }
        finally { ComponentDispatcher.EnterThreadModal -= entered; }
    }

    private static void FlushOwnerCloseDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }
}
