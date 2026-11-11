using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Desktop.Imports;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Import;
using PersonalTradingJournal.Desktop.Views.Import;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Infrastructure.Imports.Tradovate;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Desktop.Tests.Import;

public sealed class TradovateBoundPreviewTests
{
    // Synthetic reduction of the offset-bearing preview failure. One ordinary matched row
    // is sufficient; a second contract/winter row additionally guards rollover and both offsets.
    private const string Csv =
        "symbol,_priceFormat,_priceFormatType,_tickSize,buyFillId,sellFillId,qty,buyPrice,sellPrice,pnl,boughtTimestamp,soldTimestamp,duration\r\n" +
        "MNQU6,2,0,0.25,SYN-B1,SYN-S1,1,20000,20001,$2.00,09/10/2026 16:30:03,09/10/2026 16:30:15,12sec\r\n" +
        "MNQZ6,2,0,0.25,SYN-B2,SYN-S2,1,20000,20001,$2.00,12/10/2026 16:30:03,12/10/2026 16:30:15,12sec";

    [Fact]
    public async Task LiveBoundPreviewPublishesNewYorkPeriodsWithoutWritingOrConfirming()
    {
        const string host = "PTJ_TRADOVATE_BOUND_PREVIEW_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(TradovateBoundPreviewTests), "tradovate-bound-preview",
                host, TimeSpan.FromMinutes(2), caseHangTimeout: TimeSpan.FromSeconds(30));
            return;
        }
        string root = Path.Combine(Path.GetTempPath(), $"PTJ-bound-preview-{Guid.NewGuid():N}");
        var paths = new LocalApplicationPaths(root);
        paths.EnsureDirectoriesExist();
        var services = new ServiceCollection();
        services.AddPersistence(paths);
        var provider = services.BuildServiceProvider();
        try
        {
            await provider.GetRequiredService<JournalDatabaseInitializer>().InitializeAsync();
            var now = DateTimeOffset.UnixEpoch;
            var account = new TradingAccount("Synthetic Account", TradingAccountType.PropEvaluation,
                "Tradovate", "SYN-COST", "USD", 0m, now);
            await provider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
            var accounts = provider.GetRequiredService<ITradingAccountReader>();
            var preparation = new TradovateImportPreparationService(accounts);
            var factory = provider.GetRequiredService<IDbContextFactory<JournalDbContext>>();
            await CalendarStaTest.RunAsync(() =>
            {
                // Do not instantiate Desktop.App, which opens production storage.
                var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var previousContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                try
                {
                    foreach (bool existing in new[] { false, true })
                    {
                        if (existing) Complete(provider.GetRequiredService<IInstrumentStore>().AddAsync(
                            new Instrument("MNQ", "Synthetic MNQ", AssetClass.Futures, "CME", "USD", .25m, .5m, now)));
                        foreach (string theme in new[] { "Light", "Dark" })
                        {
                            app.Resources.MergedDictionaries.Clear();
                            foreach (string resource in new[] { $"Themes/{theme}Theme", "Typography", "Spacing", "Icons", "Controls" })
                                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                                { Source = new Uri($"/PersonalTradingJournal.Desktop;component/Resources/{resource}.xaml", UriKind.Relative) });
                            var dialogs = new FakeDialogService { ConfirmationResult = false };
                            var picker = new Picker();
                            var vm = new ImportViewModel(accounts, new TradovateCsvParser(), new TradovateExecutionReconstructor(),
                                new TradovateInstrumentResolver(provider.GetRequiredService<IInstrumentReader>()), preparation,
                                new TradovateImportPreviewBuilder(), picker,
                                new ImportTradovateTradesUseCase(preparation, provider.GetRequiredService<ITradovateImportStore>(), TimeProvider.System),
                                dialogs, new Infrastructure.Imports.Csv.ImportCsvFormatDetector(), null!, null!, null!, null!, new TradovateCostCsvParser());
                            var view = new ImportView { DataContext = vm };
                            var window = new Window { Content = view, Width = 1280, Height = 900, ShowInTaskbar = false };
                            try
                            {
                                window.Show();
                                Complete(vm.EnsureLoadedAsync());
                                vm.SelectedSource = vm.Sources.Single(s => s.Name == "Tradovate");
                                Complete(vm.SelectCsvCommand.ExecuteAsync(null));
                                vm.SelectedAccount = Assert.Single(vm.Accounts);
                                Complete(vm.BuildPreviewCommand.ExecuteAsync(null));
                                view.UpdateLayout();
                                Assert.Null(vm.WorkflowErrorMessage);
                                Assert.Equal(ImportWorkflowPhase.PreviewReady, vm.Phase);
                                Assert.Equal(2, vm.Trades.Count);
                                Assert.True(vm.ConfirmImportCommand.CanExecute(null));
                                Assert.Null(dialogs.ConfirmationRequest);
                                var periodText = Descendants(view).OfType<TextBlock>()
                                    .Where(t => t.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path?.Path
                                        .StartsWith("PreviewSummary.Period", StringComparison.Ordinal) == true)
                                    .Select(t => t.Text).ToArray();
                                Assert.Equal(2, periodText.Length);
                                Assert.Contains(periodText, t => t.Contains("09:30:03 UTC-4", StringComparison.Ordinal));
                                Assert.Contains(periodText, t => t.Contains("09:30:15 UTC-5", StringComparison.Ordinal));
                                Assert.Equal(TimeSpan.FromHours(-4), vm.PreviewSummary!.PeriodStartNewYork!.Value.Offset);
                                Assert.Equal(TimeSpan.FromHours(-5), vm.PreviewSummary.PeriodEndNewYork!.Value.Offset);
                                Assert.Equal(20000m, vm.Trades[0].AverageEntry);
                                Complete(AssertReadOnly());
                                // A declined explicit confirmation also cannot persist a proposed Instrument.
                                Complete(vm.ConfirmImportCommand.ExecuteAsync(null));
                                Assert.NotNull(dialogs.ConfirmationRequest);
                                Complete(AssertReadOnly());

                                // Supporting exports remain read-only until the same explicit confirmation.
                                picker.Content = FillsCsv;
                                Complete(vm.SelectFillsCommand.ExecuteAsync(null));
                                Complete(vm.BuildPreviewCommand.ExecuteAsync(null));
                                Assert.Equal(ImportWorkflowPhase.Blocked, vm.Phase);
                                Assert.False(vm.ConfirmImportCommand.CanExecute(null));
                                picker.Content = CashCsv;
                                Complete(vm.SelectCashHistoryCommand.ExecuteAsync(null));
                                Complete(vm.BuildPreviewCommand.ExecuteAsync(null));
                                view.UpdateLayout();
                                Assert.Equal(ImportWorkflowPhase.PreviewReady, vm.Phase);
                                Assert.Contains("Gross 4.00", vm.VerifiedCostSummary);
                                Assert.Contains("Costs 2.08", vm.VerifiedCostSummary);
                                Assert.Contains("Net 1.92", vm.VerifiedCostSummary);
                                var verifiedText = Assert.IsType<TextBlock>(view.FindName("VerifiedCostPreview"));
                                Assert.Equal(vm.VerifiedCostSummary, verifiedText.Text);
                                Assert.DoesNotContain(vm.Diagnostics, d => d.Code == "COSTS_UNAVAILABLE");
                                Complete(vm.ConfirmImportCommand.ExecuteAsync(null));
                                Assert.Contains("Net 1.92", dialogs.ConfirmationRequest!.Message);
                                Complete(AssertReadOnly());

                                // A failed replacement cannot silently reuse the previously verified source.
                                picker.Content = "invalid";
                                Complete(vm.SelectFillsCommand.ExecuteAsync(null));
                                Complete(vm.BuildPreviewCommand.ExecuteAsync(null));
                                Assert.False(vm.ConfirmImportCommand.CanExecute(null));
                                Assert.Null(vm.VerifiedCostSummary);
                                picker.Content = CashCsv;
                                Complete(vm.SelectCashHistoryCommand.ExecuteAsync(null));
                                Complete(vm.BuildPreviewCommand.ExecuteAsync(null));
                                Assert.False(vm.ConfirmImportCommand.CanExecute(null));
                                Assert.Contains("both Fills and Cash History", vm.WorkflowErrorMessage);
                                vm.ClearCostSourcesCommand.Execute(null);

                                vm.PropertyChanged += (_, args) =>
                                {
                                    if (args.PropertyName == nameof(vm.PreviewSummary) && vm.PreviewSummary is not null)
                                        throw new ArgumentException("Synthetic display failure; never expose exception text.");
                                };
                                Complete(vm.BuildPreviewCommand.ExecuteAsync(null));
                                Assert.Equal(ImportWorkflowPhase.Failed, vm.Phase);
                                Assert.Contains("TVP-PREVIEW-DISPLAY-ARGUMENT", vm.WorkflowErrorMessage);
                                Assert.Null(vm.PreviewSummary);
                                Assert.Empty(vm.Trades);
                                Assert.False(vm.ConfirmImportCommand.CanExecute(null));
                                Complete(vm.ConfirmImportCommand.ExecuteAsync(null));
                                Complete(AssertReadOnly());

                                async Task AssertReadOnly()
                                {
                                    await using var db = await factory.CreateDbContextAsync();
                                    Assert.Equal(0, await db.Trades.CountAsync());
                                    Assert.Equal(0, await db.TradeExecutions.CountAsync());
                                    Assert.Equal(0, await db.TradovateImportedExecutions.CountAsync());
                                    Assert.Equal(existing ? 1 : 0, await db.Instruments.CountAsync());
                                }
                            }
                            finally { window.Close(); }
                        }
                    }
                }
                finally { SynchronizationContext.SetSynchronizationContext(previousContext); app.Shutdown(); }
            });
        }
        finally
        {
            await provider.DisposeAsync();
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = paths.DatabasePath, ForeignKeys = true }.ToString());
            SqliteConnection.ClearPool(connection);
            Directory.Delete(root, recursive: true);
        }
    }

    private static void Complete(Task task)
    {
        // Coordinate actual completion, not a sleep; the STA and child supervisor bound hangs.
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var dispatcher = Dispatcher.CurrentDispatcher;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private sealed class Picker : ITradovateCsvFilePicker
    {
        public string Content { get; set; } = Csv;
        public TradovateCsvFileSelection Pick() => new("synthetic.csv", new MemoryStream(Encoding.UTF8.GetBytes(Content)));
    }

    private const string FillsCsv = "_id,Fill ID,Account,Contract,B/S,Quantity,Price,Timestamp,_timestamp,_tradeDate,commission,_active\n" +
        "SYN-B1,SYN-B1,SYN-COST,MNQU6,Buy,1,20000,09/10/2026 16:30:03,2026-09-10 13:30:03Z,2026-09-10,0.52,true\n" +
        "SYN-S1,SYN-S1,SYN-COST,MNQU6,Sell,1,20001,09/10/2026 16:30:15,2026-09-10 13:30:15Z,2026-09-10,0.52,true\n" +
        "SYN-B2,SYN-B2,SYN-COST,MNQZ6,Buy,1,20000,12/10/2026 16:30:03,2026-12-10 14:30:03Z,2026-12-10,0.52,true\n" +
        "SYN-S2,SYN-S2,SYN-COST,MNQZ6,Sell,1,20001,12/10/2026 16:30:15,2026-12-10 14:30:15Z,2026-12-10,0.52,true";
    private const string CashCsv = "Account,Transaction ID,Timestamp,Date,Delta,Amount,Cash Change Type,Currency,Contract\n" +
        "SYN-COST,C1,09/10/2026 16:30:03,2026-09-10,-0.52,99.48,Commission,USD,MNQU6\n" +
        "SYN-COST,C2,09/10/2026 16:30:15,2026-09-10,-0.52,98.96,Commission,USD,MNQU6\n" +
        "SYN-COST,P1,09/10/2026 16:30:15,2026-09-10,2.00,100.96,Trade Paired,USD,MNQU6\n" +
        "SYN-COST,C3,12/10/2026 16:30:03,2026-12-10,-0.52,100.44,Commission,USD,MNQZ6\n" +
        "SYN-COST,C4,12/10/2026 16:30:15,2026-12-10,-0.52,99.92,Commission,USD,MNQZ6\n" +
        "SYN-COST,P2,12/10/2026 16:30:15,2026-12-10,2.00,101.92,Trade Paired,USD,MNQZ6";
}
