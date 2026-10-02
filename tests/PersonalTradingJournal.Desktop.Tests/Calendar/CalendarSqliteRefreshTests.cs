using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed class CalendarSqliteRefreshTests
{
    [Fact]
    public async Task ActiveSelectedDayAndMonthRereadCommittedCreateCorrectionAndDeletionFromIsolatedMigratedDatabase()
    {
        string root = Path.Combine(Path.GetTempPath(), $"PTJ-Calendar-Refresh-{Guid.NewGuid():N}");
        var paths = new LocalApplicationPaths(root);
        paths.EnsureDirectoriesExist();
        await using ServiceProvider provider = new ServiceCollection().AddPersistence(paths).BuildServiceProvider();
        try
        {
            await provider.GetRequiredService<JournalDatabaseInitializer>().InitializeAsync();
            DateTimeOffset audit = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var account = new TradingAccount("Synthetic inactive Calendar account", TradingAccountType.Personal, null, null, "USD", null, audit);
            account.Deactivate(audit.AddDays(1));
            var instrument = new Instrument("CAL", "Synthetic Calendar", AssetClass.Futures, "CME", "USD", 1m, 1m, audit);
            await provider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
            await provider.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
            var vm = new CalendarViewModel(provider.GetRequiredService<ITradingCalendarReader>(), new FixedTimeProvider(),
                provider.GetRequiredService<ITradingCalendarDayReader>(), provider.GetRequiredService<ITradingAccountReader>());
            await vm.ActivateAsync();
            vm.SelectedAccount = vm.Accounts.Single(a => a.Id == account.Id);
            await vm.LoadTask;
            DateOnly date = new(2026, 9, 5);
            CalendarDayCell saturday = CalendarDayDetailsTests.Cell(vm, date);
            await vm.SelectDayCommand.ExecuteAsync(saturday);
            Assert.True(vm.IsMonthEmpty);
            Assert.True(vm.IsSelectedDayEmpty);
            Guid id = Guid.NewGuid();
            DateTimeOffset close = new(2026, 9, 5, 16, 0, 0, TimeSpan.Zero);
            var trade = Trade.Rehydrate(id, account.Id, instrument.Id, new(1m, "USD"), null,
                [new TradeExecution(id, 1, close.AddHours(-1), ExecutionSide.Buy, 2m, 100m, null, null, null, null, null),
                 new TradeExecution(id, 2, close, ExecutionSide.Sell, 2m, 104m, null, null, null, null, null)], audit, audit);
            await provider.GetRequiredService<ITradeStore>().AddAsync(trade);
            vm.OnDataCommitted();
            await vm.LoadTask;
            Assert.False(vm.IsMonthEmpty);
            Assert.Equal(8m, Assert.Single(vm.DaySummaries).Amount);
            Assert.True(vm.DaySummaries[0].Metrics.EffectiveNet.IsEstimated);
            Assert.Null(Assert.Single(vm.DayTrades).Trade.NetPnL);
            Assert.Equal(8m, Assert.Single(saturday.WeeklySummaries).Amount);
            vm.SelectedCurrency = "USD";
            await vm.LoadTask;
            TradeExecution[] corrected = trade.Executions.Select(e => TradeExecution.Rehydrate(e.Id, id, e.Sequence,
                e.ExecutedAtUtc, e.Side, e.Quantity, e.Sequence == 2 ? 95m : e.Price,
                e.Sequence == 2 ? 1m : 0m, 0m, null, null, null)).ToArray();
            trade.CorrectDetails(account.Id, instrument.Id, trade.Pricing, null, corrected, close.AddDays(1));
            await provider.GetRequiredService<ITradeMutationStore>().SaveAsync(trade);
            vm.OnDataCommitted();
            await vm.LoadTask;
            Assert.Equal(-11m, Assert.Single(vm.DaySummaries).Amount);
            Assert.False(vm.DaySummaries[0].Metrics.EffectiveNet.IsEstimated);
            Assert.Equal(-11m, Assert.Single(saturday.WeeklySummaries).Amount);
            await vm.RefreshCommand.ExecuteAsync(null);
            await using (var db = await provider.GetRequiredService<IDbContextFactory<JournalDbContext>>().CreateDbContextAsync())
            {
                Assert.Equal(1, await db.Trades.CountAsync());
                Assert.Equal(2, await db.TradeExecutions.CountAsync());
                Assert.Equal(trade.UpdatedAtUtc, (await db.Trades.AsNoTracking().SingleAsync()).UpdatedAtUtc);
            }
            await provider.GetRequiredService<ITradeDeletionStore>().DeleteAsync(id);
            vm.OnDataCommitted();
            await vm.LoadTask;
            Assert.True(vm.IsMonthEmpty);
            Assert.True(vm.IsSelectedDayEmpty);
            Assert.Empty(vm.DayTrades);
            Assert.Empty(saturday.WeeklySummaries);
            Assert.Same(saturday, CalendarDayDetailsTests.Cell(vm, date));
            Assert.Equal(date, vm.SelectedDate);
            Assert.Equal(account.Id, vm.SelectedAccount.Id);
            Assert.Equal("USD", vm.SelectedCurrency);
            Assert.Equal(new DateOnly(2026, 9, 1), vm.SelectedMonth);
        }
        finally
        {
            await provider.DisposeAsync();
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, ForeignKeys = true }.ToString());
            SqliteConnection.ClearPool(connection);
            Directory.Delete(root, recursive: true);
        }
    }
}
