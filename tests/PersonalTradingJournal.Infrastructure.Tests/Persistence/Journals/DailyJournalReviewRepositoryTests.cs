using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Infrastructure.Journals;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Journals;

public sealed partial class DailyJournalRepositoryTests
{
    [Fact]
    public async Task LegacyAnswersOnlyCompletionRemainsReadableWithoutMigrationAndRecompletionRequiresText()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var repository = GetRepository(db);
        var entry = (await repository.CreateAsync(new(TradingDate, null, "Originally valid", false, CompleteReview))).Journal!.Entry;
        // Reproduce data written by the former answers-only completion rule, in an isolated database.
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            await context.DailyJournals.Where(j => j.Id == entry.Id).ExecuteUpdateAsync(s => s.SetProperty(j => j.Text, ""));
            await context.DailyJournalRevisions.Where(j => j.JournalId == entry.Id).ExecuteUpdateAsync(s => s.SetProperty(j => j.Text, ""));
        }
        var loaded = (await repository.GetAsync(TradingDate))!.Entry;
        Assert.False(loaded.IsDraft);
        Assert.Empty(loaded.Text);
        Assert.Equal(CompleteReview, loaded.Review);
        Assert.Empty(Assert.Single(await repository.GetHistoryAsync(entry.Id)).Text);
        Assert.Equal(DailyJournalWriteStatus.Unchanged, (await repository.UpdateAsync(new(entry.Id, 1, "", false))).Status);
        await repository.UpdateAsync(new(entry.Id, 1, "", true));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpdateAsync(new(entry.Id, 2, "", false)));
        Assert.Equal(2, (await repository.GetHistoryAsync(entry.Id)).Count);
        Assert.True((await repository.GetAsync(TradingDate))!.Entry.IsDraft);
        await repository.UpdateAsync(new(entry.Id, 2, "Now includes Journal text", false, DailyReviewAnswers.Empty));
        var history = await repository.GetHistoryAsync(entry.Id);
        Assert.Equal(new[] { false, true, false }, history.Select(r => r.IsDraft));
        Assert.Empty(history[0].Text);
        Assert.Equal(CompleteReview, history[0].Review);
        Assert.Equal(DailyReviewAnswers.Empty, history[2].Review);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreeformOnlyCompletionAndPartialAnswerUpdateAreAtomicScopedAndRevisionChecked(bool scoped)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid? account = scoped ? await SeedAccountAsync(database, "Archived review", false) : null;
        var repository = GetRepository(database);
        const string text = "  Freeform only\r\nПлан 📈 ";
        var created = await repository.CreateAsync(new(TradingDate, account, text, false));
        Assert.Equal(DailyJournalWriteStatus.Created, created.Status);
        var entry = created.Journal!.Entry;
        Assert.False(entry.IsDraft);
        Assert.Equal(DailyReviewAnswers.Empty, entry.Review);
        await repository.UpdateAsync(new(entry.Id, 1, text, true)); // Explicit exact-content reopen.
        var partial = new DailyReviewAnswers("  One answer\r\n", "", " \t");
        var updated = await repository.UpdateAsync(new(entry.Id, 2, text, false, partial));
        Assert.Equal(DailyJournalWriteStatus.Updated, updated.Status);
        Assert.False(updated.Journal!.Entry.IsDraft);
        Assert.Equal(DailyJournalWriteStatus.Conflict,
            (await repository.UpdateAsync(new(entry.Id, 2, "stale", false, partial))).Status);
        var loaded = (await repository.GetAsync(TradingDate, account))!.Entry;
        Assert.Equal(account, loaded.TradingAccountId);
        Assert.Equal(TradingDate, loaded.TradingDate);
        Assert.Equal(text, loaded.Text);
        Assert.Equal(partial, loaded.Review);
        var history = await repository.GetHistoryAsync(entry.Id);
        Assert.Equal(new[] { false, true, false }, history.Select(r => r.IsDraft));
        Assert.Equal(new long[] { 1, 2, 3 }, history.Select(r => r.Revision));
        Assert.All(history, r => Assert.Equal(text, r.Text));
        Assert.Equal(partial, history[2].Review);
        await using var context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await context.Trades.ToArrayAsync());
    }

    [Fact]
    public async Task PartialAnswersAndOptionalTextRoundTripExactlyAndEveryEditHasHistory()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository repository = GetRepository(database);
        var partial = new DailyReviewAnswers("  План\r\n📈  ", "", "\t ");
        DailyJournalDetails created = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, null, "", true, partial))).Journal);
        var edited = new DailyReviewAnswers(partial.WentWell, "Waited too little\n", "");

        DailyJournalWriteResult save = await repository.UpdateAsync(new UpdateDailyJournalCommand(
            created.Entry.Id, 1, "", true, edited));
        DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(await repository.GetAsync(TradingDate));

        Assert.Equal(DailyJournalWriteStatus.Updated, save.Status);
        Assert.Equal("", loaded.Entry.Text);
        Assert.Equal(edited, loaded.Entry.Review);
        Assert.True(loaded.Entry.IsDraft);
        Assert.Collection(await repository.GetHistoryAsync(created.Entry.Id),
            revision => Assert.Equal(partial, revision.Review),
            revision => Assert.Equal(edited, revision.Review));
    }

    [Fact]
    public async Task ZeroTradeDayCompletesReopensAndEditsWithDurableScopedRevisions()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid account = await SeedAccountAsync(database, "Historical review", false);
        var clock = new JournalTestClock(CreatedAtUtc);
        var repository = new DailyJournalRepository(database.ContextFactory, clock);
        DailyJournalDetails original = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, account, "", true, CompleteReview))).Journal);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(DailyJournalWriteStatus.Updated, (await repository.UpdateAsync(
            new UpdateDailyJournalCommand(original.Entry.Id, 1, "Journal", false))).Status);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(DailyJournalWriteStatus.Updated, (await repository.UpdateAsync(
            new UpdateDailyJournalCommand(original.Entry.Id, 2, "Journal", true))).Status);
        var revisedAnswers = new DailyReviewAnswers("Kept discipline", CompleteReview.NeedsImprovement, CompleteReview.NextTradingDay);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(DailyJournalWriteStatus.Updated, (await repository.UpdateAsync(
            new UpdateDailyJournalCommand(original.Entry.Id, 3, "Notes after reopen", true, revisedAnswers))).Status);

        var freshRepository = new DailyJournalRepository(database.ContextFactory);
        DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(await freshRepository.GetAsync(TradingDate, account));
        Assert.Equal(original.Entry.Id, loaded.Entry.Id);
        Assert.Equal(TradingDate, loaded.Entry.TradingDate);
        Assert.Equal(account, loaded.Entry.TradingAccountId);
        Assert.Equal(DailyJournalAccountState.Inactive, loaded.AccountState);
        Assert.Equal(CreatedAtUtc, loaded.Entry.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc.AddMinutes(3), loaded.Entry.UpdatedAtUtc);
        Assert.Equal(revisedAnswers, loaded.Entry.Review);
        Assert.True(loaded.Entry.IsDraft);
        Assert.Equal(new[] { true, false, true, true }, (await freshRepository.GetHistoryAsync(original.Entry.Id)).Select(r => r.IsDraft));
        Assert.Equal(new[] { 1L, 2L, 3L, 4L }, (await freshRepository.GetHistoryAsync(original.Entry.Id)).Select(r => r.Revision));
        Assert.Equal(new[] { CompleteReview, CompleteReview, CompleteReview, revisedAnswers },
            (await freshRepository.GetHistoryAsync(original.Entry.Id)).Select(r => r.Review));
        Assert.Null(await freshRepository.GetAsync(TradingDate));
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await context.Trades.ToArrayAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ContentWithoutAnyLetterOrDigitCannotCompleteOrWriteHistory(int missingAnswer)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository repository = GetRepository(database);
        DailyJournalDetails original = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, null, "Freeform cannot replace review", true, CompleteReview))).Journal);
        string[] answers = ["", "", ""];
        answers[missingAnswer] = " \r\n\t...📈";
        var incomplete = new DailyReviewAnswers(answers[0], answers[1], answers[2]);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpdateAsync(
            new UpdateDailyJournalCommand(original.Entry.Id, 1, "...", false, incomplete)));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate.AddDays(1), null, "...", false, incomplete)));

        DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(await repository.GetAsync(TradingDate));
        Assert.Equal(original.Entry.Review, loaded.Entry.Review);
        Assert.Equal(original.Entry.Text, loaded.Entry.Text);
        Assert.True(loaded.Entry.IsDraft);
        Assert.Single(await repository.GetHistoryAsync(original.Entry.Id));
        Assert.Null(await repository.GetAsync(TradingDate.AddDays(1)));
    }

    [Fact]
    public async Task CompletedReviewRejectsContentEditsUntilExactContentIsReopened()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository repository = GetRepository(database);
        DailyJournalDetails completed = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, null, "Protected text", false, CompleteReview))).Journal);
        var changed = new DailyReviewAnswers("Changed", CompleteReview.NeedsImprovement, CompleteReview.NextTradingDay);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UpdateAsync(
            new UpdateDailyJournalCommand(completed.Entry.Id, 1, "Edited text", false, CompleteReview)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UpdateAsync(
            new UpdateDailyJournalCommand(completed.Entry.Id, 1, "Protected text", true, changed)));
        Assert.Equal(DailyJournalWriteStatus.Unchanged, (await repository.UpdateAsync(
            new UpdateDailyJournalCommand(completed.Entry.Id, 1, "Protected text", false, CompleteReview))).Status);
        Assert.Single(await repository.GetHistoryAsync(completed.Entry.Id));
        Assert.Equal(CompleteReview, Assert.IsType<DailyJournalDetails>(await repository.GetAsync(TradingDate)).Entry.Review);
    }

    [Fact]
    public async Task StaleCompletionOrReopenReportsLatestRevisionBeforeValidatingLocalContent()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository repository = GetRepository(database);
        DailyJournalDetails original = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, null, "", true, CompleteReview))).Journal);
        await repository.UpdateAsync(new UpdateDailyJournalCommand(original.Entry.Id, 1, "Journal", false));

        DailyJournalWriteResult staleCompletion = await repository.UpdateAsync(
            new UpdateDailyJournalCommand(original.Entry.Id, 1, "Local draft", false, DailyReviewAnswers.Empty));
        DailyJournalWriteResult staleReopen = await repository.UpdateAsync(
            new UpdateDailyJournalCommand(original.Entry.Id, 1, "Local draft", true, DailyReviewAnswers.Empty));

        foreach (DailyJournalWriteResult conflict in new[] { staleCompletion, staleReopen })
        {
            Assert.Equal(DailyJournalWriteStatus.Conflict, conflict.Status);
            DailyJournalDetails latest = Assert.IsType<DailyJournalDetails>(conflict.Journal);
            Assert.Equal(2L, latest.Entry.Revision);
            Assert.False(latest.Entry.IsDraft);
            Assert.Equal(CompleteReview, latest.Entry.Review);
        }
        Assert.Equal(2, (await repository.GetHistoryAsync(original.Entry.Id)).Count);
    }

    [Fact]
    public async Task OmittedReviewOnUpdateRetainsAnswersAndExactAnswerNoOpAddsNoRevision()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository repository = GetRepository(database);
        DailyJournalDetails original = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, null, "", true, CompleteReview))).Journal);
        await repository.UpdateAsync(new UpdateDailyJournalCommand(original.Entry.Id, 1, "Added text", true));
        DailyJournalWriteResult noOp = await repository.UpdateAsync(new UpdateDailyJournalCommand(
            original.Entry.Id, 2, "Added text", true, CompleteReview));

        Assert.Equal(DailyJournalWriteStatus.Unchanged, noOp.Status);
        Assert.Equal(CompleteReview, Assert.IsType<DailyJournalDetails>(await repository.GetAsync(TradingDate)).Entry.Review);
        Assert.Equal(2, (await repository.GetHistoryAsync(original.Entry.Id)).Count);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CompletionAndReopenCancellationOrFailureRollBackAnswersStateAndHistory(bool reopen, bool cancel)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository reader = GetRepository(database);
        DailyJournalDetails original = Assert.IsType<DailyJournalDetails>((await reader.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, null, "Journal", !reopen, CompleteReview))).Journal);
        using var cancellation = new CancellationTokenSource();
        Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor interceptor = cancel
            ? new CancelAfterSaveInterceptor(cancellation) : new FailAfterSaveInterceptor();
        var repository = new DailyJournalRepository(await InterceptingFactory.CreateAsync(database, interceptor));

        Func<Task> write = () => repository.UpdateAsync(new UpdateDailyJournalCommand(
            original.Entry.Id, 1, "Journal", reopen, CompleteReview), cancellation.Token);
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(write);
        else await Assert.ThrowsAsync<InvalidOperationException>(write);

        DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(await reader.GetAsync(TradingDate));
        Assert.Equal(original.Entry.IsDraft, loaded.Entry.IsDraft);
        Assert.Equal(CompleteReview, loaded.Entry.Review);
        Assert.Equal(original.Entry.UpdatedAtUtc, loaded.Entry.UpdatedAtUtc);
        Assert.Equal(1L, loaded.Entry.Revision);
        DailyJournalRevision history = Assert.Single(await reader.GetHistoryAsync(original.Entry.Id));
        Assert.Equal(CompleteReview, history.Review);
        Assert.Equal(original.Entry.IsDraft, history.IsDraft);
    }
}
