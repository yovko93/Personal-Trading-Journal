using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Storage;
using Xunit.Abstractions;

namespace PersonalTradingJournal.Desktop.Tests.Dashboard;

public sealed class DashboardSqliteTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(2026, 3, 8, 23)]
    [InlineData(2026, 11, 1, 25)]
    public async Task AccountAndInclusiveCustomRangeUseProductionReadersWhileRecentIgnoresDates(int year, int month, int day, int hours)
    {
        string root = Path.Combine(Path.GetTempPath(), $"PTJ-Dashboard-Filters-{Guid.NewGuid():N}");
        var paths = new LocalApplicationPaths(root);
        paths.EnsureDirectoriesExist();
        await using ServiceProvider provider = new ServiceCollection().AddPersistence(paths).BuildServiceProvider();
        try
        {
            await provider.GetRequiredService<JournalDatabaseInitializer>().InitializeAsync();
            DateOnly date = new(year, month, day);
            var bounds = new DashboardAnalyticsQuery(closedFromNewYork: date, closedThroughNewYork: date);
            DateTimeOffset first = bounds.ClosedFromUtc!.Value, after = bounds.ClosedBeforeUtc!.Value;
            Assert.Equal(hours, (after - first).TotalHours);
            DateTimeOffset audit = first.AddYears(-1);
            var accountStore = provider.GetRequiredService<ITradingAccountStore>();
            var archive = new TradingAccount("Inactive history", TradingAccountType.Personal, null, null, "USD", null, audit);
            archive.Deactivate(audit.AddDays(1));
            var other = new TradingAccount("Other account", TradingAccountType.Personal, null, null, "USD", null, audit);
            await accountStore.AddAsync(archive);
            await accountStore.AddAsync(other);
            var instrument = new Instrument("FILTER", "Synthetic filter test", AssetClass.Futures, "CME", "USD", 1m, 1m, audit);
            await provider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
            var store = provider.GetRequiredService<ITradeStore>();
            foreach (var account in new[] { archive, other })
            {
                foreach (DateTimeOffset close in new[] { first.AddTicks(-1), first, after.AddTicks(-1), after })
                {
                    Guid id = Guid.NewGuid();
                    decimal gross = account == archive ? 10m : 100m;
                    await store.AddAsync(Trade.Rehydrate(id, account.Id, instrument.Id, new(1m, "USD"), null,
                        [new TradeExecution(id, 1, close.AddHours(-1), ExecutionSide.Buy, 1m, 1000m, 0m, 0m, null, null, null),
                         new TradeExecution(id, 2, close, ExecutionSide.Sell, 1m, 1000m + gross, 0m, 0m, null, null, null)], audit, audit));
                }
            }
            Guid openId = Guid.NewGuid();
            await store.AddAsync(Trade.Rehydrate(openId, archive.Id, instrument.Id, new(1m, "USD"), null,
                [new TradeExecution(openId, 1, after.AddDays(1), ExecutionSide.Buy, 1m, 1000m, null, null, null, null, null)], audit, audit));
            var vm = new DashboardViewModel(provider.GetRequiredService<IDashboardAnalyticsReader>(), new FixedTime(after),
                provider.GetRequiredService<ITradeListReader>(), provider.GetRequiredService<ITradingAccountReader>());
            await vm.ActivateAsync();
            Assert.Equal(8, vm.Selected!.Source.Metrics.ClosedTradeCount);
            vm.SelectedAccount = vm.Accounts.Single(a => a.Id == archive.Id);
            await vm.LoadTask;
            Assert.Contains("inactive", vm.SelectedAccount.Name);
            vm.StartDate = vm.EndDate = date.ToDateTime(TimeOnly.MinValue);
            vm.ApplyRangeCommand.Execute(null);
            await vm.LoadTask;
            Assert.Equal(2, vm.Selected!.Source.Metrics.ClosedTradeCount);
            Assert.Equal(20m, vm.Selected.Source.Metrics.Net.Total);
            Assert.Equal(date, Assert.Single(vm.Selected.DailyPnl).Date);
            Assert.Equal(5, vm.RecentTrades.Count);
            Assert.Equal(openId, vm.RecentTrades[0].Id);
            Assert.All(vm.RecentTrades, t => Assert.Equal(archive.Id, t.TradingAccountId));
            Guid[] recent = vm.RecentTrades.Select(t => t.Id).ToArray();
            vm.StartDate = vm.EndDate = date.AddYears(-2).ToDateTime(TimeOnly.MinValue);
            vm.ApplyRangeCommand.Execute(null);
            await vm.LoadTask;
            Assert.True(vm.IsEmpty);
            Assert.Equal(recent, vm.RecentTrades.Select(t => t.Id));
            vm.AllHistoryCommand.Execute(null);
            await vm.LoadTask;
            Assert.Equal(4, vm.Selected!.Source.Metrics.ClosedTradeCount);
            vm.SelectedAccount = vm.Accounts.Single(a => a.Id == other.Id);
            await vm.LoadTask;
            Assert.Equal(400m, vm.Selected!.Source.Metrics.Net.Total);
            Assert.All(vm.RecentTrades, t => Assert.Equal(other.Id, t.TradingAccountId));
            await using var context = await provider.GetRequiredService<IDbContextFactory<JournalDbContext>>().CreateDbContextAsync();
            Assert.Equal(9, await context.Trades.CountAsync());
            Assert.Equal(17, await context.TradeExecutions.CountAsync());
        }
        finally
        {
            await provider.DisposeAsync();
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, ForeignKeys = true }.ToString());
            SqliteConnection.ClearPool(connection);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DashboardReadsMigratedDataAndRefreshesAfterCommittedEditsAndDeletes()
    {
        string root = Path.Combine(Path.GetTempPath(), $"PTJ-Dashboard-{Guid.NewGuid():N}");
        var paths = new LocalApplicationPaths(root);
        paths.EnsureDirectoriesExist();
        var services = new ServiceCollection().AddPersistence(paths);
        await using ServiceProvider provider = services.BuildServiceProvider();
        try
        {
            await provider.GetRequiredService<JournalDatabaseInitializer>().InitializeAsync();
            DateTimeOffset now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
            DateTimeOffset audit = now.AddYears(-2);
            var account = new TradingAccount("Dashboard isolated test", TradingAccountType.Personal, null, null, "USD", 0m, audit);
            var instrument = new Instrument("DASH", "Synthetic Dashboard", AssetClass.Futures, "CME", "USD", 1m, 1m, audit);
            await provider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
            await provider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
            Trade Create(decimal gross, decimal? costs, string currency = "USD", DateTimeOffset? closed = null)
            {
                Guid id = Guid.NewGuid();
                DateTimeOffset close = closed ?? now;
                TradeExecution[] executions = [new(id, 1, close.AddHours(-1), ExecutionSide.Buy, 1m, 1000m, 0m, 0m, null, null, null),
                    new(id, 2, close, ExecutionSide.Sell, 1m, 1000m + gross, costs, 0m, null, null, null)];
                return Trade.Rehydrate(id, account.Id, instrument.Id, new(1m, currency), null, executions, audit, audit);
            }
            ITradeStore store = provider.GetRequiredService<ITradeStore>();
            foreach (decimal pnl in new[] { 100m, -40m, 0m, 20m }) await store.AddAsync(Create(pnl, 0m));
            Trade estimated = Create(-285m, null, closed: now.AddDays(-1));
            await store.AddAsync(estimated);
            await store.AddAsync(Create(10m, 0m, "EUR"));
            await store.AddAsync(Create(500m, 0m, closed: now.AddYears(-1)));
            var vm = new DashboardViewModel(provider.GetRequiredService<IDashboardAnalyticsReader>(), new FixedTime(now), provider.GetRequiredService<ITradeListReader>(), provider.GetRequiredService<ITradingAccountReader>());
            await vm.ActivateAsync();
            vm.SelectedCurrency = "USD";
            Assert.Equal(6, vm.Selected!.Source.Metrics.ClosedTradeCount);
            Assert.Equal(295m, vm.Selected.Source.Metrics.EffectiveNet.Total);
            Assert.Null(vm.Selected.Source.Metrics.Net.Total);
            Assert.Contains(vm.RecentTrades, t => t.InstrumentSymbol == "DASH");

            vm.Period = DashboardPeriod.Year;
            await vm.LoadTask;
            Assert.Equal(5, vm.Selected!.Source.Metrics.ClosedTradeCount);
            Assert.Equal(-205m, vm.Selected.Source.Metrics.EffectiveNet.Total);
            Assert.Equal(vm.Query.ClosedFromNewYork, vm.Selected.PeriodStart);
            TradeExecution[] corrected = estimated.Executions.Select(e => TradeExecution.Rehydrate(e.Id, estimated.Id,
                e.Sequence, e.ExecutedAtUtc, e.Side, e.Quantity, e.Price, e.Sequence == 2 ? 1m : 0m, 0m, null, null, null)).ToArray();
            estimated.CorrectDetails(account.Id, instrument.Id, estimated.Pricing, null, corrected, now);
            await provider.GetRequiredService<ITradeMutationStore>().SaveAsync(estimated);
            vm.OnDataCommitted();
            await vm.LoadTask;
            Assert.Equal(-206m, vm.Selected!.Source.Metrics.Net.Total);
            Assert.False(vm.Selected.Source.Metrics.EffectiveNet.IsEstimated);
            await provider.GetRequiredService<ITradeDeletionStore>().DeleteAsync(estimated.Id);
            vm.OnDataCommitted();
            await vm.LoadTask;
            Assert.Equal(80m, vm.Selected!.Source.Metrics.Net.Total);
            Assert.Equal(50m, vm.Selected.Source.Metrics.Net.WinRatePercent);
            vm.PreviousCommand.Execute(null);
            await vm.LoadTask;
            Assert.Equal(500m, vm.Selected!.Source.Metrics.Net.Total);
            vm.PreviousCommand.Execute(null);
            await vm.LoadTask;
            Assert.True(vm.IsEmpty);
            // Latest-10 query is independently paged in SQLite, includes open Trades and deterministic ties.
            var openTrades = new List<Trade>();
            for (int i = 0; i < 12; i++)
            {
                Guid id = Guid.NewGuid();
                var open = Trade.Rehydrate(id, account.Id, instrument.Id, new(1m, i % 2 == 0 ? "EUR" : "USD"), null,
                    [new TradeExecution(id, 1, now.AddDays(1 + i / 2), ExecutionSide.Buy, 2m, 1000m, null, null, null, null, null)], audit, audit);
                await store.AddAsync(open);
                openTrades.Add(open);
            }
            await vm.RefreshAsync();
            Assert.True(vm.IsEmpty); // Empty closed period must not hide the independently loaded rows.
            var expectedIds = openTrades.OrderByDescending(t => t.Executions[0].ExecutedAtUtc).ThenBy(t => t.Id).Take(10).Select(t => t.Id).ToArray();
            Assert.Equal(expectedIds, vm.RecentTrades.Select(t => t.Id));
            Assert.All(vm.RecentTrades, t => Assert.Equal(TradeStatus.Open, t.Status));
            vm.Period = DashboardPeriod.All;
            await vm.LoadTask;
            vm.SelectedCurrency = "EUR";
            Assert.Equal(expectedIds, vm.RecentTrades.Select(t => t.Id));
            Assert.Equal(2, vm.RecentTrades.Select(t => t.Currency).Distinct().Count());
            // Leave a representative estimate in opt-in visual seed data after testing mutation behavior.
            if (Environment.GetEnvironmentVariable("PTJ_KEEP_DASHBOARD_TEST_DATA") == "1")
                await store.AddAsync(Create(-285m, null, closed: now.AddDays(-1)));
            output.WriteLine($"Isolated Dashboard root: {root}");
        }
        finally
        {
            await provider.DisposeAsync();
            // Only this fixture's connection pool is released; do not disrupt parallel SQLite tests.
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, ForeignKeys = true }.ToString());
            SqliteConnection.ClearPool(connection);
            if (Environment.GetEnvironmentVariable("PTJ_KEEP_DASHBOARD_TEST_DATA") != "1") Directory.Delete(root, recursive: true);
        }
    }
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
