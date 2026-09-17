using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Settings;
using PersonalTradingJournal.Desktop.Theming;
using PersonalTradingJournal.Desktop.ViewModels;
using PersonalTradingJournal.Desktop.ViewModels.Accounts;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Desktop.ViewModels.Instruments;
using PersonalTradingJournal.Desktop.ViewModels.Mistakes;
using PersonalTradingJournal.Desktop.ViewModels.Setups;
using PersonalTradingJournal.Desktop.ViewModels.Settings;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Storage;
using Serilog;
using System.IO;
using System.Windows;

namespace PersonalTradingJournal.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private readonly IHost _host;

    public App()
    {
        var applicationPaths = new LocalApplicationPaths();
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
            builder.Services.AddTransient<AddTradeScreenshotUseCase>();
            builder.Services.AddTransient<DeleteTradeScreenshotUseCase>();
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
            builder.Services.AddTransient<InstrumentsViewModel>();
            builder.Services.AddTransient<TradingMistakesViewModel>();
            builder.Services.AddTransient<TradingSetupsViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();
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
            throw;
        }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("Application stopping");

        try
        {
            await _host.StopAsync();
            Log.Information("Application stopped");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Application failed to stop cleanly");
        }
        finally
        {
            _host.Dispose();

            try
            {
                await Log.CloseAndFlushAsync();
            }
            finally
            {
                base.OnExit(e);
            }
        }
    }
}
