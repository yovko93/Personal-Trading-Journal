using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    public async Task CostSourceRowsRequireMatchingAccountAndCurrentPreview()
    {
        var accounts = new FakeTradingAccountReader();
        var id = Guid.NewGuid();
        var account = new TradingAccountDetails(id, "Synthetic", TradingAccountType.PropEvaluation,
            "Tradovate", "SYN-COST", "USD", 0m, true, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var picker = new Picker();
        var preparation = new TradovateImportPreparationService(accounts);
        var vm = new ImportViewModel(accounts, new TradovateCsvParser(), new TradovateExecutionReconstructor(),
            new TradovateInstrumentResolver(new FakeInstrumentReader()), preparation,
            new TradovateImportPreviewBuilder(), picker,
            new ImportTradovateTradesUseCase(preparation, new ReadOnlyImportStore(), TimeProvider.System), new FakeDialogService(),
            new Infrastructure.Imports.Csv.ImportCsvFormatDetector(), null!, null!, null!, null!, new TradovateCostCsvParser());
        vm.SelectedSource = vm.Sources.Single(s => s.Name == "Tradovate");
        await vm.SelectCsvCommand.ExecuteAsync(null);
        picker.Content = FillsCsv;
        await vm.SelectFillsCommand.ExecuteAsync(null);
        Assert.Equal("Pending · select the other export", vm.FillsSource.Status);
        Assert.Null(vm.CashHistorySource.FileName);
        picker.Content = CashCsv;
        await vm.SelectCashHistoryCommand.ExecuteAsync(null);
        Assert.Equal("Pending · select an Account", vm.CashHistorySource.Status);

        vm.SelectedAccount = new(id, account.Name, account.AccountType, "Tradovate", "OTHER", "USD", true);
        accounts.EnqueueDetailResult(account with { ExternalAccountId = "OTHER" });
        await vm.BuildPreviewCommand.ExecuteAsync(null);
        Assert.True(vm.FillsSource.IsError);
        Assert.True(vm.CashHistorySource.IsError);
        Assert.Contains("mismatched Accounts", vm.WorkflowErrorMessage);
        Assert.False(vm.ConfirmImportCommand.CanExecute(null));

        vm.SelectedAccount = vm.SelectedAccount with { ExternalAccountId = "SYN-COST" };
        Assert.False(vm.CashHistorySource.IsError);
        Assert.Equal("Pending · build preview to verify", vm.FillsSource.Status);
        accounts.EnqueueDetailResult(account);
        await vm.BuildPreviewCommand.ExecuteAsync(null);
        Assert.True(vm.FillsSource.IsVerified);
        Assert.True(vm.CashHistorySource.IsVerified);
        picker.Content = FillsCsv;
        picker.FileName = "replacement-fills.csv";
        await vm.SelectFillsCommand.ExecuteAsync(null);
        Assert.Equal(picker.FileName, vm.FillsSource.FileName);
        Assert.False(vm.FillsSource.IsVerified);
        Assert.False(vm.CashHistorySource.IsVerified);
        Assert.False(vm.ConfirmImportCommand.CanExecute(null));
        picker.Content = "invalid";
        picker.FileName = "invalid-cash.csv";
        await vm.SelectCashHistoryCommand.ExecuteAsync(null);
        Assert.Equal(picker.FileName, vm.CashHistorySource.FileName);
        Assert.True(vm.CashHistorySource.IsError);
        Assert.False(vm.FillsSource.IsError);
        vm.ClearCostSourcesCommand.Execute(null);
        Assert.Equal("No file selected", vm.FillsSource.Status);
        Assert.Equal("No file selected", vm.CashHistorySource.Status);
        accounts.EnqueueDetailResult(account);
        await vm.BuildPreviewCommand.ExecuteAsync(null);
        Assert.True(vm.ConfirmImportCommand.CanExecute(null));
        Assert.Null(vm.VerifiedCostSummary);
    }

    [Fact]
    public async Task LiveBoundPreviewPublishesNewYorkPeriodsWithoutWritingOrConfirming()
    {
        const string host = "PTJ_TRADOVATE_BOUND_PREVIEW_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(TradovateBoundPreviewTests), "tradovate-bound-preview",
                host, TimeSpan.FromMinutes(2), caseHangTimeout: TimeSpan.FromSeconds(30),
                testCaseFilter: $"FullyQualifiedName={typeof(TradovateBoundPreviewTests).FullName}.{nameof(LiveBoundPreviewPublishesNewYorkPeriodsWithoutWritingOrConfirming)}");
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
            var otherAccount = new TradingAccount("Other synthetic Account", TradingAccountType.PropEvaluation,
                "Tradovate", "OTHER", "USD", 0m, now);
            await provider.GetRequiredService<ITradingAccountStore>().AddAsync(otherAccount);
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
                                var matchingAccount = vm.Accounts.Single(a => a.Id == account.Id);
                                vm.SelectedAccount = matchingAccount;
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
                                picker.FileName = "synthetic-fills-september-through-december-2026.csv";
                                Complete(vm.SelectFillsCommand.ExecuteAsync(null));
                                CheckRow("FillsDetails", "Pending · select the other export");
                                CheckRow("CashHistoryDetails", "No file selected");
                                Complete(vm.BuildPreviewCommand.ExecuteAsync(null));
                                Assert.Equal(ImportWorkflowPhase.Blocked, vm.Phase);
                                Assert.False(vm.ConfirmImportCommand.CanExecute(null));
                                picker.Content = CashCsv;
                                picker.FileName = "synthetic-cash-history-september-through-december-2026.csv";
                                Complete(vm.SelectCashHistoryCommand.ExecuteAsync(null));
                                CheckRow("CashHistoryDetails", "Pending · build preview to verify");
                                vm.SelectedAccount = vm.Accounts.Single(a => a.Id == otherAccount.Id);
                                Complete(vm.BuildPreviewCommand.ExecuteAsync(null));
                                CheckRow("FillsDetails", "Reconciliation failed. Check the error below.");
                                CheckRow("CashHistoryDetails", "Reconciliation failed. Check the error below.");
                                Assert.Contains("mismatched Accounts", vm.WorkflowErrorMessage);
                                Assert.False(vm.ConfirmImportCommand.CanExecute(null));
                                vm.SelectedAccount = matchingAccount;
                                Complete(vm.BuildPreviewCommand.ExecuteAsync(null));
                                view.UpdateLayout();
                                Assert.Equal(ImportWorkflowPhase.PreviewReady, vm.Phase);
                                Assert.Contains("Gross 4.00", vm.VerifiedCostSummary);
                                Assert.Contains("Costs 2.08", vm.VerifiedCostSummary);
                                Assert.Contains("Net 1.92", vm.VerifiedCostSummary);
                                var verifiedText = Assert.IsType<TextBlock>(view.FindName("VerifiedCostPreview"));
                                Assert.Equal(vm.VerifiedCostSummary, verifiedText.Text);
                                CheckRow("FillsDetails", "✓ Verified");
                                CheckRow("CashHistoryDetails", "✓ Verified");
                                CheckSourceLayout(vm, theme, existing ? 360 : 960, existing ? 240 : 96, "verified");
                                Assert.DoesNotContain(vm.Diagnostics, d => d.Code == "COSTS_UNAVAILABLE");
                                Complete(vm.ConfirmImportCommand.ExecuteAsync(null));
                                Assert.Contains("Net 1.92", dialogs.ConfirmationRequest!.Message);
                                Complete(AssertReadOnly());

                                // A failed replacement cannot silently reuse the previously verified source.
                                picker.Content = "invalid";
                                picker.FileName = "invalid-fills.csv";
                                Complete(vm.SelectFillsCommand.ExecuteAsync(null));
                                CheckRow("FillsDetails", "Invalid CSV. Replace the file.");
                                CheckRow("CashHistoryDetails", "Pending · select the other export");
                                CheckSourceLayout(vm, theme, existing ? 360 : 960, existing ? 240 : 96, "invalid");
                                Complete(vm.BuildPreviewCommand.ExecuteAsync(null));
                                Assert.False(vm.ConfirmImportCommand.CanExecute(null));
                                Assert.Null(vm.VerifiedCostSummary);
                                picker.Content = CashCsv;
                                Complete(vm.SelectCashHistoryCommand.ExecuteAsync(null));
                                Complete(vm.BuildPreviewCommand.ExecuteAsync(null));
                                Assert.False(vm.ConfirmImportCommand.CanExecute(null));
                                Assert.Contains("both Fills and Cash History", vm.WorkflowErrorMessage);
                                vm.ClearCostSourcesCommand.Execute(null);
                                CheckRow("FillsDetails", "No file selected");
                                CheckRow("CashHistoryDetails", "No file selected");

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

                                void CheckRow(string name, string status)
                                {
                                    view.UpdateLayout();
                                    var details = Assert.IsType<ContentControl>(view.FindName(name));
                                    var row = Assert.IsType<ImportCostSourceRow>(details.Content);
                                    var text = Descendants(details).OfType<TextBlock>().ToArray();
                                    Assert.Equal(row.FileName ?? "No file selected", text[0].Text);
                                    Assert.Equal(status, text[1].Text);
                                    Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(text[1]));
                                    Assert.Equal(row.Announcement, AutomationProperties.GetName(text[1]));
                                }

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

    private static void CheckSourceLayout(ImportViewModel vm, string theme, int width, int dpi, string state)
    {
        var view = new ImportView { DataContext = vm };
        var root = new Border { Child = view };
        root.SetResourceReference(Border.BackgroundProperty, "PtjBackgroundBrush");
        // Materialize the content templates before applying the synthetic monitor DPI.
        root.Measure(new(width, 840));
        root.Arrange(new(0, 0, width, 840));
        VisualTreeHelper.SetRootDpi(root, new DpiScale(dpi / 96d, dpi / 96d));
        root.UpdateLayout();

        var panel = Assert.IsType<Border>(view.FindName("SourcePanel"));
        var buttons = new[] { "SelectPerformance", "SelectFills", "SelectCashHistory", "PerformanceOnly" }
            .Select(name => Assert.IsType<Button>(view.FindName(name))).ToArray();
        Assert.Equal(4, buttons.Select(button => BrushColor(button.Background)).Distinct().Count());
        foreach (Button button in buttons)
        {
            Assert.True(button.Focusable);
            AssertFits(button);
            Color background = BrushColor(button.Background);
            Color foreground = BrushColor(button.Foreground);
            Color tint = BrushColor(Assert.IsType<Border>(button.Template.FindName("HoverSurface", button)).Background);
            Assert.IsType<Border>(button.Template.FindName("FocusIndicator", button));
            Assert.True(Contrast(foreground, background) >= 4.5);
            Assert.True(Contrast(foreground, Color.FromRgb(Blend(background.R, tint.R),
                Blend(background.G, tint.G), Blend(background.B, tint.B))) >= 4.5);
        }

        foreach (string name in new[] { "FillsDetails", "CashHistoryDetails" })
        {
            var details = Assert.IsType<ContentControl>(view.FindName(name));
            var text = Descendants(details).OfType<TextBlock>().ToArray();
            Assert.Equal(2, text.Length);
            foreach (TextBlock item in text)
            {
                Assert.Equal(TextWrapping.Wrap, item.TextWrapping);
                AssertFits(item);
                Assert.True(Contrast(BrushColor(item.Foreground), BrushColor(panel.Background)) >= 4.5);
            }
        }
        Assert.True(buttons[1].TranslatePoint(new(), root).Y < buttons[2].TranslatePoint(new(), root).Y);
        Assert.Equal(dpi, VisualTreeHelper.GetDpi(panel).PixelsPerInchX);

        if (Environment.GetEnvironmentVariable("PTJ_IMPORT_RENDER_DIRECTORY") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            var bitmap = new RenderTargetBitmap(width * dpi / 96, 840 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(directory, $"{theme}-{width}dip-{dpi}dpi-{state}.png"));
            encoder.Save(file);
        }

        void AssertFits(FrameworkElement element)
        {
            var point = element.TranslatePoint(new(), panel);
            Assert.True(element.ActualWidth > 0 && point.X >= 0 &&
                point.X + element.ActualWidth <= panel.ActualWidth + 1, $"{element} exceeds the panel width.");
            Assert.True(element.DesiredSize.Height <= element.ActualHeight + element.Margin.Top + element.Margin.Bottom + 1);
        }

        static Color BrushColor(Brush brush) => Assert.IsType<SolidColorBrush>(brush).Color;
        static byte Blend(byte background, byte tint) => (byte)Math.Round(background * 0.84 + tint * 0.16);
        static double Contrast(Color first, Color second)
        {
            double a = Luminance(first);
            double b = Luminance(second);
            return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
        }
        static double Luminance(Color color) => .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        static double Linear(byte channel)
        {
            double value = channel / 255d;
            return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        }
    }

    private sealed class ReadOnlyImportStore : ITradovateImportStore
    {
        public Task<TradovateImportResult> ImportAsync(TradovateImportRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Source status tests must not import trades.");
    }

    private sealed class Picker : ITradovateCsvFilePicker
    {
        public string Content { get; set; } = Csv;
        public string FileName { get; set; } = "synthetic.csv";
        public TradovateCsvFileSelection Pick() => new(FileName, new MemoryStream(Encoding.UTF8.GetBytes(Content)));
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
