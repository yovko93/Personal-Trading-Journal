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
                "Tradovate", null, "USD", 0m, now);
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
                            var vm = new ImportViewModel(accounts, new TradovateCsvParser(), new TradovateExecutionReconstructor(),
                                new TradovateInstrumentResolver(provider.GetRequiredService<IInstrumentReader>()), preparation,
                                new TradovateImportPreviewBuilder(), new Picker(),
                                new ImportTradovateTradesUseCase(preparation, provider.GetRequiredService<ITradovateImportStore>(), TimeProvider.System),
                                dialogs, new Infrastructure.Imports.Csv.ImportCsvFormatDetector(), null!, null!, null!, null!);
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
        public TradovateCsvFileSelection Pick() => new("synthetic.csv", new MemoryStream(Encoding.UTF8.GetBytes(Csv)));
    }
}
