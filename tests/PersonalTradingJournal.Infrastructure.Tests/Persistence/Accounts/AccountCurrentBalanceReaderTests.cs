using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Accounts;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Accounts;

public sealed class AccountCurrentBalanceReaderTests
{
    [Fact]
    public async Task BatchedReadUsesDomainGrossAndAllocatedExecutionCostsWithoutWritesAndRefreshesAfterChanges()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var now = new DateTimeOffset(2026, 10, 2, 18, 0, 0, TimeSpan.Zero);
        var accounts = db.ServiceProvider.GetRequiredService<ITradingAccountStore>();
        var a = new TradingAccount("A", TradingAccountType.Personal, null, null, "USD", 1000, now);
        var b = new TradingAccount("B", TradingAccountType.Personal, null, null, "EUR", 2000, now);
        await accounts.AddAsync(a); await accounts.AddAsync(b);
        for (int i = 0; i < 20; i++) await accounts.AddAsync(new("Empty " + i, TradingAccountType.Personal, null, null, "USD", 1000, now));
        var instrument = new Instrument("TEST", "Synthetic", AssetClass.Futures, "CME", "USD", .25m, .25m, now);
        await db.ServiceProvider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        var store = db.ServiceProvider.GetRequiredService<ITradeStore>();
        async Task<Trade> Add(Guid owner, string currency, decimal exit, decimal? c1, decimal? f1, decimal? c2, decimal? f2, bool partial = false)
        {
            var id = Guid.NewGuid();
            var trade = Trade.Rehydrate(id, owner, instrument.Id, new(1, currency), null,
                [new(id, 1, now.AddHours(-1), ExecutionSide.Buy, 2, 100, c1, f1, null, null, null),
                 new(id, 2, now, ExecutionSide.Sell, partial ? 1 : 2, exit, c2, f2, null, null, null)], now, now);
            await store.AddAsync(trade); return trade;
        }
        var known = await Add(a.Id, "USD", 110, 1, 2, 3, 4);
        var estimated = await Add(a.Id, "USD", 110, null, 1, 2, null);
        await Add(a.Id, "EUR", 599.5m, 0, 0, 0, 0);
        await Add(a.Id, "USD", 999, null, null, null, null, partial: true);
        await Add(b.Id, "EUR", 95, 0, 0, 0, 0);
        Assert.Equal(10, known.NetPnL); Assert.Null(estimated.NetPnL);

        await using var connection = await db.ContextFactory.CreateDbContextAsync();
        var counter = new ReadsOnly();
        var options = new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(connection.Database.GetDbConnection().ConnectionString)
            .AddInterceptors(counter).Options;
        var reader = new TradingAccountReader(new Factory(options));
        var result = await reader.GetAllWithBalancesAsync();
        Assert.Equal(3, counter.Selects); // Independent of Account and Trade count.
        var balance = result.Single(x => x.Id == a.Id).CurrentBalance!;
        Assert.Equal(1027, balance.Value); Assert.True(balance.IsEstimated);
        Assert.Equal(2, balance.ClosedTradeCount); Assert.Equal(1, balance.TradesWithUnknownCosts);
        Assert.Equal(1, balance.UnknownCommissionCount); Assert.Equal(1, balance.UnknownFeeCount);
        Assert.Equal(1, balance.OpenTradeCount); Assert.Equal(1, balance.OtherCurrencyTradeCount);
        Assert.Equal(1990, result.Single(x => x.Id == b.Id).CurrentBalance!.Value);
        Assert.All(result.Where(x => x.Name.StartsWith("Empty")), x => Assert.Equal(1000, x.CurrentBalance!.Value));
        Assert.Null((await db.ServiceProvider.GetRequiredService<ITradeDetailReader>().GetByIdAsync(estimated.Id))!.NetPnL);

        // Account edits select historical currency, never convert prior economics.
        await new UpdateTradingAccountUseCase(accounts, TimeProvider.System).ExecuteAsync(new(a.Id, a.Name, a.AccountType, null, null, "EUR", 2000));
        Assert.Equal(2999, (await reader.GetAllWithBalancesAsync()).Single(x => x.Id == a.Id).CurrentBalance!.Value);
        var delete = db.ServiceProvider.GetRequiredService<IAccountTradeDeletionStore>();
        await delete.DeleteAsync((await delete.PrepareAsync(a.Id))!);
        var after = await reader.GetAllWithBalancesAsync();
        Assert.Equal(2000, after.Single(x => x.Id == a.Id).CurrentBalance!.Value);
        Assert.False(after.Single(x => x.Id == a.Id).CurrentBalance!.IsEstimated);
        Assert.Equal(1990, after.Single(x => x.Id == b.Id).CurrentBalance!.Value);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetAllWithBalancesAsync(new(true)));
    }

    private sealed class Factory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    { public JournalDbContext CreateDbContext() => new(options); }
    private sealed class ReadsOnly : DbCommandInterceptor
    {
        public int Selects;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<DbDataReader> result, CancellationToken token = default)
        {
            Assert.StartsWith("SELECT", command.CommandText.TrimStart()); Selects++;
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<int> result, CancellationToken token = default) => throw new InvalidOperationException("Read must not write.");
    }
}
