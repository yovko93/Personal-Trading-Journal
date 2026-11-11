using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Desktop.Imports;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Import;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Infrastructure.Imports.Tradovate;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Storage;
using System.IO;
using System.Text;

namespace PersonalTradingJournal.Desktop.Tests.Import;

public sealed class TradovateImportAcceptanceTests
{
    private const string Csv =
        "symbol,_priceFormat,_priceFormatType,_tickSize,buyFillId,sellFillId,qty,buyPrice,sellPrice,pnl,boughtTimestamp,soldTimestamp,duration\r\n" +
        "MNQU6,2,0,0.25,M10-7-BUY,M10-7-SELL,2,20123.125,20124.375,$125.00,09/10/2026 16:30:03,09/10/2026 16:30:15,12sec";

    [Fact]
    public async Task ConfirmedDesktopWorkflowPersistsOnceAndReportsDuplicateReplay()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(TradovateImportAcceptanceTests)}-{Guid.NewGuid():N}");
        var paths = new LocalApplicationPaths(testRoot);
        paths.EnsureDirectoriesExist();
        ServiceProvider? provider = null;

        try
        {
            var services = new ServiceCollection();
            services.AddPersistence(paths);
            provider = services.BuildServiceProvider();
            await provider.GetRequiredService<JournalDatabaseInitializer>()
                .InitializeAsync();

            DateTimeOffset now = new(2026, 9, 23, 15, 30, 0, TimeSpan.Zero);
            var account = new TradingAccount(
                "Tradovate Acceptance",
                TradingAccountType.Personal,
                "Tradovate",
                "M10-7-SIM",
                "USD",
                50000m,
                now.AddDays(-1));
            await provider.GetRequiredService<ITradingAccountStore>().AddAsync(account);

            ITradingAccountReader accountReader =
                provider.GetRequiredService<ITradingAccountReader>();
            var preparation = new TradovateImportPreparationService(accountReader);
            var dialog = new FakeDialogService { ConfirmationResult = true };
            var picker = new ReusableCsvFilePicker(Csv);
            var viewModel = new ImportViewModel(
                accountReader,
                new TradovateCsvParser(),
                new TradovateExecutionReconstructor(),
                new TradovateInstrumentResolver(
                    provider.GetRequiredService<
                        PersonalTradingJournal.Application.Instruments.IInstrumentReader>()),
                preparation,
                new TradovateImportPreviewBuilder(),
                picker,
                new ImportTradovateTradesUseCase(
                    preparation,
                    provider.GetRequiredService<ITradovateImportStore>(),
                    new AcceptanceTimeProvider(now)),
                dialog, new PersonalTradingJournal.Infrastructure.Imports.Csv.ImportCsvFormatDetector(), null!, null!, null!, null!);

            await viewModel.EnsureLoadedAsync();
            viewModel.SelectedSource = viewModel.Sources.Single(s => s.Name == "Tradovate");
            await viewModel.SelectCsvCommand.ExecuteAsync(null);
            viewModel.SelectedAccount = Assert.Single(viewModel.Accounts);
            await viewModel.BuildPreviewCommand.ExecuteAsync(null);
            Assert.Equal(ImportWorkflowPhase.PreviewReady, viewModel.Phase);
            ImportTradePreviewItem candidate = Assert.Single(viewModel.Trades);
            Assert.Equal(20123.125m, candidate.AverageEntry);
            Assert.Equal(20124.375m, candidate.AverageExit);

            IDbContextFactory<JournalDbContext> contextFactory =
                provider.GetRequiredService<IDbContextFactory<JournalDbContext>>();
            var calendar = new CalendarViewModel(provider.GetRequiredService<ITradingCalendarReader>(),
                new AcceptanceTimeProvider(now), provider.GetRequiredService<ITradingCalendarDayReader>(), accountReader);
            await calendar.ActivateAsync();
            await calendar.SelectDayCommand.ExecuteAsync(calendar.Weeks.SelectMany(w => w.Days)
                .Single(d => d.Date == new DateOnly(2026, 9, 10)));
            viewModel.ImportCommitted += (_, _) => calendar.OnDataCommitted();
            Task originalCalendarRead = calendar.LoadTask;
            dialog.ConfirmationResult = false;
            await viewModel.ConfirmImportCommand.ExecuteAsync(null);
            Assert.Same(originalCalendarRead, calendar.LoadTask);
            Assert.True(calendar.IsSelectedDayEmpty);
            await using (JournalDbContext context = await contextFactory.CreateDbContextAsync())
            {
                Assert.Empty(await context.Trades.ToListAsync());
                Assert.Empty(await context.TradeExecutions.ToListAsync());
                Assert.Empty(await context.TradovateImportedExecutions.ToListAsync());
                Assert.Empty(await context.Instruments.ToListAsync());
            }
            Assert.Equal(ImportWorkflowPhase.PreviewReady, viewModel.Phase);

            dialog.ConfirmationResult = true;
            await viewModel.ConfirmImportCommand.ExecuteAsync(null);
            await calendar.LoadTask;
            Assert.False(calendar.IsMonthEmpty);
            Assert.Equal(1, calendar.DayDetails!.ClosedTradeCount);
            Assert.Equal(5m, Assert.Single(calendar.DaySummaries).Amount);
            Task committedCalendarRead = calendar.LoadTask;

            Assert.Equal(ImportWorkflowPhase.Completed, viewModel.Phase);
            Assert.Equal("Imported", viewModel.ImportResultStatus);
            Assert.Equal(1, viewModel.ImportedTradeCount);
            Assert.Equal(1, viewModel.CreatedInstrumentCount);
            Assert.False(viewModel.HasAnalysis);
            Assert.False(viewModel.HasPreview);
            Assert.True(viewModel.ShowConfirmationSection);

            await using (JournalDbContext context =
                         await contextFactory.CreateDbContextAsync())
            {
                Assert.Equal(1, await context.Trades.CountAsync());
                Assert.Equal(2, await context.TradeExecutions.CountAsync());
                decimal[] persistedPrices = (await context.TradeExecutions
                    .OrderBy(execution => execution.Sequence)
                    .Select(execution => execution.Price)
                    .ToListAsync()).ToArray();
                Assert.Equal([20123.125m, 20124.375m], persistedPrices);
                Assert.Equal(2, await context.TradovateImportedExecutions.CountAsync());
                Assert.Equal(1, await context.Instruments.CountAsync());
            }

            await viewModel.SelectCsvCommand.ExecuteAsync(null);
            Assert.False(viewModel.HasImportResult);
            viewModel.SelectedAccount = Assert.Single(viewModel.Accounts);
            await viewModel.BuildPreviewCommand.ExecuteAsync(null);
            await viewModel.ConfirmImportCommand.ExecuteAsync(null);

            Assert.Equal(ImportWorkflowPhase.Completed, viewModel.Phase);
            Assert.Equal("No changes", viewModel.ImportResultStatus);
            Assert.Same(committedCalendarRead, calendar.LoadTask);
            Assert.Equal(0, viewModel.ImportedTradeCount);
            Assert.Equal(1, viewModel.SkippedDuplicateTradeCount);
            Assert.Equal(2, picker.CallCount);
            Assert.False(viewModel.HasAnalysis);
            Assert.False(viewModel.HasPreview);
            Assert.True(viewModel.ShowConfirmationSection);

            picker.Csv = Csv.Replace("M10-7-SELL", "M10-7-OVERLAPPING-SELL", StringComparison.Ordinal);
            await viewModel.SelectCsvCommand.ExecuteAsync(null);
            viewModel.SelectedAccount = Assert.Single(viewModel.Accounts);
            await viewModel.BuildPreviewCommand.ExecuteAsync(null);
            await viewModel.ConfirmImportCommand.ExecuteAsync(null);

            Assert.False(viewModel.HasImportResult);
            Assert.Contains(TradovateImportConflictCodes.DeduplicationConflict,
                viewModel.ImportErrorMessage, StringComparison.Ordinal);
            Assert.Same(committedCalendarRead, calendar.LoadTask);
            Assert.False(viewModel.ConfirmImportCommand.CanExecute(null));
            Assert.True(viewModel.SelectCsvCommand.CanExecute(null));
            await using (JournalDbContext context = await contextFactory.CreateDbContextAsync())
            {
                Assert.Equal(1, await context.Trades.CountAsync());
                Assert.Equal(2, await context.TradeExecutions.CountAsync());
                Assert.Equal(2, await context.TradovateImportedExecutions.CountAsync());
                Assert.Equal(1, await context.Instruments.CountAsync());
            }
        }
        finally
        {
            if (provider is not null)
            {
                await provider.DisposeAsync();
            }

            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = paths.DatabasePath,
                ForeignKeys = true,
            }.ToString());
            SqliteConnection.ClearPool(connection);
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task VerifiedCostEnrichmentRequiresConfirmationAndNotifiesWithoutCreatingAnotherTrade()
    {
        string root = Path.Combine(Path.GetTempPath(), $"PTJ-cost-confirmation-{Guid.NewGuid():N}");
        var paths = new LocalApplicationPaths(root);
        paths.EnsureDirectoriesExist();
        var services = new ServiceCollection();
        services.AddPersistence(paths);
        var provider = services.BuildServiceProvider();
        try
        {
            await provider.GetRequiredService<JournalDatabaseInitializer>().InitializeAsync();
            var now = new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero);
            var account = new TradingAccount("Cost acceptance", TradingAccountType.Personal, "Tradovate", "SIM-COST", "USD", 50000m, now);
            await provider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
            var accounts = provider.GetRequiredService<ITradingAccountReader>();
            var preparation = new TradovateImportPreparationService(accounts);
            var dialog = new FakeDialogService { ConfirmationResult = true };
            var picker = new ReusableCsvFilePicker(Csv.Replace("$125.00", "$5.00"));
            var vm = new ImportViewModel(accounts, new TradovateCsvParser(), new TradovateExecutionReconstructor(),
                new TradovateInstrumentResolver(provider.GetRequiredService<PersonalTradingJournal.Application.Instruments.IInstrumentReader>()),
                preparation, new TradovateImportPreviewBuilder(), picker,
                new ImportTradovateTradesUseCase(preparation, provider.GetRequiredService<ITradovateImportStore>(), new AcceptanceTimeProvider(now)),
                dialog, new Infrastructure.Imports.Csv.ImportCsvFormatDetector(), null!, null!, null!, null!, new TradovateCostCsvParser());
            int notifications = 0;
            vm.ImportCommitted += (_, _) => notifications++;
            await vm.EnsureLoadedAsync();
            vm.SelectedSource = vm.Sources.Single(s => s.Name == "Tradovate");
            await vm.SelectCsvCommand.ExecuteAsync(null);
            vm.SelectedAccount = Assert.Single(vm.Accounts);
            await vm.BuildPreviewCommand.ExecuteAsync(null);
            await vm.ConfirmImportCommand.ExecuteAsync(null);
            Assert.Equal(1, vm.ImportedTradeCount);
            var balances = provider.GetRequiredService<ITradingAccountBalanceReader>();
            Assert.True(Assert.Single(await balances.GetAllWithBalancesAsync()).CurrentBalance!.IsEstimated);

            for (int replay = 0; replay < 2; replay++)
            {
                picker.Csv = Csv.Replace("$125.00", "$5.00");
                await vm.SelectCsvCommand.ExecuteAsync(null);
                vm.SelectedAccount = Assert.Single(vm.Accounts);
                picker.Csv = "_id,Fill ID,Account,Contract,B/S,Quantity,Price,Timestamp,_timestamp,_tradeDate,commission,_active\n" +
                    "M10-7-BUY,M10-7-BUY,SIM-COST,MNQU6,Buy,2,20123.125,09/10/2026 16:30:03,2026-09-10 13:30:03Z,2026-09-10,1.04,true\n" +
                    "M10-7-SELL,M10-7-SELL,SIM-COST,MNQU6,Sell,2,20124.375,09/10/2026 16:30:15,2026-09-10 13:30:15Z,2026-09-10,1.04,true";
                await vm.SelectFillsCommand.ExecuteAsync(null);
                picker.Csv = "Account,Transaction ID,Timestamp,Date,Delta,Amount,Cash Change Type,Currency,Contract\n" +
                    "SIM-COST,C1,09/10/2026 16:30:03,2026-09-10,-1.04,98.96,Commission,USD,MNQU6\n" +
                    "SIM-COST,C2,09/10/2026 16:30:15,2026-09-10,-1.04,97.92,Commission,USD,MNQU6\n" +
                    "SIM-COST,P1,09/10/2026 16:30:15,2026-09-10,5.00,102.92,Trade Paired,USD,MNQU6";
                await vm.SelectCashHistoryCommand.ExecuteAsync(null);
                await vm.BuildPreviewCommand.ExecuteAsync(null);
                Assert.Equal(ImportWorkflowPhase.PreviewReady, vm.Phase);
                Assert.Contains("Gross 5.00", vm.VerifiedCostSummary);
                Assert.Contains("Costs 2.08", vm.VerifiedCostSummary);
                Assert.Contains("Net 2.92", vm.VerifiedCostSummary);
                dialog.ConfirmationResult = false;
                await vm.ConfirmImportCommand.ExecuteAsync(null);
                Assert.Equal(replay == 0 ? 1 : 2, notifications);
                dialog.ConfirmationResult = true;
                await vm.ConfirmImportCommand.ExecuteAsync(null);
                Assert.Equal(0, vm.ImportedTradeCount);
                Assert.Equal(replay == 0 ? "Imported" : "No changes", vm.ImportResultStatus);
                Assert.Equal(2, notifications); // enrichment emits the normal refresh event; replay does not
                var balance = Assert.Single(await balances.GetAllWithBalancesAsync()).CurrentBalance!;
                Assert.Equal(50002.92m, balance.Value);
                Assert.False(balance.IsEstimated);
            }
        }
        finally
        {
            await provider.DisposeAsync();
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, ForeignKeys = true }.ToString());
            SqliteConnection.ClearPool(connection);
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class ReusableCsvFilePicker(string csv) : ITradovateCsvFilePicker
    {
        public int CallCount { get; private set; }

        public string Csv { get; set; } = csv;

        public TradovateCsvFileSelection Pick()
        {
            CallCount++;
            return new TradovateCsvFileSelection(
                "acceptance.csv",
                new MemoryStream(Encoding.UTF8.GetBytes(Csv), writable: false));
        }
    }

    private sealed class AcceptanceTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
