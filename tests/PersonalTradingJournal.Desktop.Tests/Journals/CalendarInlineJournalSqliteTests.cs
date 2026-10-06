using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class CalendarInlineJournalSqliteTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InlineLifecycleUsesExactScopeAndRefreshesWithoutChangingTradesOrStandaloneDraft(bool scoped)
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var account = new TradingAccount("Inactive history", TradingAccountType.Personal, null, null, "USD", 0m, DateTimeOffset.UtcNow);
        account.Deactivate(DateTimeOffset.UtcNow);
        await db.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        var dialogs = new FakeDialogService();
        var calendar = new CalendarViewModel(db.Provider.GetRequiredService<ITradingCalendarReader>(), new FixedTimeProvider(),
            db.Provider.GetRequiredService<ITradingCalendarDayReader>(), db.Provider.GetRequiredService<ITradingAccountReader>(),
            journalStatusReader: db.Provider.GetRequiredService<IDailyJournalStatusReader>(), journalRepository: db.Repository, journalDialogs: dialogs);
        try
        {
            await calendar.ActivateAsync();
            if (scoped) { calendar.SelectedAccount = calendar.Accounts.Single(a => a.Id == account.Id); await calendar.LoadTask; }
            calendar.SelectedCurrency = "USD";
            await calendar.LoadTask;
            var day = calendar.Weeks.SelectMany(w => w.Days).First(d => d.IsSaturday);
            await calendar.SelectDayCommand.ExecuteAsync(day);
            var trades = calendar.DayDetails;
            var month = calendar.SelectedMonth;
            var standalone = await db.OpenEditorAsync(day.Date, scoped ? account.Id : null);
            standalone.OpenEditorCommand.Execute(null);
            standalone.Text = "Standalone unsaved text";
            int navigations = 0;
            calendar.OpenJournalAsync = (_, _) => { navigations++; return Task.CompletedTask; };
            await calendar.OpenInlineJournalCommand.ExecuteAsync(null);
            var journal = Assert.IsType<JournalViewModel>(calendar.InlineJournal);
            Assert.NotSame(standalone, journal);
            Assert.True(journal.IsEditorOpen && journal.CanEdit);
            Assert.Equal(day.Date.ToDateTime(TimeOnly.MinValue), journal.SelectedDate);
            Assert.Equal(scoped ? account.Id : (Guid?)null, journal.SelectedAccount.Id);
            Assert.Null(journal.History);
            Assert.False(journal.TradeContext.HasResult); // Existing modal Trades, not another query/panel.
            journal.Text = "Saved inside modal";
            journal.WentWell = "Patience";
            journal.NeedsImprovement = "Discipline";
            journal.NextTradingDay = "Wait";
            Assert.False(calendar.TryCloseDayDialog());
            Assert.Same(journal, calendar.InlineJournal);
            journal.CloseEditorCommand.Execute(null); // Cancel + keep editing.
            Assert.True(journal.IsEditorOpen);
            await journal.SaveCommand.ExecuteAsync(null);
            await calendar.JournalLoadTask;
            Assert.False(journal.IsEditorOpen);
            Assert.Equal("Draft", day.JournalStatusText);
            calendar.CloseInlineJournalCommand.Execute(null);
            Assert.Null(calendar.InlineJournal);
            await calendar.OpenInlineJournalCommand.ExecuteAsync(null);
            journal = calendar.InlineJournal!;
            Assert.True(journal.IsEditorOpen); // Continue.
            await journal.CompleteReviewCommand.ExecuteAsync(null);
            await calendar.JournalLoadTask;
            Assert.Equal("Completed", day.JournalStatusText);
            calendar.CloseInlineJournalCommand.Execute(null);
            await calendar.OpenInlineJournalCommand.ExecuteAsync(null);
            journal = calendar.InlineJournal!;
            Assert.True(journal.IsCompleted && journal.IsReadOnly);
            Assert.False(journal.IsEditorOpen);
            await journal.ReopenReviewCommand.ExecuteAsync(null);
            await calendar.JournalLoadTask;
            Assert.True(journal.IsEditorOpen);
            journal.Text = "Unsaved inline edit";
            dialogs.ConfirmationResult = true;
            journal.CloseEditorCommand.Execute(null);
            Assert.False(journal.IsEditorOpen);
            Assert.Equal("Saved inside modal", journal.Text);
            var persisted = (await db.Repository.GetAsync(day.Date, scoped ? account.Id : null))!.Entry;
            await db.Repository.UpdateAsync(new(persisted.Id, persisted.Revision, "Newer external text", true, persisted.Review));
            journal.OpenEditorCommand.Execute(null);
            journal.Text = "Keep on conflict";
            await journal.SaveCommand.ExecuteAsync(null);
            Assert.True(journal.IsEditorOpen && journal.IsDirty);
            Assert.Equal("Keep on conflict", journal.Text);
            Assert.Equal("Patience", journal.WentWell);
            Assert.NotNull(journal.ErrorMessage);
            await journal.ReloadCommand.ExecuteAsync(null); // Explicit, confirmed discard loads the newer revision.
            await journal.DeleteCommand.ExecuteAsync(null);
            await calendar.JournalLoadTask;
            Assert.False(day.HasJournal);
            Assert.False(journal.IsExisting);
            Assert.Null(await db.Repository.GetAsync(day.Date, scoped ? account.Id : null));
            Assert.Same(trades, calendar.DayDetails);
            Assert.Equal(month, calendar.SelectedMonth);
            Assert.Equal(day.Date, calendar.SelectedDate);
            Assert.Equal("USD", calendar.SelectedCurrency);
            Assert.Equal("Standalone unsaved text", standalone.Text);
            Assert.True(standalone.IsDirty);
            Assert.Equal(0, navigations);
        }
        finally { calendar.TryCloseInlineJournal(); calendar.Deactivate(); }
    }

    [Fact]
    public async Task ConflictRetainsAllFieldsAndCloseCancellationRejectsLateRead()
    {
        var repository = new FakeDailyJournalRepository();
        var dialogs = new FakeDialogService();
        var calendar = await CalendarPage.CalendarSummaryFixture.CreateAsync(journalStatusReader: repository, journalRepository: repository, journalDialogs: dialogs);
        var day = calendar.Weeks[0].Days[5];
        await calendar.SelectDayCommand.ExecuteAsync(day);
        await calendar.OpenInlineJournalCommand.ExecuteAsync(null);
        var editor = calendar.InlineJournal!;
        await editor.SaveCommand.ExecuteAsync(null);
        await calendar.JournalLoadTask;
        var saved = (await repository.GetAsync(day.Date))!.Entry;
        await repository.UpdateAsync(new(saved.Id, saved.Revision, "Newer", true));
        editor.OpenEditorCommand.Execute(null);
        editor.Text = "Local"; editor.WentWell = "One"; editor.NeedsImprovement = "Two"; editor.NextTradingDay = "Three";
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.True(editor.IsEditorOpen && editor.IsDirty);
        Assert.Equal(new[] { "Local", "One", "Two", "Three" }, new[] { editor.Text, editor.WentWell, editor.NeedsImprovement, editor.NextTradingDay });
        Assert.NotNull(editor.ErrorMessage);
        Assert.False(calendar.TryCloseDayDialog());
        dialogs.ConfirmationResult = true;
        Assert.True(calendar.TryCloseInlineJournal());
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<DailyJournalDetails?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        repository.Read = (_, _, ct) => { token = ct; started.SetResult(); return delayed.Task; };
        var opening = calendar.OpenInlineJournalCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(calendar.TryCloseInlineJournal());
        Assert.True(token.IsCancellationRequested);
        repository.Read = null;
        delayed.SetResult(new(saved, DailyJournalAccountState.AllAccounts, null));
        await opening;
        Assert.Null(calendar.InlineJournal);
        await calendar.OpenInlineJournalCommand.ExecuteAsync(null);
        Assert.Equal("Newer", calendar.InlineJournal!.Text);
        calendar.Deactivate();
    }
}
