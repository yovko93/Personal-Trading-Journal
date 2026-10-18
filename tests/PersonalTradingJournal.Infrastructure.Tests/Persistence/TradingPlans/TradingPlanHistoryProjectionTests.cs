using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Infrastructure.TradingPlans;
using static PersonalTradingJournal.Infrastructure.Tests.Persistence.TradingPlans.TradingPlanPersistenceTests;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.TradingPlans;

public sealed class TradingPlanHistoryProjectionTests
{
    [Fact]
    public async Task SummaryUsesOneSnapshotAcrossConcurrentCatalogChangeAndBoundedQueryCount()
    {
        await using var f = await Fixture.Create(); await f.CreatePlan();
        await using (var db = f.Context()) await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL");
        int commands = 0;
        var interceptor = new ReadObserver(async () =>
        {
            if (Interlocked.Increment(ref commands) != 1) return;
            await using var writer = f.Context();
            await writer.TradingAccounts.ExecuteUpdateAsync(s => s.SetProperty(a => a.Name, "New concurrent name"));
        });
        var store = new TradingPlanStore(f.Factory(interceptor), f.Clock);
        var rows = await store.HistoryAsync(new(f.Account.Id, f.Key.NewYorkDate, f.Key.NewYorkDate));
        Assert.Equal(f.Account.Name, Assert.Single(rows).Summary!.AccountName);
        Assert.InRange(commands, 1, 4);
        Assert.Equal("New concurrent name", Assert.Single(await f.Store.HistoryAsync(new(f.Account.Id, f.Key.NewYorkDate, f.Key.NewYorkDate))).Summary!.AccountName);
    }

    [Fact]
    public async Task ReadySummaryUsesFrozenLabelsAndDiscoveryIsBoundedAcrossRestart()
    {
        await using var f = await Fixture.Create(); var plan = await f.Ready(await f.CreatePlan());
        await using (var db = f.Context())
        {
            await db.TradingAccounts.ExecuteUpdateAsync(s => s.SetProperty(a => a.Name, "current name").SetProperty(a => a.IsActive, false));
            await db.Instruments.ExecuteUpdateAsync(s => s.SetProperty(i => i.Symbol, "CHANGED"));
        }
        await f.Restart(); var q = new TradingPlanHistoryQuery(f.Account.Id, new(2026, 10, 31), new(2026, 11, 1));
        var summary = Assert.Single(await f.Store.HistoryAsync(q)).Summary!;
        Assert.Equal(f.Account.Name, summary.AccountName); Assert.Contains("NQ", summary.Instruments);
        Assert.Equal(plan.LatestReadyRevision!.ReadyAtUtc, summary.SavedAtUtc);
        var scope = Assert.Single(await f.Store.HistoryAccountsAsync(q.FirstDate, q.LastDate, limit: 1));
        Assert.False(scope.IsActive); Assert.True(scope.IsAvailable); Assert.Equal(f.Account.Id, scope.AccountId);
        Assert.Empty(await f.Store.HistoryAccountsAsync(q.FirstDate, q.LastDate, offset: 1, limit: 1));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Store.HistoryAccountsAsync(q.FirstDate, q.LastDate, limit: 101));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Store.HistoryAccountsAsync(q.LastDate, q.FirstDate));
        using var ct = new CancellationTokenSource(); ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Store.HistoryAccountsAsync(q.FirstDate, q.LastDate, cancellationToken: ct.Token));
        Assert.Empty(await f.Store.HistoryAsync(new(f.Account.Id, new(2026, 10, 31), new(2026, 10, 31))));
    }

    private sealed class ReadObserver(Func<Task> action) : DbCommandInterceptor
    {
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        { await action(); return result; }
    }
}
