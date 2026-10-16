using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.DailyReview;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.PeriodReviews;
using PersonalTradingJournal.Desktop.ViewModels.PeriodReviews;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    [Fact]
    public async Task PeriodJournalCitationRejectsReplacementBetweenLinkCheckAndEditorLoad()
    {
        var week = new PeriodFixture(); var month = new PeriodFixture(ReviewPeriodKind.Month);
        var f = CreateFixture(periods: new(week.Vm, month.Vm));
        var source = ReviewFixture.Evidence(new(ReviewFixture.Day)).Journals.First(j => j.TradingAccountId is null);
        var replacement = new DailyJournalEntry(source.TradingDate, null, "Synthetic replacement", true, ReviewFixture.Now);
        f.JournalRepository.Read = (_, _, _) => Task.FromResult<DailyJournalDetails?>(new(replacement, DailyJournalAccountState.AllAccounts, null));
        f.Main.NavigateCommand.Execute(NavigationDestination.WeeklyReview); await week.Vm.LoadTask;
        // The period link checked the original identity. The source changes before the editor's independent read.
        await week.Vm.OpenJournalAsync!(source);
        Assert.Equal(NavigationDestination.Journal, f.Main.CurrentDestination);
        Assert.Null(f.Journal.JournalId); Assert.DoesNotContain("Synthetic replacement", f.Journal.Text);
        Assert.Contains("moved or deleted", f.Journal.ErrorMessage); Assert.Empty(f.JournalRepository.CreateCalls);
        f.JournalRepository.Read = (_, _, _) => Task.FromResult<DailyJournalDetails?>(null);
        f.Journal.SelectedDate = source.TradingDate.AddDays(1).ToDateTime(TimeOnly.MinValue); await f.Journal.LoadTask;
        Assert.Null(f.Journal.ErrorMessage); Assert.Null(f.Journal.JournalId);
        f.Main.Dispose();
    }
    [Fact]
    public async Task WeeklyMonthlyNavigateIndependentlyAndRespectJournalGuard()
    {
        var week = new PeriodFixture(); var month = new PeriodFixture(ReviewPeriodKind.Month);
        var f = CreateFixture(periods: new(week.Vm, month.Vm));
        f.Main.NavigateCommand.Execute(NavigationDestination.WeeklyReview); await week.Vm.LoadTask;
        Assert.Same(week.Vm, f.Main.CurrentContentViewModel); week.Vm.SelectedDate = new(2026, 9, 12); await week.Vm.LoadTask;
        var selected = week.Vm.Query;
        f.Main.NavigateCommand.Execute(NavigationDestination.MonthlyReview); await month.Vm.LoadTask;
        Assert.Same(month.Vm, f.Main.CurrentContentViewModel); Assert.NotEqual(selected!.FirstDate, month.Vm.Query!.FirstDate);
        f.Main.NavigateCommand.Execute(NavigationDestination.WeeklyReview); await week.Vm.LoadTask; Assert.Equal(selected, week.Vm.Query);
        f.Main.NavigateCommand.Execute(NavigationDestination.Journal); await f.Journal.LoadTask;
        f.Journal.OpenEditorCommand.Execute(null); f.Journal.Text = "Synthetic unsaved draft";
        await week.Vm.OpenTradeCommand.ExecuteAsync(new PeriodCitation("Saved Trade", "", Guid.NewGuid()));
        Assert.Equal(NavigationDestination.Journal, f.Main.CurrentDestination); Assert.Equal("Synthetic unsaved draft", f.Journal.Text); Assert.Equal(0, f.TradeDetailReader.CallCount);
        Assert.Equal(0, week.Provider.Calls); Assert.Equal(0, month.Provider.Calls); f.Main.Dispose();
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task OwnerCloseAndNavigationVetoThenCancelInProgressPeriodGeneration(bool window)
    {
        var week = new PeriodFixture(); var month = new PeriodFixture(ReviewPeriodKind.Month);
        var pending = new TaskCompletionSource<PersonalTradingJournal.Application.DailyReview.Coaching.CoachingProviderReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        week.Provider.Handler = (_, _) => pending.Task; var f = CreateFixture(periods: new(week.Vm, month.Vm));
        f.Main.NavigateCommand.Execute(NavigationDestination.WeeklyReview); await week.Vm.LoadTask;
        var task = week.Vm.GenerateCommand.ExecuteAsync(null); var token = await week.Provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        f.Main.NavigateCommand.Execute(NavigationDestination.WeeklyReview);
        Assert.False(token.IsCancellationRequested); Assert.Equal(1, week.Provider.Calls);
        if (window) Assert.False(f.Main.TryCloseWindow()); else f.Main.NavigateCommand.Execute(NavigationDestination.Dashboard);
        Assert.Equal(NavigationDestination.WeeklyReview, f.Main.CurrentDestination); Assert.False(token.IsCancellationRequested);
        week.Dialogs.ConfirmationResult = true;
        if (window) Assert.True(f.Main.TryCloseWindow()); else f.Main.NavigateCommand.Execute(NavigationDestination.Dashboard);
        await task.WaitAsync(TimeSpan.FromSeconds(10)); Assert.True(token.IsCancellationRequested); Assert.Equal(0, week.Store.Saves);
        pending.SetResult(PeriodFixture.ProviderFake.Success(week.Provider.Packet!)); f.Main.Dispose();
    }
}
