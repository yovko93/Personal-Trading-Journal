using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Imports;
using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Desktop.Imports;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Import;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Infrastructure.Imports.Tradovate;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Desktop.Tests.Import;

public sealed class TopstepDesktopRoutingTests
{
    [Theory]
    [InlineData("TopstepX", true)]
    [InlineData("Tradovate", true)]
    [InlineData("TopstepX", false)]
    [InlineData("Tradovate", false)]
    public async Task OverLimitSelectionCannotConfirmAndSelectingValidFileRecovers(string source, bool bytes)
    {
        await using var f = await Fixture.Create();
        f.Vm.SelectedSource = f.Vm.Sources.Single(s => s.Name == source);
        string valid = source == "TopstepX" ? Csv : TradovateCsv;
        f.Picker.Csv = valid + "\n" + (bytes ? new string(' ', CsvImportLimits.SourceBytes + 1) : new string(',', 100_000));
        await f.Vm.SelectCsvCommand.ExecuteAsync(null);
        Assert.False(f.Vm.ConfirmImportCommand.CanExecute(null));
        Assert.False(f.Vm.BuildPreviewCommand.CanExecute(null));
        Assert.True(f.Vm.SelectCsvCommand.CanExecute(null));
        Assert.True(f.Vm.WorkflowErrorMessage?.Contains(CsvImportLimitException.DiagnosticCode) == true ||
            f.Vm.Diagnostics.Any(d => d.Code == CsvImportLimitException.DiagnosticCode));
        await f.AssertTradeCount(0);
        f.Picker.Csv = valid;
        await f.Vm.SelectCsvCommand.ExecuteAsync(null);
        f.Vm.SelectedAccount = f.Vm.Accounts.Single(a => a.ProviderName == (source == "TopstepX" ? "Topstep" : "Tradovate"));
        await f.Vm.BuildPreviewCommand.ExecuteAsync(null);
        Assert.True(f.Vm.ConfirmImportCommand.CanExecute(null));
        Assert.Null(f.Vm.WorkflowErrorMessage);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task ReopenedTopstepSourceIsLimitedAtPreviewAndConfirmationWithoutPartialWrites(bool confirming, bool bytes)
    {
        await using var f = await Fixture.Create();
        await f.Preview();
        f.Picker.Csv = Csv + "\n" + (bytes ? new string(' ', CsvImportLimits.SourceBytes + 1) : new string(',', 100_000));
        if (confirming) await f.Vm.ConfirmImportCommand.ExecuteAsync(null);
        else await f.Vm.BuildPreviewCommand.ExecuteAsync(null);
        Assert.Equal(ImportWorkflowPhase.Blocked, f.Vm.Phase);
        Assert.False(f.Vm.ConfirmImportCommand.CanExecute(null));
        Assert.True(f.Vm.ImportErrorMessage?.Contains(CsvImportLimitException.DiagnosticCode) == true ||
            f.Vm.WorkflowErrorMessage?.Contains(CsvImportLimitException.DiagnosticCode) == true ||
            f.Vm.Diagnostics.Any(d => d.Code == CsvImportLimitException.DiagnosticCode));
        await f.AssertTradeCount(0);
        await using (var db = await f.Factory.CreateDbContextAsync()) Assert.False(await db.Instruments.AnyAsync());
        f.Picker.Csv = Csv;
        await f.Preview();
        await f.Vm.ConfirmImportCommand.ExecuteAsync(null);
        Assert.Equal("Imported", f.Vm.ImportResultStatus);
        await f.AssertTradeCount(1);
    }

    [Fact]
    public async Task SourceSelectionIsExplicitAndRequiredBeforePickerCanOpen()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(new[] { "Tradovate", "TopstepX" }, f.Vm.Sources.Select(s => s.Name));
        Assert.Null(f.Vm.SelectedSource);
        Assert.False(f.Vm.SelectCsvCommand.CanExecute(null));
        await f.Vm.SelectCsvCommand.ExecuteAsync(null);
        Assert.Equal(0, f.Picker.PickCount);
        f.Vm.SelectedSource = f.Vm.Sources[1];
        Assert.True(f.Vm.SelectCsvCommand.CanExecute(null));
        await f.Vm.SelectCsvCommand.ExecuteAsync(null);
        Assert.Equal(1, f.Picker.PickCount);
    }

