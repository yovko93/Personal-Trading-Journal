using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Desktop.Imports;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Import;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Infrastructure.Imports.Tradovate;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Desktop.Tests.Import;

public sealed class TradovateTimestampWorkflowTests
{
    private const string Header =
        "symbol,_priceFormat,_priceFormatType,_tickSize,buyFillId,sellFillId,qty,buyPrice,sellPrice,pnl,boughtTimestamp,soldTimestamp,duration\n";
    private const string ResolvedCsv = Header +
        "MNQU6,2,0,0.25,ENTRY,Z-CLOSE,1,100,102,$4,09/10/2026 09:00:00,09/10/2026 10:00:00,1hr\n" +
        "MNQU6,2,0,0.25,COVER,A-OPEN,1,101,103,$4,09/10/2026 11:00:00,09/10/2026 10:00:00,1hr";
    private const string UnresolvedCsv = Header +
        "MNQU6,2,0,0.25,TIED-BUY,TIED-SELL,1,100,102,$4,09/10/2026 10:00:00,09/10/2026 10:00:00,0sec";

    [Fact]
    public async Task BlockedFileAccountChangesAndResolvedFileRestoreReadOnlyPreviewThenImportOnce()
    {
        string root = Path.Combine(Path.GetTempPath(), $"TimestampWorkflow-{Guid.NewGuid():N}");
        var paths = new LocalApplicationPaths(root);
        paths.EnsureDirectoriesExist();
        try
        {
            var services = new ServiceCollection();
            services.AddPersistence(paths);
            await using ServiceProvider provider = services.BuildServiceProvider();
            await provider.GetRequiredService<JournalDatabaseInitializer>().InitializeAsync();
            foreach (string name in new[] { "First", "Second" })
            {
                await provider.GetRequiredService<ITradingAccountStore>().AddAsync(new TradingAccount(
                    name, TradingAccountType.Personal, "Tradovate", "SYNTHETIC", "USD", 10000m,
                    new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)));
            }
            var picker = new Picker { Csv = UnresolvedCsv };
            var preparation = new TradovateImportPreparationService(provider.GetRequiredService<ITradingAccountReader>());
            var vm = new ImportViewModel(provider.GetRequiredService<ITradingAccountReader>(),
                new TradovateCsvParser(), new TradovateExecutionReconstructor(),
                new TradovateInstrumentResolver(provider.GetRequiredService<IInstrumentReader>()),
                preparation, new TradovateImportPreviewBuilder(), picker,
                new ImportTradovateTradesUseCase(preparation, provider.GetRequiredService<ITradovateImportStore>(), TimeProvider.System),
                new FakeDialogService { ConfirmationResult = true },
                new PersonalTradingJournal.Infrastructure.Imports.Csv.ImportCsvFormatDetector(), null!, null!, null!, null!);
            var factory = provider.GetRequiredService<IDbContextFactory<JournalDbContext>>();

            await vm.EnsureLoadedAsync();
            vm.SelectedSource = vm.Sources.Single(s => s.Name == "Tradovate");
            await vm.SelectCsvCommand.ExecuteAsync(null);
            Assert.Equal(ImportWorkflowPhase.Blocked, vm.Phase);
            vm.SelectedAccount = vm.Accounts[0];
            Assert.False(vm.BuildPreviewCommand.CanExecute(null));
            Assert.False(vm.ConfirmImportCommand.CanExecute(null));
            Assert.Contains(vm.Diagnostics, item => item.Code == TradovateReconstructionDiagnosticCodes.TimestampOrderAmbiguous);

            picker.Csv = ResolvedCsv;
            await vm.SelectCsvCommand.ExecuteAsync(null);
            Assert.Equal(ImportWorkflowPhase.FileAnalyzed, vm.Phase);
            Assert.Null(vm.SelectedAccount);
            vm.SelectedAccount = vm.Accounts[0];
            Assert.True(vm.BuildPreviewCommand.CanExecute(null));
            Assert.False(vm.HasPreview);
            Assert.DoesNotContain(vm.Diagnostics, item => item.Code == TradovateReconstructionDiagnosticCodes.TimestampOrderAmbiguous);
            await vm.BuildPreviewCommand.ExecuteAsync(null);
            Assert.Equal(ImportWorkflowPhase.PreviewReady, vm.Phase);
            Assert.Equal(2, vm.Trades.Count);
            Assert.True(vm.ConfirmImportCommand.CanExecute(null));

            vm.SelectedAccount = vm.Accounts[1];
            Assert.False(vm.HasPreview);
            Assert.False(vm.ConfirmImportCommand.CanExecute(null));
            Assert.True(vm.BuildPreviewCommand.CanExecute(null));
            await vm.BuildPreviewCommand.ExecuteAsync(null);
            Assert.Equal("Second", vm.PreviewSummary!.AccountName);
            vm.SelectedAccount = null;
            Assert.False(vm.BuildPreviewCommand.CanExecute(null));
            vm.SelectedAccount = vm.Accounts[1];
            await vm.BuildPreviewCommand.ExecuteAsync(null);
            await using (JournalDbContext read = await factory.CreateDbContextAsync())
            {
                Assert.Empty(await read.Trades.ToArrayAsync());
                Assert.Empty(await read.TradeExecutions.ToArrayAsync());
                Assert.Empty(await read.TradovateImportedExecutions.ToArrayAsync());
                Assert.Empty(await read.Instruments.ToArrayAsync());
            }

            await vm.ConfirmImportCommand.ExecuteAsync(null);
            Assert.Equal("Imported", vm.ImportResultStatus);
            Assert.Equal(2, vm.ImportedTradeCount);
            Assert.False(vm.ConfirmImportCommand.CanExecute(null));
            await vm.SelectCsvCommand.ExecuteAsync(null);
            Assert.False(vm.HasImportResult);
            vm.SelectedAccount = vm.Accounts[1];
            await vm.BuildPreviewCommand.ExecuteAsync(null);
            await vm.ConfirmImportCommand.ExecuteAsync(null);
            Assert.Equal("No changes", vm.ImportResultStatus);
            Assert.Equal(2, vm.SkippedDuplicateTradeCount);
            await using JournalDbContext fresh = await factory.CreateDbContextAsync();
            Assert.Equal(2, await fresh.Trades.CountAsync());
            Assert.Equal(4, await fresh.TradeExecutions.CountAsync());
            Assert.Equal(4, await fresh.TradovateImportedExecutions.CountAsync());
            Assert.All(await fresh.TradeBrowse.ToArrayAsync(), item =>
            {
                Assert.Equal(4m, item.GrossPnL);
                Assert.Null(item.NetPnL);
            });
        }
        finally
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = paths.DatabasePath,
                ForeignKeys = true,
            }.ToString());
            SqliteConnection.ClearPool(connection);
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class Picker : ITradovateCsvFilePicker
    {
        public string Csv { get; set; } = ResolvedCsv;
        public TradovateCsvFileSelection Pick() => new("synthetic.csv", new MemoryStream(Encoding.UTF8.GetBytes(Csv)));
    }
}
