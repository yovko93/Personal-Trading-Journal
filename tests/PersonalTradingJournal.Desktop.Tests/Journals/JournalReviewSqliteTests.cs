using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Domain.Journals;
using JournalTestDatabase = PersonalTradingJournal.Desktop.Tests.Journals.JournalSqliteTests.JournalTestDatabase;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalReviewSqliteTests
{
    private static readonly DateOnly Day = new(2026, 11, 1);

    [Fact]
    public async Task EmptyTradingDayPartialDraftCompletionReopenAndEditRoundTripExactDurableSnapshots()
    {
        await using JournalTestDatabase database = await JournalTestDatabase.CreateAsync();
        var editor = await database.OpenEditorAsync(Day);
        Assert.True(editor.TradeContext.IsEmpty);
        var tradingData = await database.ReadTradeDataAsync();
        const string well = "  I stayed patient.\r\nТърпение 📈\t ";
        editor.WentWell = well;
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.Null(editor.ErrorMessage);
        Assert.True(editor.IsCompleted);
        Assert.Equal(1L, editor.Revision);
        Assert.Empty(editor.Text);
        Assert.Empty(editor.NeedsImprovement);
        Assert.Empty(editor.NextTradingDay);

        Assert.False(editor.IsEditorOpen);
        await editor.ReopenReviewCommand.ExecuteAsync(null);
        editor.NeedsImprovement = "  I chased one entry.\n";
        editor.NextTradingDay = "I will wait for confirmation.  ";
        var completedAnswers = new DailyReviewAnswers(well, editor.NeedsImprovement, editor.NextTradingDay);
        await editor.CompleteReviewCommand.ExecuteAsync(null);
        Assert.Null(editor.ErrorMessage);
        Assert.True(editor.IsCompleted);
        Assert.True(editor.IsReadOnly);
        Assert.True(editor.CanReadContent);
        Assert.Equal(3L, editor.Revision);
        Assert.False(editor.IsDirty);
        Assert.Empty(editor.Text);
        Assert.True(editor.TradeContext.IsEmpty);

        var loaded = (await database.Repository.GetAsync(Day))!;
        Assert.Equal(completedAnswers, loaded.Entry.Review);
        Assert.False(loaded.Entry.IsDraft);
        var second = await database.OpenEditorAsync(Day);
        Assert.Equal(well, second.WentWell);
        Assert.Equal(editor.NeedsImprovement, second.NeedsImprovement);
        Assert.Equal(editor.NextTradingDay, second.NextTradingDay);
        second.Text = "A completed journal cannot be changed.";
        second.WentWell = "Nor can its answers.";
        Assert.Empty(second.Text);
        Assert.Equal(well, second.WentWell);
        Assert.False(second.SaveCommand.CanExecute(null));

        await second.ReopenReviewCommand.ExecuteAsync(null);
        Assert.Null(second.ErrorMessage);
        Assert.True(second.IsDraft);
        Assert.True(second.CanEdit);
        Assert.Equal(4L, second.Revision);
        Assert.Equal(Day.ToDateTime(TimeOnly.MinValue), second.SelectedDate);
        Assert.Null(second.SelectedAccount.Id);
        second.NeedsImprovement = "More patience at the open.";
        await second.SaveCommand.ExecuteAsync(null);
        Assert.Equal(5L, second.Revision);
        second.OpenEditorCommand.Execute(null);
        await second.CompleteReviewCommand.ExecuteAsync(null);
        Assert.Equal(5L, second.Revision);

        IReadOnlyList<DailyJournalRevision> history = await database.Repository.GetHistoryAsync(loaded.Entry.Id);
        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, history.Select(r => r.Revision));
        Assert.Equal(new[] { false, true, false, true, false }, history.Select(r => r.IsDraft));
        Assert.Equal(new DailyReviewAnswers(well, "", ""), history[0].Review);
        Assert.Equal(history[0].Review, history[1].Review);
        Assert.Equal(completedAnswers, history[2].Review);
        Assert.Equal(completedAnswers, history[3].Review);
        Assert.Equal("More patience at the open.", history[4].Review!.NeedsImprovement);
        Assert.All(history, revision => Assert.Empty(revision.Text));
        Assert.Equivalent(tradingData, await database.ReadTradeDataAsync(), strict: true);
    }

    [Fact]
    public async Task StaleCompletionAndReopenKeepLocalAnswersUntilExplicitReloadAndNeverOverwriteWinner()
    {
        await using JournalTestDatabase database = await JournalTestDatabase.CreateAsync();
        var original = new DailyReviewAnswers("A solid plan.", "Be patient.", "Wait for confirmation.");
        var created = await database.Repository.CreateAsync(new(Day, null, "", Review: original));
        var first = await database.OpenEditorAsync(Day);
        var dialogs = new FakeDialogService();
        var stale = await database.OpenEditorAsync(Day, dialogs: dialogs);
        stale.WentWell = "  My local answer must be kept.\r\n ";
        await first.CompleteReviewCommand.ExecuteAsync(null);
        await stale.CompleteReviewCommand.ExecuteAsync(null);
        Assert.Contains("changed elsewhere", stale.ErrorMessage);
        Assert.Equal("  My local answer must be kept.\r\n ", stale.WentWell);
        Assert.True(stale.IsDirty);
        Assert.True(stale.IsDraft);
        Assert.False(stale.SaveCommand.CanExecute(null));
        Assert.False(stale.CompleteReviewCommand.CanExecute(null));
        Assert.Equal(original, (await database.Repository.GetAsync(Day))!.Entry.Review);
        Assert.Equal(2, (await database.Repository.GetHistoryAsync(created.Journal!.Entry.Id)).Count);

        await stale.ReloadCommand.ExecuteAsync(null); // Keep editing does not discard the local answer.
        Assert.True(stale.IsDirty);
        Assert.Equal("  My local answer must be kept.\r\n ", stale.WentWell);
        dialogs.ConfirmationResult = true;
        await stale.ReloadCommand.ExecuteAsync(null);
        Assert.False(stale.IsDirty);
        Assert.True(stale.IsCompleted);
        Assert.Equal(original.WentWell, stale.WentWell);
        await first.ReopenReviewCommand.ExecuteAsync(null);
        await stale.ReopenReviewCommand.ExecuteAsync(null);
        Assert.Contains("changed elsewhere", stale.ErrorMessage);
        Assert.True(stale.IsCompleted);
        Assert.Equal(2L, stale.Revision);
        Assert.False(stale.ReopenReviewCommand.CanExecute(null));
        Assert.Equal(3L, (await database.Repository.GetAsync(Day))!.Entry.Revision);
        await stale.ReloadCommand.ExecuteAsync(null);
        Assert.True(stale.IsDraft);
        Assert.Equal(3L, stale.Revision);
        stale.NextTradingDay = "Wait for the confirmed signal.";
        await stale.SaveCommand.ExecuteAsync(null);
        Assert.Null(stale.ErrorMessage);
        Assert.Equal(4L, stale.Revision);
        var history = await database.Repository.GetHistoryAsync(created.Journal.Entry.Id);
        Assert.Equal(new[] { true, false, true, false }, history.Select(r => r.IsDraft));
        Assert.Equal(original, history[0].Review);
        Assert.Equal(original, history[1].Review);
        Assert.Equal(original, history[2].Review);
        Assert.Equal(stale.NextTradingDay, history[3].Review!.NextTradingDay);
    }
}