    [Theory]
    [InlineData("Tradovate")]
    [InlineData("TopstepX")]
    public async Task WrongSourceSchemaBlocksOnceWithoutInvokingEitherProviderPipeline(string source)
    {
        await using var f = await Fixture.Create();
        f.Vm.SelectedSource = f.Vm.Sources.Single(s => s.Name == source);
        f.Picker.Csv = source == "Tradovate" ? Csv : TradovateCsv;
        await f.Vm.SelectCsvCommand.ExecuteAsync(null);
        Assert.StartsWith("CSV_SOURCE_MISMATCH:", f.Vm.WorkflowErrorMessage);
        Assert.Null(f.Vm.AnalysisSummary);
        Assert.Empty(f.Vm.Diagnostics);
        Assert.Empty(f.Vm.Instruments);
        Assert.Null(f.Vm.TopstepPreview);
        Assert.False(f.Vm.BuildPreviewCommand.CanExecute(null));
        Assert.False(f.Vm.ConfirmImportCommand.CanExecute(null));
        await f.AssertTradeCount(0);
        f.Picker.Csv = source == "Tradovate" ? TradovateCsv : Csv;
        await f.Vm.SelectCsvCommand.ExecuteAsync(null);
        Assert.Null(f.Vm.WorkflowErrorMessage);
        Assert.Equal(1, f.Vm.AnalysisSummary!.ValidRecordCount);
    }

    [Theory]
    [InlineData("preview")]
    [InlineData("blocked")]
    [InlineData("completed")]
    public async Task SourceSwitchClearsAllPriorPresentationAndCannotReuseConfirmation(string state)
    {
        await using var f = await Fixture.Create();
        await f.Preview();
        if (state == "blocked") f.Picker.Csv += "\n";
        if (state != "preview") await f.Vm.ConfirmImportCommand.ExecuteAsync(null);
        f.Vm.SelectedSource = f.Vm.Sources.Single(s => s.Name == "Tradovate");
        Assert.Equal(ImportWorkflowPhase.Idle, f.Vm.Phase);
        Assert.Null(f.Vm.SelectedAccount);
        Assert.Null(f.Vm.SelectedFileName);
        Assert.Null(f.Vm.AnalysisSummary);
        Assert.False(f.Vm.HasPreview);
        Assert.False(f.Vm.HasImportResult);
        Assert.Null(f.Vm.ImportErrorMessage);
        Assert.Null(f.Vm.WorkflowErrorMessage);
        Assert.Empty(f.Vm.Diagnostics);
        Assert.Empty(f.Vm.Instruments);
        Assert.Empty(f.Vm.TopstepCandidates);
        Assert.Empty(f.Vm.Trades);
        Assert.False(f.Vm.BuildPreviewCommand.CanExecute(null));
        Assert.False(f.Vm.ConfirmImportCommand.CanExecute(null));
        f.Vm.SelectedSource = f.Vm.Sources.Single(s => s.Name == "TopstepX");
        Assert.False(f.Vm.ConfirmImportCommand.CanExecute(null));
    }

    public const string Csv = "Id,ContractName,EnteredAt,ExitedAt,EntryPrice,ExitPrice,Fees,PnL,Size,Type,TradeDay,TradeDuration,Commissions\n" +
        "SYNTHETIC-1,MNQZ6,07/10/2026 17:00:00 +03:00,07/10/2026 17:00:02 +03:00,20000.125,20001.375,1.44,5,2,Long,07/10/2026 00:00:00 -05:00,00:00:01.1234567,1.00";
    public const string TradovateCsv = "symbol,_priceFormat,_priceFormatType,_tickSize,buyFillId,sellFillId,qty,buyPrice,sellPrice,pnl,boughtTimestamp,soldTimestamp,duration\n" +
        "MNQU6,2,0,0.25,SYN-BUY,SYN-SELL,2,20123.125,20124.375,$125.00,09/10/2026 16:30:03,09/10/2026 16:30:15,12sec";

