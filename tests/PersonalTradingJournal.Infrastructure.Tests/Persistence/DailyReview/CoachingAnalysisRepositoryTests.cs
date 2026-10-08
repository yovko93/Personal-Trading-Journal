using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.DailyReview.Coaching;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.DailyReview;

public sealed class CoachingAnalysisRepositoryTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 20, 0, 0, TimeSpan.Zero);
    private static CoachingAnalysisSnapshot Snapshot(CoachingEvidencePacket packet, DateTimeOffset? time = null) =>
        CoachingAnalysisSnapshot.Create(packet, new(CoachingGenerationStatus.Success,
            new(packet.ContractVersion, packet.PacketId, new("Snapshot facts.", new[] { "calculated:day" }), [], [], [], []),
            new("Fake", "test-model", "client-test", "req_test", "resp_test", new(10, 2, 5, 15)), "Ignored diagnostic"),
            time ?? Now);
    private static CoachingEvidencePacket EmptyPacket(Guid? account = null, DateOnly? date = null) =>
        CoachingEvidencePacketBuilder.Build(new(new(date ?? Day, account), [], [])).Packet!;
    private static ICoachingAnalysisRepository Repository(ReaderTestDatabase db) =>
        db.ServiceProvider.GetRequiredService<ICoachingAnalysisRepository>();

    [Fact]
    public async Task CompleteSnapshotsSurviveTradeJournalAndAccountChangesAndDeletion()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var account = new TradingAccount("P 21", TradingAccountType.Personal, null, null, "USD", 0m, Now);
        var instrument = new Instrument("SNAP", "Snapshot", AssetClass.Futures, "CME", "USD", .25m, .25m, Now);
        await db.ServiceProvider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        await db.ServiceProvider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        var journals = db.ServiceProvider.GetRequiredService<IDailyJournalRepository>();
        var original = (await journals.CreateAsync(new(Day, account.Id, "  Original journal\n", true))).Journal!.Entry;
        var allJournal = (await journals.CreateAsync(new(Day, null, "Aggregate journal", false))).Journal!.Entry;
        Guid tradeId = Guid.NewGuid();
        var trade = Trade.Rehydrate(tradeId, account.Id, instrument.Id, new(1, "EUR"), null,
            [new(tradeId, 1, Now.AddHours(-2), ExecutionSide.Buy, 1, 300, null, 0, null, null, null),
             new(tradeId, 2, Now.AddHours(-1), ExecutionSide.Sell, 1, 15, 0, 0, null, null, null)], Now, Now);
        Guid otherTradeId = Guid.NewGuid();
        var otherTrade = Trade.Rehydrate(otherTradeId, account.Id, instrument.Id, new(1, "USD"), null,
            [new(otherTradeId, 1, Now.AddHours(-3), ExecutionSide.Buy, 1, 100, 0, 0, null, null, null),
             new(otherTradeId, 2, Now.AddHours(-2), ExecutionSide.Sell, 1, 100, 0, 0, null, null, null)], Now, Now);
        await using (var seed = await db.ContextFactory.CreateDbContextAsync())
        {
            foreach (var source in new[] { trade, otherTrade })
            {
                seed.Trades.Add(TradePersistenceMapper.ToRecord(source));
                seed.TradeExecutions.AddRange(source.Executions.Select(TradeExecutionPersistenceMapper.ToRecord));
                seed.TradeBrowse.Add(TradeBrowsePersistenceMapper.ToRecord(source));
            }
            await seed.SaveChangesAsync();
        }
        var reader = db.ServiceProvider.GetRequiredService<IDailyReviewEvidenceReader>();
        var packet = CoachingEvidencePacketBuilder.Build(await reader.GetAsync(new(Day))).Packet!;
        var exactPacket = CoachingEvidencePacketBuilder.Build(await reader.GetAsync(new(Day, account.Id))).Packet!;
        var repository = Repository(db);
        var saved = await repository.SaveAsync(Snapshot(packet));
        var exact = await repository.SaveAsync(Snapshot(exactPacket));
        Assert.Equal("P 21", exact.Summary.AccountDisplayName);
        Assert.Equal(CoachingAnalysisScopeKind.ExactAccount, exact.Summary.Scope.Kind);
        Assert.Equal(2, packet.Content.CalculatedFacts.Currencies.Count);
        Assert.Equal(-285, packet.Content.CalculatedFacts.Currencies.Single(c => c.Currency == "EUR").Gross.Metrics.Total);
        Assert.Null(packet.Content.CalculatedFacts.Currencies.Single(c => c.Currency == "EUR").Net.Metrics.Total);
        Assert.Equal(0, packet.Content.CalculatedFacts.Currencies.Single(c => c.Currency == "USD").Net.Metrics.Total);
        Assert.Contains(packet.Content.UntrustedJournalObservations, j => j.JournalId == original.Id && j.IsDraft && j.Revision == 1);
        Assert.Contains(packet.Content.UntrustedJournalObservations, j => j.JournalId == allJournal.Id && !j.IsDraft);
        Assert.Equal(packet.Json, saved.EvidenceJson);

        await journals.UpdateAsync(new(original.Id, original.Revision, "Edited journal", true));
        await using (var edit = await db.ContextFactory.CreateDbContextAsync())
        {
            await edit.TradingAccounts.Where(a => a.Id == account.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Name, "Renamed"));
            await edit.TradeExecutions.Where(e => e.TradeId == tradeId).ExecuteUpdateAsync(s => s.SetProperty(e => e.Commission, 1m));
        }
        Assert.Equal(saved, await repository.GetAsync(saved.Summary.Id));
        await journals.DeleteAsync(new(original.Id, 2));
        await journals.DeleteAsync(new(allJournal.Id, 1));
        await using (var delete = await db.ContextFactory.CreateDbContextAsync())
        {
            await delete.Trades.Where(t => t.Id == tradeId || t.Id == otherTradeId).ExecuteDeleteAsync();
            await delete.TradingAccounts.Where(a => a.Id == account.Id).ExecuteDeleteAsync();
        }
        Assert.Equal(saved, await repository.GetAsync(saved.Summary.Id));
        Assert.Equal(exact, await repository.GetAsync(exact.Summary.Id));
        var current = await reader.GetAsync(new(Day));
        Assert.Empty(current.Trades); Assert.Empty(current.Journals);
        Assert.Equal(exact.Summary, Assert.Single((await repository.BrowseAsync(new(Day, new(account.Id)))).Items));
    }

    [Fact]
    public async Task PagingIsDateAndExactScopeBoundedNewestFirstWithStableTies()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var repository = Repository(db);
        var account = Guid.NewGuid(); // Unavailable Account is valid historical identity, never an FK.
        var packet = EmptyPacket(account);
        var saved = new List<SavedCoachingAnalysis>();
        for (int i = 0; i < 21; i++)
            saved.Add(await repository.SaveAsync(Snapshot(packet, Now.AddMinutes(i / 2))));
        await repository.SaveAsync(Snapshot(EmptyPacket()));
        await repository.SaveAsync(Snapshot(EmptyPacket(Guid.NewGuid())));
        await repository.SaveAsync(Snapshot(EmptyPacket(account, Day.AddDays(-1))));
        var expected = saved.OrderByDescending(s => s.Summary.GeneratedAtUtc).ThenBy(s => s.Summary.Id.ToString(), StringComparer.Ordinal)
            .Select(s => s.Summary.Id).ToArray();
        var actual = new List<Guid>();
        for (int page = 1; page <= 3; page++)
        {
            var result = await repository.BrowseAsync(new(Day, new(account), page, 10));
            Assert.Equal(21, result.TotalCount);
            Assert.Equal(page > 1, result.HasPrevious);
            Assert.Equal(page < 3, result.HasNext);
            Assert.Equal(page == 3 ? 1 : 10, result.Items.Count);
            actual.AddRange(result.Items.Select(i => i.Id));
            Assert.All(result.Items, i => Assert.Null(i.AccountDisplayName));
        }
        Assert.Equal(expected, actual);
        Assert.Empty((await repository.BrowseAsync(new(Day, new(account), 4, 10))).Items);
        var aggregate = await repository.BrowseAsync(new(Day, new()));
        Assert.Equal(1, aggregate.TotalCount); // Not all exact-account analyses.
        Assert.Equal(CoachingAnalysisScopeKind.AllAccounts, Assert.Single(aggregate.Items).Scope.Kind);
        Assert.Empty((await repository.BrowseAsync(new(Day.AddDays(1), new()))).Items);
    }

    [Fact]
    public async Task DeleteOneAnalysisLeavesOtherAnalysesAndJournalsUnchanged()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var journals = db.ServiceProvider.GetRequiredService<IDailyJournalRepository>();
        var journal = (await journals.CreateAsync(new(Day, null, "Keep journal", true))).Journal!;
        var packet = CoachingEvidencePacketBuilder.Build(await db.ServiceProvider.GetRequiredService<IDailyReviewEvidenceReader>()
            .GetAsync(new(Day))).Packet!;
        var repository = Repository(db);
        var first = await repository.SaveAsync(Snapshot(packet));
        var second = await repository.SaveAsync(Snapshot(packet));
        Assert.True(await repository.DeleteAsync(first.Summary.Id));
        Assert.False(await repository.DeleteAsync(first.Summary.Id));
        Assert.Null(await repository.GetAsync(first.Summary.Id));
        Assert.Equal(second, await repository.GetAsync(second.Summary.Id));
        Assert.Equal(journal.Entry.Text, (await journals.GetAsync(Day))!.Entry.Text);
        Assert.Single(await journals.GetHistoryAsync(journal.Entry.Id));
        Assert.Equal(1, (await repository.BrowseAsync(new(Day, new()))).TotalCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureOrCancellationAfterInsertRollsBackEntireSnapshot(bool cancel)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        using var cts = new CancellationTokenSource();
        var interceptor = new AfterInsert(cts, cancel);
        var factory = await Factory.Create(db, interceptor);
        var repository = new CoachingAnalysisRepository(factory);
        var snapshot = Snapshot(EmptyPacket());
        await Assert.ThrowsAnyAsync<Exception>(() => repository.SaveAsync(snapshot, cts.Token));
        Assert.True(interceptor.Inserted);
        Assert.Null(await Repository(db).GetAsync(snapshot.Analysis.Summary.Id));
        Assert.Equal(0, (await Repository(db).BrowseAsync(new(Day, new()))).TotalCount);
        // A later explicit storage operation succeeds without regenerating or a partial row collision.
        Assert.Equal(snapshot.Analysis, await Repository(db).SaveAsync(snapshot));
    }

    [Fact]
    public async Task PrecancelledOperationsDoNotWriteOrDeleteAndReadsWorkReadOnly()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var repository = Repository(db);
        var saved = await repository.SaveAsync(Snapshot(EmptyPacket()));
        var cancelled = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.SaveAsync(Snapshot(EmptyPacket()), cancelled));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.DeleteAsync(saved.Summary.Id, cancelled));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetAsync(saved.Summary.Id, cancelled));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.BrowseAsync(new(Day, new()), cancelled));
        var commands = new ReadCommands();
        var readOnly = new CoachingAnalysisRepository(await Factory.Create(db, commands, readOnly: true));
        Assert.Equal(saved, await readOnly.GetAsync(saved.Summary.Id));
        commands.Sql.Clear();
        Assert.Single((await readOnly.BrowseAsync(new(Day, new()))).Items);
        Assert.Equal(2, commands.Sql.Count);
        Assert.All(commands.Sql, sql =>
        {
            Assert.StartsWith("SELECT", sql.TrimStart(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("EvidenceJson", sql);
            Assert.DoesNotContain("ResponseJson", sql);
            Assert.DoesNotContain("JOIN", sql, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task DuplicateSnapshotIdCannotReplaceHistoricalEvidence()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var snapshot = Snapshot(EmptyPacket());
        var repository = Repository(db);
        await repository.SaveAsync(snapshot);
        await Assert.ThrowsAsync<DbUpdateException>(() => repository.SaveAsync(snapshot));
        Assert.Equal(snapshot.Analysis, await repository.GetAsync(snapshot.Analysis.Summary.Id));
        Assert.Single((await repository.BrowseAsync(new(Day, new()))).Items);
    }

    private sealed class Factory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    {
        public JournalDbContext CreateDbContext() => new(options);
        public static async Task<Factory> Create(ReaderTestDatabase db, IInterceptor interceptor, bool readOnly = false)
        {
            await using var context = await db.ContextFactory.CreateDbContextAsync();
            var cs = new SqliteConnectionStringBuilder(context.Database.GetConnectionString());
            if (readOnly) cs.Mode = SqliteOpenMode.ReadOnly;
            return new(new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(cs.ToString()).AddInterceptors(interceptor).Options);
        }
    }
    private sealed class AfterInsert(CancellationTokenSource cts, bool cancel) : SaveChangesInterceptor
    {
        public bool Inserted { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken token = default)
        {
            Inserted = true;
            if (cancel) { cts.Cancel(); return ValueTask.FromResult(result); }
            throw new IOException("Simulated failure after insertion before commit");
        }
    }
    private sealed class ReadCommands : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken token = default)
        { Sql.Add(command.CommandText); return ValueTask.FromResult(result); }
    }
}
