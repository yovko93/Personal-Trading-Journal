using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels;
using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    [Theory]
    [InlineData("2026-03-08T04:59:59Z", "2026-03-08T05:00:00Z", "2026-03-08", false)]
    [InlineData("2026-03-08T06:59:59Z", "2026-03-08T07:00:00Z", "2026-03-08", true)]
    [InlineData("2026-11-01T03:59:59Z", "2026-11-01T04:00:00Z", "2026-11-01", true)]
    [InlineData("2026-11-01T05:59:59Z", "2026-11-01T06:00:00Z", "2026-11-01", false)]
    [InlineData("2026-11-02T04:59:59Z", "2026-11-02T05:00:00Z", "2026-11-02", true)]
    public async Task GuardedReentryResetsTodayScopeHistoryAndEditor(string before, string after, string expected, bool todayExists)
    {
        var clock = new ReentryClock(DateTimeOffset.Parse(before));
        var today = DateOnly.Parse(expected);
        var history = new JournalHistoryTestReader();
        for (int i = 0; i < 21; i++) history.Add(new DateOnly(2026, 1, 31).AddDays(-i), revision: 23);
        var repository = new NavigationJournalRepository
        {
            Read = (date, account, _) => Task.FromResult<DailyJournalDetails?>(
                date == today && account is null && todayExists
                    ? new(new(date, null, "Today's completed note", false, clock.GetUtcNow()), DailyJournalAccountState.AllAccounts, null)
                    : new(new(date, account, "Saved old draft", clock.GetUtcNow()),
                        account is null ? DailyJournalAccountState.AllAccounts : DailyJournalAccountState.Unavailable, null))
        };
        var fixture = CreateJournalFixture(repository, clock: clock, historyReader: history);
        using var main = fixture.Main;
        var vm = fixture.Journal;
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await vm.LoadTask;
        await vm.History!.NextCommand.ExecuteAsync(null);
        await vm.History.OpenCommand.ExecuteAsync(vm.History.Entries[0]);
        vm.History.OpenInEditorCommand.Execute(null); // Read-only viewing no longer retargets the editor.
        await vm.LoadTask;
        await vm.History.NextRevisionsCommand.ExecuteAsync(null);
        await vm.History.ViewRevisionCommand.ExecuteAsync(vm.History.Revisions[0]);
        vm.OpenEditorCommand.Execute(null);
        vm.Text = "Unsaved note retained on veto";
        vm.NextTradingDay = "Retained answer";
        var selected = vm.SelectedDate;
        main.NavigateCommand.Execute(NavigationDestination.Notebook);
        Assert.Equal(NavigationDestination.Journal, main.CurrentDestination);
        Assert.Equal(selected, vm.SelectedDate);
        Assert.StartsWith("Page 2", vm.History.PageText);
        Assert.NotNull(vm.History.Snapshot);
        Assert.True(vm.IsEditorOpen && vm.IsDirty);
        Assert.Equal("Retained answer", vm.NextTradingDay);
        fixture.Dialogs.ConfirmationResult = true;
        main.NavigateCommand.Execute(NavigationDestination.Notebook);
        clock.Now = DateTimeOffset.Parse(after);
        repository.Read = (date, account, _) => Task.FromResult<DailyJournalDetails?>(todayExists
            ? new(new(date, account, "Today's completed note", false, clock.GetUtcNow()), DailyJournalAccountState.AllAccounts, null) : null);
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await vm.LoadTask;
        Assert.Equal(today.ToDateTime(TimeOnly.MinValue), vm.SelectedDate);
        Assert.Null(vm.SelectedAccount.Id);
        Assert.False(vm.IsEditorOpen || vm.IsDirty);
        Assert.Null(vm.History.SelectedEntry);
        Assert.Null(vm.History.Snapshot);
        Assert.Empty(vm.History.Revisions);
        Assert.StartsWith("Page 1", vm.History.PageText);
        Assert.StartsWith("Page 1", vm.History.RevisionPageText);
        Assert.Equal(todayExists ? "Completed" : "No entry", vm.EntryStateLabel);
        Assert.Equal(!todayExists, vm.OpenEditorCommand.CanExecute(null));
        Assert.Equal(today, repository.ReadCalls.Last().Date);
        Assert.Null(repository.ReadCalls.Last().AccountId);
        Assert.Empty(repository.CreateCalls);
    }

    [Fact]
    public async Task UnavailableAccountDoesNotBecomeDefaultUntilSuccessfulDepartureAndReturn()
    {
        var fixture = CreateJournalFixture();
        using var main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await fixture.Journal.LoadTask;
        var unavailable = Guid.NewGuid();
        fixture.Journal.SelectedAccount = new(unavailable, "Removed account", false);
        await fixture.Journal.LoadTask;
        Assert.Equal(unavailable, fixture.Journal.SelectedAccount.Id);
        main.NavigateCommand.Execute(NavigationDestination.Notebook);
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await fixture.Journal.LoadTask;
        Assert.Null(fixture.Journal.SelectedAccount.Id);
        Assert.Equal("All accounts", fixture.Journal.SelectedAccount.Name);
        Assert.False(fixture.Journal.IsEditorOpen);
    }

    private sealed class ReentryClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
