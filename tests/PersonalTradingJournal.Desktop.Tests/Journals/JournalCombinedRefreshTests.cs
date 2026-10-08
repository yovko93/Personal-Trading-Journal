using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalCombinedRefreshTests
{
    [Fact]
    public async Task HistoryRefreshUpdatesSelectedJournalOpenReviewAndSnapshotWithoutChangingScopeOrValidPage()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        DateOnly date = new(2026, 10, 1);
        for (int i = 0; i < 11; i++) await db.Repository.CreateAsync(new(date.AddDays(-i), null, $"Entry {i}"));
        var dialogs = new FakeDialogService();
        var vm = new JournalViewModel(db.Repository, db.Provider.GetRequiredService<ITradingAccountReader>(), dialogs,
            db.CreateTradeContext(), historyReader: db.Provider.GetRequiredService<IDailyJournalHistoryReader>());
        try
        {
            vm.SelectedDate = date.ToDateTime(TimeOnly.MinValue); await vm.ActivateAsync();
            var history = vm.History!;
            await history.NextCommand.ExecuteAsync(null);
            await history.OpenCommand.ExecuteAsync(Assert.Single(history.Entries));
            await history.ViewRevisionCommand.ExecuteAsync(history.Revisions[0]);
            var opened = history.SelectedEntry!.Item.Id;
            var snapshot = history.Snapshot!;
            vm.OpenEditorCommand.Execute(null); vm.Text = "Local draft";
            var selected = (await db.Repository.GetAsync(date))!.Entry;
            await db.Repository.UpdateAsync(new(selected.Id, selected.Revision, "Latest", false));
            var old = (await db.Repository.GetAsync(date.AddDays(-10)))!.Entry;
            await db.Repository.UpdateAsync(new(old.Id, old.Revision, "Fresh preview", false));
            var rows = history.Entries;
            await history.RefreshCommand.ExecuteAsync(null); // Veto leaves every displayed section untouched.
            Assert.Same(rows, history.Entries); Assert.Same(snapshot, history.Snapshot);
            Assert.Equal("Local draft", vm.Text); Assert.True(vm.IsEditorOpen);
            await vm.SaveCommand.ExecuteAsync(null);
            Assert.Contains("changed elsewhere", vm.ErrorMessage);
            dialogs.ConfirmationResult = true;
            await history.RefreshCommand.ExecuteAsync(null);
            Assert.Equal("Latest", vm.Text); Assert.True(vm.IsCompleted); Assert.False(vm.IsEditorOpen);
            Assert.Equal(date.ToDateTime(TimeOnly.MinValue), vm.SelectedDate); Assert.Null(vm.SelectedAccount.Id);
            Assert.Equal("Page 2 · 11 reviews", history.PageText);
            Assert.Equal(opened, history.SelectedEntry!.Item.Id);
            Assert.Equal("Fresh preview", Assert.Single(history.Previews).Text);
            Assert.Equal(snapshot.Text, history.Snapshot!.Text); // Prior immutable revision remains prior content.
            Assert.Equal(2, history.Revisions.Count);
            await db.Repository.DeleteAsync(new(old.Id, old.Revision + 1));
            await history.RefreshCommand.ExecuteAsync(null);
            Assert.Equal("Page 1 · 10 reviews", history.PageText);
            Assert.Null(history.SelectedEntry); Assert.Null(history.Snapshot);
            Assert.Equal(2, (await db.Repository.GetAsync(date))!.Entry.Revision); // Refresh never writes.
        }
        finally { vm.Deactivate(); }
    }
}
