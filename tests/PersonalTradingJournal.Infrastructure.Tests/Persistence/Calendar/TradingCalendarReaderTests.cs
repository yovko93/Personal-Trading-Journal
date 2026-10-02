using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Calendar;

public sealed class TradingCalendarReaderTests
{
    private static readonly DateTimeOffset Audit = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InactiveAccountScopedMonthAndDayResultsShareHistoricalCurrenciesAndWeekendTotals()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        Guid account = await AddAccount(db, "Calendar historical");
        Guid other = await AddAccount(db, "Calendar other");
        Guid instrument = await AddInstrument(db);
        var store = db.ServiceProvider.GetRequiredService<ITradingAccountStore>();
        TradingAccount historical = (await store.GetByIdAsync(account))!;
        historical.Deactivate(Audit.AddDays(1));
        await store.UpdateAsync(historical);
        DateTimeOffset saturday = new(2026, 9, 5, 16, 0, 0, TimeSpan.Zero);
        await Add(db, Closed(account, instrument, saturday, -285m, null),
            Closed(account, instrument, saturday, 0m),
            Closed(account, instrument, saturday, 10m, currency: "EUR"),
            Closed(account, instrument, saturday.AddDays(1), 20m),
            Closed(other, instrument, saturday, 999m));
        var monthReader = db.ServiceProvider.GetRequiredService<ITradingCalendarReader>();
        var dayReader = db.ServiceProvider.GetRequiredService<ITradingCalendarDayReader>();
        var month = await monthReader.GetAsync(new(2026, 9, account));
        var day = await dayReader.GetAsync(new(new(2026, 9, 5), account));
        Assert.Equal(3, day.ClosedTradeCount);
        Assert.All(day.Trades, t => Assert.Equal(account, t.TradingAccountId));
        foreach (string currency in new[] { "USD", "EUR" })
        {
            var bucket = month.Currencies.Single(c => c.Currency == currency);
            var week = bucket.Weeks[0];
            var daily = day.Currencies.Single(c => c.Currency == currency).Metrics;
            Assert.Equal(daily, week.Days[5].Metrics);
            Assert.Equal(currency == "USD" ? -265m : 10m, week.EffectiveNetTotal);
            Assert.Equal(currency == "USD" ? 3 : 1, week.ClosedTradeCount);
            Assert.Equal(currency == "USD" ? 2 : 1, day.Trades.Count(t => t.Currency == currency));
        }
        Assert.Empty((await monthReader.GetAsync(new(2026, 10, account))).Currencies);
        Assert.Empty((await dayReader.GetAsync(new(new(2026, 10, 5), account))).Trades);
        var monthly = TradingCalendarMonthlyPnl.From(month);
        Assert.Equal(-265m, Assert.Single(monthly, m => m.Currency == "USD").Total);
        Assert.Equal(10m, Assert.Single(monthly, m => m.Currency == "EUR").Total);
        Assert.True(monthly.Single(m => m.Currency == "USD").IsEstimated);
        await using JournalDbContext check = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(5, await check.Trades.CountAsync());
        Assert.Equal(10, await check.TradeExecutions.CountAsync());
        Assert.Empty(check.ChangeTracker.Entries());
        Assert.Contains(await db.ServiceProvider.GetRequiredService<ITradingAccountReader>().GetAllAsync(),
            a => a.Id == account && !a.IsActive);
    }

    [Fact]
    public async Task MigratedSqliteUsesNewYorkClosuresFullGridAccountFilterAndNoWrites()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        Guid account = await AddAccount(db, "Calendar A");
        Guid other = await AddAccount(db, "Calendar B");
        Guid instrument = await AddInstrument(db);
        // DST starts Mar 8: 04:59 UTC is Mar 7 in New York; 05:00 UTC is Mar 8.
        await Add(db, Closed(account, instrument, new(2026, 3, 8, 4, 59, 0, TimeSpan.Zero), 3m),
            Closed(account, instrument, new(2026, 3, 8, 5, 0, 0, TimeSpan.Zero), 0m),
            Closed(account, instrument, new(2026, 3, 8, 12, 0, 0, TimeSpan.Zero), -5m, null),
            Closed(account, instrument, new(2026, 3, 9, 12, 0, 0, TimeSpan.Zero), 2m),
            Closed(other, instrument, new(2026, 3, 8, 12, 0, 0, TimeSpan.Zero), 100m),
            Closed(account, instrument, new(2026, 3, 8, 12, 0, 0, TimeSpan.Zero), 8m, currency: "EUR"),
            Open(account, instrument, new(2026, 3, 8, 12, 0, 0, TimeSpan.Zero)));

        ITradingCalendarReader reader = db.ServiceProvider.GetRequiredService<ITradingCalendarReader>();
        TradingCalendarMonth march = await reader.GetAsync(new(2026, 3, account));
        Assert.Equal(new DateOnly(2026, 2, 23), march.GridStart);
        Assert.Equal(new DateOnly(2026, 4, 5), march.GridEnd);
        var usd = Assert.Single(march.Currencies, x => x.Currency == "USD");
        TradingCalendarWeek dstWeek = Assert.Single(usd.Weeks, x => x.Monday == new DateOnly(2026, 3, 2));
        Assert.Equal(3, dstWeek.ClosedTradeCount); // Saturday + two Sunday, including the estimated loss.
        Assert.Equal(-2m, dstWeek.EffectiveNetTotal);
        Assert.True(dstWeek.IsEstimated);
        Assert.Null(dstWeek.Metrics!.Net.Total);
        Assert.Equal(3m, dstWeek.Days[5].EffectiveNetTotal);
        Assert.Equal(-5m, dstWeek.Days[6].EffectiveNetTotal);
        Assert.Equal(2, dstWeek.Days[6].ClosedTradeCount);
        Assert.Equal(2m, usd.Weeks[2].EffectiveNetTotal); // Monday after the DST Sunday.
        Assert.Equal(8m, Assert.Single(march.Currencies, x => x.Currency == "EUR")
            .Weeks[1].EffectiveNetTotal);

        TradingCalendarMonth all = await reader.GetAsync(new(2026, 3));
        Assert.Equal(98m, Assert.Single(all.Currencies, x => x.Currency == "USD")
            .Weeks[1].EffectiveNetTotal);
        TradingCalendarMonth unavailable = await reader.GetAsync(new(2026, 3, Guid.NewGuid()));
        Assert.Empty(unavailable.Currencies);
        Assert.Equal(march.GridDates, unavailable.GridDates);

        await using JournalDbContext check = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(7, await check.Trades.CountAsync());
        Assert.Equal(13, await check.TradeExecutions.CountAsync());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetAsync(new(2026, 3, account), cancellation.Token));
        Assert.Equal(7, await check.Trades.CountAsync());
    }

    [Fact]
    public async Task FallDstBoundaryUsesNewYorkDatesRatherThanFixedUtcOffset()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        Guid account = await AddAccount(db, "Fall DST");
        Guid instrument = await AddInstrument(db);
        await Add(db,
            Closed(account, instrument, new(2026, 11, 1, 3, 59, 0, TimeSpan.Zero), 1m), // Oct 31
            Closed(account, instrument, new(2026, 11, 1, 4, 0, 0, TimeSpan.Zero), 2m), // Nov 1 EDT
            Closed(account, instrument, new(2026, 11, 2, 4, 59, 0, TimeSpan.Zero), 3m), // Nov 1 EST
            Closed(account, instrument, new(2026, 11, 2, 5, 0, 0, TimeSpan.Zero), 4m)); // Nov 2

        TradingCalendarMonth november = await db.ServiceProvider.GetRequiredService<ITradingCalendarReader>()
            .GetAsync(new(2026, 11, account));
        TradingCalendarCurrency usd = Assert.Single(november.Currencies);
        TradingCalendarWeek first = usd.Weeks[0];
        Assert.Equal(new DateOnly(2026, 10, 26), first.Monday);
        Assert.Equal(1m, first.Days[5].EffectiveNetTotal);
        Assert.Equal(5m, first.Days[6].EffectiveNetTotal);
        Assert.Equal(3, first.ClosedTradeCount);
        Assert.Equal(6m, first.EffectiveNetTotal);
        Assert.Equal(4m, usd.Weeks[1].Days[0].EffectiveNetTotal);
        Assert.Equal(9m, Assert.Single(TradingCalendarMonthlyPnl.From(november)).Total); // Oct 31 is excluded, including at the DST edge.
    }

    private static async Task<Guid> AddAccount(ReaderTestDatabase db, string name)
    {
        var account = new TradingAccount(name, TradingAccountType.Personal, null, null, "USD", 0m, Audit);
        await db.ServiceProvider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        return account.Id;
    }

    private static async Task<Guid> AddInstrument(ReaderTestDatabase db)
    {
        var instrument = new Instrument("CAL", "Calendar", AssetClass.Futures, "CME", "USD", .25m, .25m, Audit);
        await db.ServiceProvider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        return instrument.Id;
    }

    private static Trade Closed(Guid account, Guid instrument, DateTimeOffset closed, decimal gross,
        decimal? costs = 0m, string currency = "USD")
    {
        Guid id = Guid.NewGuid();
        return Trade.Rehydrate(id, account, instrument, new(1m, currency), null,
            [new TradeExecution(id, 1, closed.AddMinutes(-1), ExecutionSide.Buy, 1m, 100m, 0m, 0m, null, null, null),
             new TradeExecution(id, 2, closed, ExecutionSide.Sell, 1m, 100m + gross, costs, 0m, null, null, null)],
            Audit, Audit);
    }

    private static Trade Open(Guid account, Guid instrument, DateTimeOffset opened)
    {
        Guid id = Guid.NewGuid();
        return Trade.Rehydrate(id, account, instrument, new(1m, "USD"), null,
            [new TradeExecution(id, 1, opened, ExecutionSide.Buy, 1m, 100m, 0m, 0m, null, null, null)], Audit, Audit);
    }

    private static async Task Add(ReaderTestDatabase db, params Trade[] trades)
    {
        await using JournalDbContext context = await db.ContextFactory.CreateDbContextAsync();
        context.Trades.AddRange(trades.Select(TradePersistenceMapper.ToRecord));
        context.TradeExecutions.AddRange(trades.SelectMany(t => t.Executions).Select(TradeExecutionPersistenceMapper.ToRecord));
        context.TradeBrowse.AddRange(trades.Select(TradeBrowsePersistenceMapper.ToRecord));
        await context.SaveChangesAsync();
    }
}
