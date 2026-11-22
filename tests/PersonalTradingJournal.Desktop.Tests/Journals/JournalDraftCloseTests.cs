using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalDraftCloseTests
{
    [Theory]
    [InlineData(false, "")]
    [InlineData(true, "")]
    [InlineData(false, " \t")]
    [InlineData(true, "...🙂")]
    public async Task EmptyNewCancelWritesNothingButNonemptyDraftIsNotDiscarded(bool inline, string text)
    {
        var repository = new FakeDailyJournalRepository();
        var dialogs = new FakeDialogService();
        var calendar = await CalendarSummaryFixture.CreateAsync(journalStatusReader: repository,
            journalRepository: repository, journalDialogs: dialogs);
        JournalViewModel? editor = null;
        try
        {
            if (inline)
            {
                await calendar.SelectDayCommand.ExecuteAsync(calendar.Weeks[0].Days[5]);
                await calendar.OpenInlineJournalCommand.ExecuteAsync(null);
                editor = calendar.InlineJournal!;
            }
            else
            {
                var accounts = new FakeTradingAccountReader();
                editor = new(repository, accounts, dialogs,
                    new JournalTradeContextViewModel(new FakeTradingCalendarDayReader(), accounts), new FixedTimeProvider());
                await editor.ActivateAsync();
                editor.OpenEditorCommand.Execute(null);
            }
            var date = editor.SelectedDate;
            var details = calendar.DayDetails;
            editor.Text = text;
            await editor.SaveDraftAndCloseCommand.ExecuteAsync(null);
            Assert.False(editor.IsEditorOpen || editor.IsDirty);
            Assert.Null(dialogs.ConfirmationRequest);
            Assert.Equal(text.Length == 0 ? 0 : 1, repository.Writes);
            Assert.Equal(text.Length != 0, editor.IsExisting);
            Assert.Equal(text, editor.Text);
            Assert.Equal(date, editor.SelectedDate);
            Assert.Same(details, calendar.DayDetails);
            if (inline) Assert.Same(editor, calendar.InlineJournal);
        }
        finally { editor?.Deactivate(); calendar.Deactivate(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DraftUpdateAppendsHistoryAndNoOpDoesNot(bool clearFields)
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var date = new DateOnly(2026, 9, 9);
        var original = (await db.Repository.CreateAsync(new(date, null, "Original", true,
            new DailyReviewAnswers("Original answer", "", "")))).Journal!.Entry;
        var dialogs = new FakeDialogService();
        var editor = await db.OpenEditorAsync(date, dialogs: dialogs);
        try
        {
            var text = clearFields ? "" : "  Changed\r\nnotes ";
            var answers = clearFields ? DailyReviewAnswers.Empty : new DailyReviewAnswers("Well", "Improve", "Next");
            editor.Text = text; editor.WentWell = answers.WentWell;
            editor.NeedsImprovement = answers.NeedsImprovement; editor.NextTradingDay = answers.NextTradingDay;
            await editor.SaveDraftAndCloseCommand.ExecuteAsync(null);
            Assert.False(editor.IsEditorOpen);
            Assert.True(editor.IsDraft);
            Assert.Null(dialogs.ConfirmationRequest);
            var current = (await db.Repository.GetAsync(date))!.Entry;
            Assert.Equal(original.Id, current.Id);
            Assert.Equal(2, current.Revision);
            Assert.Equal(text, current.Text); Assert.Equal(answers, current.Review);
            var history = await db.Repository.GetHistoryAsync(current.Id);
            Assert.Equal(2, history.Count);
            Assert.Equal("Original", history[0].Text);
            Assert.Equal(text, history[1].Text); Assert.True(history[1].IsDraft);
            editor.OpenEditorCommand.Execute(null);
            await editor.SaveDraftAndCloseCommand.ExecuteAsync(null);
            Assert.False(editor.IsEditorOpen);
            Assert.Equal(history, await db.Repository.GetHistoryAsync(current.Id));
        }
        finally { editor.Deactivate(); }
    }

    [Fact]
    public async Task StaleDraftCloseCannotOverwriteCompletedRevisionAndNavigationStillGuards()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var date = new DateOnly(2026, 9, 9);
        var original = (await db.Repository.CreateAsync(new(date, null, "Original"))).Journal!.Entry;
        var dialogs = new FakeDialogService();
        var editor = await db.OpenEditorAsync(date, dialogs: dialogs);
        try
        {
            editor.Text = "Local notes"; editor.WentWell = "Local answer";
            await db.Repository.UpdateAsync(new(original.Id, 1, "Newer completed", false, DailyReviewAnswers.Empty));
            await editor.SaveDraftAndCloseCommand.ExecuteAsync(null);
            Assert.Null(dialogs.ConfirmationRequest);
            Assert.True(editor.IsEditorOpen && editor.IsDirty);
            Assert.Contains("changed elsewhere", editor.ErrorMessage);
            Assert.Equal("Local notes", editor.Text); Assert.Equal("Local answer", editor.WentWell);
            Assert.False(editor.SaveDraftAndCloseCommand.CanExecute(null));
            Assert.False(editor.TryLeave());
            Assert.NotNull(dialogs.ConfirmationRequest);
            var current = (await db.Repository.GetAsync(date))!.Entry;
            Assert.False(current.IsDraft); Assert.Equal("Newer completed", current.Text);
            Assert.Equal(2, (await db.Repository.GetHistoryAsync(original.Id)).Count);
        }
        finally { editor.Deactivate(); }
    }
}
