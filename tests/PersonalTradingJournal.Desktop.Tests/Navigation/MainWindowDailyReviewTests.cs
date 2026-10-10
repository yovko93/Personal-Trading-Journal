using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.DailyReview;
using PersonalTradingJournal.Desktop.ViewModels.DailyReview;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    [Fact]
    public async Task ConfigureAiRoutesToSettingsWithoutGenerating()
    {
        var provider = new DailyReviewGenerationTests.Provider();
        var review = DailyReviewGenerationTests.Ready(provider);
        var f = CreateFixture(dailyReview: review.Vm);
        f.Main.NavigateCommand.Execute(NavigationDestination.DailyReview);
        await review.Vm.LoadTask;
        review.Vm.ConfigureAiCommand.Execute(null);
        Assert.Equal(NavigationDestination.Settings, f.Main.CurrentDestination);
        Assert.Equal(0, provider.Calls);
        f.Main.Dispose();
    }
    [Theory]
    [InlineData("close")]
    [InlineData("navigate")]
    [InlineData("dispose")]
    public async Task LeavingDailyReviewCancelsGenerationAndRejectsLateSuccess(string action)
    {
        var pending = new TaskCompletionSource<PersonalTradingJournal.Application.DailyReview.Coaching.CoachingProviderReply>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new DailyReviewGenerationTests.Provider { Handler = (_, _) => pending.Task };
        var review = DailyReviewGenerationTests.Ready(provider);
        var f = CreateFixture(dailyReview: review.Vm);
        f.Main.NavigateCommand.Execute(NavigationDestination.DailyReview);
        await review.Vm.LoadTask;
        var request = review.Vm.GenerateCommand.ExecuteAsync(null);
        var token = await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (action == "close") Assert.True(f.Main.TryCloseWindow()); // MainWindow.OnClosing delegates here.
        else if (action == "navigate") f.Main.NavigateCommand.Execute(NavigationDestination.Dashboard);
        else f.Main.Dispose();
        await request.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(token.IsCancellationRequested);
        pending.SetResult(DailyReviewGenerationTests.Provider.Success(provider.Packet!));
        Assert.Equal(0, review.History.Writes);
        Assert.Null(review.Vm.Snapshot);
        Assert.False(review.Vm.GenerateCommand.CanExecute(null));
        if (action != "dispose") f.Main.Dispose();
    }

    [Fact]
    public async Task DailyReviewNavigationUsesWorkspaceAndTradeIdentityIndependentOfPaging()
    {
        var review = new ReviewFixture();
        var f = CreateFixture(dailyReview: review.Vm);
        f.Main.NavigateCommand.Execute(NavigationDestination.DailyReview);
        await review.Vm.LoadTask;
        Assert.Same(review.Vm, f.Main.CurrentContentViewModel);
        var citation = ReviewSnapshot.Read(ReviewFixture.Saved(ReviewFixture.Evidence(new(ReviewFixture.Day)), citeAll: true))
            .Statements.SelectMany(s => s.Citations).First(c => c.HasTrade);
        Guid sourceId = citation.Trade!.Id;
        await review.Vm.OpenTradeCommand.ExecuteAsync(citation.Trade);
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
        var journals = ReviewSnapshot.Read(ReviewFixture.Saved(ReviewFixture.Evidence(new(ReviewFixture.Day)), citeAll: true))
            .Statements.SelectMany(s => s.Citations).Where(c => c.HasJournal).Select(c => c.Journal!).ToArray();
        await review.Vm.OpenJournalCommand.ExecuteAsync(journals.Single(j => j.Source.TradingAccountId is null));
        Assert.Equal(NavigationDestination.Journal, f.Main.CurrentDestination);
        Assert.Equal(ReviewFixture.Day.ToDateTime(TimeOnly.MinValue), f.Journal.SelectedDate);
        Assert.Null(f.Journal.SelectedAccount.Id);
        f.Journal.OpenEditorCommand.Execute(null);
        f.Journal.Text = "Unsaved journal text must survive a veto.";
        f.Main.NavigateCommand.Execute(NavigationDestination.DailyReview);
        Assert.Equal(NavigationDestination.Journal, f.Main.CurrentDestination);
        Assert.Contains("Unsaved journal", f.Journal.Text);
        // Targeting another account also goes through the same editor's unsaved guard.
        await review.Vm.OpenJournalCommand.ExecuteAsync(journals.Single(j => j.Source.TradingAccountId == ReviewFixture.AccountId));
        Assert.Null(f.Journal.SelectedAccount.Id);
        Assert.Contains("Unsaved journal", f.Journal.Text);
        f.Main.Dispose();
    }
}
