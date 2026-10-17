using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.ViewModels.Breakdown;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Analytics;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Trades;
using PersonalTradingJournal.Infrastructure.Storage;

#if DEBUG
const string configuration = "Debug";
#else
const string configuration = "Release";
#endif

// Explicit opt-in measurement, not an additional test project or a production-data tool.
// No input database path is accepted. Every run creates and removes its own temporary root.
string root = Path.Combine(Path.GetTempPath(), "ptj-breakdown-measure-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
bool retainLiveFixture = false;
try
{
    if (args.SequenceEqual(new[] { "--live-fixture" }))
    {
        var paths = new LocalApplicationPaths(root);
        paths.EnsureDirectoriesExist();
        await Seed(new Factory(new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(
            new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, Pooling = false }.ToString()).Options), 1000);
        retainLiveFixture = true;
        Console.WriteLine(JsonSerializer.Serialize(new { IsolatedDataRoot = root, Trades = 1000 }));
        return;
    }
    if (args.Length != 0) throw new ArgumentException("Only --live-fixture is supported; no existing database may be supplied.");
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        Environment.ProcessorCount, Build = configuration, Warmups = 1, Samples = 3,
        Presentation = "Formatting/sorting/chart preparation only; no WPF window or render"
    }));
    foreach (int count in new[] { 1_000, 10_000, 50_000 })
    {
        var timer = Stopwatch.StartNew();
        string path = Path.Combine(root, $"synthetic-{count}.db");
        var factory = new Factory(new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(
            new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()).Options);
        await Seed(factory, count);
        double preparationMs = timer.Elapsed.TotalMilliseconds;
        var reader = new BreakdownAnalyticsReader(factory);
        var query = new BreakdownQuery("USD", grouping: BreakdownGrouping.Direction);
        var warm = await reader.GetAsync(query);
        Check(warm.Summary.EligibleTradeCount == count * 4 / 5, "Currency population");
        var cohort = new TradeIdCohort(warm.Groups.OrderByDescending(g => g.Metrics.EligibleTradeCount).First().Metrics.TradeIds);
        var tradeReader = new TradeListReader(factory);
        int lastPage = (cohort.TradeIds.Count - 1) / 20 + 1;
        var samples = new List<object>();
        // The same warm-up covers SQL compilation, calculator, presentation and both pages.
        for (int run = -1; run < 3; run++)
        {
            var read = await Measure(() => reader.GetAsync(query));
            var result = read.Value;
            Check(result.Groups.Sum(g => g.Metrics.EligibleTradeCount) == result.Summary.EligibleTradeCount, "Partition count");
            Check(result.Groups.Sum(g => g.Metrics.Selected.Total) == result.Summary.Selected.Total, "Partition P&L");
            Check(result.Groups.SelectMany(g => g.Metrics.TradeIds).Distinct().Count() == result.Summary.TradeIds.Count, "Unique membership");
            var present = await Measure(() =>
            {
                var p = new BreakdownPresentation(result);
                var rows = p.Sort(BreakdownSortColumn.Pnl, true);
                var chart = new BreakdownChartPresentation(rows, BreakdownChartMetric.Pnl, query.Currency, p.Summary.Basis);
                foreach (var row in rows) _ = row.AccessibleDescription;
                return Task.FromResult(chart);
            });
            var first = await Measure(() => tradeReader.GetPageAsync(Page(1)));
            var last = await Measure(() => tradeReader.GetPageAsync(Page(lastPage)));
            Check(first.Value.TotalCount == cohort.TradeIds.Count && last.Value.TotalCount == cohort.TradeIds.Count, "Cohort count");
            Check(first.Value.Items.Count == 20 && last.Value.Items.Count > 0, "Cohort pages");
            if (run >= 0) samples.Add(new
            {
                Reader = read.Stats, Presentation = present.Stats, FirstPage = first.Stats, LastPage = last.Stats,
                WorkingSetBytes = Process.GetCurrentProcess().WorkingSet64, ManagedHeapBytes = GC.GetTotalMemory(false)
            });
        }
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Trades = count, Executions = count * 2, PreparationMs = preparationMs,
            EligibleUsd = warm.Summary.EligibleTradeCount, Groups = warm.Groups.Count,
            Cohort = cohort.TradeIds.Count, LastPage = lastPage,
            warm.Summary.FinancialSources, Samples = samples
        }));
        TradeListQuery Page(int page) => new(page, 20, TradeListSortColumn.OpenedAtUtc, TradeListSortDirection.Descending, cohort: cohort);
    }
}
finally
{
    // Only the fresh, owned root; no pooled connections or outstanding tasks survive here.
    if (!retainLiveFixture) Directory.Delete(root, recursive: true);
}

