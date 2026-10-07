using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalHistorySqliteTests
{
    [Fact]
    public async Task AllScopesHistoryOpensEditsAndDeletesOriginalAccountWithoutChangingOtherEntries()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var p21 = new TradingAccount("P 21", TradingAccountType.Personal, null, null, "USD", 0m, JournalHistoryTestReader.Now);
        p21.Deactivate(JournalHistoryTestReader.Now.AddDays(1));
        var other = new TradingAccount("Other account", TradingAccountType.Personal, null, null, "USD", 0m, JournalHistoryTestReader.Now);
        var accounts = db.Provider.GetRequiredService<ITradingAccountStore>();
        await accounts.AddAsync(p21);
        await accounts.AddAsync(other);
        DateOnly date = new(2026, 9, 9);
        var global = (await db.Repository.CreateAsync(new(date, null, "Global"))).Journal!.Entry;
        var scoped = (await db.Repository.CreateAsync(new(date, p21.Id, "P21 original"))).Journal!.Entry;
        await db.Repository.CreateAsync(new(date, other.Id, "Other"));
        var dialogs = new FakeDialogService();
        var vm = new JournalViewModel(db.Repository, db.Provider.GetRequiredService<ITradingAccountReader>(), dialogs,
            db.CreateTradeContext(), new JournalHistoryViewModelTests.Clock(), db.Provider.GetRequiredService<IDailyJournalHistoryReader>());
        try
        {
            await vm.ActivateAsync();
            Assert.Null(vm.SelectedAccount.Id);
            Assert.False(vm.IsExisting);
            Assert.Contains("Account-specific", vm.EmptyReviewText);
            Assert.Equal(3, vm.History!.Entries.Count);
            var row = vm.History.Entries.Single(r => r.Item.Id == scoped.Id);
            Assert.Contains("P 21", row.ScopeText);
            Assert.Contains("inactive", row.ScopeText);
            vm.OpenEditorCommand.Execute(null);
            vm.Text = "Unsaved All accounts text";
            var rows = vm.History.Entries;
            string page = vm.History.PageText;
            await vm.History.OpenCommand.ExecuteAsync(row); // Read-only viewing never navigates.
            Assert.Equal("P21 original", Assert.Single(vm.History.Previews).Text);
            Assert.Same(rows, vm.History.Entries);
            Assert.Equal(page, vm.History.PageText);
            await vm.History.ViewRevisionCommand.ExecuteAsync(vm.History.Revisions[0]);
            Assert.Equal("P21 original", vm.History.Snapshot!.Text);
            vm.History.OpenInEditorCommand.Execute(null); // Explicit editing can be vetoed.
            Assert.Null(vm.SelectedAccount.Id);
            Assert.Equal("Unsaved All accounts text", vm.Text);
            Assert.Same(row, vm.History.SelectedEntry);
            dialogs.ConfirmationResult = true;
            vm.History.OpenInEditorCommand.Execute(null);
            await vm.LoadTask;
            await vm.History.LoadTask;
            Assert.Equal(p21.Id, vm.SelectedAccount.Id);
            Assert.Equal(date, DateOnly.FromDateTime(vm.SelectedDate!.Value));
            Assert.Equal("P21 original", vm.Text);
            await vm.History.OpenCommand.ExecuteAsync(vm.History.Entries.Single(r => r.Item.Id == scoped.Id));
            Assert.Equal(scoped.Id, vm.History.SelectedEntry!.Item.Id);
            await vm.History.ViewRevisionCommand.ExecuteAsync(vm.History.Revisions[0]);
            Assert.Equal("P21 original", vm.History.Snapshot!.Text);
            vm.OpenEditorCommand.Execute(null);
            vm.Text = "P21 revised";
            await vm.SaveDraftAndCloseCommand.ExecuteAsync(null);
            Assert.Equal("P21 revised", (await db.Repository.GetAsync(date, p21.Id))!.Entry.Text);
            Assert.Equal(2, vm.Revision);
            vm.OpenEditorCommand.Execute(null);
            await vm.SaveCommand.ExecuteAsync(null);
            Assert.False((await db.Repository.GetAsync(date, p21.Id))!.Entry.IsDraft);
            Assert.Equal(3, vm.Revision);
            await vm.DeleteCommand.ExecuteAsync(null);
            Assert.Null(vm.ErrorMessage);
            Assert.Null(await db.Repository.GetAsync(date, p21.Id));
            Assert.Equal("Global", (await db.Repository.GetAsync(date))!.Entry.Text);
            Assert.Single(await db.Repository.GetHistoryAsync(global.Id));
            Assert.Equal("Other", (await db.Repository.GetAsync(date, other.Id))!.Entry.Text);
            Assert.Empty(vm.History.Entries);
            vm.SelectedAccount = vm.Accounts.Single(a => a.Id is null);
            await vm.LoadTask;
            await vm.History.LoadTask;
            Assert.Equal(2, vm.History.Entries.Count);
        }
        finally { vm.Deactivate(); }
    }

    [Fact]
    public async Task CommittedDeletionClearsHistorySnapshotAndCalendarStatusAndStaleDeletionKeepsDraft()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var dialogs = new FakeDialogService { ConfirmationResult = true };
        var vm = new JournalViewModel(db.Repository, db.Provider.GetRequiredService<ITradingAccountReader>(), dialogs,
            db.CreateTradeContext(), new JournalHistoryViewModelTests.Clock(), db.Provider.GetRequiredService<IDailyJournalHistoryReader>());
        try
        {
            await vm.ActivateAsync();
            vm.OpenEditorCommand.Execute(null);
            vm.Text = "Original";
            await vm.SaveCommand.ExecuteAsync(null);
            await vm.ReopenReviewCommand.ExecuteAsync(null);
            vm.Text += " edited";
            await vm.SaveDraftAndCloseCommand.ExecuteAsync(null);
            var row = Assert.Single(vm.History!.Entries);
            await vm.History.OpenCommand.ExecuteAsync(row);
            await vm.LoadTask;
            await vm.History.ViewRevisionCommand.ExecuteAsync(vm.History.Revisions[0]);
            Assert.NotNull(vm.History.Snapshot);
            await db.Repository.UpdateAsync(new(row.Item.Id, 2, "Other writer", true));
            vm.OpenEditorCommand.Execute(null);
            vm.Text = "Local text";
            await vm.DeleteCommand.ExecuteAsync(null);
            Assert.True(vm.IsEditorOpen && vm.IsDirty);
            Assert.Equal("Local text", vm.Text);
            Assert.NotNull(vm.ErrorMessage);
            Assert.Equal(3, (await db.Repository.GetHistoryAsync(row.Item.Id)).Count);
            await vm.ReloadCommand.ExecuteAsync(null); // Explicit discard; now revision 3 is reviewed.
            await vm.DeleteCommand.ExecuteAsync(null);
            Assert.Null(vm.ErrorMessage);
            Assert.False(vm.IsExisting || vm.IsEditorOpen || vm.IsDirty);
            Assert.Empty(vm.History.Entries);
            Assert.Null(vm.History.SelectedEntry);
            Assert.Null(vm.History.Snapshot);
            var status = db.Provider.GetRequiredService<IDailyJournalStatusReader>();
            Assert.Empty(await status.GetAsync(row.Item.TradingDate, row.Item.TradingDate, null));
            Assert.Empty(await db.Repository.GetHistoryAsync(row.Item.Id));
            vm.OpenEditorCommand.Execute(null);
            vm.Text = "Recreated review";
            await vm.SaveCommand.ExecuteAsync(null);
            Assert.NotEqual(row.Item.Id, Assert.Single(vm.History.Entries).Item.Id);
        }
        finally { vm.Deactivate(); }
    }

    [Fact]
    public async Task EditorSaveCompleteReopenRefreshesHistoryWhileViewingOldSnapshotsNeverWrites()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var reader = db.Provider.GetRequiredService<IDailyJournalHistoryReader>();
        var vm = new JournalViewModel(db.Repository, db.Provider.GetRequiredService<ITradingAccountReader>(), new FakeDialogService(),
            db.CreateTradeContext(), new JournalHistoryViewModelTests.Clock(), reader);
        try
        {
            await vm.ActivateAsync();
            vm.OpenEditorCommand.Execute(null);
            Assert.Empty(vm.History!.Entries);
            vm.Text = "  freeform\r\n";
            vm.WentWell = "I waited";
            vm.NeedsImprovement = "My patience";
            vm.NextTradingDay = "Follow the plan";
            await vm.SaveCommand.ExecuteAsync(null);
            var row = Assert.Single(vm.History.Entries);
            Assert.False(row.Item.IsDraft);
            Assert.Equal(1, row.Item.Revision);
            await vm.History.OpenCommand.ExecuteAsync(row);
            await vm.LoadTask;
            vm.OpenEditorCommand.Execute(null);
            await vm.CompleteReviewCommand.ExecuteAsync(null);
            Assert.False(Assert.Single(vm.History.Entries).Item.IsDraft);
            Assert.False(vm.History.SelectedEntry!.Item.IsDraft);
            Assert.Equal(1, vm.History.Revisions[0].Item.Revision);
            await vm.ReopenReviewCommand.ExecuteAsync(null);
            Assert.False(Assert.Single(vm.History.Entries).Item.IsDraft);
            Assert.Equal(1, vm.History.Revisions[0].Item.Revision);
            vm.OpenEditorCommand.Execute(null);
            vm.Text = "new unsaved text";
            vm.NextTradingDay = "new unsaved answer";
            await vm.History.ViewRevisionCommand.ExecuteAsync(vm.History.Revisions.Single(r => r.Item.Revision == 1));
            Assert.Equal("  freeform\r\n", vm.History.Snapshot!.Text);
            Assert.False(vm.History.Snapshot.IsDraft);
            Assert.Equal(new DailyReviewAnswers("I waited", "My patience", "Follow the plan"), vm.History.Snapshot.Review);
            Assert.Equal("new unsaved text", vm.Text);
            Assert.Equal("new unsaved answer", vm.NextTradingDay);
            Assert.True(vm.IsDirty);
            vm.History.CloseViewCommand.Execute(null);
            Assert.Null(vm.History.Snapshot);
            Assert.Equal(row.Item.Id, vm.History.SelectedEntry!.Item.Id);
            Assert.Equal("new unsaved text", vm.Text);
            Assert.Equal("new unsaved answer", vm.NextTradingDay);
            var date = vm.SelectedDate;
            var account = vm.SelectedAccount;
            vm.History.CloseReviewCommand.Execute(null);
            Assert.Null(vm.History.SelectedEntry);
            Assert.Empty(vm.History.Revisions);
            Assert.Equal(date, vm.SelectedDate);
            Assert.Same(account, vm.SelectedAccount);
            Assert.Equal("new unsaved text", vm.Text);
            Assert.Equal("new unsaved answer", vm.NextTradingDay);
            Assert.Equal(1, vm.Revision);
            Assert.Single(await db.Repository.GetHistoryAsync(row.Item.Id));
            await using var context = await db.ContextFactory.CreateDbContextAsync();
            Assert.Equal(1, await context.DailyJournalRevisions.CountAsync());
            Assert.Empty(await context.Trades.ToArrayAsync());
        }
        finally { vm.Deactivate(); await vm.LoadTask; }
        vm.OpenEditorCommand.Execute(null);
    }

    [Fact]
    public async Task HistoryOpenFreshLatestDoesNotRestoreSnapshotOrBypassRevisionConflict()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        DateOnly day = new(2026, 10, 5);
        var created = (await db.Repository.CreateAsync(new(day, null, "first"))).Journal!.Entry;
        var reader = db.Provider.GetRequiredService<IDailyJournalHistoryReader>();
        var vm = new JournalViewModel(db.Repository, db.Provider.GetRequiredService<ITradingAccountReader>(), new FakeDialogService(),
            db.CreateTradeContext(), new JournalHistoryViewModelTests.Clock(), reader);
        try
        {
            await vm.ActivateAsync();
            vm.OpenEditorCommand.Execute(null);
            await db.Repository.UpdateAsync(new(created.Id, 1, "external second", true));
            await vm.History!.RefreshCommand.ExecuteAsync(null);
            await vm.History.OpenCommand.ExecuteAsync(vm.History.Entries[0]);
            vm.History.OpenInEditorCommand.Execute(null);
            await vm.LoadTask;
            vm.OpenEditorCommand.Execute(null);
            Assert.Equal("external second", vm.Text);
            Assert.Equal(2, vm.Revision);
            vm.Text = "local draft";
            await db.Repository.UpdateAsync(new(created.Id, 2, "external third", true));
            await vm.SaveCommand.ExecuteAsync(null);
            Assert.Equal("local draft", vm.Text);
            Assert.Contains("Refresh required", vm.StatusText);
            await vm.History.RefreshCommand.ExecuteAsync(null);
            await vm.History.OpenCommand.ExecuteAsync(vm.History.Entries[0]); // Same scope: conflict must remain protected.
            await vm.History.ViewRevisionCommand.ExecuteAsync(vm.History.Revisions[^1]);
            Assert.Equal("first", vm.History.Snapshot!.Text);
            Assert.Equal("local draft", vm.Text);
            Assert.False(vm.SaveCommand.CanExecute(null));
            Assert.Equal("external third", (await db.Repository.GetAsync(day))!.Entry.Text);
            Assert.Equal(3, (await db.Repository.GetHistoryAsync(created.Id)).Count);
        }
        finally { vm.Deactivate(); await vm.LoadTask; }
        vm.OpenEditorCommand.Execute(null);
    }
}
