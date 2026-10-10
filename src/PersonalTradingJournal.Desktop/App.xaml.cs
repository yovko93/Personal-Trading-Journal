using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Imports;
using PersonalTradingJournal.Desktop.Settings;
using PersonalTradingJournal.Desktop.Theming;
using PersonalTradingJournal.Desktop.ViewModels;
using PersonalTradingJournal.Desktop.ViewModels.Accounts;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.ViewModels.Instruments;
using PersonalTradingJournal.Desktop.ViewModels.Import;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Desktop.ViewModels.Mistakes;
using PersonalTradingJournal.Desktop.ViewModels.Setups;
using PersonalTradingJournal.Desktop.ViewModels.Settings;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.DailyReview.Coaching;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Imports.Tradovate;
using PersonalTradingJournal.Infrastructure.Storage;
using Serilog;
using System.IO;
using System.Windows;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Desktop.DataManagement;
using PersonalTradingJournal.Desktop.Views.Settings;
using PersonalTradingJournal.Infrastructure.Backups;

namespace PersonalTradingJournal.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private readonly IHost? _host;
    private readonly JournalDataSession? _dataSession;
    private readonly LocalApplicationPaths _paths;
    private readonly MaintenanceLaunch _launch;
    private JournalRestoreRequest? _pendingRestore;
    private ThemeService? _maintenanceTheme;

    public App() : this(Environment.GetCommandLineArgs().Skip(1).ToArray()) { }

    internal App(string[] arguments)
    {
        _launch = MaintenanceLaunch.Parse(arguments);
        var applicationPaths = _paths = DesktopApplicationPaths.FromArguments(_launch.NormalArguments);
        // Maintenance has no normal host, settings store, logger, migration or DB session.
        if (_launch.Restore is not null) return;
        // Block maintenance/interrupted restore before creating data folders, logging, loading settings or migrating.
        try { _dataSession = new(applicationPaths); }
        catch (JournalMaintenanceException) { return; } // A recovery-only window replaces normal startup.
        applicationPaths.EnsureDirectoriesExist();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(applicationPaths.LogsDirectory, "ptj-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] " +
                    "{Message:lj} {Properties:j}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton(applicationPaths);
            builder.Services.AddSingleton<IApplicationPaths>(applicationPaths);
            builder.Services.AddPersistence(applicationPaths);
            builder.Services.AddSingleton(new CoachingConfiguration(applicationPaths.CoachingProviderPath,
                new(applicationPaths.CoachingCredentialsPath, () => Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
                new(applicationPaths.GroqCredentialsPath, () => Environment.GetEnvironmentVariable("GROQ_API_KEY")),
                File.Exists(applicationPaths.SettingsPath) || File.Exists(applicationPaths.DatabasePath)));
            builder.Services.AddSingleton<PersonalTradingJournal.Application.DailyReview.Coaching.ICoachingConfiguration>(
                s => s.GetRequiredService<CoachingConfiguration>());
            builder.Services.AddSingleton<PersonalTradingJournal.Application.DailyReview.Coaching.ICoachingCredentials>(
                s => s.GetRequiredService<CoachingConfiguration>());
            builder.Services.AddDailyCoaching();
            builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
            builder.Services.AddSingleton<ISystemThemeProvider, WindowsSystemThemeProvider>();
            builder.Services.AddSingleton<IThemeService>(services => new ThemeService(
                Resources,
                services.GetRequiredService<ISystemThemeProvider>()));
            builder.Services.AddSingleton<IDesktopSettingsStore, JsonDesktopSettingsStore>();
            builder.Services.AddTransient<CreateTradingAccountUseCase>();
            builder.Services.AddTransient<GetTradingAccountDetailsUseCase>();
            builder.Services.AddTransient<UpdateTradingAccountUseCase>();
            builder.Services.AddTransient<DeleteTradingAccountUseCase>();
            builder.Services.AddTransient<TradingAccountLifecycleUseCase>();
            builder.Services.AddTransient<CreateInstrumentUseCase>();
            builder.Services.AddTransient<GetInstrumentDetailsUseCase>();
            builder.Services.AddTransient<UpdateInstrumentUseCase>();
            builder.Services.AddTransient<DeleteInstrumentUseCase>();
            builder.Services.AddTransient<InstrumentLifecycleUseCase>();
            builder.Services.AddTransient<CreateTradingMistakeUseCase>();
            builder.Services.AddTransient<GetTradingMistakeDetailsUseCase>();
            builder.Services.AddTransient<UpdateTradingMistakeUseCase>();
            builder.Services.AddTransient<DeleteTradingMistakeUseCase>();
            builder.Services.AddTransient<TradingMistakeLifecycleUseCase>();
            builder.Services.AddTransient<AssignTradeMistakeUseCase>();
            builder.Services.AddTransient<RemoveTradeMistakeUseCase>();
            builder.Services.AddTransient<CreateTradingSetupUseCase>();
            builder.Services.AddTransient<GetTradingSetupDetailsUseCase>();
            builder.Services.AddTransient<UpdateTradingSetupUseCase>();
            builder.Services.AddTransient<DeleteTradingSetupUseCase>();
            builder.Services.AddTransient<TradingSetupLifecycleUseCase>();
            builder.Services.AddTransient<CreateManualTradeUseCase>();
            builder.Services.AddTransient<SetTradeTradingSetupUseCase>();
            builder.Services.AddTransient<CloseManualTradeUseCase>();
            builder.Services.AddTransient<UpdateTradeUseCase>();
            builder.Services.AddTransient<DeleteTradeUseCase>();
            builder.Services.AddTransient<DeleteAccountTradesUseCase>();
            builder.Services.AddTransient<AddTradeScreenshotUseCase>();
            builder.Services.AddTransient<DeleteTradeScreenshotUseCase>();
            builder.Services.AddTransient<ITradovateCsvParser, TradovateCsvParser>();
            builder.Services.AddTransient<
                ITradovateExecutionReconstructor,
                TradovateExecutionReconstructor>();
            builder.Services.AddTransient<TradovateInstrumentResolver>();
            builder.Services.AddTransient<TradovateImportPreparationService>();
            builder.Services.AddTransient<TradovateImportPreviewBuilder>();
            builder.Services.AddTransient<ImportTradovateTradesUseCase>();
            builder.Services.AddTopstepDesktopImport();
            builder.Services.AddTransient<ITradovateCsvFilePicker, WpfTradovateCsvFilePicker>();
            builder.Services.AddTransient<
                ITradeScreenshotFilePicker,
                WpfTradeScreenshotFilePicker>();
            builder.Services.AddSingleton<
                ITradeScreenshotImageDecoder,
                WpfTradeScreenshotImageDecoder>();
            builder.Services.AddSingleton<IDialogService, WpfDialogService>();
            builder.Services.AddSingleton<
                ITradeScreenshotDeleteConfirmation,
                WpfTradeScreenshotDeleteConfirmation>();
            builder.Services.AddTransient<AccountsViewModel>();
            builder.Services.AddTransient<DashboardViewModel>();
            builder.Services.AddTransient<PersonalTradingJournal.Desktop.ViewModels.DailyReview.DailyReviewViewModel>();
            builder.Services.AddTransient<CalendarViewModel>();
            builder.Services.AddTransient<JournalViewModel>();
            builder.Services.AddTransient<JournalTradeContextViewModel>();
            builder.Services.AddTransient<InstrumentsViewModel>();
            builder.Services.AddTransient<ImportViewModel>();
            builder.Services.AddTransient<TradingMistakesViewModel>();
            builder.Services.AddTransient<TradingSetupsViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddSingleton<IDataBackupInteraction, WpfDataBackupInteraction>();
            builder.Services.AddTransient(s => new DataBackupsViewModel(
                s.GetRequiredService<IBackupArchiveService>(), s.GetRequiredService<IPortableExportService>(),
                s.GetRequiredService<IRestorePreflightService>(), s.GetRequiredService<IDataBackupInteraction>(),
                s.GetRequiredService<IDialogService>(), Path.GetTempPath()));
            builder.Services.AddTransient<TradesViewModel>();
            builder.Services.AddTransient<MainWindowViewModel>();
            builder.Services.AddTransient<MainWindow>();
            builder.Services.AddSerilog(Log.Logger, dispose: false);

            _host = builder.Build();
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Application host configuration failed");
            Log.CloseAndFlush();
            _dataSession.Dispose();
            throw;
        }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (_host is null)
        {
            ShowMaintenanceWindow();
            return;
        }

        Log.Information("Application starting");

        try
        {
            await _host.StartAsync();

            IDesktopSettingsStore settingsStore =
                _host.Services.GetRequiredService<IDesktopSettingsStore>();
            DesktopSettings settings = await settingsStore.LoadAsync();
            IThemeService themeService = _host.Services.GetRequiredService<IThemeService>();
            themeService.SetPreferredTheme(settings.Theme);
            Log.Information(
                "Theme preference loaded and applied: {PreferredTheme}; effective theme: {EffectiveTheme}",
                themeService.PreferredTheme,
                themeService.EffectiveTheme);

            Log.Information("Initializing database");
            JournalDatabaseInitializer initializer =
                _host.Services.GetRequiredService<JournalDatabaseInitializer>();
            await initializer.InitializeAsync();
            Log.Information("Database initialized");

            MainWindow mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            Log.Information("Application started");
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Application failed to start");
            Shutdown(-1);
        }
    }

    internal void ShowMaintenanceWindow()
    {
        if (_host is not null) throw new InvalidOperationException("Maintenance cannot run beneath a normal application host.");
        _maintenanceTheme = new ThemeService(Resources, new WindowsSystemThemeProvider(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WindowsSystemThemeProvider>.Instance));
        var vm = new MaintenanceViewModel(new JournalRestoreService(_paths),
            token => MaintenanceLaunch.WaitForParentAsync(_launch.ParentId, token),
            () => { MaintenanceLaunch.Start(_launch.NormalArguments); Shutdown(); });
        var window = new MaintenanceWindow { DataContext = vm };
        MainWindow = window;
        if (_launch.Restore is { } request)
        {
            RoutedEventHandler? start = null;
            start = async (_, _) => { window.Loaded -= start; await vm.RunAsync(request); };
            window.Loaded += start;
        }
        window.Show();
    }

    public bool RequestOfflineRestore(JournalRestoreRequest request)
    {
        if (_host is null || MainWindow is not { IsVisible: true } window || _pendingRestore is not null) return false;
        _pendingRestore = request;
        window.Close(); // MainWindow.OnClosing retains the existing guarded path.
        if (window.IsVisible) { _pendingRestore = null; return false; }
        return true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application stopping");

        try
        {
            // WPF cannot await async-void OnExit. Finish disposal before launching maintenance.
            _host?.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Log.Information("Application stopped");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Application failed to stop cleanly");
        }
        finally
        {
            _host?.Dispose();

            try
            {
                Log.CloseAndFlush();
            }
            finally
            {
                _dataSession?.Dispose();
                _maintenanceTheme?.Dispose();
                if (_pendingRestore is { } request)
                {
                    try { MaintenanceLaunch.Start(MaintenanceLaunch.RestoreArguments(_launch.NormalArguments, request, Environment.ProcessId)); }
                    catch (Exception)
                    {
                        MessageBox.Show("Offline maintenance could not be launched. No restore was started. Start Personal Trading Journal again and retry from Settings.",
                            "Restore not started", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                base.OnExit(e);
            }
        }
    }
}
