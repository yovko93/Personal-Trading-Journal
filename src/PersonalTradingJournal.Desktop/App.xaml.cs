using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Desktop.ViewModels;
using PersonalTradingJournal.Desktop.ViewModels.Accounts;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
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
            builder.Services.AddTransient<CreateTradingAccountUseCase>();
            builder.Services.AddTransient<AccountsViewModel>();
            builder.Services.AddTransient<DashboardViewModel>();
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
