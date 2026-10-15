using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;
using PersonalTradingJournal.Infrastructure.PeriodReviews;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.DailyReview;

public sealed class PeriodReviewRepositoryTests
{
    internal static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    internal static PeriodReviewQuery Query(Guid? account = null, ReviewPeriodKind kind = ReviewPeriodKind.Week) => new(kind, new(2026, 11, 1), account);
    internal static PeriodReviewSnapshot Snapshot(PeriodReviewQuery? query = null, DateTimeOffset? generated = null, string label = "Saved original")
    {
        var q = query ?? Query(); var previous = PeriodComparisonEvidence.PreviousQuery(q);
        var journal = new DailyReviewJournalEvidence(Guid.NewGuid(), q.FirstDate, q.Scope.AccountId,
            q.Scope.AccountId.HasValue ? label : null, q.Scope.AccountId.HasValue ? DailyJournalAccountState.Active : DailyJournalAccountState.AllAccounts,
            "Synthetic exact text\nБългарски", new("", "Partial answer", ""), true, 1, q.FromUtc, q.FromUtc);
        var pair = new PeriodComparisonEvidence(new(previous, [], [], [], [], [], [], []), new(q, [], [], [journal], [], [], [], []), PeriodSourceConsistency.SingleReadTransaction);
        var packet = PeriodCoachingProviderTests.Packet(pair);
        return FromPacket(packet, generated ?? Now);
    }
    internal static PeriodReviewSnapshot FromPacket(PeriodCoachingPacket packet, DateTimeOffset? time = null) => PeriodReviewSnapshot.Create(
        new(PeriodCoachingStatus.Success, PeriodCoachingResponseValidator.Validate(PeriodCoachingProviderTests.Review(packet), packet).Response,
            packet, new("Fake", "synthetic", "client", "req_synthetic", "resp_synthetic", new(12, 2, 8, 20)), "not saved"), time ?? Now);
    private static IPeriodReviewRepository Store(ReaderTestDatabase db) => db.ServiceProvider.GetRequiredService<IPeriodReviewRepository>();

