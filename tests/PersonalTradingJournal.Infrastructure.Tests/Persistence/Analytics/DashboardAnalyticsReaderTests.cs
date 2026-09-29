using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Analytics;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Analytics;

public sealed class DashboardAnalyticsReaderTests
{
    private static readonly DateTimeOffset Audit = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Close = new(2026, 3, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FiltersAreIndependentCombinedAndStatelessIncludingMissingIds()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        var (a1, i1) = await References(db);
        var (a2, i2) = await References(db);
        await Add(db, TradeFact(a1, i1, 1m), TradeFact(a1, i2, 2m), TradeFact(a2, i1, 4m), TradeFact(a2, i2, 8m));
        IDashboardAnalyticsReader reader = Reader(db);
        Assert.Equal(15m, Total(await reader.GetAsync(new())));
        Assert.Equal(3m, Total(await reader.GetAsync(new(tradingAccountId: a1))));
        Assert.Equal(5m, Total(await reader.GetAsync(new(instrumentId: i1))));
        Assert.Equal(2m, Total(await reader.GetAsync(new(a1, i2))));
        Assert.Equal(15m, Total(await reader.GetAsync(new()))); // No remembered Account.
        Assert.Empty((await reader.GetAsync(new(Guid.NewGuid()))).Currencies);
        Assert.Empty((await reader.GetAsync(new(instrumentId: Guid.NewGuid()))).Currencies);
    }

