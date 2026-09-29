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
    public async Task OutcomeAveragesUseAllFilteredClosedTradesAndHistoricalCurrency()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        var (otherAccount, otherInstrument) = await References(db);
        await Add(db, TradeFact(account, instrument, 100m), TradeFact(account, instrument, -40m),
            TradeFact(account, instrument, 0m), TradeFact(account, instrument, 20m),
            TradeFact(account, instrument, 500m, currency: "EUR"),
            TradeFact(otherAccount, instrument, 900m), TradeFact(account, otherInstrument, 900m),
            TradeFact(account, instrument, 900m, Close.AddDays(-1)),
            TradeFact(account, instrument, 900m, Close.AddDays(1)),
            TradeFact(account, instrument, 900m, exitQuantity: 1m));
        IDashboardAnalyticsReader reader = Reader(db);
        DashboardAnalyticsSnapshot snapshot = await reader.GetAsync(new(account, instrument,
            new(2026, 3, 8), new(2026, 3, 8)));
        Assert.Equal(5, snapshot.SelectedTradeCount);
        CurrencyTradeMetrics usd = Assert.Single(snapshot.Currencies, c => c.Currency == "USD");
        foreach (ClosedTradeMetrics scope in new[] { usd.Metrics, usd.Days[0].Metrics,
                     usd.Weeks[0].CumulativeMetrics, usd.Setups[0].Metrics })
        foreach (PnlMetrics basis in new[] { scope.Gross, scope.Net, scope.EffectiveNet })
        {
            Assert.Equal(4, basis.Coverage.ClosedTradeCount);
            Assert.Equal(50m, basis.WinRatePercent);
            Assert.Equal(new AveragePnlMetric(AveragePnlStatus.Defined, 60m), basis.AverageWin);
            Assert.Equal(new AveragePnlMetric(AveragePnlStatus.Defined, 40m), basis.AverageLoss);
            Assert.Equal(new ProfitFactorMetric(ProfitFactorStatus.Defined, 3m), basis.ProfitFactor);
            Assert.False(basis.IsEstimated);
        }
        PnlMetrics eur = Assert.Single(snapshot.Currencies, c => c.Currency == "EUR").Metrics.Net;
        Assert.Equal(500m, eur.AverageWin.Value);
        Assert.Equal(new AveragePnlMetric(AveragePnlStatus.NoLosses, null), eur.AverageLoss);
        Assert.Equal(new ProfitFactorMetric(ProfitFactorStatus.NoLosses, null), eur.ProfitFactor);
        Assert.Empty((await reader.GetAsync(new(account, instrument, new(2026, 3, 11)))).Currencies);
    }

    [Fact]
    public async Task PersistedOppositeSignsAndEstimatedLossKeepIndependentOutcomeMetrics()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        await Add(db, TradeFact(account, instrument, 5m, costs: 8m),
            TradeFact(account, instrument, 10m, costs: 2m), TradeFact(account, instrument, -285m, costs: null));
        ClosedTradeMetrics result = Assert.Single((await Reader(db).GetAsync(new())).Currencies).Metrics;
        Assert.Equal(7.5m, result.Gross.AverageWin.Value);
        Assert.Equal(285m, result.Gross.AverageLoss.Value);
        Assert.Equal(200m / 3m, result.Gross.WinRatePercent);
        Assert.Equal(15m / 285m, result.Gross.ProfitFactor.Value);
        Assert.Equal(new AveragePnlMetric(AveragePnlStatus.IncompleteCoverage, null), result.Net.AverageWin);
        Assert.Equal(new AveragePnlMetric(AveragePnlStatus.IncompleteCoverage, null), result.Net.AverageLoss);
        Assert.Null(result.Net.WinRatePercent);
        Assert.Null(result.Net.ProfitFactor.Value);
        Assert.True(result.EffectiveNet.IsEstimated);
        Assert.Equal(1, result.EffectiveNet.EstimatedTradeCount);
        Assert.Equal(2, result.EffectiveNet.VerifiedTradeCount);
        Assert.Equal(8m, result.EffectiveNet.AverageWin.Value);
        Assert.Equal(144m, result.EffectiveNet.AverageLoss.Value);
        Assert.Equal(100m / 3m, result.EffectiveNet.WinRatePercent);
        Assert.Equal(8m / 288m, result.EffectiveNet.ProfitFactor.Value);
    }

    [Fact]
    public async Task EstimatedNetIsNotPersistedAndRealCostCorrectionReplacesItOnEveryRead()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        Trade trade = TradeFact(account, instrument, -285m, costs: null);
        await db.ServiceProvider.GetRequiredService<ITradeStore>().AddAsync(trade);
        IDashboardAnalyticsReader reader = Reader(db);
        ITradeListReader list = db.ServiceProvider.GetRequiredService<ITradeListReader>();
        ITradeDetailReader details = db.ServiceProvider.GetRequiredService<ITradeDetailReader>();
        var listQuery = new TradeListQuery(1, 20, TradeListSortColumn.OpenedAtUtc, TradeListSortDirection.Descending);
        var initial = Assert.Single((await reader.GetAsync(new())).Currencies);
        Assert.Null(initial.Metrics.Net.Total);
        Assert.Equal(-285m, initial.Metrics.EffectiveNet.Total);
        Assert.True(initial.Days[0].CumulativeMetrics.EffectiveNet.IsEstimated);
        Assert.Equal(285m, initial.Metrics.EffectiveNet.AverageLoss.Value);
        Assert.Equal(AveragePnlStatus.IncompleteCoverage, initial.Metrics.Net.AverageLoss.Status);
        TradeListItem row = Assert.Single((await list.GetPageAsync(listQuery)).Items);
        TradeDetail detail = Assert.IsType<TradeDetail>(await details.GetByIdAsync(trade.Id));
        Assert.Equal(new EffectiveNetPnL(-285m, NetPnLProvenance.Estimated), row.EffectiveNet);
        Assert.Equal(row.EffectiveNet, detail.EffectiveNet);
        Assert.Null(row.NetPnL);
        Assert.Null(detail.NetPnL);
        await using (JournalDbContext context = await db.ContextFactory.CreateDbContextAsync())
        {
            Assert.Null((await context.TradeBrowse.SingleAsync()).NetPnL);
            Assert.Null((await context.TradeExecutions.SingleAsync(e => e.Sequence == 2)).Commission);
        }

        TradeExecution[] corrected = trade.Executions.Select(e => TradeExecution.Rehydrate(e.Id, trade.Id, e.Sequence,
            e.ExecutedAtUtc, e.Side, e.Quantity, e.Price, e.Sequence == 2 ? 1.2m : 0m, e.Sequence == 2 ? .3m : 0m,
            null, null, null)).ToArray();
        trade.CorrectDetails(account, instrument, trade.Pricing, null, corrected, Audit.AddDays(1));
        await db.ServiceProvider.GetRequiredService<ITradeMutationStore>().SaveAsync(trade);
        var refreshed = Assert.Single((await reader.GetAsync(new())).Currencies);
        Assert.Equal(-286.5m, refreshed.Metrics.Net.Total);
        Assert.Equal(-286.5m, refreshed.Metrics.EffectiveNet.Total);
        Assert.False(refreshed.Metrics.EffectiveNet.IsEstimated);
        Assert.False(refreshed.Weeks[0].CumulativeMetrics.EffectiveNet.IsEstimated);
        Assert.Equal(286.5m, refreshed.Metrics.Net.AverageLoss.Value);
        Assert.Equal(refreshed.Metrics.Net.AverageLoss, refreshed.Metrics.EffectiveNet.AverageLoss);
        Assert.Equal(0, refreshed.Metrics.EffectiveNet.EstimatedTradeCount);
        Assert.Equal(AveragePnlStatus.NoWins, refreshed.Metrics.Net.AverageWin.Status);
        Assert.Equal(0m, refreshed.Metrics.Net.WinRatePercent);
        Assert.Equal(0m, refreshed.Metrics.Net.ProfitFactor.Value);
        Assert.Equal(new EffectiveNetPnL(-286.5m, NetPnLProvenance.Verified),
            Assert.Single((await list.GetPageAsync(listQuery)).Items).EffectiveNet);
        Assert.Equal(NetPnLProvenance.Verified, (await details.GetByIdAsync(trade.Id))!.EffectiveNet.Provenance);
        await db.ServiceProvider.GetRequiredService<ITradeDeletionStore>().DeleteAsync(trade.Id);
        Assert.Empty((await reader.GetAsync(new())).Currencies);
    }

    [Fact]
    public async Task FilteredPersistedFactsProduceDailyWeeklyAndSelectionRelativeCumulativeSeries()
    {
        await using ReaderTestDatabase db = await ReaderTestDatabase.CreateAsync();
        var (account, instrument) = await References(db);
        var (otherAccount, otherInstrument) = await References(db);
        DateTimeOffset sunday = new(2026, 3, 8, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset monday = sunday.AddDays(1);
        await Add(db, Enumerable.Range(0, 121).Select(_ => TradeFact(account, instrument, 1m, sunday)).ToArray());
        await Add(db, TradeFact(account, instrument, 5m, monday, costs: null),
            TradeFact(account, instrument, 0m, monday.AddDays(1)),
            TradeFact(account, instrument, 2m, monday, currency: "EUR"),
            TradeFact(account, instrument, 900m, sunday.AddDays(-1)),
            TradeFact(account, instrument, 900m, monday.AddDays(2)),
            TradeFact(otherAccount, instrument, 900m, sunday),
            TradeFact(account, otherInstrument, 900m, sunday),
            TradeFact(account, instrument, 900m, sunday, exitQuantity: 1m));

        IDashboardAnalyticsReader reader = Reader(db);
        DashboardAnalyticsSnapshot result = await reader.GetAsync(new(account, instrument, new(2026, 3, 8), new(2026, 3, 10)));
        Assert.Equal(124, result.SelectedTradeCount);
        CurrencyTradeMetrics usd = Assert.Single(result.Currencies, c => c.Currency == "USD");
        Assert.Equal(3, usd.Days.Count);
        Assert.Equal(121, usd.Days[0].Metrics.ClosedTradeCount);
        Assert.Equal(121m, usd.Days[0].Metrics.Net.Total);
        Assert.Equal(new DateOnly(2026, 3, 2), usd.Weeks[0].WeekStartingMonday);
        Assert.Equal(new DateOnly(2026, 3, 9), usd.Weeks[1].WeekStartingMonday);
        Assert.Equal(5m, usd.Weeks[1].Metrics.Gross.Total);
        Assert.Null(usd.Weeks[1].Metrics.Net.Total);
        Assert.Equal(0m, usd.Weeks[1].Metrics.Net.KnownSubtotal);
        Assert.Equal(126m, usd.Weeks[1].CumulativeMetrics.Gross.Total);
        Assert.Equal(121m, usd.Weeks[1].CumulativeMetrics.Net.KnownSubtotal);
        Assert.Null(usd.Weeks[1].CumulativeMetrics.Net.Total);
        Assert.Equal(new MetricCoverage(123, 122), usd.Weeks[1].CumulativeMetrics.Net.Coverage);
        Assert.Equal(usd.Metrics, usd.Days[^1].CumulativeMetrics);
        Assert.Equal(2m, Assert.Single(result.Currencies, c => c.Currency == "EUR").Weeks[0].CumulativeMetrics.Net.Total);

        // Excluding the earlier unknown-Net day starts a fresh complete sequence, not retained coverage.
        CurrencyTradeMetrics zeroOnly = Assert.Single((await reader.GetAsync(
            new(account, instrument, new(2026, 3, 10), new(2026, 3, 10)))).Currencies);
        Assert.Equal(0m, Assert.Single(zeroOnly.Weeks).CumulativeMetrics.Net.Total);
        Assert.Equal(new DateOnly(2026, 3, 9), zeroOnly.Weeks[0].WeekStartingMonday);
        Assert.Empty((await reader.GetAsync(new(account, instrument, new(2026, 3, 12), new(2026, 3, 12)))).Currencies);
    }

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
        Assert.Equal(1.5m, Assert.Single(result.Currencies).Metrics.Net.AverageWin.Value);
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
        CurrencyTradeMetrics series = Assert.Single(result.Currencies);
        Assert.Equal(121m, Assert.Single(series.Days).CumulativeMetrics.Net.Total);
        Assert.Equal(121m, Assert.Single(series.Weeks).CumulativeMetrics.Net.Total);
        Assert.Equal(1m, series.Metrics.Net.AverageWin.Value);
        Assert.Equal(1m, series.Weeks[0].CumulativeMetrics.Net.AverageWin.Value);
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
