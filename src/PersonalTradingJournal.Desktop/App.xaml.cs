using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Infrastructure.Storage;
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
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<LocalApplicationPaths>();
        builder.Services.AddSingleton<IApplicationPaths>(
            static serviceProvider => serviceProvider.GetRequiredService<LocalApplicationPaths>());
        builder.Services.AddTransient<MainWindow>();

        _host = builder.Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        await _host.StartAsync();

        LocalApplicationPaths applicationPaths =
            _host.Services.GetRequiredService<LocalApplicationPaths>();
        applicationPaths.EnsureDirectoriesExist();

        MainWindow mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            await _host.StopAsync();
        }
        finally
        {
            _host.Dispose();
            base.OnExit(e);
        }
    }
}
