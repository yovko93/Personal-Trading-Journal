using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.DailyReview;
using PersonalTradingJournal.Desktop.Tests.PeriodReviews;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;
using PersonalTradingJournal.Desktop.ViewModels.PeriodReviews;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    [Theory]
    [InlineData(ManualReviewKind.Daily, false)] [InlineData(ManualReviewKind.Daily, true)]
    [InlineData(ManualReviewKind.Weekly, false)] [InlineData(ManualReviewKind.Weekly, true)]
    [InlineData(ManualReviewKind.Monthly, false)] [InlineData(ManualReviewKind.Monthly, true)]
    public async Task ManualResponseGuardProtectsNavigationAndOwnerClose(ManualReviewKind kind, bool close)
    {
        var d = new ReviewFixture(); var w = new PeriodFixture(); var m = new PeriodFixture(ReviewPeriodKind.Month);
        var daily = new DailyReviewViewModel(d.Reader, d.History, d.Accounts, d.Dialogs, TimeProvider.System,
            manualReviews: new ManualReviewWorkspaceTests.Store(), clipboard: new ManualReviewWorkspaceTests.Clip());
        PeriodReviewViewModel Period(PeriodFixture f, ReviewPeriodKind k) => new(k, f.Reader, f.Store, f.Accounts, f.Dialogs, TimeProvider.System,
            (_, _) => throw new InvalidOperationException("Provider path must not run"), f.Credentials,
            manualReviews: new ManualReviewWorkspaceTests.Store(), clipboard: new ManualReviewWorkspaceTests.Clip());
        var week = Period(w, ReviewPeriodKind.Week); var month = Period(m, ReviewPeriodKind.Month);
        var fixture = CreateFixture(dailyReview: daily, periods: new(week, month));
        var destination = kind switch { ManualReviewKind.Daily => NavigationDestination.DailyReview, ManualReviewKind.Weekly => NavigationDestination.WeeklyReview, _ => NavigationDestination.MonthlyReview };
        await daily.ActivateAsync(); await week.ActivateAsync(); await month.ActivateAsync();
        fixture.Main.NavigateCommand.Execute(destination);
        var manual = kind switch { ManualReviewKind.Daily => daily.Manual!, ManualReviewKind.Weekly => week.Manual!, _ => month.Manual! };
        await manual.PrepareCommand.ExecuteAsync(null); manual.ResponseText = "Unfinished synthetic response";
        string scope = manual.PendingScope;
        if (close) Assert.False(fixture.Main.TryCloseWindow()); else fixture.Main.NavigateCommand.Execute(NavigationDestination.Dashboard);
        Assert.Equal(destination, fixture.Main.CurrentDestination); Assert.Equal(scope, manual.PendingScope);
        Assert.Equal("Unfinished synthetic response", manual.ResponseText);
        await manual.KeepCommand.ExecuteAsync(null);
        if (close) Assert.True(fixture.Main.TryCloseWindow()); else
        {
            fixture.Main.NavigateCommand.Execute(NavigationDestination.Dashboard); Assert.Equal(NavigationDestination.Dashboard, fixture.Main.CurrentDestination);
            fixture.Main.NavigateCommand.Execute(destination); Assert.Equal("Unfinished synthetic response", manual.ResponseText);
        }
        fixture.Main.Dispose();
    }
}