    [Fact]
    public async Task DiagnosticVisibilityTracksPreviewAccountSourceFileAndCompletion()
    {
        await using var f = await Fixture.Create();
        var vm = f.Vm;
        Assert.False(vm.HasDiagnostics);

        await f.Preview(); // Missing MNQ produces a real proposal warning.
        Assert.Contains(vm.Diagnostics, d => d.Code == TopstepReferenceDiagnosticCodes.InstrumentCreationProposed);
        Assert.True(vm.HasDiagnostics);

        vm.SelectedAccount = vm.Accounts.Single(a => a.ProviderName == "Tradovate");
        Assert.Empty(vm.Diagnostics);
        Assert.False(vm.HasDiagnostics);
        await vm.BuildPreviewCommand.ExecuteAsync(null);
        Assert.Equal(ImportWorkflowPhase.Blocked, vm.Phase);
        Assert.Contains(vm.Diagnostics, d => d.Code == TopstepReferenceDiagnosticCodes.AccountProviderMismatch);
        Assert.True(vm.HasDiagnostics);

        vm.SelectedSource = vm.Sources.Single(s => s.Name == "Tradovate");
        Assert.Empty(vm.Diagnostics);
        Assert.False(vm.HasDiagnostics);
        vm.SelectedSource = vm.Sources.Single(s => s.Name == "TopstepX");
        await using (var db = await f.Factory.CreateDbContextAsync())
        {
            db.Instruments.Add(Instrument());
            await db.SaveChangesAsync();
        }

        await f.Preview(); // Existing verified MNQ has no warning or error.
        Assert.Equal(ImportWorkflowPhase.PreviewReady, vm.Phase);
        Assert.Empty(vm.Diagnostics);
        Assert.False(vm.HasDiagnostics);

        f.Picker.Csv = "Unknown,Mixed\n1,2";
        await vm.SelectCsvCommand.ExecuteAsync(null);
        Assert.Equal(ImportWorkflowPhase.Blocked, vm.Phase);
        Assert.Empty(vm.Diagnostics);
        Assert.False(vm.HasDiagnostics);
        Assert.StartsWith("CSV_FORMAT_UNSUPPORTED:", vm.WorkflowErrorMessage);

        f.Picker.Csv = Csv;
        await f.Preview();
        Assert.False(vm.HasDiagnostics);
        await vm.ConfirmImportCommand.ExecuteAsync(null);
        Assert.Equal("Imported", vm.ImportResultStatus);
        Assert.Empty(vm.Diagnostics);
        Assert.False(vm.HasDiagnostics);
    }

