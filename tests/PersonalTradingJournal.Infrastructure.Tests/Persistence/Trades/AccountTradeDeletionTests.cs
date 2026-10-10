using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

public sealed class AccountTradeDeletionTests
{
    [Fact]
    public async Task WriterQueuedBehindRecheckCannotHaveItsNewTradeDeleted()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var g = await Seed(db);
        var store = db.ServiceProvider.GetRequiredService<IAccountTradeDeletionStore>();
        var plan = (await store.PrepareAsync(g.Account))!;
        await using var source = await db.ContextFactory.CreateDbContextAsync();
        var added = await source.Trades.AsNoTracking().SingleAsync(t => t.Id == g.Trade);
        added.Id = Guid.NewGuid();
        var gate = new ReadGate();
        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite(source.Database.GetDbConnection().ConnectionString).AddInterceptors(gate).Options;
        var deleting = Task.Run(() => new PersonalTradingJournal.Infrastructure.Trades.AccountTradeDeletionStore(new Factory(options)).DeleteAsync(plan));
        Task? writing = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var writerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            writing = Task.Run(async () =>
            {
                await using var writer = await db.ContextFactory.CreateDbContextAsync();
                writer.Trades.Add(added);
                writerStarted.TrySetResult();
                await writer.SaveChangesAsync();
            });
            await writerStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { gate.Release.TrySetResult(); }
        Assert.Equal(AccountTradeDeletionStatus.Deleted, (await deleting.WaitAsync(TimeSpan.FromSeconds(15))).Status);
        await writing!.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(await source.Trades.AnyAsync(t => t.Id == added.Id));
        Assert.True(await source.Trades.AnyAsync(t => t.Id == g.OtherTrade));
        Assert.False(await source.Trades.AnyAsync(t => t.Id == g.Trade));
    }

    private sealed class Factory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    {
        public JournalDbContext CreateDbContext() => new(options);
    }
    private sealed class ReadGate : DbCommandInterceptor
    {
        internal TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _once;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _once, 1) == 0)
            {
                Assert.NotNull(command.Transaction); // Writer reservation precedes this recheck.
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 2, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DeletesOwnedGraphAndFilesButKeepsOtherAccountJournalCatalogAndHistoricalSnapshot()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var g = await Seed(db);
        var journals = db.ServiceProvider.GetRequiredService<IDailyJournalRepository>();
        var journal = await journals.CreateAsync(new(new DateOnly(2026, 10, 2), g.Account, "Keep this journal"));
        var evidenceReader = db.ServiceProvider.GetRequiredService<IDailyReviewEvidenceReader>();
        var packet = CoachingEvidencePacketBuilder.Build(await evidenceReader.GetAsync(new(new(2026, 10, 2), g.Account))).Packet!;
        var history = db.ServiceProvider.GetRequiredService<ICoachingAnalysisRepository>();
        var snapshot = CoachingAnalysisSnapshot.Create(packet, new(CoachingGenerationStatus.Success,
            new(packet.ContractVersion, packet.PacketId, new("Synthetic review", ["calculated:day"]), [], [], [], []),
            new("Fake", "test-model", "synthetic"), ""), Now);
        var saved = await history.SaveAsync(snapshot);
        var store = db.ServiceProvider.GetRequiredService<IAccountTradeDeletionStore>();
        var files = db.ServiceProvider.GetRequiredService<ITradeScreenshotFileStorage>();
        var plan = (await store.PrepareAsync(g.Account))!;
        Assert.Equal(1, plan.TradeCount);
        Assert.Equal(plan, await store.PrepareAsync(g.Account)); // Preparation writes nothing.
        await using (var file = await files.OpenReadAsync(g.Key)) Assert.NotNull(file);
        var result = await new DeleteAccountTradesUseCase(store, files).ExecuteAsync(plan);
        Assert.Equal(new(AccountTradeDeletionStatus.Deleted, 1, 0), result);
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(g.OtherTrade, (await context.Trades.SingleAsync()).Id);
        Assert.False(await context.TradeExecutions.AnyAsync(e => e.TradeId == g.Trade));
        Assert.False(await context.TradeBrowse.AnyAsync(e => e.TradeId == g.Trade));
        Assert.False(await context.TradeScreenshots.AnyAsync(e => e.TradeId == g.Trade));
        Assert.False(await context.TradeMistakes.AnyAsync(e => e.TradeId == g.Trade));
        Assert.Equal(2, await context.TradingAccounts.CountAsync());
        Assert.Single(await context.Instruments.ToArrayAsync());
        Assert.Single(await context.TradingSetups.ToArrayAsync());
        Assert.Single(await context.TradingMistakes.ToArrayAsync());
        Assert.Single(await context.DailyJournals.ToArrayAsync());
        Assert.Single(await context.DailyJournalRevisions.ToArrayAsync());
        Assert.Equal(saved, await history.GetAsync(saved.Summary.Id));
        Assert.Empty((await evidenceReader.GetAsync(new(new(2026, 10, 2), g.Account))).Trades);
        Assert.Null(await db.ServiceProvider.GetRequiredService<ITradeDetailReader>().GetByIdAsync(g.Trade));
        Assert.Null(await files.OpenReadAsync(g.Key));
        await using var otherFile = await files.OpenReadAsync(g.OtherKey);
        Assert.NotNull(otherFile);
    }

    [Theory]
    [InlineData("added-trade")]
    [InlineData("account")]
    [InlineData("execution")]
    [InlineData("screenshot")]
    [InlineData("mistake")]
    public async Task AnyConfirmedGraphChangeStopsTheWholeDeletion(string change)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var g = await Seed(db);
        var store = db.ServiceProvider.GetRequiredService<IAccountTradeDeletionStore>();
        var plan = (await store.PrepareAsync(g.Account))!;
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            switch (change)
            {
                case "added-trade":
                    var old = await context.Trades.AsNoTracking().SingleAsync(t => t.Id == g.Trade);
                    old.Id = Guid.NewGuid(); context.Trades.Add(old); break;
                case "account": (await context.TradingAccounts.SingleAsync(a => a.Id == g.Account)).Name = "Changed"; break;
                case "execution": (await context.TradeExecutions.FirstAsync(e => e.TradeId == g.Trade)).Price++; break;
                case "screenshot": (await context.TradeScreenshots.SingleAsync(e => e.TradeId == g.Trade)).Description = "Changed"; break;
                case "mistake": context.TradeMistakes.Remove(await context.TradeMistakes.SingleAsync(e => e.TradeId == g.Trade)); break;
            }
            await context.SaveChangesAsync();
        }
        Assert.Equal(AccountTradeDeletionStatus.Changed, (await store.DeleteAsync(plan)).Status);
        await using var check = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(change == "added-trade" ? 3 : 2, await check.Trades.CountAsync());
        Assert.Equal(2, await check.TradeScreenshots.CountAsync());
        await using var file = await db.ServiceProvider.GetRequiredService<ITradeScreenshotFileStorage>().OpenReadAsync(g.Key);
        Assert.NotNull(file);
    }

    [Fact]
    public async Task FailureAfterChildDeletesRollsBackAndNeverTouchesFiles()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var g = await Seed(db);
        var store = db.ServiceProvider.GetRequiredService<IAccountTradeDeletionStore>();
        var plan = (await store.PrepareAsync(g.Account))!;
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
            await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_delete BEFORE DELETE ON Trades BEGIN SELECT RAISE(ABORT, 'synthetic failure'); END;");
        await Assert.ThrowsAnyAsync<Exception>(() => new DeleteAccountTradesUseCase(store,
            db.ServiceProvider.GetRequiredService<ITradeScreenshotFileStorage>()).ExecuteAsync(plan));
        await using var check = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(2, await check.Trades.CountAsync());
        Assert.Equal(2, await check.TradeScreenshots.CountAsync());
        Assert.Single(await check.TradeMistakes.ToArrayAsync());
        await using var file = await db.ServiceProvider.GetRequiredService<ITradeScreenshotFileStorage>().OpenReadAsync(g.Key);
        Assert.NotNull(file);
    }

    [Fact]
    public async Task ZeroMissingCancelledAndRepeatedConfirmationNeverDeleteNewTrades()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var g = await Seed(db);
        var store = db.ServiceProvider.GetRequiredService<IAccountTradeDeletionStore>();
        Assert.Null(await store.PrepareAsync(Guid.NewGuid()));
        var plan = (await store.PrepareAsync(g.Account))!;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.PrepareAsync(g.Account, new(true)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.DeleteAsync(plan, new(true)));
        Assert.Equal(plan, await store.PrepareAsync(g.Account));
        Assert.Equal(AccountTradeDeletionStatus.Deleted, (await store.DeleteAsync(plan)).Status);
        Assert.Equal(AccountTradeDeletionStatus.Changed, (await store.DeleteAsync(plan)).Status);
        var empty = (await store.PrepareAsync(g.Account))!;
        Assert.Equal(0, empty.TradeCount);
        Assert.Equal(0, (await store.DeleteAsync(empty)).DeletedCount);
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        var replacement = await context.Trades.AsNoTracking().SingleAsync();
        replacement.Id = Guid.NewGuid(); replacement.TradingAccountId = g.Account;
        context.Trades.Add(replacement); await context.SaveChangesAsync();
        Assert.Equal(AccountTradeDeletionStatus.Changed, (await store.DeleteAsync(empty)).Status);
        Assert.Equal(2, await context.Trades.CountAsync());
    }

    [Fact]
    public async Task SharedFileKeyIsNotRemovedFromSurvivingTrade()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var g = await Seed(db);
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            (await context.TradeScreenshots.SingleAsync(s => s.TradeId == g.OtherTrade)).StorageKey = g.Key;
            await context.SaveChangesAsync();
        }
        var store = db.ServiceProvider.GetRequiredService<IAccountTradeDeletionStore>();
        var result = await store.DeleteAsync((await store.PrepareAsync(g.Account))!);
        Assert.Empty(result.ScreenshotStorageKeys);
        await using var file = await db.ServiceProvider.GetRequiredService<ITradeScreenshotFileStorage>().OpenReadAsync(g.Key);
        Assert.NotNull(file);
    }

    private sealed record Graph(Guid Account, Guid Trade, Guid OtherTrade, string Key, string OtherKey);
    private static async Task<Graph> Seed(ReaderTestDatabase db)
    {
        var account = new TradingAccount("Synthetic target", TradingAccountType.Personal, null, null, "USD", 0m, Now);
        var other = new TradingAccount("Synthetic other", TradingAccountType.Personal, null, null, "USD", 0m, Now);
        var instrument = new Instrument("TEST", "Synthetic", AssetClass.Futures, "CME", "USD", .25m, .25m, Now);
        await db.ServiceProvider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        await db.ServiceProvider.GetRequiredService<ITradingAccountStore>().AddAsync(other);
        await db.ServiceProvider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        var files = db.ServiceProvider.GetRequiredService<ITradeScreenshotFileStorage>();
        using var content = new MemoryStream([1, 2, 3]); // Storage tests don't decode synthetic file bytes.
        string key = await files.StoreAsync(content, ".png");
        content.Position = 0;
        string otherKey = await files.StoreAsync(content, ".png");
        Guid setup = Guid.NewGuid(), mistake = Guid.NewGuid();
        Trade Make(Guid owner, Guid? classification)
        {
            Guid id = Guid.NewGuid();
            return Trade.Rehydrate(id, owner, instrument.Id, new(1, "USD"), classification,
                [new(id, 1, Now.AddHours(-1), ExecutionSide.Buy, 1, 100, 0, 0, null, null, null),
                 new(id, 2, Now, ExecutionSide.Sell, 1, 110, 0, 0, null, null, null)], Now, Now);
        }
        var trade = Make(account.Id, setup); var otherTrade = Make(other.Id, null);
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        context.TradingSetups.Add(new() { Id = setup, Name = "Setup", IsActive = true, CreatedAtUtc = Now, UpdatedAtUtc = Now });
        context.TradingMistakes.Add(new() { Id = mistake, Name = "Mistake", IsActive = true, CreatedAtUtc = Now, UpdatedAtUtc = Now });
        foreach (var t in new[] { trade, otherTrade })
        {
            context.Trades.Add(TradePersistenceMapper.ToRecord(t));
            context.TradeExecutions.AddRange(t.Executions.Select(TradeExecutionPersistenceMapper.ToRecord));
            context.TradeBrowse.Add(TradeBrowsePersistenceMapper.ToRecord(t));
            context.TradeScreenshots.Add(new() { Id = Guid.NewGuid(), TradeId = t.Id, StorageKey = t == trade ? key : otherKey,
                FileName = "Synthetic.png", CreatedAtUtc = Now, UpdatedAtUtc = Now });
        }
        context.TradeMistakes.Add(new() { Id = Guid.NewGuid(), TradeId = trade.Id, TradingMistakeId = mistake, CreatedAtUtc = Now, UpdatedAtUtc = Now });
        await context.SaveChangesAsync();
        return new(account.Id, trade.Id, otherTrade.Id, key, otherKey);
    }
}
