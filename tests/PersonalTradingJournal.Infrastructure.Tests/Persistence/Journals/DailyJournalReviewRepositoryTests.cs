using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Infrastructure.Journals;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Journals;

public sealed partial class DailyJournalRepositoryTests
{
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
            new UpdateDailyJournalCommand(original.Entry.Id, 1, "", false))).Status);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(DailyJournalWriteStatus.Updated, (await repository.UpdateAsync(
            new UpdateDailyJournalCommand(original.Entry.Id, 2, "", true))).Status);
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
    public async Task MissingMeaningfulAnswerCannotCompleteOrWriteHistory(int missingAnswer)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository repository = GetRepository(database);
        DailyJournalDetails original = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, null, "Freeform cannot replace review", true, CompleteReview))).Journal);
        string[] answers = [CompleteReview.WentWell, CompleteReview.NeedsImprovement, CompleteReview.NextTradingDay];
        answers[missingAnswer] = " \r\n\t...📈";
        var incomplete = new DailyReviewAnswers(answers[0], answers[1], answers[2]);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpdateAsync(
            new UpdateDailyJournalCommand(original.Entry.Id, 1, "Freeform", false, incomplete)));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate.AddDays(1), null, "Freeform", false, incomplete)));

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
        await repository.UpdateAsync(new UpdateDailyJournalCommand(original.Entry.Id, 1, "", false));

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
            new CreateDailyJournalCommand(TradingDate, null, "", !reopen, CompleteReview))).Journal);
        using var cancellation = new CancellationTokenSource();
        Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor interceptor = cancel
            ? new CancelAfterSaveInterceptor(cancellation) : new FailAfterSaveInterceptor();
        var repository = new DailyJournalRepository(await InterceptingFactory.CreateAsync(database, interceptor));

        Func<Task> write = () => repository.UpdateAsync(new UpdateDailyJournalCommand(
            original.Entry.Id, 1, "", reopen, CompleteReview), cancellation.Token);
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
