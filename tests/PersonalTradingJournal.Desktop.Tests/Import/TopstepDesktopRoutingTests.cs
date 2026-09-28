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
    public const string Csv = "Id,ContractName,EnteredAt,ExitedAt,EntryPrice,ExitPrice,Fees,PnL,Size,Type,TradeDay,TradeDuration,Commissions\n" +
        "SYNTHETIC-1,MNQZ6,07/10/2026 17:00:00 +03:00,07/10/2026 17:00:02 +03:00,20000.125,20001.375,1.44,5,2,Long,07/10/2026 00:00:00 -05:00,00:00:01.1234567,1.00";
    public const string TradovateCsv = "symbol,_priceFormat,_priceFormatType,_tickSize,buyFillId,sellFillId,qty,buyPrice,sellPrice,pnl,boughtTimestamp,soldTimestamp,duration\n" +
        "MNQU6,2,0,0.25,SYN-BUY,SYN-SELL,2,20123.125,20124.375,$125.00,09/10/2026 16:30:03,09/10/2026 16:30:15,12sec";

    [Fact]
    public async Task ActualDesktopCommandsRouteReviewCancelImportReplayAndSwitchToTradovate()
    {
        await using var f = await Fixture.Create();
        await f.Preview();
        var vm = f.Vm;
        Assert.Equal(ImportCsvFormat.Topstep, vm.SourceFormat);
        Assert.Equal(1, vm.AnalysisSummary!.ValidRecordCount);
        Assert.DoesNotContain(vm.Diagnostics, d => d.Code == "MISSING_HEADER");
        Assert.False(vm.ConfirmImportCommand.CanExecute(null));
        Assert.Contains(vm.ReviewChoices, r => r.Requirement.Kind == TopstepPreviewReviewKind.InstrumentCreationApproval && r.Description.Contains("Point value 2"));
        TopstepCandidatePresentation row = Assert.Single(vm.TopstepCandidates);
        Assert.Equal(20000.125m, row.Candidate.EntryPrice);
        Assert.Contains("20000.13", row.Prices);
        Assert.Equal(2.56m, row.Candidate.CalculatedNet);
        await f.AssertTradeCount(0);
        f.Accept();
        f.Dialog.ConfirmationResult = false;
        await vm.ConfirmImportCommand.ExecuteAsync(null);
        await f.AssertTradeCount(0);
        Assert.True(vm.HasPreview);
        f.Dialog.ConfirmationResult = true;
        await vm.ConfirmImportCommand.ExecuteAsync(null);
        Assert.Equal("Imported", vm.ImportResultStatus);
        Assert.Equal(1, vm.ImportedTradeCount);
        Assert.False(vm.HasPreview);
        Assert.Empty(vm.ReviewChoices);
        Assert.False(vm.ConfirmImportCommand.CanExecute(null));
        await vm.ConfirmImportCommand.ExecuteAsync(null);
        await f.AssertTradeCount(1);
        await f.Preview();
        Assert.Null(vm.ImportResultStatus);
        Assert.All(vm.ReviewChoices, r => Assert.False(r.IsAccepted));
        f.Accept();
        await vm.ConfirmImportCommand.ExecuteAsync(null);
        Assert.Equal("NoChanges", vm.ImportResultStatus);
        Assert.Equal(1, vm.SkippedDuplicateTradeCount);
        await f.AssertTradeCount(1);

        f.Picker.Csv = TradovateCsv;
        await vm.SelectCsvCommand.ExecuteAsync(null);
        Assert.Equal(ImportCsvFormat.Tradovate, vm.SourceFormat);
        Assert.Null(vm.SelectedAccount);
        Assert.Null(vm.ImportResultStatus);
        Assert.Empty(vm.ReviewChoices);
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
    public async Task ChangesAfterReviewBlockAndRequireFreshAcknowledgments(string change)
    {
        await using var f = await Fixture.Create();
        await f.Preview();
        f.Accept();
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
        Assert.All(f.Vm.ReviewChoices, r => Assert.False(r.IsAccepted));
        f.Accept(); // Old checkboxes cannot reactivate a stale preview.
        Assert.False(f.Vm.ConfirmImportCommand.CanExecute(null));
        Assert.True(f.Vm.BuildPreviewCommand.CanExecute(null));
        await f.AssertTradeCount(0);
        await f.Vm.BuildPreviewCommand.ExecuteAsync(null);
        Assert.All(f.Vm.ReviewChoices, r => Assert.False(r.IsAccepted));
        f.Accept();
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
        f.Accept();
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
        if (kind == "provider") { Assert.Null(f.Vm.TopstepPreview); Assert.Empty(f.Vm.ReviewChoices); }
        await f.Vm.BuildPreviewCommand.ExecuteAsync(null);
        Assert.Equal(TopstepPreviewState.Blocked, f.Vm.TopstepPreview!.State);
        f.Accept();
        Assert.False(f.Vm.ConfirmImportCommand.CanExecute(null));
        await f.Vm.ConfirmImportCommand.ExecuteAsync(null);
        await f.AssertTradeCount(0);
    }

    [Fact]
    public async Task UnknownHeaderGivesOneFormatMessageAndNextSelectionRecovers()
    {
        await using var f = await Fixture.Create();
        await f.Preview();
        f.Accept();
        f.Picker.Csv = "Unknown,Mixed\n1,2";
        await f.Vm.SelectCsvCommand.ExecuteAsync(null);
        Assert.Equal(ImportCsvFormat.Unknown, f.Vm.SourceFormat);
        Assert.StartsWith("CSV_FORMAT_UNSUPPORTED:", f.Vm.WorkflowErrorMessage);
        Assert.Empty(f.Vm.Diagnostics);
        Assert.Empty(f.Vm.ReviewChoices);
        Assert.Null(f.Vm.SelectedAccount);
        Assert.False(f.Vm.BuildPreviewCommand.CanExecute(null));
        f.Picker.Csv = Csv;
        await f.Preview();
        Assert.Null(f.Vm.WorkflowErrorMessage);
        Assert.Equal(TopstepPreviewState.RequiresReview, f.Vm.TopstepPreview!.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentClicksAreRejectedAndCancelOrFailureRetainsUsablePreview(bool cancel)
    {
        var gate = new GatedStore();
        await using var f = await Fixture.Create(gate);
        await f.Preview();
        f.Accept();
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
        f.Accept();
        f.Picker.Csv = TradovateCsv;
        await f.Vm.BuildPreviewCommand.ExecuteAsync(null);
        Assert.StartsWith("CSV_FORMAT_CHANGED:", f.Vm.WorkflowErrorMessage);
        Assert.Null(f.Vm.TopstepPreview);
        Assert.Empty(f.Vm.ReviewChoices);
        Assert.False(f.Vm.ConfirmImportCommand.CanExecute(null));
        Assert.DoesNotContain(f.Vm.Diagnostics, d => d.Code == "MISSING_HEADER");
        await f.AssertTradeCount(0);
    }

    private static InstrumentRecord Instrument() => new() { Id = Guid.NewGuid(), Symbol = "MNQ", DisplayName = "Micro E-mini Nasdaq-100", AssetClass = AssetClass.Futures,
        Exchange = "CME", Currency = "USD", TickSize = .25m, TickValue = .5m, IsActive = true, CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch };

    [Fact]
    public async Task CompiledImportViewLoadsRealResourcesAndMaterializesTopstepReviewControls()
    {
        await using var f = await Fixture.Create();
        await f.Preview();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
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
                Assert.Equal(f.Vm.ReviewChoices.Count, checkboxes.Length);
                Assert.All(checkboxes, checkbox => Assert.False(checkbox.IsChecked));
                Assert.All(checkboxes, checkbox => Assert.False(string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(checkbox))));
                Assert.Contains(Descendants(view).OfType<System.Windows.Controls.TextBlock>(), text => text.Text.Contains("Net 2.56 USD"));
                completed.SetResult();
            }
            catch (Exception exception) { completed.SetException(exception); }
            finally { application?.Shutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(30));

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
        public TradovateCsvFileSelection Pick() => new("synthetic.csv", Open(), Open);
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
            await vm.SelectCsvCommand.ExecuteAsync(null);
            vm.SelectedAccount = vm.Accounts.Single(a => a.ProviderName == "Topstep");
            Assert.True(vm.BuildPreviewCommand.CanExecute(null));
            await vm.BuildPreviewCommand.ExecuteAsync(null);
        }
        public void Accept() { foreach (var choice in vm.ReviewChoices) choice.IsAccepted = true; }
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
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }
}
