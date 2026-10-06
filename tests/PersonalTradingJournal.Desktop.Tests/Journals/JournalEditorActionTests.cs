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
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SavePersistsEveryFieldAsDraftAndCollapsesOnlyItsEditor(bool inline, bool partial)
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
            var answers = new DailyReviewAnswers("  Went well\r\n ", partial ? " \t" : "Improve patience", partial ? "" : "Wait next day");
            editor.Text = exact;
            editor.WentWell = answers.WentWell;
            editor.NeedsImprovement = answers.NeedsImprovement;
            editor.NextTradingDay = answers.NextTradingDay;
            await editor.SaveCommand.ExecuteAsync(null);
            if (calendar is not null) await calendar.JournalLoadTask;

            Assert.Null(editor.ErrorMessage);
            Assert.False(editor.IsEditorOpen || editor.IsDirty || editor.IsCompleted);
            Assert.True(editor.ShowCompactReview && editor.IsDraft);
            Assert.Equal(exact, editor.SavedText);
            Assert.Equal(answers, editor.SavedReview);
            var stored = (await db.Repository.GetAsync(date))!.Entry;
            Assert.Null(stored.TradingAccountId);
            Assert.Equal(exact, stored.Text);
            Assert.Equal(answers, stored.Review);
            Assert.True(stored.IsDraft); // Even three meaningful answers do not complete a review on Save.
            var revision = Assert.Single(await db.Repository.GetHistoryAsync(stored.Id));
            Assert.Equal(answers, revision.Review);
            Assert.Equal(exact, revision.Text);
            Assert.True(revision.IsDraft);
            if (calendar is not null)
            {
                Assert.Same(editor, calendar.InlineJournal);
                Assert.Same(dayData, calendar.DayDetails);
                Assert.Equal(month, calendar.SelectedMonth);
                Assert.Equal(date, calendar.SelectedDate);
                Assert.True(calendar.SelectedDayJournalStatus!.IsDraft);
                Assert.Equal("Draft", calendar.Weeks.SelectMany(w => w.Days).Single(d => d.Date == date).JournalStatusText);
            }
            else
            {
                var historyRow = Assert.Single(editor.History!.Entries);
                Assert.Equal(stored.Id, historyRow.Item.Id);
                Assert.Equal("Draft", historyRow.StateText);
            }
        }
        finally { editor?.Deactivate(); calendar?.Deactivate(); }
    }

    [Theory]
    [InlineData(false, "conflict")]
    [InlineData(false, "failure")]
    [InlineData(false, "cancellation")]
    [InlineData(true, "conflict")]
    [InlineData(true, "failure")]
    [InlineData(true, "cancellation")]
    public async Task UnsuccessfulSaveAndCancelKeepAllFieldsUntilExplicitDiscard(bool inline, string outcome)
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
            var saving = editor.SaveCommand.ExecuteAsync(null);
            var command = await repository.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(command.IsDraft);
            Assert.Equal(fields[0], command.Text);
            Assert.Equal(new DailyReviewAnswers(fields[1], fields[2], fields[3]), command.Review);
            Assert.False(editor.CloseEditorCommand.CanExecute(null));
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
            editor.CloseEditorCommand.Execute(null); // Keep editing.
            Assert.True(editor.IsEditorOpen);
            Assert.Equal(fields, new[] { editor.Text, editor.WentWell, editor.NeedsImprovement, editor.NextTradingDay });
            dialogs.ConfirmationResult = true;
            editor.CloseEditorCommand.Execute(null); // Explicit discard.
            Assert.False(editor.IsEditorOpen || editor.IsDirty);
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
