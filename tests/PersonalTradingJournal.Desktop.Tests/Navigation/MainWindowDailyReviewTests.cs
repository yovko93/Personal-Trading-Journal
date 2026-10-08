using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.DailyReview;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    [Fact]
    public async Task DailyReviewNavigationUsesWorkspaceAndTradeIdentityIndependentOfPaging()
    {
        var review = new ReviewFixture();
        var f = CreateFixture(dailyReview: review.Vm);
        f.Main.NavigateCommand.Execute(NavigationDestination.DailyReview);
        await review.Vm.LoadTask;
        Assert.Same(review.Vm, f.Main.CurrentContentViewModel);
        Guid sourceId = Guid.NewGuid();
        await review.Vm.OpenTradeAsync!(sourceId);
        Assert.Equal(NavigationDestination.Trades, f.Main.CurrentDestination);
        Assert.True(f.Trades.IsTradeDetailNotFound);
        Assert.True(f.Trades.IsTradeDetailVisible);
        Assert.Equal(1, f.TradeDetailReader.CallCount);
        Assert.Equal(sourceId, Assert.Single(f.TradeDetailReader.RequestedTradeIds));
        f.Main.Dispose();
        Assert.Null(review.Vm.OpenTradeAsync);
    }

    [Fact]
    public async Task DailyReviewJournalNavigationKeepsExactScopeAndStandaloneUnsavedGuard()
    {
        var review = new ReviewFixture();
        var f = CreateFixture(dailyReview: review.Vm);
        f.Main.NavigateCommand.Execute(NavigationDestination.DailyReview);
        await review.Vm.LoadTask;
        await review.Vm.OpenJournalAsync!(ReviewFixture.Journal(null, "All accounts", ReviewFixture.Day));
        Assert.Equal(NavigationDestination.Journal, f.Main.CurrentDestination);
        Assert.Equal(ReviewFixture.Day.ToDateTime(TimeOnly.MinValue), f.Journal.SelectedDate);
        Assert.Null(f.Journal.SelectedAccount.Id);
        f.Journal.OpenEditorCommand.Execute(null);
        f.Journal.Text = "Unsaved journal text must survive a veto.";
        f.Main.NavigateCommand.Execute(NavigationDestination.DailyReview);
        Assert.Equal(NavigationDestination.Journal, f.Main.CurrentDestination);
        Assert.Contains("Unsaved journal", f.Journal.Text);
        // Targeting another account also goes through the same editor's unsaved guard.
        await review.Vm.OpenJournalAsync!(ReviewFixture.Journal(ReviewFixture.AccountId, "P 21", ReviewFixture.Day));
        Assert.Null(f.Journal.SelectedAccount.Id);
        Assert.Contains("Unsaved journal", f.Journal.Text);
        f.Main.Dispose();
    }
}
