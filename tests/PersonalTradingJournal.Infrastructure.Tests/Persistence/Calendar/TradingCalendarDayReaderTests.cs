using Microsoft.Data.Sqlite;
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
using PersonalTradingJournal.Infrastructure.Storage;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Calendar;

public sealed class TradingCalendarDayReaderTests
{
    [Fact]
    public async Task DayRowsBatchHistoricalClassificationAndPreserveActualExitInstants()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        DateTimeOffset close = new(2026, 9, 5, 8, 0, 0, TimeSpan.Zero); // A genuine 04:00 New York close.
        Trade first = Closed(account, instrument, close, 5);
        Trade later = Closed(account, instrument, close.AddHours(6).AddTicks(1234567), 7);
        await Add(db, first, later);
        Guid setupId = Guid.NewGuid(), mistakeId = Guid.NewGuid();
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            context.TradingSetups.Add(new TradingSetupRecord { Id = setupId, Name = "Historical Setup", IsActive = false, CreatedAtUtc = Audit, UpdatedAtUtc = Audit });
            context.TradingMistakes.Add(new TradingMistakeRecord { Id = mistakeId, Name = "Historical Mistake", IsActive = false, CreatedAtUtc = Audit, UpdatedAtUtc = Audit });
            (await context.Trades.SingleAsync(t => t.Id == first.Id)).TradingSetupId = setupId;
            context.TradeMistakes.Add(new TradeMistakeRecord { Id = Guid.NewGuid(), TradeId = first.Id, TradingMistakeId = mistakeId, CreatedAtUtc = Audit, UpdatedAtUtc = Audit });
            await context.SaveChangesAsync();
        }
        var result = await db.ServiceProvider.GetRequiredService<ITradingCalendarDayReader>().GetAsync(new(new(2026, 9, 5)));
        Assert.Equal(new[] { later.Id, first.Id }, result.Trades.Select(t => t.Id));
        Assert.Equal(first.ClosedAtUtc, result.Trades[1].ClosedAtUtc);
        Assert.Equal(later.Executions[^1].ExecutedAtUtc, result.Trades[0].ClosedAtUtc);
        var assigned = result.Classifications[first.Id];
        Assert.Equal("Historical Setup", assigned.SetupName);
        Assert.False(assigned.IsSetupActive);
        Assert.Equal("Historical Mistake", Assert.Single(assigned.Mistakes).Name);
        Assert.False(assigned.Mistakes[0].IsActive);
        Assert.Null(result.Classifications[later.Id].SetupId);
        Assert.Empty(result.Classifications[later.Id].Mistakes);
        // Simulate dangling historical references in this isolated database only.
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            await context.Database.OpenConnectionAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
            await context.TradingSetups.Where(s => s.Id == setupId).ExecuteDeleteAsync();
            await context.TradingMistakes.Where(m => m.Id == mistakeId).ExecuteDeleteAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");
        }
        result = await db.ServiceProvider.GetRequiredService<ITradingCalendarDayReader>().GetAsync(new(new(2026, 9, 5)));
        Assert.Equal(2, result.Trades.Count);
        Assert.Equal(setupId, result.Classifications[first.Id].SetupId);
        Assert.Null(result.Classifications[first.Id].SetupName);
        Assert.Equal(mistakeId, Assert.Single(result.Classifications[first.Id].Mistakes).Id);
        Assert.Null(result.Classifications[first.Id].Mistakes[0].Name);
    }

    private static readonly DateTimeOffset Audit = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(2026, 3, 8)]
    [InlineData(2026, 11, 1)]
    public async Task MigratedQueryUsesExactDstDayBoundariesAndExcludesOpenTrades(int year, int month, int day)
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        var query = new TradingCalendarDayQuery(new(year, month, day));
        Trade before = Closed(account, instrument, query.ClosedFromUtc.AddTicks(-1), 1m);
        Trade first = Closed(account, instrument, query.ClosedFromUtc, 2m);
        Trade last = Closed(account, instrument, query.ClosedBeforeUtc.AddTicks(-1), 3m);
        Trade after = Closed(account, instrument, query.ClosedBeforeUtc, 4m);
        Trade open = Open(account, instrument, query.ClosedFromUtc.AddHours(2));
        await Add(db, before, first, last, after, open);
        var reader = db.ServiceProvider.GetRequiredService<ITradingCalendarDayReader>();
        TradingCalendarDayDetails result = await reader.GetAsync(query);
        Assert.Equal(new[] { last.Id, first.Id }, result.Trades.Select(t => t.Id));
        Assert.Equal(2, result.ClosedTradeCount);
        Assert.Equal(5m, Assert.Single(result.Currencies).Metrics.EffectiveNet.Total);
        Assert.All(result.Trades, t => Assert.Equal(1m, t.Size));
        Assert.Empty((await reader.GetAsync(new(query.Date, Guid.NewGuid()))).Trades);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetAsync(query, new CancellationToken(true)));
        await using JournalDbContext check = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(5, await check.Trades.CountAsync());
        Assert.Equal(9, await check.TradeExecutions.CountAsync());
        Assert.Equal(5, await check.TradeBrowse.CountAsync());
        Assert.Empty(check.ChangeTracker.Entries());
    }

    [Fact]
    public async Task SaturdayQueryReturnsOwnTradesCurrenciesEstimatesAndPeakSizeNotWeeklyActivity()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        DateOnly date = new(2026, 10, 3);
        DateTimeOffset close = new(2026, 10, 3, 16, 0, 0, TimeSpan.Zero);
        Trade estimated = Closed(account, instrument, close, -285m, null);
        Trade zero = Closed(account, instrument, close.AddHours(-1), 0m);
        Trade eur = Closed(account, instrument, close, 10m, currency: "EUR");
        Guid scaledId = Guid.NewGuid();
        Trade scaled = Trade.Rehydrate(scaledId, account, instrument, new(1m, "USD"), null,
            [new TradeExecution(scaledId, 1, close.AddMinutes(-4), ExecutionSide.Buy, 2m, 100m, 0m, 0m, null, null, null),
             new TradeExecution(scaledId, 2, close.AddMinutes(-3), ExecutionSide.Sell, 1m, 100m, 0m, 0m, null, null, null),
             new TradeExecution(scaledId, 3, close.AddMinutes(-2), ExecutionSide.Buy, 2m, 100m, 0m, 0m, null, null, null),
             new TradeExecution(scaledId, 4, close.AddMinutes(-1), ExecutionSide.Sell, 3m, 110m, 0m, 0m, null, null, null)], Audit, Audit);
        Trade sunday = Closed(account, instrument, close.AddDays(1), 100m);
        await Add(db, estimated, zero, eur, scaled, sunday, Open(account, instrument, close));
        TradingCalendarDayDetails result = await db.ServiceProvider.GetRequiredService<ITradingCalendarDayReader>().GetAsync(new(date));
        Assert.Equal(4, result.ClosedTradeCount);
        Assert.DoesNotContain(result.Trades, t => t.Id == sunday.Id);
        Assert.Equal(3m, result.Trades.Single(t => t.Id == scaledId).Size); // Four cumulative entries, peak three.
        Assert.Equal(result.Trades.OrderByDescending(t => t.ClosedAtUtc).ThenBy(t => t.Id).Select(t => t.Id), result.Trades.Select(t => t.Id));
        Assert.Equal(-255m, result.Currencies.Single(c => c.Currency == "USD").Metrics.EffectiveNet.Total);
        Assert.True(result.Currencies.Single(c => c.Currency == "USD").Metrics.EffectiveNet.IsEstimated);
        Assert.Equal(10m, result.Currencies.Single(c => c.Currency == "EUR").Metrics.EffectiveNet.Total);
        Assert.Null(result.Trades.Single(t => t.Id == estimated.Id).NetPnL);
        Assert.Equal(0m, result.Trades.Single(t => t.Id == zero.Id).NetPnL);
        var month = await db.ServiceProvider.GetRequiredService<ITradingCalendarReader>().GetAsync(new(2026, 10));
        var week = month.Currencies.Single(c => c.Currency == "USD").Weeks.Single(w => w.Monday == new DateOnly(2026, 9, 28));
        Assert.Equal(-155m, week.EffectiveNetTotal); // Sunday contributes to the week, never Saturday details.
        Assert.Equal(4, week.ClosedTradeCount);

        // Optional synthetic snapshot for an isolated live Desktop acceptance run.
        if (Environment.GetEnvironmentVariable("PTJ_CALENDAR_ACCEPTANCE_ROOT") is { Length: > 0 } root)
        {
            var paths = new LocalApplicationPaths(root);
            paths.EnsureDirectoriesExist();
            await using JournalDbContext source = await db.ContextFactory.CreateDbContextAsync();
            await source.Database.OpenConnectionAsync();
            await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath }.ToString());
            await destination.OpenAsync();
            ((SqliteConnection)source.Database.GetDbConnection()).BackupDatabase(destination);
        }
    }

    [Fact]
    public async Task DayQueryIsNotTruncatedToABrowsePageAndReflectsDeletes()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        DateTimeOffset close = new(2026, 9, 5, 16, 0, 0, TimeSpan.Zero);
        Trade[] trades = Enumerable.Range(0, 75).Select(i => Closed(account, instrument, close.AddSeconds(i), 1m)).ToArray();
        await Add(db, trades);
        var reader = db.ServiceProvider.GetRequiredService<ITradingCalendarDayReader>();
        Assert.Equal(75, (await reader.GetAsync(new(new(2026, 9, 5), account))).ClosedTradeCount);
        await using JournalDbContext context = await db.ContextFactory.CreateDbContextAsync();
        context.Trades.Remove(await context.Trades.SingleAsync(t => t.Id == trades[0].Id));
        await context.SaveChangesAsync();
        Assert.Equal(74, (await reader.GetAsync(new(new(2026, 9, 5), account))).ClosedTradeCount);
    }

    private static async Task<(Guid Account, Guid Instrument)> References(ReaderTestDatabase db)
    {
        var account = new TradingAccount("Calendar historical account", TradingAccountType.Personal, null, null, "USD", 0m, Audit);
        account.Deactivate(Audit.AddDays(1));
        var instrument = new Instrument("CAL", "Calendar test", AssetClass.Futures, "CME", "USD", .25m, .25m, Audit);
        await db.ServiceProvider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        await db.ServiceProvider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        return (account.Id, instrument.Id);
    }

    private static Trade Closed(Guid account, Guid instrument, DateTimeOffset closed, decimal gross,
        decimal? costs = 0m, string currency = "USD")
    {
        Guid id = Guid.NewGuid();
        return Trade.Rehydrate(id, account, instrument, new(1m, currency), null,
            [new TradeExecution(id, 1, closed.AddMinutes(-1), ExecutionSide.Buy, 1m, 1000m, 0m, 0m, null, null, null),
             new TradeExecution(id, 2, closed, ExecutionSide.Sell, 1m, 1000m + gross, costs, 0m, null, null, null)], Audit, Audit);
    }

    private static Trade Open(Guid account, Guid instrument, DateTimeOffset opened)
    {
        Guid id = Guid.NewGuid();
        return Trade.Rehydrate(id, account, instrument, new(1m, "USD"), null,
            [new TradeExecution(id, 1, opened, ExecutionSide.Buy, 1m, 1000m, 0m, 0m, null, null, null)], Audit, Audit);
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