    [Fact]
    public async Task ActualDesktopCommandsRouteReviewCancelImportReplayAndSwitchToTradovate()
    {
        await using var f = await Fixture.Create();
        await f.Preview();
        var vm = f.Vm;
        bool? notifiedSelectCsvAvailable = null;
        vm.SelectCsvCommand.CanExecuteChanged += (_, _) => notifiedSelectCsvAvailable = vm.SelectCsvCommand.CanExecute(null);
        Assert.Equal(ImportCsvFormat.Topstep, vm.SourceFormat);
        Assert.Equal(1, vm.AnalysisSummary!.ValidRecordCount);
        Assert.DoesNotContain(vm.Diagnostics, d => d.Code == "MISSING_HEADER");
        Assert.True(vm.ConfirmImportCommand.CanExecute(null));
        Assert.DoesNotContain(vm.Diagnostics, d => d.Stage == "Reconstruction");
        TopstepCandidatePresentation row = Assert.Single(vm.TopstepCandidates);
        Assert.Equal(20000.125m, row.Candidate.EntryPrice);
        Assert.Contains("20000.13", row.Prices);
        Assert.Equal(2.56m, row.Candidate.CalculatedNet);
        await f.AssertTradeCount(0);

        f.Dialog.ConfirmationResult = false;
        await vm.ConfirmImportCommand.ExecuteAsync(null);
        await f.AssertTradeCount(0);
        Assert.True(vm.HasPreview);
        Assert.Contains("Point value 2", f.Dialog.ConfirmationRequest!.Message);
        Assert.Contains("Tick 0.25 / value 0.5", f.Dialog.ConfirmationRequest.Message);
        Assert.Contains("USD", f.Dialog.ConfirmationRequest.Message);
        f.Dialog.ConfirmationResult = true;
        await vm.ConfirmImportCommand.ExecuteAsync(null);
        Assert.Equal("Imported", vm.ImportResultStatus);
        Assert.True(notifiedSelectCsvAvailable); // The actual bound button must be re-enabled, not just its queried predicate.
        Assert.Equal(1, vm.ImportedTradeCount);
        Assert.False(vm.HasPreview);

        Assert.False(vm.ConfirmImportCommand.CanExecute(null));
        await vm.ConfirmImportCommand.ExecuteAsync(null);
        await f.AssertTradeCount(1);
        await f.Preview();
        Assert.Null(vm.ImportResultStatus);

        await vm.ConfirmImportCommand.ExecuteAsync(null);
        Assert.Equal("NoChanges", vm.ImportResultStatus);
        Assert.Equal(1, vm.SkippedDuplicateTradeCount);
        await f.AssertTradeCount(1);

        vm.SelectedSource = vm.Sources.Single(s => s.Name == "Tradovate");
        f.Picker.Csv = TradovateCsv;
        await vm.SelectCsvCommand.ExecuteAsync(null);
        Assert.Equal(ImportCsvFormat.Tradovate, vm.SourceFormat);
        Assert.Null(vm.SelectedAccount);
        Assert.Null(vm.ImportResultStatus);

        Assert.Null(vm.TopstepPreview);
        vm.SelectedAccount = vm.Accounts.Single(a => a.ProviderName == "Tradovate");
        await vm.BuildPreviewCommand.ExecuteAsync(null);
        Assert.Equal(ImportWorkflowPhase.PreviewReady, vm.Phase);
        Assert.Single(vm.Trades);
        Assert.True(vm.ConfirmImportCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("account")]
    [InlineData("instrument")]
    [InlineData("source")]
    public async Task ChangesAfterConfirmationBlockAndRequireFreshPreview(string change)
    {
        await using var f = await Fixture.Create();
        await f.Preview();
        bool? notifiedBuildAvailable = null;
        f.Vm.BuildPreviewCommand.CanExecuteChanged += (_, _) => notifiedBuildAvailable = f.Vm.BuildPreviewCommand.CanExecute(null);

        if (change == "source") f.Picker.Csv += "\n";
        else
        {
            await using var db = await f.Factory.CreateDbContextAsync();
            if (change == "account") (await db.TradingAccounts.SingleAsync(a => a.ProviderName == "Topstep")).Name = "Changed";
            else db.Instruments.Add(Instrument()); // The previously approved absence/proposal is stale.
            await db.SaveChangesAsync();
        }
        await f.Vm.ConfirmImportCommand.ExecuteAsync(null);
        Assert.Contains(change == "source" ? "SOURCE_CHANGED" : "REFERENCE_DATA_CHANGED", f.Vm.ImportErrorMessage);
        Assert.Contains("Rebuild", f.Vm.ImportErrorMessage);
        Assert.True(f.Vm.HasPreview);

        Assert.False(f.Vm.ConfirmImportCommand.CanExecute(null));
        Assert.True(f.Vm.BuildPreviewCommand.CanExecute(null));
        Assert.True(notifiedBuildAvailable); // WPF must receive recovery-command availability after the guard is released.
        await f.AssertTradeCount(0);
        await f.Vm.BuildPreviewCommand.ExecuteAsync(null);

        Assert.True(f.Vm.ConfirmImportCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("ambiguous")]
    [InlineData("pricing")]
    public async Task InvalidReferenceBlocksWithoutWritesAndAccountChangeResetsReview(string kind)
    {
        await using var f = await Fixture.Create();
        await f.Preview();

        if (kind == "provider") f.Vm.SelectedAccount = f.Vm.Accounts.Single(a => a.ProviderName == "Tradovate");
        else
        {
            await using var db = await f.Factory.CreateDbContextAsync();
            var instrument = Instrument();
            if (kind == "pricing") instrument.TickValue = 1m;
            db.Instruments.Add(instrument);
            if (kind == "ambiguous") db.Instruments.Add(Instrument());
            await db.SaveChangesAsync();
        }
        if (kind == "provider") { Assert.Null(f.Vm.TopstepPreview); Assert.Empty(f.Vm.Diagnostics); }
        await f.Vm.BuildPreviewCommand.ExecuteAsync(null);
        Assert.Equal(TopstepPreviewState.Blocked, f.Vm.TopstepPreview!.State);

        Assert.False(f.Vm.ConfirmImportCommand.CanExecute(null));
        await f.Vm.ConfirmImportCommand.ExecuteAsync(null);
        await f.AssertTradeCount(0);
    }

    [Fact]
    public async Task UnknownHeaderGivesOneFormatMessageAndNextSelectionRecovers()
    {
        await using var f = await Fixture.Create();
        await f.Preview();

        f.Picker.Csv = "Unknown,Mixed\n1,2";
        await f.Vm.SelectCsvCommand.ExecuteAsync(null);
        Assert.Equal(ImportCsvFormat.Unknown, f.Vm.SourceFormat);
        Assert.StartsWith("CSV_FORMAT_UNSUPPORTED:", f.Vm.WorkflowErrorMessage);
        Assert.Empty(f.Vm.Diagnostics);

        Assert.Null(f.Vm.SelectedAccount);
        Assert.False(f.Vm.BuildPreviewCommand.CanExecute(null));
        f.Picker.Csv = Csv;
        await f.Preview();
        Assert.Null(f.Vm.WorkflowErrorMessage);
        Assert.Equal(TopstepPreviewState.ReadyForConfirmation, f.Vm.TopstepPreview!.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentClicksAreRejectedAndCancelOrFailureRetainsUsablePreview(bool cancel)
    {
        var gate = new GatedStore();
        await using var f = await Fixture.Create(gate);
        await f.Preview();

        Assert.True(f.Vm.ConfirmImportCommand.CanExecute(null));
        Task first = f.Vm.ConfirmImportCommand.ExecuteAsync(null);
        await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.False(f.Vm.ConfirmImportCommand.CanExecute(null));
        Assert.False(f.Vm.SelectCsvCommand.CanExecute(null));
        // WPF does not invoke a disabled command. Do not bypass CanExecute: AsyncRelayCommand
        // intentionally cancels its old token when ExecuteAsync is forcibly invoked again.
        if (f.Vm.ConfirmImportCommand.CanExecute(null)) await f.Vm.ConfirmImportCommand.ExecuteAsync(null);
        Assert.Equal(1, gate.Count);
        if (cancel) f.Vm.CancelOperationCommand.Execute(null);
        else gate.Release.SetException(new IOException("Sensitive source content must not reach UI"));
        await first.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(f.Vm.HasPreview);
        Assert.True(f.Vm.ConfirmImportCommand.CanExecute(null));
        Assert.True(f.Vm.SelectCsvCommand.CanExecute(null));
        Assert.DoesNotContain("Sensitive", f.Vm.ImportErrorMessage);
        await f.AssertTradeCount(0);
    }

    [Fact]
    public void IsolationSwitchRequiresAbsoluteSeparateRoot()
    {
        Assert.Throws<ArgumentException>(() => DesktopApplicationPaths.FromArguments(["--isolated-data-root", "relative"]));
        Assert.Throws<ArgumentException>(() => DesktopApplicationPaths.FromArguments(["--isolated-data-root", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)]));
        Assert.StartsWith(Path.GetTempPath(), DesktopApplicationPaths.FromArguments(["--isolated-data-root", Path.Combine(Path.GetTempPath(), "ptj-acceptance")]).DatabasePath);
    }

    [Fact]
    public async Task ReplacedFileHeaderBeforePreviewRequiresReselectionWithoutWrongProviderDiagnostics()
    {
        await using var f = await Fixture.Create();
        await f.Preview();

        f.Picker.Csv = TradovateCsv;
        await f.Vm.BuildPreviewCommand.ExecuteAsync(null);
        Assert.StartsWith("CSV_FORMAT_CHANGED:", f.Vm.WorkflowErrorMessage);
        Assert.Null(f.Vm.TopstepPreview);

        Assert.False(f.Vm.ConfirmImportCommand.CanExecute(null));
        Assert.DoesNotContain(f.Vm.Diagnostics, d => d.Code == "MISSING_HEADER");
        await f.AssertTradeCount(0);
    }

    private static InstrumentRecord Instrument() => new() { Id = Guid.NewGuid(), Symbol = "MNQ", DisplayName = "Micro E-mini Nasdaq-100", AssetClass = AssetClass.Futures,
        Exchange = "CME", Currency = "USD", TickSize = .25m, TickValue = .5m, IsActive = true, CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch };

    [Fact]
    public async Task CompiledImportViewLoadsRealResourcesAndMaterializesTopstepReviewControls()
    {
        const string hostVariable = "PTJ_IMPORT_RESOURCE_TEST_HOST";
        if (Environment.GetEnvironmentVariable(hostVariable) != "1")
        {
            // Application is process-wide even after Shutdown. Keep this resource
            // test independent of every later native window/Popup test's order.
            await IsolatedTestProcess.RunSuiteAsync(typeof(TopstepDesktopRoutingTests), "import-resources",
                hostVariable, TimeSpan.FromMinutes(2), caseHangTimeout: TimeSpan.FromSeconds(30),
                testCaseFilter: $"FullyQualifiedName={typeof(TopstepDesktopRoutingTests).FullName}.{nameof(CompiledImportViewLoadsRealResourcesAndMaterializesTopstepReviewControls)}");
            return;
        }
        await using var f = await Fixture.Create();
        await f.Preview();
        await CalendarStaTest.RunAsync(() =>
        {
            System.Windows.Application? application = null;
            try
            {
                // Never instantiate Desktop.App: its production constructor opens application storage.
                application = new System.Windows.Application();
                foreach (string file in new[] { "Themes/DarkTheme", "Typography", "Spacing", "Icons", "Controls" })
                    application.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
                    {
                        Source = new Uri($"/PersonalTradingJournal.Desktop;component/Resources/{file}.xaml", UriKind.Relative),
                    });
                var view = new PersonalTradingJournal.Desktop.Views.Import.ImportView { DataContext = f.Vm };
                view.Measure(new System.Windows.Size(1280, 900));
                view.Arrange(new System.Windows.Rect(0, 0, 1280, 900));
                view.UpdateLayout();
                var checkboxes = Descendants(view).OfType<System.Windows.Controls.CheckBox>().ToArray();
                Assert.Empty(checkboxes);
                var sourceSelector = Assert.Single(Descendants(view).OfType<System.Windows.Controls.ComboBox>(),
                    combo => System.Windows.Automation.AutomationProperties.GetName(combo) == "Import source");
                Assert.Equal(new[] { "Tradovate", "TopstepX" },
                    sourceSelector.Items.Cast<ImportSourceOption>().Select(s => s.Name));
                Assert.Contains(Descendants(view).OfType<System.Windows.Controls.TextBlock>(), text => text.Text.Contains("Net 2.56 USD"));
                var diagnosticsSection = Assert.IsType<System.Windows.Controls.Border>(view.FindName("DiagnosticsSection"));
                Assert.True(f.Vm.HasDiagnostics);
                Assert.Equal(System.Windows.Visibility.Visible, diagnosticsSection.Visibility);
                f.Vm.SelectedSource = f.Vm.Sources.Single(s => s.Name == "Tradovate");
                view.UpdateLayout();
                Assert.False(f.Vm.HasDiagnostics);
                Assert.Equal(System.Windows.Visibility.Collapsed, diagnosticsSection.Visibility);
            }
            finally { application?.Shutdown(); }
        });

        static IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject root)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }
    }

