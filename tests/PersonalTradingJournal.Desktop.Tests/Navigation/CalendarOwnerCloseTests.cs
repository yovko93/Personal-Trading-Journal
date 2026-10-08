using System.Windows;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Tests.Trades;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.Views.Calendar;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    private const string OwnerCloseHostVariable = "PTJ_CALENDAR_OWNER_CLOSE_HOST";
    private static readonly Lazy<Task> OwnerCloseHost = new(() => IsolatedTestProcess.RunSuiteAsync(
        typeof(MainWindowViewModelTests), "calendar-owner-close", OwnerCloseHostVariable,
        TimeSpan.FromSeconds(60), testCaseFilter:
        $"FullyQualifiedName~{typeof(MainWindowViewModelTests).FullName}.CalendarOwnerClose"));

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CalendarOwnerCloseProtectsDirtyJournalAndClosesCleanWithoutDuplicatePrompt(bool dirty, bool accept)
    {
        if (Environment.GetEnvironmentVariable(OwnerCloseHostVariable) != "1") { await OwnerCloseHost.Value; return; }
        var dialogs = new CountingOwnerCloseDialogs { Accept = accept };
        var repository = new FakeDailyJournalRepository();
        var fixture = CreateFixture(calendarJournalRepository: repository, calendarJournalDialogs: dialogs);
        var main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        var calendar = Assert.IsType<CalendarViewModel>(main.CurrentContentViewModel);
        await calendar.LoadTask;
        var day = calendar.Weeks[0].Days[5]; // Saturday, with exact date retained on a veto.
        await calendar.SelectDayCommand.ExecuteAsync(day);
        await calendar.AddDayJournalCommand.ExecuteAsync(null);
        var journal = calendar.InlineJournal!;
        if (dirty)
        {
            journal.Text = "Keep this Calendar note";
            journal.WentWell = "Well";
            journal.NeedsImprovement = "Improve";
            journal.NextTradingDay = "Next";
        }
        var month = calendar.SelectedMonth;
        var account = calendar.SelectedAccount;
        var currency = calendar.SelectedCurrency;
        await RunOwnerCloseSta(() =>
        {
            var owner = CreateCloseTestWindow(main);
            var dialog = new CalendarDayDialogWindow { DataContext = calendar };
            bool ownerClosed = false, dialogClosed = false;
            owner.Closed += (_, _) => ownerClosed = true;
            dialog.Closed += (_, _) => dialogClosed = true;
            try
            {
                owner.Show(); dialog.Owner = owner; dialog.Show();
                owner.Close(); // Calls the real MainWindow.OnClosing, not only the ViewModel method.
                if (dirty && !accept)
                {
                    Assert.False(ownerClosed); Assert.False(dialogClosed);
                    Assert.True(owner.IsVisible); Assert.True(dialog.IsVisible);
                    Assert.Same(journal, calendar.InlineJournal);
                    Assert.True(journal.IsEditorOpen); Assert.True(journal.IsDirty);
                    Assert.Equal("Keep this Calendar note", journal.Text);
                    Assert.Equal("Well", journal.WentWell);
                    Assert.Equal("Improve", journal.NeedsImprovement);
                    Assert.Equal("Next", journal.NextTradingDay);
                    Assert.Equal(month, calendar.SelectedMonth); Assert.Equal(day.Date, calendar.SelectedDate);
                    Assert.Same(account, calendar.SelectedAccount); Assert.Equal(currency, calendar.SelectedCurrency);
                    Assert.Equal(NavigationDestination.Calendar, main.CurrentDestination);
                    Assert.True(day.IsSelected);
                }
                else
                {
                    Assert.True(ownerClosed); Assert.True(dialogClosed);
                    Assert.Null(calendar.InlineJournal);
                }
                Assert.Equal(dirty ? 1 : 0, dialogs.Count);
                Assert.Equal(0, repository.Writes); // Discard/close never saves or deletes.
            }
            finally { dialogs.Accept = true; if (!ownerClosed) owner.Close(); main.Dispose(); }
        });
    }

    [Fact]
    public async Task CalendarOwnerCloseVetoesInProgressTradeBeforeConsultingDirtyJournal()
    {
        var date = new DateOnly(2026, 9, 5);
        var row = CalendarDayDetailsTests.Row(date, 10, 9);
        var detail = TradesViewModelTests.CreateEditableTradeDetail(row, null);
        var details = new FakeTradeDetailReader { HoldRead = true };
        details.EnqueueResult(detail); details.EnqueueResult(detail); // View, then Edit reload.
        var editor = TradesViewModelTests.CreateViewModel(tradeDetailReader: details,
            reader: TradesViewModelTests.CreateEditReferenceReader(detail));
        var dialogs = new CountingOwnerCloseDialogs { Accept = true };
        var fixture = CreateFixture(calendarEditor: editor,
            calendarDayReader: new FakeTradingCalendarDayReader { Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [row], ct)) },
            calendarJournalRepository: new FakeDailyJournalRepository(), calendarJournalDialogs: dialogs);
        using var main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Calendar);
        var calendar = Assert.IsType<CalendarViewModel>(main.CurrentContentViewModel);
        await calendar.LoadTask;
        await calendar.SelectDayCommand.ExecuteAsync(calendar.Weeks.SelectMany(w => w.Days).Single(d => d.Date == date));
        await calendar.AddDayJournalCommand.ExecuteAsync(null);
        var journal = calendar.InlineJournal!; journal.Text = "Do not discard while Trade is loading";
        Task load = calendar.ViewTradeCommand.ExecuteAsync(row);
        try
        {
            await details.ReadStarted.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(calendar.HasInlineWork); Assert.False(main.TryCloseWindow());
            Assert.False(details.CancellationToken.IsCancellationRequested);
            Assert.Same(journal, calendar.InlineJournal); Assert.Equal(0, dialogs.Count);
            Assert.Contains("Finish loading", calendar.DayErrorMessage);
        }
        finally { details.ReleaseRead(); await load; }
        await editor.ShowSelectedTradeEditCommand.ExecuteAsync(null);
        Assert.True(editor.IsTradeEditVisible); Assert.False(main.TryCloseWindow());
        Assert.Same(journal, calendar.InlineJournal); Assert.Equal(0, dialogs.Count);
        editor.CancelTradeEditCommand.Execute(null);
        Assert.True(main.TryCloseWindow()); Assert.Equal(1, dialogs.Count);
    }

    [Fact]
    public async Task CalendarOwnerCloseStandaloneJournalStillUsesItsOwnGuard()
    {
        if (Environment.GetEnvironmentVariable(OwnerCloseHostVariable) != "1") { await OwnerCloseHost.Value; return; }
        var fixture = CreateJournalFixture();
        var main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await fixture.Journal.LoadTask;
        fixture.Journal.OpenEditorCommand.Execute(null);
        fixture.Journal.Text = "Standalone note";
        await RunOwnerCloseSta(() =>
        {
            var owner = CreateCloseTestWindow(main);
            bool closed = false; owner.Closed += (_, _) => closed = true;
            try
            {
                owner.Show(); owner.Close();
                Assert.False(closed); Assert.True(owner.IsVisible);
                Assert.Equal("Standalone note", fixture.Journal.Text);
                Assert.True(fixture.Journal.IsEditorOpen);
                Assert.NotNull(fixture.Dialogs.ConfirmationRequest);
                fixture.Dialogs.ConfirmationResult = true;
                owner.Close(); Assert.True(closed);
                Assert.Empty(fixture.Repository.CreateCalls);
            }
            finally { fixture.Dialogs.ConfirmationResult = true; if (!closed) owner.Close(); main.Dispose(); }
        });
    }

    private static MainWindow CreateCloseTestWindow(ViewModels.MainWindowViewModel main)
    {
        // No App constructor/startup: only theme resources and fake readers, never LocalAppData.
        var application = System.Windows.Application.Current ??
            new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Resources = CalendarViewLayoutTests.SharedThemeResources("Dark");
        return new MainWindow(main) { Content = new System.Windows.Controls.Border(), ShowInTaskbar = false };
    }

    private static Task RunOwnerCloseSta(Action action) => CalendarStaTest.RunAsync(action,
        timeout: TimeSpan.FromSeconds(30), shutdownDispatcher: false);

    private sealed class CountingOwnerCloseDialogs : IDialogService
    {
        public bool Accept { get; set; }
        public int Count { get; private set; }
        public bool Confirm(ConfirmationDialogRequest request) { Count++; return Accept; }
        public void ShowInformation(InformationDialogRequest request) { }
    }
}
