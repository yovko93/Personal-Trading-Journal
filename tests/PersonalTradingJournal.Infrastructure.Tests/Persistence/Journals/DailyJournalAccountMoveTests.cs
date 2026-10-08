using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Infrastructure.Journals;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Journals;

public sealed partial class DailyJournalRepositoryTests
{
    [Fact]
    public async Task AccountMovePreservesIdentityHistoryAndMonotonicRevisionAndFreesOriginalScope()
    {
        await using var database = await ReaderTestDatabase.CreateAsync();
        var repository = GetRepository(database);
        Guid account = await SeedAccountAsync(database, "Archived", isActive: false);
        var original = (await repository.CreateAsync(new(TradingDate, null, "Original", false))).Journal!;
        var moved = await repository.UpdateAsync(new(original.Entry.Id, 1, "Moved", false,
            CompleteReview, ReopenCompleted: true, TargetScope: new(account)));
        Assert.Equal(DailyJournalWriteStatus.Updated, moved.Status);
        Assert.Equal(original.Entry.Id, moved.Journal!.Entry.Id);
        Assert.Equal(original.Entry.CreatedAtUtc, moved.Journal.Entry.CreatedAtUtc);
        Assert.Equal(2, moved.Journal.Entry.Revision);
        Assert.Equal(account, moved.Journal.Entry.TradingAccountId);
        Assert.Equal(DailyJournalAccountState.Inactive, moved.Journal.AccountState);
        Assert.Null(await repository.GetAsync(TradingDate));
        Assert.Equal(original.Entry.Id, (await repository.GetAsync(TradingDate, account))!.Entry.Id);
        Assert.Collection(await repository.GetHistoryAsync(original.Entry.Id),
            r => { Assert.Equal(1, r.Revision); Assert.Equal("Original", r.Text); },
            r => { Assert.Equal(2, r.Revision); Assert.Equal("Moved", r.Text); Assert.Equal(CompleteReview, r.Review); });

        var returned = await repository.UpdateAsync(new(original.Entry.Id, 2, "Moved", true,
            CompleteReview, ReopenCompleted: true, TargetScope: new(null)));
        Assert.Equal(3, returned.Journal!.Entry.Revision);
        Assert.Null(returned.Journal.Entry.TradingAccountId);
        Assert.Null(await repository.GetAsync(TradingDate, account));
        Assert.Equal(DailyJournalWriteStatus.Created,
            (await repository.CreateAsync(new(TradingDate, account, "Separate entry"))).Status);
        Assert.Equal(DailyJournalWriteStatus.Conflict,
            (await repository.UpdateAsync(new(original.Entry.Id, 2, "Stale", true, TargetScope: new(account)))).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OccupiedAccountScopeRejectsMoveWithoutChangingEitherJournal(bool targetAllAccounts)
    {
        await using var database = await ReaderTestDatabase.CreateAsync();
        var repository = GetRepository(database);
        Guid account = await SeedAccountAsync(database, "Target");
        Guid? source = targetAllAccounts ? account : null;
        Guid? target = targetAllAccounts ? null : account;
        var original = (await repository.CreateAsync(new(TradingDate, source, "Source"))).Journal!;
        var occupied = (await repository.CreateAsync(new(TradingDate, target, "Target"))).Journal!;
        var result = await repository.UpdateAsync(new(original.Entry.Id, 1, "Local edits", false,
            TargetScope: new(target)));
        Assert.Equal(DailyJournalWriteStatus.AccountScopeOccupied, result.Status);
        Assert.Equal("Source", (await repository.GetAsync(TradingDate, source))!.Entry.Text);
        Assert.Equal(occupied.Entry.Id, (await repository.GetAsync(TradingDate, target))!.Entry.Id);
        Assert.Single(await repository.GetHistoryAsync(original.Entry.Id));
        Assert.Single(await repository.GetHistoryAsync(occupied.Entry.Id));
    }

    [Fact]
    public async Task MissingTargetAndStaleTokenRejectMoveWithoutWrites()
    {
        await using var database = await ReaderTestDatabase.CreateAsync();
        var repository = GetRepository(database);
        var original = (await repository.CreateAsync(new(TradingDate, null, "Source"))).Journal!;
        Assert.Equal(DailyJournalWriteStatus.AccountUnavailable,
            (await repository.UpdateAsync(new(original.Entry.Id, 1, "Edit", true, TargetScope: new(Guid.NewGuid())))).Status);
        await repository.UpdateAsync(new(original.Entry.Id, 1, "Newer", true));
        Assert.Equal(DailyJournalWriteStatus.Conflict,
            (await repository.UpdateAsync(new(original.Entry.Id, 1, "Stale", true, TargetScope: new(Guid.NewGuid())))).Status);
        Assert.Equal("Newer", (await repository.GetAsync(TradingDate))!.Entry.Text);
        Assert.Equal(2, (await repository.GetHistoryAsync(original.Entry.Id)).Count);
        Assert.Throws<ArgumentException>(() => new DailyJournalAccountScope(Guid.Empty));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MoveFailureOrCancellationRollsBackScopeContentAndHistory(bool cancelled)
    {
        await using var database = await ReaderTestDatabase.CreateAsync();
        var reader = GetRepository(database);
        Guid account = await SeedAccountAsync(database, "Target");
        var original = (await reader.CreateAsync(new(TradingDate, null, "Original"))).Journal!;
        using var cancellation = new CancellationTokenSource();
        var factory = await InterceptingFactory.CreateAsync(database, cancelled
            ? new CancelAfterSaveInterceptor(cancellation) : new FailAfterSaveInterceptor());
        var writer = new DailyJournalRepository(factory);
        var command = new UpdateDailyJournalCommand(original.Entry.Id, 1, "Move", true, TargetScope: new(account));
        if (cancelled) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.UpdateAsync(command, cancellation.Token));
        else await Assert.ThrowsAsync<InvalidOperationException>(() => writer.UpdateAsync(command));
        Assert.Null(await reader.GetAsync(TradingDate, account));
        Assert.Equal("Original", (await reader.GetAsync(TradingDate))!.Entry.Text);
        Assert.Single(await reader.GetHistoryAsync(original.Entry.Id));
    }
}
