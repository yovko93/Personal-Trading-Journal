using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class CalendarAggregateJournalTests
{
    [Fact]
    public async Task P21OnOctober7AppearsInAllAccountsAndMixedStatesRefreshWithoutChangingExactEditorScope()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var date = new DateOnly(2026, 10, 7);
        var p21 = new TradingAccount("P 21", TradingAccountType.Personal, null, null, "USD", 0m, DateTimeOffset.UtcNow);
        var other = new TradingAccount("Inactive", TradingAccountType.Personal, null, null, "EUR", 0m, DateTimeOffset.UtcNow);
        other.Deactivate(DateTimeOffset.UtcNow);
        var store = db.Provider.GetRequiredService<ITradingAccountStore>();
        await store.AddAsync(p21); await store.AddAsync(other);
        var p21Entry = (await db.Repository.CreateAsync(new(date, p21.Id, "P21 completed", false))).Journal!.Entry;
        var calendar = new CalendarViewModel(db.Provider.GetRequiredService<ITradingCalendarReader>(), new Clock(),
            db.Provider.GetRequiredService<ITradingCalendarDayReader>(), db.Provider.GetRequiredService<ITradingAccountReader>(),
            journalStatusReader: db.Provider.GetRequiredService<IDailyJournalStatusReader>(), journalRepository: db.Repository,
            journalDialogs: new FakeDialogService { ConfirmationResult = true });
        try
        {
            await calendar.ActivateAsync();
            CalendarDayCell Cell() => calendar.Weeks.SelectMany(w => w.Days).Single(d => d.Date == date);
            await calendar.SelectDayCommand.ExecuteAsync(Cell());
            Assert.Equal("✓", Cell().JournalIndicatorText);
            Assert.Equal(1, Cell().JournalCount);
            Assert.Null(calendar.SelectedDayJournalStatus); // Aggregate marker does not open P21 implicitly.
            Assert.Equal("Add Journal", calendar.DayJournalActionText);
            await calendar.OpenInlineJournalCommand.ExecuteAsync(null);
            Assert.Null(calendar.InlineJournal!.SelectedAccount.Id);
            Assert.False(calendar.InlineJournal.IsExisting);
            calendar.CloseInlineJournalCommand.Execute(null);

            var another = (await db.Repository.CreateAsync(new(date, other.Id, "Other draft"))).Journal!.Entry;
            var all = (await db.Repository.CreateAsync(new(date, null, "All completed", false))).Journal!.Entry;
            calendar.OnJournalCommitted(); await calendar.JournalLoadTask;
            Assert.Equal(3, Cell().JournalCount);
            Assert.Equal("Draft", Cell().JournalIndicatorText);
            Assert.Contains("1 Draft, 2 Completed", Cell().JournalAccessibleDescription);
            Assert.Equal(all.Id, calendar.SelectedDayJournalStatus!.JournalId);
            Assert.Equal("Open Journal", calendar.DayJournalActionText);
            calendar.SelectedAccount = calendar.Accounts.Single(a => a.Id == p21.Id);
            await calendar.LoadTask;
            Assert.Equal("✓", Cell().JournalIndicatorText);
            Assert.Equal(p21Entry.Id, calendar.SelectedDayJournalStatus!.JournalId);
            await calendar.OpenInlineJournalCommand.ExecuteAsync(null);
            var editor = calendar.InlineJournal!;
            await editor.ReopenReviewCommand.ExecuteAsync(null);
            editor.NextTradingDay = "Changed plan";
            await editor.SaveDraftAndCloseCommand.ExecuteAsync(null);
            await calendar.JournalLoadTask;
            Assert.Equal("Draft", Cell().JournalIndicatorText);
            editor.OpenEditorCommand.Execute(null);
            await editor.SaveCommand.ExecuteAsync(null);
            await calendar.JournalLoadTask;
            Assert.Equal("✓", Cell().JournalIndicatorText);
            await editor.DeleteCommand.ExecuteAsync(null);
            await calendar.JournalLoadTask;
            Assert.False(Cell().HasJournal);
            calendar.CloseInlineJournalCommand.Execute(null);
            calendar.SelectedAccount = calendar.Accounts.Single(a => a.Id is null);
            await calendar.LoadTask;
            Assert.Equal(2, Cell().JournalCount);
            Assert.Contains("1 Draft, 1 Completed", Cell().JournalAccessibleDescription);
            await db.Repository.DeleteAsync(new(another.Id, another.Revision));
            calendar.OnJournalCommitted(); await calendar.JournalLoadTask;
            Assert.Equal("✓", Cell().JournalIndicatorText);
            await db.Repository.DeleteAsync(new(all.Id, all.Revision));
            calendar.OnJournalCommitted(); await calendar.JournalLoadTask;
            Assert.False(Cell().HasJournal);
            Assert.Empty(calendar.MonthlySummaries);
        }
        finally { calendar.Deactivate(); }
    }
    private sealed class Clock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 10, 7, 14, 0, 0, TimeSpan.Zero); }
}