static async Task<(T Value, Measurement Stats)> Measure<T>(Func<Task<T>> action)
{
    long allocated = GC.GetTotalAllocatedBytes(precise: true);
    var timer = Stopwatch.StartNew();
    T value = await action();
    timer.Stop();
    return (value, new(timer.Elapsed.TotalMilliseconds, GC.GetTotalAllocatedBytes(precise: true) - allocated));
}

static void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("Synthetic reconciliation failed: " + label);
}

static async Task Seed(Factory factory, int count)
{
    DateTimeOffset audit = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    var accounts = Enumerable.Range(0, 10).Select(i => new TradingAccount($"Synthetic {i}", TradingAccountType.Personal,
        null, null, i % 5 == 0 ? "EUR" : "USD", 10000m, audit)).ToArray();
    var instruments = Enumerable.Range(0, 20).Select(i => new Instrument($"TEST{i}", $"Synthetic {i}",
        AssetClass.Futures, "TEST", "USD", .25m, .25m, audit)).ToArray();
    var setups = Enumerable.Range(0, 8).Select(i => new TradingSetup($"Setup {i}", null, audit)).ToArray();
    setups[^1].Deactivate(audit.AddDays(1));
    await using (var context = factory.CreateDbContext())
    {
        await context.Database.MigrateAsync();
        context.TradingAccounts.AddRange(accounts.Select(TradingAccountPersistenceMapper.ToRecord));
        context.Instruments.AddRange(instruments.Select(InstrumentPersistenceMapper.ToRecord));
        context.TradingSetups.AddRange(setups.Select(TradingSetupPersistenceMapper.ToRecord));
        await context.SaveChangesAsync();
    }
    for (int offset = 0; offset < count; offset += 500)
    {
        await using var context = factory.CreateDbContext();
        var trades = new List<Trade>();
        for (int i = offset; i < Math.Min(offset + 500, count); i++)
        {
            Guid id = Guid.NewGuid();
            var close = audit.AddDays(1 + i % 280).AddMinutes(i % 1440);
            bool shortTrade = i % 2 == 0;
            decimal gross = (i % 101 - 50) * 2m;
            decimal? costs = i % 4 == 0 ? null : i % 11 == 0 && gross >= 0 ? gross : 2m;
            var executions = new TradeExecution[]
            {
                new(id, 1, close.AddMinutes(-1 - i % 1500), shortTrade ? ExecutionSide.Sell : ExecutionSide.Buy,
                    2m, 100m, 0m, 0m, null, null, null),
                new(id, 2, close, shortTrade ? ExecutionSide.Buy : ExecutionSide.Sell,
                    2m, 100m + (shortTrade ? -gross : gross) / 2m, costs, 0m, null, null, null)
            };
            trades.Add(Trade.Rehydrate(id, accounts[i % 10].Id, instruments[i % 20].Id,
                new(1m, accounts[i % 10].Currency), i % 7 == 0 ? null : setups[i % 8].Id, executions, audit, audit));
        }
        context.Trades.AddRange(trades.Select(TradePersistenceMapper.ToRecord));
        context.TradeExecutions.AddRange(trades.SelectMany(t => t.Executions).Select(TradeExecutionPersistenceMapper.ToRecord));
        context.TradeBrowse.AddRange(trades.Select(TradeBrowsePersistenceMapper.ToRecord));
        await context.SaveChangesAsync();
    }
}

sealed record Measurement(double Milliseconds, long AllocatedBytes);
sealed class Factory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
{
    public JournalDbContext CreateDbContext() => new(options);
}
