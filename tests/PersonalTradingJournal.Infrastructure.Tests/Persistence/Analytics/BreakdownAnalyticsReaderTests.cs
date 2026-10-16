using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Analytics;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Analytics;

public sealed class BreakdownAnalyticsReaderTests
{
    private static readonly DateTimeOffset Audit = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Close = new(2026, 3, 8, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<BreakdownGrouping, BreakdownCalculationMode> Dimensions
    {
        get
        {
            var data = new TheoryData<BreakdownGrouping, BreakdownCalculationMode>();
            foreach (var grouping in Enum.GetValues<BreakdownGrouping>())
            foreach (var mode in Enum.GetValues<BreakdownCalculationMode>())
                data.Add(grouping, mode);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Dimensions))]
    public async Task CompletePopulationMatchesDirectCalculatorForEveryDimensionAndMode(
        BreakdownGrouping grouping, BreakdownCalculationMode mode)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (a, i) = await References(db);
        var (b, j) = await References(db);
        var setup = await Setup(db, "Example", false);
        Trade[] trades = Enumerable.Range(0, 123).Select(n => TradeFact(n % 2 == 0 ? a : b, n % 3 == 0 ? i : j,
            n % 7 - 3m, costs: n % 4 == 0 ? null : 1m, setup: n % 3 == 0 ? setup.Id : null,
            opened: Close.AddHours(-n - 1), shortTrade: n % 2 == 0)).ToArray();
        await Add(db, trades);
        // Multiple execution and screenshot rows must never multiply facts.
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            foreach (var trade in trades.Take(2))
            for (int n = 0; n < 3; n++)
                context.TradeScreenshots.Add(new()
                {
                    Id = Guid.NewGuid(), TradeId = trade.Id, StorageKey = Guid.NewGuid().ToString("N") + ".png",
                    FileName = "synthetic.png", CreatedAtUtc = Audit, UpdatedAtUtc = Audit,
                });
            await context.SaveChangesAsync();
        }
        var query = new BreakdownQuery("USD", grouping: grouping, mode: mode);
        var expected = BreakdownMetricCalculator.Calculate(query, trades.Select(t => Fact(t, setup)));
        var result = await Reader(db).GetAsync(query);
        Assert.Equal(expected.Query, result.Query);
        AssertMetrics(expected.Summary, result.Summary);
        Assert.Equal(expected.Groups.Select(g => (g.Key, g.Label)), result.Groups.Select(g => (g.Key, g.Label)));
        foreach (var (expectedGroup, actualGroup) in expected.Groups.Zip(result.Groups))
            AssertMetrics(expectedGroup.Metrics, actualGroup.Metrics);
        Assert.Equal(123, result.Summary.EligibleTradeCount);
        Assert.Equal(trades.Select(t => t.Id).Order(), result.Groups.SelectMany(g => g.Metrics.TradeIds).Order());
        Assert.Equal(123, result.Groups.Sum(g => g.Metrics.EligibleTradeCount));
        Assert.Equal(result.Summary.Selected.Total, result.Groups.Sum(g => g.Metrics.Selected.Total));
    }

    [Fact]
    public async Task KnownZeroNetFallbackAndUnavailableProjectionsRemainDistinct()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (a, i) = await References(db);
        var zero = TradeFact(a, i, 5m, costs: 5m);
        var fallback = TradeFact(a, i, 7m, costs: null);
        var unavailable = TradeFact(a, i, 9m, costs: null);
        await Add(db, zero, fallback, unavailable);
        // An unavailable persisted projection is deliberately synthetic; do not invent
        // financial values or remove it from the closed population's coverage denominator.
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            (await context.TradeBrowse.SingleAsync(t => t.TradeId == unavailable.Id)).GrossPnL = null;
            await context.SaveChangesAsync();
        }
        var result = (await Reader(db).GetAsync(new("USD"))).Summary;
        Assert.Equal(new(BreakdownCalculationMode.NetPreferred, 3, 1, 1, 0, 1), result.FinancialSources);
        Assert.Equal(BreakdownFinancialSource.NetGross, result.FinancialSources.Source);
        Assert.Equal(7m, result.Selected.KnownSubtotal);
        Assert.Null(result.Selected.Total);
        Assert.Equal(1, result.Selected.BreakEvens);
        Assert.Equal(2, result.Data.MissingCostTradeCount);
        Assert.Equal(new MetricCoverage(3, 1), result.Data.ActualNet);
        var gross = (await Reader(db).GetAsync(new("USD", mode: BreakdownCalculationMode.Gross))).Summary;
        Assert.Equal(12m, gross.Selected.KnownSubtotal);
        Assert.Equal(new(BreakdownCalculationMode.Gross, 3, 0, 0, 2, 1), gross.FinancialSources);
        await using var check = await db.ContextFactory.CreateDbContextAsync();
        Assert.Null((await check.TradeBrowse.SingleAsync(t => t.TradeId == fallback.Id)).NetPnL);
        Assert.Equal(0m, (await check.TradeBrowse.SingleAsync(t => t.TradeId == zero.Id)).NetPnL);
    }

    [Fact]
    public async Task AllUnknownNetUsesGrossFallbackWithoutChangingPersistedEconomics()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (a, i) = await References(db);
        await Add(db, TradeFact(a, i, 3m, costs: null), TradeFact(a, i, -2m, costs: null));
        var result = (await Reader(db).GetAsync(new("USD"))).Summary;
        Assert.Equal(BreakdownFinancialSource.GrossFallback, result.FinancialSources.Source);
        Assert.Equal(1m, result.Selected.Total);
        Assert.Equal(2, result.FinancialSources.GrossFallbackCount);
        Assert.Equal(0, result.Data.ActualNet.KnownTradeCount);
    }

    [Fact]
    public async Task OptionalFiltersCombineWithoutRememberedScopeAndPreserveDecimalPrecision()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (a, i) = await References(db);
        var (b, j) = await References(db);
        var fine = TradeFact(a, i, .1234567890123456m);
        var second = TradeFact(a, j, 2m);
        var third = TradeFact(b, i, 4m, Close.AddDays(1));
        await Add(db, fine, second, third, TradeFact(b, j, 8m, Close.AddDays(-1)));
        var reader = Reader(db);
        Assert.Equal(14.1234567890123456m, (await reader.GetAsync(new("USD"))).Summary.Selected.Total);
        Assert.Equal(2.1234567890123456m, (await reader.GetAsync(new("USD", tradingAccountId: a))).Summary.Selected.Total);
        Assert.Equal(4.1234567890123456m, (await reader.GetAsync(new("USD", instrumentId: i))).Summary.Selected.Total);
        Assert.Equal(new[] { fine.Id }, (await reader.GetAsync(new("USD", a, i))).Summary.TradeIds);
        Assert.Equal(new[] { third.Id }, (await reader.GetAsync(new("USD", closedFromNewYork: new(2026, 3, 9)))).Summary.TradeIds);
        Assert.Equal(10.1234567890123456m,
            (await reader.GetAsync(new("USD", closedThroughNewYork: new(2026, 3, 8)))).Summary.Selected.Total);
        Assert.Equal(14.1234567890123456m, (await reader.GetAsync(new("USD"))).Summary.Selected.Total);
        Assert.Empty((await reader.GetAsync(new("JPY"))).Groups);
    }

    [Fact]
    public async Task CalculationFailureReleasesDatabaseAndReturnsNoPartialMetrics()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (a, i) = await References(db);
        await Add(db, TradeFact(a, i, 1m));
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            (await context.TradeBrowse.SingleAsync()).TotalCosts = null; // Invalid: known Net without known costs.
            await context.SaveChangesAsync();
        }
        var (reader, factory) = await Observed(db, new ReadObserver());
        await Assert.ThrowsAsync<ArgumentException>(() => reader.GetAsync(new("USD")));
        AssertReleased(factory);
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            (await context.TradeBrowse.SingleAsync()).TotalCosts = 0m;
            await context.SaveChangesAsync();
        }
        Assert.Equal(1m, (await reader.GetAsync(new("USD"))).Summary.Selected.Total);
        AssertReleased(factory);
    }

    [Theory]
    [InlineData(3, 7)]
    [InlineData(3, 8)]
    [InlineData(3, 9)]
    [InlineData(10, 31)]
    [InlineData(11, 1)]
    [InlineData(11, 2)]
    public async Task SqlFiltersPreserveExactClosureTicksAcrossDstAndIndependentEntryGrouping(int month, int day)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (a, i) = await References(db);
        var (b, j) = await References(db);
        DateOnly date = new(2026, month, day);
        var query = new BreakdownQuery("USD", a, i, date, date, grouping: BreakdownGrouping.EntryHour);
        var start = query.TradeFilter.ClosedFromUtc!.Value;
        var end = query.TradeFilter.ClosedBeforeUtc!.Value;
        var first = TradeFact(a, i, 1m, start, opened: start.AddMinutes(-30));
        var last = TradeFact(a, i, 2m, end.AddTicks(-1));
        await Add(db, first, last, TradeFact(a, i, 100m, start.AddTicks(-1)), TradeFact(a, i, 100m, end),
            TradeFact(b, i, 100m, start), TradeFact(a, j, 100m, start),
            TradeFact(a, i, 100m, start, currency: "EUR"), TradeFact(a, i, 100m, start, exitQuantity: 1m),
            TradeFact(a, i, 100m, start, exitQuantity: 0m));
        var result = await Reader(db).GetAsync(query);
        Assert.Equal(new[] { first.Id, last.Id }.Order(), result.Summary.TradeIds);
        Assert.Equal(3m, result.Summary.Selected.Total);
        Assert.Contains(first.Id, Assert.Single(result.Groups, g => g.Key.EntryHour == 23).Metrics.TradeIds);
        Assert.Empty((await Reader(db).GetAsync(new("USD", Guid.NewGuid()))).Groups);
        Assert.Empty((await Reader(db).GetAsync(new("USD", instrumentId: Guid.NewGuid()))).Groups);
        Assert.Equal(1, (await Reader(db).GetAsync(new("EUR", a, i, date, date))).Summary.EligibleTradeCount);
    }

    [Fact]
    public async Task DuplicateNamesInactiveAndMissingReferencesRetainExactIdentity()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (a, i) = await References(db);
        var (b, j) = await References(db);
        var one = await Setup(db, "Duplicate", false);
        var two = await Setup(db, "Duplicate", true);
        var missing = await Setup(db, "Missing", true);
        var absent = TradeFact(a, i, 4m, setup: missing.Id);
        await Add(db, TradeFact(a, i, 1m, setup: one.Id), TradeFact(b, j, 2m, setup: two.Id),
            TradeFact(a, i, 3m), absent);
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            (await context.TradingAccounts.SingleAsync(t => t.Id == a)).IsActive = false;
            var instrument = await context.Instruments.SingleAsync(t => t.Id == i);
            instrument.IsActive = false;
            instrument.Currency = "EUR";
            (await context.TradingAccounts.SingleAsync(t => t.Id == a)).Currency = "EUR";
            await context.SaveChangesAsync();
            string connection = new SqliteConnectionStringBuilder(context.Database.GetConnectionString())
                { ForeignKeys = false, Pooling = false }.ToString();
            // Out-of-band damage only in this isolated fixture, as in existing analytics tests.
            await using var damaged = new JournalDbContext(new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(connection).Options);
            damaged.TradingSetups.Remove(await damaged.TradingSetups.SingleAsync(t => t.Id == missing.Id));
            await damaged.SaveChangesAsync();
        }
        var result = await Reader(db).GetAsync(new("USD", grouping: BreakdownGrouping.TradingSetup));
        Assert.Equal(4, result.Groups.Count);
        Assert.Equal(10m, result.Summary.Selected.Total);
        Assert.Equal(1, result.Summary.Data.UnclassifiedTradeCount);
        Assert.Equal(1, result.Summary.Data.UnavailableSetupMetadataCount);
        Assert.Equal(new[] { absent.Id }, Assert.Single(result.Groups, g => g.Key.ReferenceId == missing.Id).Metrics.TradeIds);
        Assert.Contains("inactive", Assert.Single(result.Groups, g => g.Key.ReferenceId == one.Id).Label);
        var accounts = await Reader(db).GetAsync(new("USD", grouping: BreakdownGrouping.Account));
        Assert.Equal(2, accounts.Groups.Count);
        Assert.Contains("inactive", Assert.Single(accounts.Groups, g => g.Key.ReferenceId == a).Label);
        var instruments = await Reader(db).GetAsync(new("USD"));
        Assert.Equal(2, instruments.Groups.Count);
        Assert.Contains("inactive", Assert.Single(instruments.Groups, g => g.Key.ReferenceId == i).Label);
    }

    [Theory]
    [InlineData(BreakdownGrouping.TradingSetup, "Before", "After · inactive")]
    [InlineData(BreakdownGrouping.Account, "Account", "New account label")]
    [InlineData(BreakdownGrouping.Instrument, "TEST", "NEW")]
    public async Task SingleSelectRemainsCoherentWhileWriterCommitsAndNextRequestIsFresh(
        BreakdownGrouping grouping, string oldLabel, string newLabel)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (a, i) = await References(db);
        var oldSetup = await Setup(db, "Before", true);
        var newSetup = await Setup(db, "After", false);
        var original = TradeFact(a, i, 5m, setup: oldSetup.Id);
        var removed = TradeFact(a, i, 6m, setup: oldSetup.Id);
        var inserted = TradeFact(a, i, 9m, setup: newSetup.Id);
        await Add(db, original, removed);
        var observer = new ReadObserver();
        var (reader, factory) = await Observed(db, observer);
        bool committed = false;
        observer.AfterExecute = async () =>
        {
            // ExecuteReader has stepped SQLite's SELECT to its first row. Commit on a
            // separate WAL connection before EF consumes/materializes the result set.
            await using var writer = await db.ContextFactory.CreateDbContextAsync();
            await using var transaction = await writer.Database.BeginTransactionAsync();
            (await writer.Trades.SingleAsync(t => t.Id == original.Id)).TradingSetupId = newSetup.Id;
            var projection = await writer.TradeBrowse.SingleAsync(t => t.TradeId == original.Id);
            projection.GrossPnL = 20m; projection.NetPnL = 20m;
            (await writer.TradingSetups.SingleAsync(t => t.Id == oldSetup.Id)).Name = "Renamed";
            (await writer.TradingAccounts.SingleAsync(t => t.Id == a)).Name = "New account label";
            (await writer.Instruments.SingleAsync(t => t.Id == i)).Symbol = "NEW";
            writer.Trades.Remove(await writer.Trades.SingleAsync(t => t.Id == removed.Id));
            writer.Trades.Add(TradePersistenceMapper.ToRecord(inserted));
            writer.TradeExecutions.AddRange(inserted.Executions.Select(TradeExecutionPersistenceMapper.ToRecord));
            writer.TradeBrowse.Add(TradeBrowsePersistenceMapper.ToRecord(inserted));
            await writer.SaveChangesAsync();
            await transaction.CommitAsync();
            committed = true;
        };
        var query = new BreakdownQuery("USD", grouping: grouping);
        var before = await reader.GetAsync(query);
        Assert.True(committed);
        Assert.Equal(11m, before.Summary.Selected.Total);
        Assert.Equal(oldLabel, Assert.Single(before.Groups).Label);
        Assert.Equal(new[] { original.Id, removed.Id }.Order(), before.Summary.TradeIds);
        Assert.Single(observer.Sql);
        AssertReleased(factory);
        observer.AfterExecute = null;
        var after = await reader.GetAsync(query);
        Assert.Equal(29m, after.Summary.Selected.Total);
        Assert.Equal(new[] { original.Id, inserted.Id }.Order(), after.Summary.TradeIds);
        Assert.Equal(newLabel, Assert.Single(after.Groups).Label);
        if (grouping == BreakdownGrouping.TradingSetup)
            Assert.Equal(newSetup.Id, after.Groups[0].Key.ReferenceId);
        Assert.Equal("New account label", Assert.Single((await reader.GetAsync(new("USD", grouping: BreakdownGrouping.Account))).Groups).Label);
        Assert.Equal("NEW", Assert.Single((await reader.GetAsync(new("USD"))).Groups).Label);
        Assert.Equal(oldLabel, before.Groups[0].Label); // Published data never changes.
    }

    [Fact]
    public async Task ReadOnlySqlCancellationFailureAndContextLifetimeDoNotPoisonSubsequentReads()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (a, i) = await References(db);
        await Add(db, TradeFact(a, i, 1m));
        var observer = new ReadObserver();
        var (reader, factory) = await Observed(db, observer);
        var query = new BreakdownQuery("USD", a, i, new(2026, 3, 8), new(2026, 3, 8));
        Assert.Equal(1m, (await reader.GetAsync(query)).Summary.Selected.Total);
        string sql = Assert.Single(observer.Sql);
        foreach (string name in new[] { "TradingAccounts", "TradingSetups", "Instruments", "PricingCurrency", "ClosedAtUtc", "WHERE" })
            Assert.Contains(name, sql);
        foreach (string name in new[] { "LIMIT", "TradeExecutions", "TradeScreenshots", "TradeMistakes" })
            Assert.DoesNotContain(name, sql);
        Assert.False(observer.Tracked);
        AssertReleased(factory);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetAsync(query, cancelled.Token));
        Assert.Single(factory.Contexts);
        using var during = new CancellationTokenSource();
        observer.BeforeExecute = () => during.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetAsync(query, during.Token));
        AssertReleased(factory);
        observer.BeforeExecute = () => throw new InvalidOperationException("Synthetic read failure");
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.GetAsync(query));
        AssertReleased(factory);
        observer.BeforeExecute = null;
        using var afterSql = new CancellationTokenSource();
        observer.AfterExecute = () => { afterSql.Cancel(); return Task.CompletedTask; };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetAsync(query, afterSql.Token));
        AssertReleased(factory);
        observer.AfterExecute = null;
        Assert.Equal(1m, (await reader.GetAsync(query)).Summary.Selected.Total);
        AssertReleased(factory);
        await Assert.ThrowsAsync<ArgumentNullException>(() => reader.GetAsync(null!));
    }

    [Fact]
    public async Task EmptyDatabaseProducesEmptyResultThroughRegisteredBoundary()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var result = await Reader(db).GetAsync(new("USD"));
        Assert.Empty(result.Groups);
        Assert.Empty(result.Summary.TradeIds);
        Assert.Null(result.Summary.Selected.Total);
    }

    private static IBreakdownAnalyticsReader Reader(ReaderTestDatabase db) => db.ServiceProvider.GetRequiredService<IBreakdownAnalyticsReader>();

    private static void AssertMetrics(BreakdownMetrics expected, BreakdownMetrics actual)
    {
        // SQLite preserves decimal value/precision, but not necessarily its trailing-zero
        // display scale. Compare numeric contracts rather than serialized number spelling.
        Assert.Equal(expected.Selected, actual.Selected);
        Assert.Equal(expected.Gross, actual.Gross);
        Assert.Equal(expected.FinancialSources, actual.FinancialSources);
        Assert.Equal(expected.Data, actual.Data);
        Assert.Equal(expected.TradeIds, actual.TradeIds);
    }

    private static async Task<(Guid, Guid)> References(ReaderTestDatabase db)
    {
        var account = new TradingAccount("Account", TradingAccountType.Personal, null, null, "USD", 0m, Audit);
        var instrument = new Instrument("TEST", "Instrument", AssetClass.Futures, "CME", "USD", .25m, .25m, Audit);
        await db.ServiceProvider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        await db.ServiceProvider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        return (account.Id, instrument.Id);
    }

    private static async Task<TradingSetup> Setup(ReaderTestDatabase db, string name, bool active)
    {
        var setup = new TradingSetup(name, null, Audit);
        if (!active) setup.Deactivate(Audit.AddDays(1));
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        context.TradingSetups.Add(TradingSetupPersistenceMapper.ToRecord(setup));
        await context.SaveChangesAsync();
        return setup;
    }

    private static Trade TradeFact(Guid account, Guid instrument, decimal gross, DateTimeOffset? closed = null,
        decimal? costs = 0m, string currency = "USD", decimal exitQuantity = 2m, Guid? setup = null,
        DateTimeOffset? opened = null, bool shortTrade = false)
    {
        var close = closed ?? Close;
        Guid id = Guid.NewGuid();
        var executions = new List<TradeExecution>
        {
            new(id, 1, opened ?? close.AddMinutes(-5), shortTrade ? ExecutionSide.Sell : ExecutionSide.Buy,
                2m, 100m, 0m, 0m, null, null, null),
        };
        if (exitQuantity > 0m) executions.Add(new(id, 2, close, shortTrade ? ExecutionSide.Buy : ExecutionSide.Sell,
            exitQuantity, 100m + (shortTrade ? -gross : gross) / 2m, costs, 0m, null, null, null));
        return Trade.Rehydrate(id, account, instrument, new(1m, currency), setup, executions, Audit, Audit);
    }

    private static BreakdownTradeFact Fact(Trade t, TradingSetup setup) => new(
        new(t.Id, t.Status, t.OpenedAtUtc, t.ClosedAtUtc, t.Pricing.Currency, t.TradingSetupId,
            t.GrossPnL, t.TotalCosts, t.NetPnL) { InstrumentSymbol = "TEST" }, t.TradingAccountId, t.InstrumentId, t.Direction)
    {
        AccountName = "Account", AccountIsActive = true, InstrumentName = "TEST", InstrumentIsActive = true,
        SetupName = t.TradingSetupId is null ? null : setup.Name,
        SetupIsActive = t.TradingSetupId is null ? null : setup.IsActive,
    };

    private static async Task Add(ReaderTestDatabase db, params Trade[] trades)
    {
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        context.Trades.AddRange(trades.Select(TradePersistenceMapper.ToRecord));
        context.TradeExecutions.AddRange(trades.SelectMany(t => t.Executions).Select(TradeExecutionPersistenceMapper.ToRecord));
        context.TradeBrowse.AddRange(trades.Select(TradeBrowsePersistenceMapper.ToRecord));
        await context.SaveChangesAsync();
    }

    private static async Task<(BreakdownAnalyticsReader, ObservedFactory)> Observed(ReaderTestDatabase db, ReadObserver observer)
    {
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        string connection = new SqliteConnectionStringBuilder(context.Database.GetConnectionString())
            { Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        var factory = new ObservedFactory(new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite(connection).AddInterceptors(observer).Options);
        return (new(factory), factory);
    }

    private static void AssertReleased(ObservedFactory factory)
    {
        foreach (var (context, connection) in factory.Contexts)
        {
            Assert.Equal(ConnectionState.Closed, connection.State);
            Assert.Throws<ObjectDisposedException>(() => context.Trades.ToList());
        }
    }

    private sealed class ObservedFactory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    {
        public List<(JournalDbContext Context, DbConnection Connection)> Contexts { get; } = [];
        public JournalDbContext CreateDbContext()
        {
            var context = new JournalDbContext(options);
            Contexts.Add((context, context.Database.GetDbConnection()));
            return context;
        }
        public Task<JournalDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(CreateDbContext()); }
    }

    private sealed class ReadObserver : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];
        public bool Tracked { get; private set; }
        public Action? BeforeExecute { get; set; }
        public Func<Task>? AfterExecute { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText);
            Assert.StartsWith("SELECT", command.CommandText);
            Tracked |= eventData.Context!.ChangeTracker.Entries().Any();
            BeforeExecute?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (AfterExecute is { } callback) await callback();
            return result;
        }
    }
}
