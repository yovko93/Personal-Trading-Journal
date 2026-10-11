using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.Settings;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    [Fact]
    public async Task DataBackupsOwnerCloseAndNavigationWaitForSafeCompletion()
    {
        const string host = "PTJ_BACKUP_OWNER_CLOSE_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(MainWindowViewModelTests), "backup-owner-close", host, TimeSpan.FromSeconds(60),
                testCaseFilter: $"FullyQualifiedName={typeof(MainWindowViewModelTests).FullName}.{nameof(DataBackupsOwnerCloseAndNavigationWaitForSafeCompletion)}");
            return;
        }
        var pending = new TaskCompletionSource<PersonalTradingJournal.Application.Backups.BackupArchiveResult>();
        var f = new BackupUiFixture { BackupWork = _ => pending.Task };
        var backups = f.ViewModel(); var fixture = CreateFixture(dataBackups: backups);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Settings);
        await CalendarStaTest.RunAsync(() =>
        {
            var window = CreateCloseTestWindow(fixture.Main); bool closed = false; window.Closed += (_, _) => closed = true;
            try
            {
                window.Show(); Task operation = backups.CreateBackupCommand.ExecuteAsync(null);
                fixture.Main.NavigateCommand.Execute(NavigationDestination.Trades);
                Assert.Equal(NavigationDestination.Settings, fixture.Main.CurrentDestination);
                window.Close(); Assert.False(closed); Assert.True(window.IsVisible);
                pending.SetResult(f.BackupResult);
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                Assert.True(operation.IsCompletedSuccessfully); window.Close(); Assert.True(closed);
            }
            finally { if (!closed) window.Close(); fixture.Main.Dispose(); }
        });
    }
}
