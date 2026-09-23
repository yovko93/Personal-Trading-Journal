using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Desktop.Imports;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Import;
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
                dialog);

            await viewModel.EnsureLoadedAsync();
            await viewModel.SelectCsvCommand.ExecuteAsync(null);
            viewModel.SelectedAccount = Assert.Single(viewModel.Accounts);
            await viewModel.BuildPreviewCommand.ExecuteAsync(null);
            Assert.Equal(ImportWorkflowPhase.PreviewReady, viewModel.Phase);

            await viewModel.ConfirmImportCommand.ExecuteAsync(null);

            Assert.Equal(ImportWorkflowPhase.Completed, viewModel.Phase);
            Assert.Equal("Imported", viewModel.ImportResultStatus);
            Assert.Equal(1, viewModel.ImportedTradeCount);
            Assert.Equal(1, viewModel.CreatedInstrumentCount);

            IDbContextFactory<JournalDbContext> contextFactory =
                provider.GetRequiredService<IDbContextFactory<JournalDbContext>>();
            await using (JournalDbContext context =
                         await contextFactory.CreateDbContextAsync())
            {
                Assert.Equal(1, await context.Trades.CountAsync());
                Assert.Equal(2, await context.TradeExecutions.CountAsync());
                Assert.Equal(2, await context.TradovateImportedExecutions.CountAsync());
                Assert.Equal(1, await context.Instruments.CountAsync());
            }

            await viewModel.SelectCsvCommand.ExecuteAsync(null);
            await viewModel.BuildPreviewCommand.ExecuteAsync(null);
            await viewModel.ConfirmImportCommand.ExecuteAsync(null);

            Assert.Equal(ImportWorkflowPhase.Completed, viewModel.Phase);
            Assert.Equal("No changes", viewModel.ImportResultStatus);
            Assert.Equal(0, viewModel.ImportedTradeCount);
            Assert.Equal(1, viewModel.SkippedDuplicateTradeCount);
            Assert.Equal(2, picker.CallCount);
        }
        finally
        {
            if (provider is not null)
            {
                await provider.DisposeAsync();
            }

            SqliteConnection.ClearAllPools();
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private sealed class ReusableCsvFilePicker(string csv) : ITradovateCsvFilePicker
    {
        public int CallCount { get; private set; }

        public TradovateCsvFileSelection Pick()
        {
            CallCount++;
            return new TradovateCsvFileSelection(
                "acceptance.csv",
                new MemoryStream(Encoding.UTF8.GetBytes(csv), writable: false));
        }
    }

    private sealed class AcceptanceTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
