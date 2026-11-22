using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalEditorActionTests
{
    [Theory]
    [InlineData(false, 0, false)]
    [InlineData(false, 1, false)]
    [InlineData(false, 2, false)]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, false)]
    [InlineData(true, 2, false)]
    [InlineData(false, 0, true)]
    [InlineData(false, 1, true)]
    [InlineData(false, 2, true)]
    [InlineData(true, 0, true)]
    [InlineData(true, 1, true)]
    [InlineData(true, 2, true)]
    public async Task SaveAndCancelPersistEveryFieldAndCollapseOnlyTheirEditor(bool inline, int answersFilled, bool draft)
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var accounts = db.Provider.GetRequiredService<ITradingAccountReader>();
        CalendarViewModel? calendar = null;
        JournalViewModel? editor = null;
        try
        {
            if (inline)
            {
                calendar = new CalendarViewModel(db.Provider.GetRequiredService<ITradingCalendarReader>(), new FixedTimeProvider(),
                    db.Provider.GetRequiredService<ITradingCalendarDayReader>(), accounts,
                    journalStatusReader: db.Provider.GetRequiredService<IDailyJournalStatusReader>(),
                    journalRepository: db.Repository, journalDialogs: new FakeDialogService());
                await calendar.ActivateAsync();
                await calendar.SelectDayCommand.ExecuteAsync(calendar.Weeks[0].Days[5]);
                await calendar.OpenInlineJournalCommand.ExecuteAsync(null);
                editor = calendar.InlineJournal!;
            }
            else
            {
                editor = new JournalViewModel(db.Repository, accounts, new FakeDialogService(), db.CreateTradeContext(),
                    new FixedTimeProvider(), db.Provider.GetRequiredService<IDailyJournalHistoryReader>());
                await editor.ActivateAsync();
                editor.OpenEditorCommand.Execute(null);
            }
            var dayData = calendar?.DayDetails;
            var month = calendar?.SelectedMonth;
            var date = DateOnly.FromDateTime(editor.SelectedDate!.Value);
            const string exact = "  Journal\r\nПлан 📈\t ";
            var answers = answersFilled == 0 ? DailyReviewAnswers.Empty :
                new DailyReviewAnswers("  Went well\r\n ", answersFilled == 1 ? " \t" : "Improve patience", answersFilled == 1 ? "" : "Wait next day");
            editor.Text = exact;
            editor.WentWell = answers.WentWell;
            editor.NeedsImprovement = answers.NeedsImprovement;
            editor.NextTradingDay = answers.NextTradingDay;
            await (draft ? editor.SaveDraftAndCloseCommand : editor.SaveCommand).ExecuteAsync(null);
            if (calendar is not null) await calendar.JournalLoadTask;

            Assert.Null(editor.ErrorMessage);
            Assert.False(editor.IsEditorOpen || editor.IsDirty);
            Assert.Equal(draft, editor.IsDraft);
            Assert.True(editor.ShowCompactReview && editor.IsReadOnly);
            Assert.Equal(!draft, editor.IsCompleted);
            Assert.Equal(exact, editor.SavedText);
            Assert.Equal(answers, editor.SavedReview);
            var stored = (await db.Repository.GetAsync(date))!.Entry;
            Assert.Null(stored.TradingAccountId);
            Assert.Equal(exact, stored.Text);
            Assert.Equal(answers, stored.Review);
            Assert.Equal(draft, stored.IsDraft);
            var revision = Assert.Single(await db.Repository.GetHistoryAsync(stored.Id));
            Assert.Equal(answers, revision.Review);
            Assert.Equal(exact, revision.Text);
            Assert.Equal(draft, revision.IsDraft);
            if (calendar is not null)
            {
                Assert.Same(editor, calendar.InlineJournal);
                Assert.Same(dayData, calendar.DayDetails);
                Assert.Equal(month, calendar.SelectedMonth);
                Assert.Equal(date, calendar.SelectedDate);
                Assert.Equal(draft, calendar.SelectedDayJournalStatus!.IsDraft);
                Assert.Equal(draft ? "Draft" : "Completed", calendar.Weeks.SelectMany(w => w.Days).Single(d => d.Date == date).JournalStatusText);
            }
            else
            {
                var historyRow = Assert.Single(editor.History!.Entries);
                Assert.Equal(stored.Id, historyRow.Item.Id);
                Assert.Equal(draft ? "Draft" : "Completed", historyRow.StateText);
            }
        }
        finally { editor?.Deactivate(); calendar?.Deactivate(); }
    }

    [Theory]
    [InlineData(false, "conflict", false)]
    [InlineData(false, "failure", false)]
    [InlineData(false, "cancellation", false)]
    [InlineData(true, "conflict", false)]
    [InlineData(true, "failure", false)]
    [InlineData(true, "cancellation", false)]
    [InlineData(false, "conflict", true)]
    [InlineData(false, "failure", true)]
    [InlineData(false, "cancellation", true)]
    [InlineData(true, "conflict", true)]
    [InlineData(true, "failure", true)]
    [InlineData(true, "cancellation", true)]
    public async Task UnsuccessfulSaveOrDraftCloseKeepsAllFieldsUntilExplicitDiscard(bool inline, string outcome, bool draft)
    {
        var source = new FakeDailyJournalRepository();
        var repository = new ControlledWrites(source);
        var dialogs = new FakeDialogService();
        CalendarViewModel? calendar = null;
        JournalViewModel editor;
        if (inline)
        {
            calendar = await CalendarSummaryFixture.CreateAsync(journalStatusReader: source, journalRepository: repository, journalDialogs: dialogs);
            await calendar.SelectDayCommand.ExecuteAsync(calendar.Weeks[0].Days[5]);
            await calendar.OpenInlineJournalCommand.ExecuteAsync(null);
            editor = calendar.InlineJournal!;
        }
        else
        {
            var accounts = new FakeTradingAccountReader();
            editor = new JournalViewModel(repository, accounts, dialogs,
                new JournalTradeContextViewModel(new FakeTradingCalendarDayReader(), accounts), new FixedTimeProvider());
            await editor.ActivateAsync();
            editor.OpenEditorCommand.Execute(null);
        }
        try
        {
            var date = editor.SelectedDate;
            var dayData = calendar?.DayDetails;
            editor.Text = "  Unsaved notes\r\n ";
            editor.WentWell = "Exact first answer";
            editor.NeedsImprovement = "Exact second answer";
            editor.NextTradingDay = "Exact next plan";
            var fields = new[] { editor.Text, editor.WentWell, editor.NeedsImprovement, editor.NextTradingDay };
            var saving = (draft ? editor.SaveDraftAndCloseCommand : editor.SaveCommand).ExecuteAsync(null);
            var command = await repository.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(draft, command.IsDraft);
            Assert.Equal(fields[0], command.Text);
            Assert.Equal(new DailyReviewAnswers(fields[1], fields[2], fields[3]), command.Review);
            Assert.False(editor.CloseEditorCommand.CanExecute(null));
            Assert.False(editor.SaveDraftAndCloseCommand.CanExecute(null));
            Assert.False(editor.TryLeave());
            if (calendar is not null) Assert.False(calendar.TryCloseDayDialog());
            if (outcome == "cancellation") editor.CancelOperationCommand.Execute(null);
            else if (outcome == "failure") repository.Result.SetException(new IOException("Synthetic write failure"));
            else repository.Result.SetResult(new(DailyJournalWriteStatus.Conflict, null));
            await saving;

            Assert.True(editor.IsEditorOpen && editor.IsDirty && editor.IsDraft);
            Assert.Equal(fields, new[] { editor.Text, editor.WentWell, editor.NeedsImprovement, editor.NextTradingDay });
            Assert.NotNull(editor.ErrorMessage);
            Assert.True(editor.ReloadCommand.CanExecute(null));
            Assert.Equal(date, editor.SelectedDate);
            Assert.Null(editor.SelectedAccount.Id);
            Assert.Equal(0, source.Writes);
            Assert.Null(dialogs.ConfirmationRequest); // Cancel/Draft save never asks to discard.
            editor.CloseEditorCommand.Execute(null); // Keep editing.
            Assert.True(editor.IsEditorOpen);
            Assert.Equal(fields, new[] { editor.Text, editor.WentWell, editor.NeedsImprovement, editor.NextTradingDay });
            dialogs.ConfirmationResult = true;
            editor.CloseEditorCommand.Execute(null); // Explicit discard.
            Assert.False(editor.IsEditorOpen || editor.IsDirty);
            Assert.False(editor.IsExisting); // Discarding an unsaved entry never creates an empty journal.
            Assert.Equal(0, source.Writes);
            if (calendar is not null)
            {
                Assert.Same(editor, calendar.InlineJournal);
                Assert.Same(dayData, calendar.DayDetails);
                Assert.Null(calendar.SelectedDayJournalStatus);
            }
        }
        finally
        {
            repository.Result.TrySetCanceled();
            if (editor.SaveCommand.ExecutionTask is { } writing) await writing;
            editor.Deactivate();
            calendar?.Deactivate();
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task GuardedCloseStillPreservesPersistedDraftAndItsHistory(bool inline, bool dirty)
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var accounts = db.Provider.GetRequiredService<ITradingAccountReader>();
        var date = new DateOnly(2026, 9, inline ? 5 : 9);
        var savedAnswers = new DailyReviewAnswers("Saved answer", "", "");
        var original = (await db.Repository.CreateAsync(new(date, null, "Saved Draft", true, savedAnswers))).Journal!.Entry;
        var historyBefore = await db.Repository.GetHistoryAsync(original.Id);
        var dialogs = new FakeDialogService();
        CalendarViewModel? calendar = null;
        JournalViewModel? editor = null;
        try
        {
            if (inline)
            {
                calendar = new CalendarViewModel(db.Provider.GetRequiredService<ITradingCalendarReader>(), new FixedTimeProvider(),
                    db.Provider.GetRequiredService<ITradingCalendarDayReader>(), accounts,
                    journalStatusReader: db.Provider.GetRequiredService<IDailyJournalStatusReader>(),
                    journalRepository: db.Repository, journalDialogs: dialogs);
                await calendar.ActivateAsync();
                await calendar.SelectDayCommand.ExecuteAsync(calendar.Weeks[0].Days[5]);
                await calendar.OpenInlineJournalCommand.ExecuteAsync(null);
                editor = calendar.InlineJournal!;
            }
            else editor = await db.OpenEditorAsync(date, dialogs: dialogs);
            Assert.True(editor.IsEditorOpen && editor.IsDraft);
            if (dirty)
            {
                editor.Text = "Local text";
                editor.WentWell = "Local well";
                editor.NeedsImprovement = "Local improvement";
                editor.NextTradingDay = "Local next";
                editor.CloseEditorCommand.Execute(null);
                Assert.True(editor.IsEditorOpen && editor.IsDirty);
                Assert.Equal("Local next", editor.NextTradingDay);
                dialogs.ConfirmationResult = true;
            }
            editor.CloseEditorCommand.Execute(null);
            Assert.False(editor.IsEditorOpen || editor.IsDirty);
            Assert.True(editor.IsDraft && editor.IsExisting && editor.ShowContinueReview);
            Assert.Equal("Saved Draft", editor.Text);
            Assert.Equal(savedAnswers, editor.SavedReview);
            var stored = (await db.Repository.GetAsync(date))!.Entry;
            Assert.Equal(original.Id, stored.Id);
            Assert.Equal(original.Revision, stored.Revision);
            Assert.True(stored.IsDraft);
            Assert.Equal("Saved Draft", stored.Text);
            Assert.Equal(savedAnswers, stored.Review);
            Assert.Equal(historyBefore, await db.Repository.GetHistoryAsync(original.Id));
            editor.OpenEditorCommand.Execute(null);
            Assert.True(editor.CanEdit);
            if (calendar is not null)
            {
                Assert.Same(editor, calendar.InlineJournal);
                Assert.Equal(date, calendar.SelectedDate);
                Assert.True(calendar.SelectedDayJournalStatus!.IsDraft);
            }
        }
        finally { editor?.Deactivate(); calendar?.Deactivate(); }
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(false, " \t\r\n")]
    [InlineData(false, "...🙂\u200B")]
    [InlineData(true, "")]
    [InlineData(true, " \t\r\n")]
    [InlineData(true, "...🙂\u200B")]
    public async Task EitherHostRejectsSaveWithoutMeaningfulContentAndRetainsEveryField(bool inline, string content)
    {
        var source = new FakeDailyJournalRepository();
        var accounts = new FakeTradingAccountReader();
        CalendarViewModel? calendar = null;
        JournalViewModel editor;
        if (inline)
        {
            calendar = await CalendarSummaryFixture.CreateAsync(journalStatusReader: source, journalRepository: source);
            await calendar.SelectDayCommand.ExecuteAsync(calendar.Weeks[0].Days[5]);
            await calendar.OpenInlineJournalCommand.ExecuteAsync(null);
            editor = calendar.InlineJournal!;
        }
        else
        {
            editor = new JournalViewModel(source, accounts, new FakeDialogService(),
                new JournalTradeContextViewModel(new FakeTradingCalendarDayReader(), accounts), new FixedTimeProvider());
            await editor.ActivateAsync();
            editor.OpenEditorCommand.Execute(null);
        }
        try
        {
            editor.Text = content;
            editor.WentWell = "Meaningful answer alone is insufficient";
            editor.NeedsImprovement = editor.NextTradingDay = content;
            await editor.SaveCommand.ExecuteAsync(null);
            Assert.Contains("Journal text is required", editor.JournalTextValidation);
            Assert.True(editor.IsEditorOpen);
            Assert.False(editor.IsExisting);
            Assert.Equal(new[] { content, "Meaningful answer alone is insufficient", content, content },
                new[] { editor.Text, editor.WentWell, editor.NeedsImprovement, editor.NextTradingDay });
            Assert.Equal(0, source.Writes);
            await editor.SaveDraftAndCloseCommand.ExecuteAsync(null);
            Assert.False(editor.IsEditorOpen);
            Assert.True(editor.IsDraft);
            Assert.Equal(1, source.Writes);
        }
        finally { editor.Deactivate(); calendar?.Deactivate(); }
    }

    private sealed class ControlledWrites(IDailyJournalRepository source) : IDailyJournalRepository
    {
        public TaskCompletionSource<CreateDailyJournalCommand> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<DailyJournalWriteResult> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<DailyJournalDetails?> GetAsync(DateOnly tradingDate, Guid? tradingAccountId = null, CancellationToken cancellationToken = default) =>
            source.GetAsync(tradingDate, tradingAccountId, cancellationToken);
        public Task<DailyJournalWriteResult> CreateAsync(CreateDailyJournalCommand command, CancellationToken cancellationToken = default)
        {
            Started.SetResult(command);
            return Result.Task.WaitAsync(cancellationToken);
        }
        public Task<DailyJournalWriteResult> UpdateAsync(UpdateDailyJournalCommand command, CancellationToken cancellationToken = default) =>
            source.UpdateAsync(command, cancellationToken);
        public Task<DailyJournalWriteResult> DeleteAsync(DeleteDailyJournalCommand command, CancellationToken cancellationToken = default) =>
            source.DeleteAsync(command, cancellationToken);
        public Task<IReadOnlyList<DailyJournalRevision>> GetHistoryAsync(Guid journalId, CancellationToken cancellationToken = default) =>
            source.GetHistoryAsync(journalId, cancellationToken);
    }
}
