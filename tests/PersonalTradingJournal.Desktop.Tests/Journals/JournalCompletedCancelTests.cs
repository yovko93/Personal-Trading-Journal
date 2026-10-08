using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalCompletedCancelTests
{
    [Theory]
    [InlineData(false, -1)] [InlineData(true, -1)] // untouched
    [InlineData(false, -2)] [InlineData(true, -2)] // all fields changed then restored
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    [InlineData(false, 3)] [InlineData(true, 3)]
    public async Task CancelComparesEveryLoadedFieldAndOnlyChangedContentAppendsDraft(bool inline, int changedField)
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var date = new DateOnly(2026, 10, 7);
        var original = (await db.Repository.CreateAsync(new(date, null, "  Original\r\n", false,
            new DailyReviewAnswers("Well", "Improve", "Next")))).Journal!.Entry;
        var dialogs = new FakeDialogService();
        var calendar = new CalendarViewModel(db.Provider.GetRequiredService<ITradingCalendarReader>(), new OctoberClock(),
            db.Provider.GetRequiredService<ITradingCalendarDayReader>(), db.Provider.GetRequiredService<ITradingAccountReader>(),
            journalStatusReader: db.Provider.GetRequiredService<IDailyJournalStatusReader>(), journalRepository: db.Repository, journalDialogs: dialogs);
        JournalViewModel? editor = null;
        try
        {
            await calendar.ActivateAsync();
            var cell = calendar.Weeks.SelectMany(w => w.Days).Single(d => d.Date == date);
            await calendar.SelectDayCommand.ExecuteAsync(cell);
            if (inline)
            {
                await calendar.OpenInlineJournalCommand.ExecuteAsync(null);
                editor = calendar.InlineJournal!;
            }
            else
            {
                editor = await db.OpenEditorAsync(date, dialogs: dialogs);
                editor.JournalDataCommitted += (_, _) => calendar.OnJournalCommitted();
            }
            int commits = 0;
            editor.JournalDataCommitted += (_, _) => commits++;
            await editor.ReopenReviewCommand.ExecuteAsync(null);
            Assert.True(editor.IsCompleted && editor.CanEdit);
            Assert.Single(await db.Repository.GetHistoryAsync(original.Id));
            string[] loaded = [editor.Text, editor.WentWell, editor.NeedsImprovement, editor.NextTradingDay];
            if (changedField == -2)
            {
                for (int i = 0; i < 4; i++) Set(editor, i, "temporary");
                for (int i = 0; i < 4; i++) Set(editor, i, loaded[i]);
            }
            else if (changedField >= 0) Set(editor, changedField, loaded[changedField] + " edited");
            var expected = (editor.Text, new DailyReviewAnswers(editor.WentWell, editor.NeedsImprovement, editor.NextTradingDay));
            await editor.SaveDraftAndCloseCommand.ExecuteAsync(null);
            await calendar.JournalLoadTask;
            Assert.False(editor.IsEditorOpen || editor.IsDirty);
            Assert.Null(dialogs.ConfirmationRequest);
            Assert.Null(editor.ErrorMessage);
            bool changed = changedField >= 0;
            var stored = (await db.Repository.GetAsync(date))!.Entry;
            Assert.Equal(changed, stored.IsDraft);
            Assert.Equal(expected, (stored.Text, stored.Review));
            Assert.Equal(changed ? 2 : 1, stored.Revision);
            var history = await db.Repository.GetHistoryAsync(original.Id);
            Assert.Equal(changed ? 2 : 1, history.Count);
            Assert.False(history[0].IsDraft);
            Assert.Equal(changed ? 1 : 0, commits);
            Assert.Equal(changed ? "Draft" : "✓", cell.JournalIndicatorText);
            Assert.Equal(date, calendar.SelectedDate);
            if (inline) Assert.Same(editor, calendar.InlineJournal);
        }
        finally { editor?.Deactivate(); calendar.Deactivate(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ChangedCompletedCancelRejectsConcurrentSaveAndPreservesAllLocalFields(bool inline)
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var date = new DateOnly(2026, 10, 7);
        var original = (await db.Repository.CreateAsync(new(date, null, "Original", false))).Journal!.Entry;
        var calendar = new CalendarViewModel(db.Provider.GetRequiredService<ITradingCalendarReader>(), new OctoberClock(),
            db.Provider.GetRequiredService<ITradingCalendarDayReader>(), db.Provider.GetRequiredService<ITradingAccountReader>(),
            journalStatusReader: db.Provider.GetRequiredService<IDailyJournalStatusReader>(), journalRepository: db.Repository, journalDialogs: new FakeDialogService());
        JournalViewModel? editor = null;
        try
        {
            await calendar.ActivateAsync();
            await calendar.SelectDayCommand.ExecuteAsync(calendar.Weeks.SelectMany(w => w.Days).Single(d => d.Date == date));
            if (inline) { await calendar.OpenInlineJournalCommand.ExecuteAsync(null); editor = calendar.InlineJournal!; }
            else editor = await db.OpenEditorAsync(date);
            await editor.ReopenReviewCommand.ExecuteAsync(null);
            editor.Text = "Local"; editor.WentWell = "Well"; editor.NeedsImprovement = "Improve"; editor.NextTradingDay = "Next";
            var winner = await db.Repository.UpdateAsync(new(original.Id, 1, "Winner", false, ReopenCompleted: true));
            Assert.Equal(DailyJournalWriteStatus.Updated, winner.Status);
            await editor.SaveDraftAndCloseCommand.ExecuteAsync(null);
            Assert.True(editor.IsEditorOpen && editor.IsDirty);
            Assert.Contains("changed elsewhere", editor.ErrorMessage);
            Assert.Equal(("Local", "Well", "Improve", "Next"), (editor.Text, editor.WentWell, editor.NeedsImprovement, editor.NextTradingDay));
            Assert.Equal("Winner", (await db.Repository.GetAsync(date))!.Entry.Text);
            Assert.Equal(2, (await db.Repository.GetHistoryAsync(original.Id)).Count);
        }
        finally { editor?.Deactivate(); calendar.Deactivate(); }
    }

    private static void Set(JournalViewModel vm, int field, string value)
    {
        switch (field) { case 0: vm.Text = value; break; case 1: vm.WentWell = value; break; case 2: vm.NeedsImprovement = value; break; case 3: vm.NextTradingDay = value; break; }
    }
    private sealed class OctoberClock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 10, 7, 14, 0, 0, TimeSpan.Zero); }
}