    private sealed class GatedStore : ITopstepImportStore
    {
        public int Count;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<TopstepImportResult> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<TopstepImportResult> ImportAsync(TopstepImportRequest request, CancellationToken cancellationToken = default)
        { Count++; Started.TrySetResult(); return await Release.Task.WaitAsync(cancellationToken); }
    }

    private sealed class Picker : ITradovateCsvFilePicker
    {
        public string Csv { get; set; } = TopstepDesktopRoutingTests.Csv;
        public int PickCount { get; private set; }
        public TradovateCsvFileSelection Pick()
        {
            PickCount++;
            return new("synthetic.csv", Open(), Open);
        }
        private Stream Open() => new MemoryStream(Encoding.UTF8.GetBytes(Csv));
    }

    private sealed class Fixture(string root, ServiceProvider services, ImportViewModel vm, Picker picker, FakeDialogService dialog) : IAsyncDisposable
    {
        public ImportViewModel Vm => vm;
        public Picker Picker => picker;
        public FakeDialogService Dialog => dialog;
        public IDbContextFactory<JournalDbContext> Factory => services.GetRequiredService<IDbContextFactory<JournalDbContext>>();
        public static async Task<Fixture> Create(ITopstepImportStore? store = null)
        {
            string root = Path.Combine(Path.GetTempPath(), "ptj-topstep-routing-" + Guid.NewGuid().ToString("N"));
            var paths = new LocalApplicationPaths(root);
            paths.EnsureDirectoriesExist();
            var collection = new ServiceCollection();
            collection.AddPersistence(paths).AddTopstepDesktopImport();
            collection.AddSingleton(TimeProvider.System);
            if (store is not null) collection.AddSingleton(store);
            var services = collection.BuildServiceProvider();
            await services.GetRequiredService<JournalDatabaseInitializer>().InitializeAsync();
            foreach (string provider in new[] { "Topstep", "Tradovate" })
                await services.GetRequiredService<ITradingAccountStore>().AddAsync(new TradingAccount("Synthetic " + provider,
                    TradingAccountType.PropFunded, provider, null, "USD", 50000m, DateTimeOffset.UnixEpoch));
            var accountReader = services.GetRequiredService<ITradingAccountReader>();
            var preparation = new TradovateImportPreparationService(accountReader);
            var picker = new Picker();
            var dialog = new FakeDialogService { ConfirmationResult = true };
            var vm = new ImportViewModel(accountReader, new TradovateCsvParser(), new TradovateExecutionReconstructor(),
                new TradovateInstrumentResolver(services.GetRequiredService<IInstrumentReader>()), preparation, new TradovateImportPreviewBuilder(), picker,
                new ImportTradovateTradesUseCase(preparation, services.GetRequiredService<ITradovateImportStore>(), TimeProvider.System), dialog,
                services.GetRequiredService<IImportCsvFormatDetector>(), services.GetRequiredService<ITopstepCsvParser>(),
                services.GetRequiredService<ITopstepTradeCandidateReconstructor>(), services.GetRequiredService<TopstepImportPreviewBuilder>(),
                services.GetRequiredService<ImportTopstepTradesUseCase>());
            await vm.EnsureLoadedAsync();
            return new(root, services, vm, picker, dialog);
        }
        public async Task Preview()
        {
            vm.SelectedSource = vm.Sources.Single(s => s.Name == "TopstepX");
            await vm.SelectCsvCommand.ExecuteAsync(null);
            vm.SelectedAccount = vm.Accounts.Single(a => a.ProviderName == "Topstep");
            Assert.True(vm.BuildPreviewCommand.CanExecute(null));
            await vm.BuildPreviewCommand.ExecuteAsync(null);
        }
        public async Task AssertTradeCount(int count)
        {
            await using var db = await Factory.CreateDbContextAsync();
            Assert.Equal(count, await db.Trades.CountAsync());
            Assert.Equal(count * 2, await db.TradeExecutions.CountAsync());
            Assert.Equal(count, await db.TopstepImportedRows.CountAsync());
        }
        public async ValueTask DisposeAsync()
        {
            await services.DisposeAsync();
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = new LocalApplicationPaths(root).DatabasePath,
                ForeignKeys = true,
            }.ToString());
            SqliteConnection.ClearPool(connection);
            Directory.Delete(root, recursive: true);
        }
    }
}
