using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Infrastructure.Journals;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Journals;

public sealed partial class DailyJournalRepositoryTests
{
    private static readonly DateOnly TradingDate = new(2026, 10, 4);
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 4, 8, 30, 0, TimeSpan.Zero);
    private static readonly DailyReviewAnswers CompleteReview = new("Followed plan", "Improve patience", "Wait for confirmation");

    [Fact]
    public async Task CreateAndReadPreserveExactTextScopeAndUtcAuditFields()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var clock = new JournalTestClock(CreatedAtUtc);
        var repository = new DailyJournalRepository(database.ContextFactory, clock);
        const string text = "  Premarket\r\n\nПлан: търпение. 📈\t  ";

        DailyJournalWriteResult result = await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, null, text, false, CompleteReview));
        DailyJournalDetails created = Assert.IsType<DailyJournalDetails>(result.Journal);
        DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(
            await repository.GetAsync(TradingDate));

        Assert.Equal(DailyJournalWriteStatus.Created, result.Status);
        Assert.NotEqual(Guid.Empty, created.Entry.Id);
        Assert.Equal(created.Entry.Id, loaded.Entry.Id);
        Assert.Equal(TradingDate, loaded.Entry.TradingDate);
        Assert.Null(loaded.Entry.TradingAccountId);
        Assert.Equal(text, loaded.Entry.Text);
        Assert.False(loaded.Entry.IsDraft);
        Assert.Equal(1L, loaded.Entry.Revision);
        Assert.Equal(CreatedAtUtc, loaded.Entry.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, loaded.Entry.UpdatedAtUtc);
        Assert.Equal(TimeSpan.Zero, loaded.Entry.CreatedAtUtc.Offset);
        Assert.Equal(TimeSpan.Zero, loaded.Entry.UpdatedAtUtc.Offset);
        Assert.Equal(DailyJournalAccountState.AllAccounts, loaded.AccountState);
        Assert.Null(loaded.AccountName);
        var revision = Assert.Single(await repository.GetHistoryAsync(created.Entry.Id));
        Assert.Equal(created.Entry.Id, revision.JournalId);
        Assert.Equal(1L, revision.Revision);
        Assert.Equal(text, revision.Text);
        Assert.False(revision.IsDraft);
        Assert.Equal(CreatedAtUtc, revision.SavedAtUtc);
    }

    [Fact]
    public async Task EmptyDraftCanBeCreatedForDayWithoutTrades()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository repository = GetRepository(database);

        DailyJournalWriteResult result = await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, null, string.Empty));

        Assert.Equal(DailyJournalWriteStatus.Created, result.Status);
        DailyJournalDetails created = Assert.IsType<DailyJournalDetails>(result.Journal);
        Assert.Equal(string.Empty, created.Entry.Text);
        Assert.True(created.Entry.IsDraft);
        Assert.Single(await repository.GetHistoryAsync(created.Entry.Id));
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await context.Trades.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task DateAndAccountScopesAreIndependentAndDuplicatesPreserveOriginal()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid firstAccountId = await SeedAccountAsync(database, "First");
        Guid secondAccountId = await SeedAccountAsync(database, "Second");
        IDailyJournalRepository repository = GetRepository(database);
        (DateOnly Date, Guid? AccountId, string Text)[] scopes =
        [
            (TradingDate, null, "All accounts"),
            (TradingDate, firstAccountId, "First account"),
            (TradingDate, secondAccountId, "Second account"),
            (TradingDate.AddDays(1), null, "Next day"),
            (TradingDate.AddDays(1), firstAccountId, "First account next day"),
        ];
        var identities = new HashSet<Guid>();

        foreach (var scope in scopes)
        {
            DailyJournalWriteResult first = await repository.CreateAsync(
                new CreateDailyJournalCommand(scope.Date, scope.AccountId, scope.Text));
            DailyJournalWriteResult duplicate = await repository.CreateAsync(
                new CreateDailyJournalCommand(scope.Date, scope.AccountId, "Replacement", false, CompleteReview));
            DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(
                await repository.GetAsync(scope.Date, scope.AccountId));

            Assert.Equal(DailyJournalWriteStatus.Created, first.Status);
            Assert.Equal(DailyJournalWriteStatus.AlreadyExists, duplicate.Status);
            Assert.Equal(scope.Date, loaded.Entry.TradingDate);
            Assert.Equal(scope.AccountId, loaded.Entry.TradingAccountId);
            Assert.Equal(scope.Text, loaded.Entry.Text);
            Assert.True(loaded.Entry.IsDraft);
            Assert.Equal(1L, loaded.Entry.Revision);
            Assert.True(identities.Add(loaded.Entry.Id));
            Assert.Single(await repository.GetHistoryAsync(loaded.Entry.Id));
        }

        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(scopes.Length, await context.DailyJournals.CountAsync());
        Assert.Equal(scopes.Length, await context.DailyJournalRevisions.CountAsync());
    }

    [Theory]
    [InlineData(2026, 3, 8)]
    [InlineData(2026, 11, 1)]
    public async Task TradingDateIsExplicitAcrossDaylightSavingTransitions(int year, int month, int day)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var date = new DateOnly(year, month, day);
        // The audit clock is deliberately on another date; it must never define the journal key.
        var clock = new JournalTestClock(new DateTimeOffset(year, month, day, 23, 30, 0, TimeSpan.Zero).AddDays(1));
        var repository = new DailyJournalRepository(database.ContextFactory, clock);

        await repository.CreateAsync(new CreateDailyJournalCommand(date, null, "DST day"));
        clock.Advance(TimeSpan.FromDays(100));
        DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(await repository.GetAsync(date));

        Assert.Equal(date, loaded.Entry.TradingDate);
        Assert.Null(await repository.GetAsync(date.AddDays(-1)));
        Assert.Null(await repository.GetAsync(date.AddDays(1)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DatabaseRejectsDirectDuplicateDateScopeInsertWithDifferentIdentity(bool accountScoped)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid? accountId = accountScoped ? await SeedAccountAsync(database, "Unique scope") : null;
        IDailyJournalRepository repository = GetRepository(database);
        DailyJournalDetails original = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, accountId, "Original"))).Journal);
        await using (JournalDbContext context = await database.ContextFactory.CreateDbContextAsync())
        {
            context.DailyJournals.Add(new DailyJournalRecord
            {
                Id = Guid.NewGuid(),
                TradingDate = TradingDate,
                TradingAccountId = accountId,
                Text = "Duplicate bypassing repository",
                IsDraft = true,
                Revision = 1,
                CreatedAtUtc = CreatedAtUtc,
                UpdatedAtUtc = CreatedAtUtc,
            });

            DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            SqliteException sqliteFailure = Assert.IsType<SqliteException>(failure.InnerException);
            Assert.Equal(19, sqliteFailure.SqliteErrorCode);
            Assert.Equal(2067, sqliteFailure.SqliteExtendedErrorCode); // SQLITE_CONSTRAINT_UNIQUE
        }

        DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(await repository.GetAsync(TradingDate, accountId));
        Assert.Equal(original.Entry.Id, loaded.Entry.Id);
        Assert.Equal("Original", loaded.Entry.Text);
        await using JournalDbContext readContext = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await readContext.DailyJournals.CountAsync());
        Assert.Equal(1, await readContext.DailyJournalRevisions.CountAsync());
    }

    [Fact]
    public async Task DatabaseRejectsDirectDuplicateJournalRevisionInsert()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository repository = GetRepository(database);
        DailyJournalDetails original = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, null, "Original snapshot"))).Journal);
        await using (JournalDbContext context = await database.ContextFactory.CreateDbContextAsync())
        {
            context.DailyJournalRevisions.Add(new DailyJournalRevisionRecord
            {
                JournalId = original.Entry.Id,
                Revision = 1,
                Text = "Duplicate snapshot bypassing repository",
                IsDraft = false,
                SavedAtUtc = CreatedAtUtc.AddMinutes(1),
            });

            DbUpdateException failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            SqliteException sqliteFailure = Assert.IsType<SqliteException>(failure.InnerException);
            Assert.Equal(19, sqliteFailure.SqliteErrorCode);
            Assert.Equal(1555, sqliteFailure.SqliteExtendedErrorCode); // SQLITE_CONSTRAINT_PRIMARYKEY
        }

        DailyJournalRevision revision = Assert.Single(await repository.GetHistoryAsync(original.Entry.Id));
        Assert.Equal("Original snapshot", revision.Text);
        Assert.True(revision.IsDraft);
        Assert.Equal(original.Entry.CreatedAtUtc, revision.SavedAtUtc);
    }

    [Fact]
    public async Task UpdateAppendsImmutableSnapshotsAndPreservesIdentityScopeAndCreatedTime()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database, "Journal account");
        var clock = new JournalTestClock(CreatedAtUtc);
        var repository = new DailyJournalRepository(database.ContextFactory, clock);
        DailyJournalDetails created = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, accountId, "First draft"))).Journal);
        clock.Advance(TimeSpan.FromMinutes(2));

        DailyJournalWriteResult edit = await repository.UpdateAsync(
            new UpdateDailyJournalCommand(created.Entry.Id, 1, "  Edited\n", true));
        clock.Advance(TimeSpan.FromMinutes(3));
        DailyJournalWriteResult completion = await repository.UpdateAsync(
            new UpdateDailyJournalCommand(created.Entry.Id, 2, "  Edited\n", false, CompleteReview));

        Assert.Equal(DailyJournalWriteStatus.Updated, edit.Status);
        Assert.Equal(DailyJournalWriteStatus.Updated, completion.Status);
        DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(
            await repository.GetAsync(TradingDate, accountId));
        Assert.Equal(created.Entry.Id, loaded.Entry.Id);
        Assert.Equal(TradingDate, loaded.Entry.TradingDate);
        Assert.Equal(accountId, loaded.Entry.TradingAccountId);
        Assert.Equal(CreatedAtUtc, loaded.Entry.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc.AddMinutes(5), loaded.Entry.UpdatedAtUtc);
        Assert.Equal(3L, loaded.Entry.Revision);
        Assert.Equal("  Edited\n", loaded.Entry.Text);
        Assert.False(loaded.Entry.IsDraft);
        var reopenedRepository = new DailyJournalRepository(database.ContextFactory);
        var history = await reopenedRepository.GetHistoryAsync(created.Entry.Id);
        Assert.Collection(history,
            revision =>
            {
                Assert.Equal(1L, revision.Revision);
                Assert.Equal("First draft", revision.Text);
                Assert.True(revision.IsDraft);
                Assert.Equal(CreatedAtUtc, revision.SavedAtUtc);
            },
            revision =>
            {
                Assert.Equal(2L, revision.Revision);
                Assert.Equal("  Edited\n", revision.Text);
                Assert.True(revision.IsDraft);
                Assert.Equal(CreatedAtUtc.AddMinutes(2), revision.SavedAtUtc);
            },
            revision =>
            {
                Assert.Equal(3L, revision.Revision);
                Assert.Equal("  Edited\n", revision.Text);
                Assert.False(revision.IsDraft);
                Assert.Equal(CreatedAtUtc.AddMinutes(5), revision.SavedAtUtc);
            });
    }

    [Fact]
    public async Task ExactNoOpDoesNotAdvanceRevisionAuditTimeOrHistory()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var clock = new JournalTestClock(CreatedAtUtc);
        var repository = new DailyJournalRepository(database.ContextFactory, clock);
        DailyJournalDetails created = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, null, "Same\r\n", false, CompleteReview))).Journal);
        clock.Advance(TimeSpan.FromHours(1));

        DailyJournalWriteResult result = await repository.UpdateAsync(
            new UpdateDailyJournalCommand(created.Entry.Id, 1, "Same\r\n", false));

        Assert.Equal(DailyJournalWriteStatus.Unchanged, result.Status);
        DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(await repository.GetAsync(TradingDate));
        Assert.Equal(1L, loaded.Entry.Revision);
        Assert.Equal(CreatedAtUtc, loaded.Entry.UpdatedAtUtc);
        Assert.Single(await repository.GetHistoryAsync(created.Entry.Id));
    }

    [Fact]
    public async Task WhitespaceOnlyEditAndClearingTextEachCreateARevision()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository repository = GetRepository(database);
        DailyJournalDetails created = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, null, "Plan"))).Journal);

        DailyJournalWriteResult whitespace = await repository.UpdateAsync(
            new UpdateDailyJournalCommand(created.Entry.Id, 1, "Plan ", true));
        DailyJournalWriteResult clear = await repository.UpdateAsync(
            new UpdateDailyJournalCommand(created.Entry.Id, 2, string.Empty, true));

        Assert.Equal(DailyJournalWriteStatus.Updated, whitespace.Status);
        Assert.Equal(DailyJournalWriteStatus.Updated, clear.Status);
        DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(await repository.GetAsync(TradingDate));
        Assert.Equal(string.Empty, loaded.Entry.Text);
        Assert.Equal(3L, loaded.Entry.Revision);
        Assert.Equal(3, (await repository.GetHistoryAsync(created.Entry.Id)).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentStaleWriterCannotOverwriteOrClaimUnchanged(bool sameAsLatest)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        var firstRepository = new DailyJournalRepository(database.ContextFactory);
        var secondRepository = new DailyJournalRepository(database.ContextFactory);
        DailyJournalDetails created = Assert.IsType<DailyJournalDetails>((await firstRepository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, null, "Original"))).Journal);
        DailyJournalDetails stale = Assert.IsType<DailyJournalDetails>(await secondRepository.GetAsync(TradingDate));
        await firstRepository.UpdateAsync(new UpdateDailyJournalCommand(created.Entry.Id, 1, "Latest", false, CompleteReview));

        DailyJournalWriteResult result = await secondRepository.UpdateAsync(
            new UpdateDailyJournalCommand(stale.Entry.Id, stale.Entry.Revision,
                sameAsLatest ? "Latest" : "Stale draft", false));

        Assert.Equal(DailyJournalWriteStatus.Conflict, result.Status);
        DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(await firstRepository.GetAsync(TradingDate));
        Assert.Equal("Latest", loaded.Entry.Text);
        Assert.False(loaded.Entry.IsDraft);
        Assert.Equal(2L, loaded.Entry.Revision);
        Assert.Equal(2, (await firstRepository.GetHistoryAsync(created.Entry.Id)).Count);
    }

    [Fact]
    public async Task MissingLookupHistoryAndUpdateDoNotCreateData()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository repository = GetRepository(database);
        Guid missingId = Guid.NewGuid();

        Assert.Null(await repository.GetAsync(TradingDate));
        Assert.Null(await repository.GetAsync(TradingDate, Guid.NewGuid()));
        Assert.Empty(await repository.GetHistoryAsync(missingId));
        DailyJournalWriteResult result = await repository.UpdateAsync(
            new UpdateDailyJournalCommand(missingId, 1, "Must not insert", true));

        Assert.Equal(DailyJournalWriteStatus.NotFound, result.Status);
        Assert.Null(result.Journal);
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(0, await context.DailyJournals.CountAsync());
        Assert.Equal(0, await context.DailyJournalRevisions.CountAsync());
    }

    [Fact]
    public async Task ReadsDoNotSaveOrChangeCurrentEntryAndHistory()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository repository = GetRepository(database);
        DailyJournalDetails created = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, null, "Read only"))).Journal);
        var reader = new DailyJournalRepository(
            await InterceptingFactory.CreateAsync(database, new RejectSaveInterceptor()));

        for (int iteration = 0; iteration < 2; iteration++)
        {
            DailyJournalDetails read = Assert.IsType<DailyJournalDetails>(await reader.GetAsync(TradingDate));
            Assert.Equal(created.Entry.Id, read.Entry.Id);
            Assert.Equal(created.Entry.UpdatedAtUtc, read.Entry.UpdatedAtUtc);
            Assert.Equal(1L, read.Entry.Revision);
            Assert.Single(await reader.GetHistoryAsync(created.Entry.Id));
            Assert.Null(await reader.GetAsync(TradingDate.AddDays(1)));
        }

        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await context.DailyJournals.CountAsync());
        Assert.Equal(1, await context.DailyJournalRevisions.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentCreatesKeepOneEntryAndOneInitialRevision(bool accountScoped)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid? accountId = accountScoped ? await SeedAccountAsync(database, "Concurrent") : null;
        var gate = new ConcurrentStartFactory(database.ContextFactory);
        var firstRepository = new DailyJournalRepository(gate);
        var secondRepository = new DailyJournalRepository(gate);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        DailyJournalWriteResult[] results = await Task.WhenAll(
            Task.Run(() => firstRepository.CreateAsync(
                new CreateDailyJournalCommand(TradingDate, accountId, "First"), timeout.Token)),
            Task.Run(() => secondRepository.CreateAsync(
                new CreateDailyJournalCommand(TradingDate, accountId, "Second"), timeout.Token)));

        Assert.Single(results, result => result.Status == DailyJournalWriteStatus.Created);
        Assert.Single(results, result => result.Status == DailyJournalWriteStatus.AlreadyExists);
        IDailyJournalRepository reader = GetRepository(database);
        DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(await reader.GetAsync(TradingDate, accountId));
        Assert.Equal(1L, loaded.Entry.Revision);
        Assert.Contains(loaded.Entry.Text, new[] { "First", "Second" });
        Assert.Single(await reader.GetHistoryAsync(loaded.Entry.Id));
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await context.DailyJournals.CountAsync());
        Assert.Equal(1, await context.DailyJournalRevisions.CountAsync());
    }

    [Fact]
    public async Task ConcurrentUpdatesAllowExactlyOneExpectedRevisionToWin()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository reader = GetRepository(database);
        DailyJournalDetails created = Assert.IsType<DailyJournalDetails>((await reader.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, null, "Original"))).Journal);
        var gate = new ConcurrentStartFactory(database.ContextFactory);
        var firstRepository = new DailyJournalRepository(gate);
        var secondRepository = new DailyJournalRepository(gate);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        DailyJournalWriteResult[] results = await Task.WhenAll(
            Task.Run(() => firstRepository.UpdateAsync(
                new UpdateDailyJournalCommand(created.Entry.Id, 1, "First", false, CompleteReview), timeout.Token)),
            Task.Run(() => secondRepository.UpdateAsync(
                new UpdateDailyJournalCommand(created.Entry.Id, 1, "Second", false, CompleteReview), timeout.Token)));

        DailyJournalWriteResult winner = Assert.Single(results, result => result.Status == DailyJournalWriteStatus.Updated);
        Assert.Single(results, result => result.Status == DailyJournalWriteStatus.Conflict);
        DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(await reader.GetAsync(TradingDate));
        Assert.Equal(2L, loaded.Entry.Revision);
        Assert.Equal(Assert.IsType<DailyJournalDetails>(winner.Journal).Entry.Text, loaded.Entry.Text);
        var history = (await reader.GetHistoryAsync(created.Entry.Id)).OrderBy(revision => revision.Revision).ToArray();
        Assert.Equal(2, history.Length);
        Assert.Equal("Original", history[0].Text);
        Assert.Equal(loaded.Entry.Text, history[1].Text);
    }

    [Fact]
    public async Task InactiveAccountRemainsReadableAndWritableWithItsOriginalIdentity()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database, "Retired", false);
        IDailyJournalRepository repository = GetRepository(database);

        DailyJournalWriteResult created = await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, accountId, "Review"));
        DailyJournalDetails first = Assert.IsType<DailyJournalDetails>(created.Journal);
        DailyJournalWriteResult updated = await repository.UpdateAsync(
            new UpdateDailyJournalCommand(first.Entry.Id, 1, "Completed review", false, CompleteReview));

        Assert.Equal(DailyJournalWriteStatus.Created, created.Status);
        Assert.Equal(DailyJournalWriteStatus.Updated, updated.Status);
        DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(await repository.GetAsync(TradingDate, accountId));
        Assert.Equal(accountId, loaded.Entry.TradingAccountId);
        Assert.Equal(DailyJournalAccountState.Inactive, loaded.AccountState);
        Assert.Equal("Retired", loaded.AccountName);
        Assert.Null(await repository.GetAsync(TradingDate));
    }

    [Fact]
    public async Task AccountDisplayStateReflectsDeactivationWithoutRewritingJournal()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database, "Active account");
        IDailyJournalRepository repository = GetRepository(database);
        DailyJournalDetails created = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, accountId, "Account journal"))).Journal);
        Assert.Equal(DailyJournalAccountState.Active, created.AccountState);
        await using (JournalDbContext context = await database.ContextFactory.CreateDbContextAsync())
        {
            TradingAccountRecord account = await context.TradingAccounts.SingleAsync(record => record.Id == accountId);
            account.IsActive = false;
            account.Name = "Renamed retired account";
            await context.SaveChangesAsync();
        }

        DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(await repository.GetAsync(TradingDate, accountId));

        Assert.Equal(DailyJournalAccountState.Inactive, loaded.AccountState);
        Assert.Equal("Renamed retired account", loaded.AccountName);
        Assert.Equal(created.Entry.Id, loaded.Entry.Id);
        Assert.Equal(created.Entry.UpdatedAtUtc, loaded.Entry.UpdatedAtUtc);
        Assert.Equal(1L, loaded.Entry.Revision);
        Assert.Single(await repository.GetHistoryAsync(created.Entry.Id));
    }

    [Fact]
    public async Task MissingAccountCannotCreateEntryOrHistory()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository repository = GetRepository(database);

        DailyJournalWriteResult result = await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, Guid.NewGuid(), "Unavailable account"));

        Assert.Equal(DailyJournalWriteStatus.AccountUnavailable, result.Status);
        Assert.Null(result.Journal);
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(0, await context.DailyJournals.CountAsync());
        Assert.Equal(0, await context.DailyJournalRevisions.CountAsync());
    }

    [Fact]
    public async Task LegacyOrphanIsReadableAsUnavailableAndNeverRetargetedOrUpdated()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database, "Legacy account");
        Guid unavailableAccountId = Guid.NewGuid();
        IDailyJournalRepository repository = GetRepository(database);
        DailyJournalDetails original = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, accountId, "Legacy account journal"))).Journal);
        await repository.CreateAsync(new CreateDailyJournalCommand(TradingDate, null, "All accounts journal"));
        await using (JournalDbContext context = await database.ContextFactory.CreateDbContextAsync())
        {
            await context.Database.OpenConnectionAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
            try
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE DailyJournals SET TradingAccountId = {unavailableAccountId} WHERE Id = {original.Entry.Id};");
            }
            finally
            {
                await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
            }
        }

        DailyJournalDetails orphan = Assert.IsType<DailyJournalDetails>(
            await repository.GetAsync(TradingDate, unavailableAccountId));
        DailyJournalWriteResult update = await repository.UpdateAsync(
            new UpdateDailyJournalCommand(original.Entry.Id, 1, "Overwrite", false));

        Assert.Equal(DailyJournalAccountState.Unavailable, orphan.AccountState);
        Assert.Null(orphan.AccountName);
        Assert.Equal(unavailableAccountId, orphan.Entry.TradingAccountId);
        Assert.Equal(original.Entry.Id, orphan.Entry.Id);
        Assert.Equal(DailyJournalWriteStatus.AccountUnavailable, update.Status);
        DailyJournalDetails unchanged = Assert.IsType<DailyJournalDetails>(
            await repository.GetAsync(TradingDate, unavailableAccountId));
        Assert.Equal("Legacy account journal", unchanged.Entry.Text);
        Assert.Equal(1L, unchanged.Entry.Revision);
        Assert.Single(await repository.GetHistoryAsync(original.Entry.Id));
        Assert.Null(await repository.GetAsync(TradingDate, accountId));
        Assert.Equal("All accounts journal", Assert.IsType<DailyJournalDetails>(
            await repository.GetAsync(TradingDate)).Entry.Text);
        Assert.Equal(DailyJournalWriteStatus.Deleted, (await repository.DeleteAsync(new(original.Entry.Id, 1))).Status);
        Assert.Null(await repository.GetAsync(TradingDate, unavailableAccountId));
        Assert.Empty(await repository.GetHistoryAsync(original.Entry.Id));
        Assert.NotNull(await repository.GetAsync(TradingDate));
    }

    [Fact]
    public async Task AccountDeletionIsBlockedByJournalWithoutAnyTrades()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        Guid accountId = await SeedAccountAsync(database, "Referenced by journal");
        IDailyJournalRepository repository = GetRepository(database);
        DailyJournalDetails created = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, accountId, "Retain history"))).Journal);
        ITradingAccountDeletionStore deletionStore = database.ServiceProvider
            .GetRequiredService<ITradingAccountDeletionStore>();
        var useCase = new DeleteTradingAccountUseCase(
            database.ServiceProvider.GetRequiredService<ITradingAccountStore>(), deletionStore);

        await Assert.ThrowsAsync<TradingAccountDeleteBlockedException>(() => deletionStore.DeleteAsync(accountId));
        Assert.Equal(DeleteTradingAccountResult.Referenced, await useCase.ExecuteAsync(accountId));

        Assert.NotNull(await repository.GetAsync(TradingDate, accountId));
        Assert.Single(await repository.GetHistoryAsync(created.Entry.Id));
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.True(await context.TradingAccounts.AnyAsync(account => account.Id == accountId));
        Assert.Equal(0, await context.Trades.CountAsync());
    }

    [Fact]
    public async Task EveryOperationPropagatesPreCancellationWithoutChangingPersistedData()
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository repository = GetRepository(database);
        DailyJournalDetails created = Assert.IsType<DailyJournalDetails>((await repository.CreateAsync(
            new CreateDailyJournalCommand(TradingDate, null, "Persisted"))).Journal);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetAsync(TradingDate, null, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetHistoryAsync(created.Entry.Id, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.CreateAsync(new CreateDailyJournalCommand(TradingDate.AddDays(1), null, "Cancelled"), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.UpdateAsync(new UpdateDailyJournalCommand(created.Entry.Id, 1, "Cancelled", false), cancellation.Token));

        DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(await repository.GetAsync(TradingDate));
        Assert.Equal("Persisted", loaded.Entry.Text);
        Assert.Equal(1L, loaded.Entry.Revision);
        Assert.Single(await repository.GetHistoryAsync(created.Entry.Id));
        Assert.Null(await repository.GetAsync(TradingDate.AddDays(1)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAfterSavingRollsBackCurrentEntryAndRevisionTogether(bool updateExisting)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository reader = GetRepository(database);
        Guid? journalId = null;
        if (updateExisting)
        {
            journalId = Assert.IsType<DailyJournalDetails>((await reader.CreateAsync(
                new CreateDailyJournalCommand(TradingDate, null, "Original"))).Journal).Entry.Id;
        }

        using var cancellation = new CancellationTokenSource();
        var interceptor = new CancelAfterSaveInterceptor(cancellation);
        var repository = new DailyJournalRepository(await InterceptingFactory.CreateAsync(database, interceptor));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            if (journalId.HasValue)
            {
                await repository.UpdateAsync(new UpdateDailyJournalCommand(journalId.Value, 1, "Cancelled", false, CompleteReview), cancellation.Token);
            }
            else
            {
                await repository.CreateAsync(new CreateDailyJournalCommand(TradingDate, null, "Cancelled"), cancellation.Token);
            }
        });

        Assert.True(interceptor.SaveCompleted);
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(updateExisting ? 1 : 0, await context.DailyJournals.CountAsync());
        Assert.Equal(updateExisting ? 1 : 0, await context.DailyJournalRevisions.CountAsync());
        if (journalId.HasValue)
        {
            DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(await reader.GetAsync(TradingDate));
            Assert.Equal("Original", loaded.Entry.Text);
            Assert.True(loaded.Entry.IsDraft);
            Assert.Equal(1L, loaded.Entry.Revision);
            Assert.Single(await reader.GetHistoryAsync(journalId.Value));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureAfterSavingRollsBackCurrentEntryAndRevisionTogether(bool updateExisting)
    {
        await using ReaderTestDatabase database = await ReaderTestDatabase.CreateAsync();
        IDailyJournalRepository reader = GetRepository(database);
        DailyJournalDetails? original = updateExisting
            ? Assert.IsType<DailyJournalDetails>((await reader.CreateAsync(
                new CreateDailyJournalCommand(TradingDate, null, "Original"))).Journal)
            : null;
        var interceptor = new FailAfterSaveInterceptor();
        var repository = new DailyJournalRepository(await InterceptingFactory.CreateAsync(database, interceptor));

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            if (original is not null)
            {
                await repository.UpdateAsync(new UpdateDailyJournalCommand(original.Entry.Id, 1, "Failed edit", false, CompleteReview));
            }
            else
            {
                await repository.CreateAsync(new CreateDailyJournalCommand(TradingDate, null, "Failed create"));
            }
        });

        Assert.Equal("Injected failure after saving journal changes.", failure.Message);
        Assert.True(interceptor.SaveCompleted);
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        Assert.Equal(updateExisting ? 1 : 0, await context.DailyJournals.CountAsync());
        Assert.Equal(updateExisting ? 1 : 0, await context.DailyJournalRevisions.CountAsync());
        if (original is not null)
        {
            DailyJournalDetails loaded = Assert.IsType<DailyJournalDetails>(await reader.GetAsync(TradingDate));
            Assert.Equal("Original", loaded.Entry.Text);
            Assert.True(loaded.Entry.IsDraft);
            Assert.Equal(1L, loaded.Entry.Revision);
            Assert.Equal(original.Entry.UpdatedAtUtc, loaded.Entry.UpdatedAtUtc);
            DailyJournalRevision revision = Assert.Single(await reader.GetHistoryAsync(original.Entry.Id));
            Assert.Equal("Original", revision.Text);
        }
    }

    private static IDailyJournalRepository GetRepository(ReaderTestDatabase database) =>
        database.ServiceProvider.GetRequiredService<IDailyJournalRepository>();

    private static async Task<Guid> SeedAccountAsync(ReaderTestDatabase database, string name, bool isActive = true)
    {
        var account = new TradingAccountRecord
        {
            Id = Guid.NewGuid(),
            Name = name,
            AccountType = TradingAccountType.Personal,
            Currency = "USD",
            IsActive = isActive,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        };
        await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
        context.TradingAccounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    private sealed class JournalTestClock(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan interval) => _utcNow += interval;
    }

    private sealed class InterceptingFactory(DbContextOptions<JournalDbContext> options)
        : IDbContextFactory<JournalDbContext>
    {
        public JournalDbContext CreateDbContext() => new(options);

        public static async Task<InterceptingFactory> CreateAsync(ReaderTestDatabase database, IInterceptor interceptor)
        {
            await using JournalDbContext context = await database.ContextFactory.CreateDbContextAsync();
            var options = new DbContextOptionsBuilder<JournalDbContext>()
                .UseSqlite(context.Database.GetConnectionString())
                .AddInterceptors(interceptor)
                .Options;
            return new InterceptingFactory(options);
        }
    }

    private sealed class ConcurrentStartFactory(IDbContextFactory<JournalDbContext> inner)
        : IDbContextFactory<JournalDbContext>
    {
        private readonly TaskCompletionSource _bothCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public JournalDbContext CreateDbContext() => inner.CreateDbContext();

        public async Task<JournalDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            JournalDbContext context = await inner.CreateDbContextAsync(cancellationToken);
            if (Interlocked.Increment(ref _arrivals) == 2)
            {
                _bothCreated.TrySetResult();
            }

            try
            {
                await _bothCreated.Task.WaitAsync(cancellationToken);
                return context;
            }
            catch
            {
                await context.DisposeAsync();
                throw;
            }
        }
    }

    private sealed class RejectSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Journal retrieval must not save changes.");
    }

    private sealed class FailAfterSaveInterceptor : SaveChangesInterceptor
    {
        public bool SaveCompleted { get; private set; }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            SaveCompleted = true;
            throw new InvalidOperationException("Injected failure after saving journal changes.");
        }
    }

    private sealed class CancelAfterSaveInterceptor(CancellationTokenSource cancellation) : SaveChangesInterceptor
    {
        public bool SaveCompleted { get; private set; }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            SaveCompleted = true;
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }
}