    [Theory]
    [InlineData(3, 7)]
    [InlineData(3, 8)]
    [InlineData(3, 9)]
    [InlineData(10, 31)]
    [InlineData(11, 1)]
    [InlineData(11, 2)]
    public async Task SqlFiltersIncludeExactLocalDayBoundariesAcrossDst(int month, int day)
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        DateOnly date = new(2026, month, day);
        var query = new DashboardAnalyticsQuery(account, instrument, date, date);
        DateTimeOffset start = query.ClosedFromUtc!.Value, end = query.ClosedBeforeUtc!.Value;
        await Add(db, TradeFact(account, instrument, 100m, start.AddTicks(-1)),
            TradeFact(account, instrument, 1m, start), TradeFact(account, instrument, 2m, end.AddTicks(-1)),
            TradeFact(account, instrument, 1000m, end));
        DashboardAnalyticsSnapshot result = await Reader(db).GetAsync(query);
        Assert.Equal(2, result.SelectedTradeCount);
        Assert.Equal(3m, Total(result));
        Assert.Equal(date, Assert.Single(Assert.Single(result.Currencies).Days).NewYorkDate);
        Assert.Equal(1003m, Total(await Reader(db).GetAsync(new(closedFromNewYork: date))));
        Assert.Equal(103m, Total(await Reader(db).GetAsync(new(closedThroughNewYork: date))));
    }

    [Fact]
    public async Task RepeatedFallBackHourIsIncludedOncePerTrade()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        await Add(db, TradeFact(account, instrument, 1m, new(2026, 11, 1, 5, 30, 0, TimeSpan.Zero)),
            TradeFact(account, instrument, 2m, new(2026, 11, 1, 6, 30, 0, TimeSpan.Zero)));
        DashboardAnalyticsSnapshot result = await Reader(db).GetAsync(new(closedFromNewYork: new(2026, 11, 1), closedThroughNewYork: new(2026, 11, 1)));
        Assert.Equal(2, result.SelectedTradeCount);
        Assert.Equal(3m, Total(result));
    }

    [Fact]
    public async Task OpenAndPartiallyExitedTradesAreExcludedBeforeCalculation()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        await Add(db, TradeFact(account, instrument, 1m, exitQuantity: 0m),
            TradeFact(account, instrument, 2m, exitQuantity: 1m), TradeFact(account, instrument, 3m));
        DashboardAnalyticsSnapshot result = await Reader(db).GetAsync(new());
        Assert.Equal(1, result.SelectedTradeCount);
        Assert.Equal(0, result.ExcludedOpenTradeCount); // Reader input is already closed-only.
        Assert.Equal(3m, Total(result));
    }

    [Fact]
    public async Task ExactValuesNullsHistoricalCurrenciesAndUnclassifiedSetupArePreserved()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        await Add(db, TradeFact(account, instrument, .1234567890123456m),
            TradeFact(account, instrument, 5m, costs: null), TradeFact(account, instrument, 0m, currency: "EUR"));
        DashboardAnalyticsSnapshot result = await Reader(db).GetAsync(new());
        CurrencyTradeMetrics usd = Assert.Single(result.Currencies, c => c.Currency == "USD");
        Assert.Equal(5.1234567890123456m, usd.Metrics.Gross.Total);
        Assert.Equal(.1234567890123456m, usd.Metrics.Net.KnownSubtotal);
        Assert.Null(usd.Metrics.Net.Total);
        Assert.Equal(1, usd.Metrics.UnknownCostTradeCount);
        Assert.Equal(1, usd.Metrics.Net.Coverage.UnknownTradeCount);
        Assert.Null(Assert.Single(usd.Setups).TradingSetupId);
        CurrencyTradeMetrics eur = Assert.Single(result.Currencies, c => c.Currency == "EUR");
        Assert.Equal(0m, eur.Metrics.Net.Total); // Snapshot EUR, even though both references are USD.
        Assert.Equal(1, eur.Metrics.Net.KnownBreakEvens);
    }

    [Fact]
    public async Task NoPagingNoExecutionLoadsNoWritesAndCancellationReachesSql()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        await Add(db, Enumerable.Range(0, 121).Select(_ => TradeFact(account, instrument, 1m)).ToArray());
        await using JournalDbContext connectionInfo = await db.ContextFactory.CreateDbContextAsync();
        // SQLite itself rejects writes on these reader connections, not just an assertion on row counts.
        string readOnlyConnection = new SqliteConnectionStringBuilder(connectionInfo.Database.GetConnectionString())
            { Mode = SqliteOpenMode.ReadOnly }.ToString();
        var commands = new ReadCommands();
        var factory = new ObservedFactory(new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite(readOnlyConnection).AddInterceptors(commands).Options);
        var reader = new DashboardAnalyticsReader(factory);
        var query = new DashboardAnalyticsQuery(account, instrument, new(2026, 3, 8), new(2026, 3, 8));
        DashboardAnalyticsSnapshot result = await reader.GetAsync(query);
        Assert.Equal(121, result.SelectedTradeCount);
        Assert.Equal(121m, Total(result));
        string sql = Assert.Single(commands.Sql);
        Assert.Contains("WHERE", sql);
        Assert.Contains("TradingAccountId", sql);
        Assert.Contains("InstrumentId", sql);
        Assert.Contains("ClosedAtUtc", sql);
        Assert.DoesNotContain("LIMIT", sql);
        Assert.DoesNotContain("TradeExecutions", sql);
        Assert.DoesNotContain("Screenshots", sql);
        Assert.False(commands.TrackedEntities);

        using var cancellation = new CancellationTokenSource();
        commands.CancelOnRead = cancellation;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetAsync(query, cancellation.Token));
        Assert.Equal(cancellation.Token, factory.LastToken);
        Assert.Equal(2, factory.CreatedContexts);
        Assert.Equal(121, await connectionInfo.Trades.CountAsync());
        Assert.Equal(242, await connectionInfo.TradeExecutions.CountAsync());
        commands.CancelOnRead = null;
        Assert.Equal(121, (await reader.GetAsync(query)).SelectedTradeCount);
    }

    [Fact]
    public async Task EmptyAndPreCancelledQueriesDoNotReturnInventedMetrics()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        IDashboardAnalyticsReader reader = Reader(db);
        Assert.Empty((await reader.GetAsync(new())).Currencies);
        await Assert.ThrowsAsync<ArgumentNullException>(() => reader.GetAsync(null!));
        using var token = new CancellationTokenSource();
        token.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetAsync(new(), token.Token));
        Assert.Empty((await reader.GetAsync(new())).Currencies);
    }

    [Fact]
    public async Task SameReaderSeesCommittedInsertCloseEditSetupAndDelete()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        IDashboardAnalyticsReader reader = Reader(db);
        Assert.Empty((await reader.GetAsync(new())).Currencies);
        Trade trade = TradeFact(account, instrument, 4m, exitQuantity: 0m);
        await db.ServiceProvider.GetRequiredService<ITradeStore>().AddAsync(trade);
        Assert.Empty((await reader.GetAsync(new())).Currencies);
        trade.AddExecution(new(trade.Id, 2, Close, ExecutionSide.Sell, 2m, 102m, 0m, 0m, null, null, null), Audit.AddDays(1));
        ITradeMutationStore mutations = db.ServiceProvider.GetRequiredService<ITradeMutationStore>();
        await mutations.SaveAsync(trade);
        Assert.Equal(4m, Total(await reader.GetAsync(new())));

        var setup = new TradingSetup("Historical Setup", null, Audit);
        setup.Deactivate(Audit.AddDays(1));
        await using (JournalDbContext context = await db.ContextFactory.CreateDbContextAsync())
        {
            context.TradingSetups.Add(TradingSetupPersistenceMapper.ToRecord(setup));
            await context.SaveChangesAsync();
        }
        TradeExecution[] corrected = trade.Executions.Select(e => TradeExecution.Rehydrate(e.Id, trade.Id, e.Sequence,
            e.ExecutedAtUtc, e.Side, e.Quantity, e.Price, 1m, 0m, null, null, null)).ToArray();
        trade.CorrectDetails(account, instrument, trade.Pricing, setup.Id, corrected, Audit.AddDays(2));
        await mutations.SaveAsync(trade);
        CurrencyTradeMetrics refreshed = Assert.Single((await reader.GetAsync(new())).Currencies);
        Assert.Equal(2m, refreshed.Metrics.Net.Total);
        Assert.Equal(setup.Id, Assert.Single(refreshed.Setups).TradingSetupId);
        Trade laterInsert = TradeFact(account, instrument, 10m);
        await db.ServiceProvider.GetRequiredService<ITradeStore>().AddAsync(laterInsert);
        Assert.Equal(12m, Total(await reader.GetAsync(new())));
        await db.ServiceProvider.GetRequiredService<ITradeDeletionStore>().DeleteAsync(trade.Id);
        Assert.Equal(10m, Total(await reader.GetAsync(new())));
        await db.ServiceProvider.GetRequiredService<ITradeDeletionStore>().DeleteAsync(laterInsert.Id);
        Assert.Empty((await reader.GetAsync(new())).Currencies);
    }

    private static IDashboardAnalyticsReader Reader(ReaderTestDatabase db) =>
        db.ServiceProvider.GetRequiredService<IDashboardAnalyticsReader>();
    private static decimal? Total(DashboardAnalyticsSnapshot snapshot) => Assert.Single(snapshot.Currencies).Metrics.Net.Total;

    private static async Task<(Guid, Guid)> References(ReaderTestDatabase db)
    {
        var account = new TradingAccount("Analytics Account", TradingAccountType.Personal, null, null, "USD", 0m, Audit);
        var instrument = new Instrument("TEST", "Analytics Instrument", AssetClass.Futures, "CME", "USD", .25m, .25m, Audit);
        await db.ServiceProvider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        await db.ServiceProvider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        return (account.Id, instrument.Id);
    }

    private static Trade TradeFact(Guid account, Guid instrument, decimal gross,
        DateTimeOffset? closed = null, decimal? costs = 0m, string currency = "USD", decimal exitQuantity = 2m)
    {
        DateTimeOffset close = closed ?? Close;
        Guid id = Guid.NewGuid();
        var executions = new List<TradeExecution> { new(id, 1, close.AddDays(-30), ExecutionSide.Buy, 2m, 100m,
            0m, 0m, null, null, null) };
        if (exitQuantity > 0m) executions.Add(new(id, 2, close, ExecutionSide.Sell, exitQuantity, 100m + gross / 2m,
            costs, 0m, null, null, null));
        return Trade.Rehydrate(id, account, instrument, new(1m, currency), null, executions, Audit, Audit);
    }

    private static async Task Add(ReaderTestDatabase db, params Trade[] trades)
    {
        await using JournalDbContext context = await db.ContextFactory.CreateDbContextAsync();
        context.Trades.AddRange(trades.Select(TradePersistenceMapper.ToRecord));
        context.TradeExecutions.AddRange(trades.SelectMany(t => t.Executions).Select(TradeExecutionPersistenceMapper.ToRecord));
        context.TradeBrowse.AddRange(trades.Select(TradeBrowsePersistenceMapper.ToRecord));
        await context.SaveChangesAsync();
    }

    private sealed class ObservedFactory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
    {
        public int CreatedContexts { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public JournalDbContext CreateDbContext() { CreatedContexts++; return new(options); }
        public Task<JournalDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        { LastToken = cancellationToken; cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(CreateDbContext()); }
    }

    private sealed class ReadCommands : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];
        public bool TrackedEntities { get; private set; }
        public CancellationTokenSource? CancelOnRead { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText);
            TrackedEntities |= eventData.Context!.ChangeTracker.Entries().Any();
            Assert.StartsWith("SELECT", command.CommandText);
            CancelOnRead?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }
}
