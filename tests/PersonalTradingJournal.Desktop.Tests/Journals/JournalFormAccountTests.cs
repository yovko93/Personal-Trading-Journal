using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalFormAccountTests
{
    private static readonly DateOnly Date = new(2026, 10, 7);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FormScopeIsIndependentOfTopFilterAndBothWriteActionsPersistAllFields(bool draft)
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        Guid account = await AccountAsync(db);
        var vm = new JournalViewModel(db.Repository, db.Provider.GetRequiredService<ITradingAccountReader>(),
            new FakeDialogService(), db.CreateTradeContext(), new FixedTimeProvider(), db.Provider.GetRequiredService<IDailyJournalHistoryReader>());
        try
        {
            await vm.ActivateAsync(); vm.SelectedDate = Date.ToDateTime(TimeOnly.MinValue); await vm.LoadTask;
            vm.OpenEditorCommand.Execute(null);
            Assert.Null(vm.EditorAccount.Id);
            SetFields(vm);
            vm.SelectedAccount = vm.Accounts.Single(a => a.Id == account); await vm.LoadTask;
            Assert.Null(vm.EditorAccount.Id); AssertFields(vm);
            vm.EditorAccount = vm.Accounts.Single(a => a.Id == account);
            vm.SelectedAccount = vm.Accounts.Single(a => a.Id is null); await vm.LoadTask;
            Assert.Equal(account, vm.EditorAccount.Id); AssertFields(vm);
            if (draft) await vm.SaveDraftAndCloseCommand.ExecuteAsync(null); else await vm.SaveCommand.ExecuteAsync(null);
            Assert.Null(vm.ErrorMessage); Assert.False(vm.IsEditorOpen);
            var entry = (await db.Repository.GetAsync(Date, account))!.Entry;
            Assert.Equal(draft, entry.IsDraft); Assert.Equal("Journal", entry.Text);
            Assert.Equal("Well", entry.Review.WentWell);
            Assert.Null(await db.Repository.GetAsync(Date));
            Assert.Null(vm.SelectedAccount.Id);
            Assert.Equal(account, vm.EditorAccount.Id);
            Assert.Equal(account, Assert.Single(vm.History!.Entries).Item.AccountId);
        }
        finally { vm.Deactivate(); }
    }

    [Fact]
    public async Task ExistingCompletedMoveCollisionAndStaleTokenRetainExactEntryAndAllLocalFields()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        Guid account = await AccountAsync(db);
        var source = (await db.Repository.CreateAsync(new(Date, null, "Original", false))).Journal!.Entry;
        var target = (await db.Repository.CreateAsync(new(Date, account, "Occupied"))).Journal!.Entry;
        var vm = await db.OpenEditorAsync(Date);
        await vm.ReopenReviewCommand.ExecuteAsync(null);
        SetFields(vm); vm.EditorAccount = vm.Accounts.Single(a => a.Id == account);
        await vm.SaveDraftAndCloseCommand.ExecuteAsync(null);
        Assert.True(vm.IsEditorOpen); AssertFields(vm); Assert.Equal(account, vm.EditorAccount.Id);
        Assert.Contains("Nothing was moved or merged", vm.ErrorMessage);
        Assert.Single(await db.Repository.GetHistoryAsync(source.Id));
        await db.Repository.DeleteAsync(new(target.Id, target.Revision));
        await vm.SaveDraftAndCloseCommand.ExecuteAsync(null);
        Assert.Null(vm.ErrorMessage); Assert.False(vm.IsEditorOpen);
        Assert.Null(await db.Repository.GetAsync(Date));
        var moved = (await db.Repository.GetAsync(Date, account))!.Entry;
        Assert.Equal(source.Id, moved.Id); Assert.True(moved.IsDraft); Assert.Equal(2, moved.Revision);
        Assert.Equal(2, (await db.Repository.GetHistoryAsync(source.Id)).Count);
        var fresh = await db.OpenEditorAsync(Date, account);
        Assert.Equal(account, fresh.EditorAccount.Id);
        await db.Repository.UpdateAsync(new(source.Id, 2, "Newer", true));
        fresh.EditorAccount = fresh.Accounts.Single(a => a.Id is null); SetFields(fresh);
        await fresh.SaveCommand.ExecuteAsync(null);
        Assert.True(fresh.IsEditorOpen); AssertFields(fresh);
        Assert.Contains("changed elsewhere", fresh.ErrorMessage);
        Assert.Null(await db.Repository.GetAsync(Date));
        Assert.Equal("Newer", (await db.Repository.GetAsync(Date, account))!.Entry.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InlineFormCanChooseAnotherScopeWithoutChangingCalendarAndRefreshesIndicators(bool calendarAccount)
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        Guid account = await AccountAsync(db);
        var calendar = new CalendarViewModel(db.Provider.GetRequiredService<ITradingCalendarReader>(), new FixedTimeProvider(),
            db.Provider.GetRequiredService<ITradingCalendarDayReader>(), db.Provider.GetRequiredService<ITradingAccountReader>(),
            journalStatusReader: db.Provider.GetRequiredService<IDailyJournalStatusReader>(), journalRepository: db.Repository,
            journalDialogs: new FakeDialogService { ConfirmationResult = true });
        try
        {
            await calendar.ActivateAsync();
            if (calendarAccount) { calendar.SelectedAccount = calendar.Accounts.Single(a => a.Id == account); await calendar.LoadTask; }
            var day = calendar.Weeks[0].Days[5];
            await calendar.SelectDayCommand.ExecuteAsync(day);
            await calendar.OpenInlineJournalCommand.ExecuteAsync(null);
            var vm = calendar.InlineJournal!;
            Assert.Equal(calendarAccount ? account : (Guid?)null, vm.EditorAccount.Id);
            Guid? target = calendarAccount ? null : account;
            vm.EditorAccount = vm.Accounts.Single(a => a.Id == target); SetFields(vm);
            var details = calendar.DayDetails;
            await vm.SaveCommand.ExecuteAsync(null); await calendar.JournalLoadTask;
            Assert.Null(vm.ErrorMessage); Assert.False(vm.IsEditorOpen); Assert.True(vm.IsCompleted);
            if (calendarAccount) Assert.Null(calendar.InlineJournal);
            else Assert.Same(vm, calendar.InlineJournal);
            Assert.Same(details, calendar.DayDetails);
            Assert.Equal(calendarAccount ? account : (Guid?)null, calendar.SelectedAccount.Id);
            Assert.Equal(!calendarAccount, day.HasJournal);
            var entry = (await db.Repository.GetAsync(day.Date, target))!.Entry;
            Assert.False(entry.IsDraft);
            if (calendarAccount)
            {
                calendar.SelectedAccount = calendar.Accounts.Single(a => a.Id is null);
                await calendar.LoadTask;
                await calendar.OpenDayJournalCommand.ExecuteAsync(calendar.DayJournals.Single(r => r.Id == entry.Id));
                vm = calendar.InlineJournal!;
                Assert.Equal(entry.Id, vm.JournalId);
            }
            await vm.ReopenReviewCommand.ExecuteAsync(null);
            vm.EditorAccount = vm.Accounts.Single(a => a.Id == (calendarAccount ? account : (Guid?)null));
            Assert.True(vm.IsDirty);
            vm.EditorAccount = vm.Accounts.Single(a => a.Id == target);
            Assert.False(vm.IsDirty);
            await vm.SaveDraftAndCloseCommand.ExecuteAsync(null);
            Assert.True(vm.IsCompleted); Assert.False(vm.IsEditorOpen);
            Assert.Single(await db.Repository.GetHistoryAsync(entry.Id));
            await vm.ReopenReviewCommand.ExecuteAsync(null);
            vm.EditorAccount = vm.Accounts.Single(a => a.Id == (calendarAccount ? account : (Guid?)null));
            vm.WentWell = "Changed";
            await vm.SaveDraftAndCloseCommand.ExecuteAsync(null); await calendar.JournalLoadTask;
            Assert.Null(vm.ErrorMessage); Assert.False(vm.IsEditorOpen);
            Assert.True(day.HasJournal); Assert.Equal("Draft", day.JournalStatusText);
            var moved = (await db.Repository.GetAsync(day.Date, calendarAccount ? account : null))!.Entry;
            Assert.Equal(entry.Id, moved.Id); Assert.Equal(2, moved.Revision); Assert.True(moved.IsDraft);
            Assert.Null(await db.Repository.GetAsync(day.Date, target));
        }
        finally { calendar.TryCloseInlineJournal(); calendar.Deactivate(); }
    }

    [Fact]
    public async Task RevisionConflictAfterTargetCollisionCannotBeClearedByChangingTheFormAccount()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        Guid account = await AccountAsync(db);
        var source = (await db.Repository.CreateAsync(new(Date, null, "Original"))).Journal!.Entry;
        await db.Repository.CreateAsync(new(Date, account, "Occupied"));
        var vm = await db.OpenEditorAsync(Date);
        vm.EditorAccount = vm.Accounts.Single(a => a.Id == account); SetFields(vm);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains("Nothing was moved or merged", vm.ErrorMessage);
        await db.Repository.UpdateAsync(new(source.Id, source.Revision, "Newer", true));
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains("changed elsewhere", vm.ErrorMessage);
        vm.EditorAccount = vm.Accounts.Single(a => a.Id is null);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.True(vm.IsEditorOpen); AssertFields(vm);
        Assert.Equal("Newer", (await db.Repository.GetAsync(Date))!.Entry.Text);
    }

    private static void SetFields(JournalViewModel vm)
    { vm.Text = "Journal"; vm.WentWell = "Well"; vm.NeedsImprovement = "Improve"; vm.NextTradingDay = "Next"; }
    private static void AssertFields(JournalViewModel vm) => Assert.Equal(new[] { "Journal", "Well", "Improve", "Next" },
        new[] { vm.Text, vm.WentWell, vm.NeedsImprovement, vm.NextTradingDay });
    private static async Task<Guid> AccountAsync(JournalSqliteTests.JournalTestDatabase db)
    {
        var account = new TradingAccount("P 21", TradingAccountType.Personal, null, null, "USD", 0m, DateTimeOffset.UtcNow);
        account.Deactivate(DateTimeOffset.UtcNow);
        await db.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        return account.Id;
    }
}