    [Theory] [InlineData(20)] [InlineData(21)] [InlineData(40)]
    public async Task StableBoundedPagingSeparatesPeriodKindAndExactScope(int count)
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var repository = Store(db); var account = Guid.NewGuid(); var q = Query(account);
        var expected = new List<SavedPeriodReview>();
        for (int i = 0; i < count; i++) expected.Add(await repository.SaveAsync(Snapshot(q, Now.AddMinutes(i / 2))));
        await repository.SaveAsync(Snapshot()); await repository.SaveAsync(Snapshot(Query(Guid.NewGuid())));
        await repository.SaveAsync(Snapshot(Query(account, ReviewPeriodKind.Month)));
        await repository.SaveAsync(Snapshot(PeriodComparisonEvidence.PreviousQuery(q)));
        var actual = new List<Guid>();
        for (int page = 1; page <= (count + 19) / 20; page++)
        {
            var result = await repository.BrowseAsync(new(q, page)); Assert.Equal(count, result.TotalCount);
            Assert.Equal(page > 1, result.HasPrevious); Assert.Equal(page * 20 < count, result.HasNext);
            actual.AddRange(result.Items.Select(i => i.Id)); Assert.All(result.Items, r => Assert.Equal(account, r.Scope.AccountId));
        }
        Assert.Equal(expected.OrderByDescending(r => r.Summary.GeneratedAtUtc).ThenBy(r => r.Summary.Id.ToString(), StringComparer.Ordinal).Select(r => r.Summary.Id), actual);
        Assert.Single((await repository.BrowseAsync(new(Query()))).Items); // Not the union of exact-account histories.
        Assert.Single((await repository.BrowseAsync(new(Query(account, ReviewPeriodKind.Month)))).Items);
        Assert.Empty((await repository.BrowseAsync(new(q, 4))).Items);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task AfterInsertFailureOrCancellationRollsBackWholeSnapshot(bool cancel)
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); using var cts = new CancellationTokenSource();
        var interceptor = new AfterInsert(() => { if (cancel) cts.Cancel(); else throw new IOException("synthetic private failure"); });
        var repository = new PeriodReviewRepository(await Factory.Create(db, interceptor)); var snapshot = Snapshot();
        await Assert.ThrowsAnyAsync<Exception>(() => repository.SaveAsync(snapshot, cts.Token)); Assert.True(interceptor.Inserted);
        Assert.Null(await Store(db).GetAsync(snapshot.Review.Summary.Id)); Assert.Equal(0, (await Store(db).BrowseAsync(new(Query()))).TotalCount);
        Assert.Equal(snapshot.Review, await Store(db).SaveAsync(snapshot)); // Explicit save, no regeneration or automatic retry.
    }

    [Fact]
    public async Task CancellationAfterCommitFenceReturnsSavedAndDuplicateIdCannotOverwrite()
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); using var cts = new CancellationTokenSource();
        var commit = new AtCommit(cts); var store = new PeriodReviewRepository(await Factory.Create(db, commit)); var snapshot = Snapshot();
        Assert.Equal(snapshot.Review, await store.SaveAsync(snapshot, cts.Token)); Assert.True(cts.IsCancellationRequested);
        Assert.Equal(snapshot.Review, await Store(db).GetAsync(snapshot.Review.Summary.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => Store(db).SaveAsync(snapshot));
        Assert.Single((await Store(db).BrowseAsync(new(Query()))).Items);
    }

    [Fact]
    public async Task ConcurrentIndependentSavesAndConfirmedDeletesDoNotTouchOtherHistory()
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var store = Store(db);
        var journal = (await db.ServiceProvider.GetRequiredService<IDailyJournalRepository>().CreateAsync(new(Query().FirstDate, null, "Keep"))).Journal!;
        var dailyPacket = CoachingEvidencePacketBuilder.Build(new(new(Query().FirstDate), [], [])).Packet!;
        var daily = CoachingAnalysisSnapshot.Create(dailyPacket, new(CoachingGenerationStatus.Success,
            new(dailyPacket.ContractVersion, dailyPacket.PacketId, new("No activity", ["calculated:day"]), [], [], [], []),
            new("Fake", "synthetic", "client"), ""), Now);
        await db.ServiceProvider.GetRequiredService<ICoachingAnalysisRepository>().SaveAsync(daily);
        var saved = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => store.SaveAsync(Snapshot()))));
        Assert.Equal(4, (await store.BrowseAsync(new(Query()))).TotalCount);
        Guid id = saved[0].Summary.Id;
        Assert.Equal(PeriodReviewDeleteStatus.NotConfirmed, await store.DeleteAsync(id, false)); Assert.NotNull(await store.GetAsync(id));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.DeleteAsync(id, true, new(true)));
        var deletes = await Task.WhenAll(Task.Run(() => store.DeleteAsync(id, true)), Task.Run(() => store.DeleteAsync(id, true)));
        Assert.Equal(new[] { PeriodReviewDeleteStatus.Deleted, PeriodReviewDeleteStatus.NotFound }, deletes.Order());
        Assert.Equal(3, (await store.BrowseAsync(new(Query()))).TotalCount);
        Assert.Equal(daily.Analysis, await db.ServiceProvider.GetRequiredService<ICoachingAnalysisRepository>().GetAsync(daily.Analysis.Summary.Id));
        Assert.Equal(journal.Entry.Text, (await db.ServiceProvider.GetRequiredService<IDailyJournalRepository>().GetAsync(Query().FirstDate))!.Entry.Text);
    }

    [Fact]
    public async Task BoundedReadsUseSavedScopesWithoutPayloadsOrCurrentAccountJoins()
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var account = Guid.NewGuid(); var exact = Snapshot(Query(account));
        await Store(db).SaveAsync(exact); await Store(db).SaveAsync(Snapshot(Query(account), Now.AddMinutes(-1), "Older label")); await Store(db).SaveAsync(Snapshot());
        var reads = new Reads(); var readOnly = new PeriodReviewRepository(await Factory.Create(db, reads, true));
        Assert.Equal(exact.Review, await readOnly.GetAsync(exact.Review.Summary.Id)); reads.Sql.Clear();
        Assert.Equal(2, (await readOnly.BrowseAsync(new(Query(account)))).TotalCount);
        var scopes = await readOnly.BrowseScopesAsync(new(Query(), 1, 1)); Assert.Equal(2, scopes.TotalCount); Assert.Null(Assert.Single(scopes.Items).Scope.AccountId);
        var second = Assert.Single((await readOnly.BrowseScopesAsync(new(Query(), 2, 1))).Items);
        Assert.Equal(account, second.Scope.AccountId); Assert.Equal("Saved original", second.AccountDisplayName);
        Assert.Equal(6, reads.Sql.Count);
        Assert.All(reads.Sql, sql => { Assert.StartsWith("SELECT", sql); Assert.DoesNotContain("EvidenceJson", sql); Assert.DoesNotContain("ResponseJson", sql); Assert.DoesNotContain("TradingAccounts", sql); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readOnly.BrowseAsync(new(Query()), new(true)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readOnly.GetAsync(exact.Review.Summary.Id, new(true)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store(db).SaveAsync(Snapshot(), new(true)));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CountAndPageRemainConsistentAcrossConcurrentCommit(bool discovery)
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var first = await Store(db).SaveAsync(Snapshot());
        var reads = new Reads(); bool committed = false;
        reads.BeforeRead = async count =>
        {
            if (count != 2) return;
            await Store(db).SaveAsync(Snapshot(discovery ? Query(Guid.NewGuid()) : Query(), Now.AddHours(1)));
            committed = true;
        };
        var reader = new PeriodReviewRepository(await Factory.Create(db, reads, true));
        if (discovery)
        {
            var page = await reader.BrowseScopesAsync(new(Query())); Assert.Equal(1, page.TotalCount);
            Assert.Null(Assert.Single(page.Items).Scope.AccountId);
        }
        else
        {
            var page = await reader.BrowseAsync(new(Query())); Assert.Equal(1, page.TotalCount);
            Assert.Equal(first.Summary.Id, Assert.Single(page.Items).Id);
        }
        Assert.True(committed); reads.BeforeRead = null;
        Assert.Equal(2, discovery ? (await reader.BrowseScopesAsync(new(Query()))).TotalCount : (await reader.BrowseAsync(new(Query()))).TotalCount);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task DeleteFailureOrCancellationAfterStatementRollsBack(bool cancel)
    {
        await using var db = await ReaderTestDatabase.CreateAsync(); var saved = await Store(db).SaveAsync(Snapshot());
        using var cts = new CancellationTokenSource();
        var hook = new AfterDelete(() => { if (cancel) cts.Cancel(); else throw new IOException("Synthetic failure"); });
        var store = new PeriodReviewRepository(await Factory.Create(db, hook));
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.DeleteAsync(saved.Summary.Id, true, cts.Token));
        else await Assert.ThrowsAsync<IOException>(() => store.DeleteAsync(saved.Summary.Id, true));
        Assert.True(hook.Deleted); Assert.Equal(saved, await Store(db).GetAsync(saved.Summary.Id));
        Assert.Single((await Store(db).BrowseAsync(new(Query()))).Items);
    }

    private sealed class AfterDelete(Action action) : DbCommandInterceptor
    {
        public bool Deleted;
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData data,
            int result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("DELETE", StringComparison.Ordinal)) { Deleted = true; action(); }
            return ValueTask.FromResult(result);
        }
    }

    internal sealed class Factory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    {
        public JournalDbContext CreateDbContext() => new(options);
        internal static async Task<Factory> Create(ReaderTestDatabase db, IInterceptor interceptor, bool readOnly = false)
        {
            await using var context = await db.ContextFactory.CreateDbContextAsync();
            var cs = new SqliteConnectionStringBuilder(context.Database.GetConnectionString()) { Pooling = false };
            if (readOnly) cs.Mode = SqliteOpenMode.ReadOnly;
            return new(new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(cs.ToString()).AddInterceptors(interceptor).Options);
        }
    }
    private sealed class AfterInsert(Action action) : SaveChangesInterceptor
    {
        public bool Inserted;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
        { Inserted = true; action(); return ValueTask.FromResult(result); }
    }
    private sealed class AtCommit(CancellationTokenSource cts) : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData data,
            InterceptionResult result, CancellationToken cancellationToken = default)
        { Assert.False(cancellationToken.CanBeCanceled); cts.Cancel(); return ValueTask.FromResult(result); }
    }
    private sealed class Reads : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];
        public Func<int, Task>? BeforeRead;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        { Sql.Add(command.CommandText); if (BeforeRead is { } hook) await hook(Sql.Count); return result; }
    }
}
