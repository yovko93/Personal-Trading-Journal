using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Settings;
using PersonalTradingJournal.Desktop.Views.Settings;

namespace PersonalTradingJournal.Desktop.Tests.Settings;

public sealed class DataBackupsLayoutTests
{
    [Fact]
    public async Task CompiledSettingsAndMaintenanceWrapScrollAndExposeNamedActionsAcrossThemesAndDpi()
    {
        if (await Child(nameof(CompiledSettingsAndMaintenanceWrapScrollAndExposeNamedActionsAcrossThemesAndDpi))) return;
        await CalendarStaTest.RunAsync(() =>
        {
            _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var (theme, width, dpi) in new[] { ("Light", 760, 96), ("Dark", 760, 96), ("Light", 400, 240), ("Dark", 400, 240) })
            {
                var resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                System.Windows.Application.Current.Resources = resources;
                var f = new BackupUiFixture(); var vm = f.ViewModel(); vm.InspectCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                using var settings = new SettingsViewModel(new FakeThemeService(), new FakeDesktopSettingsStore(), NullLogger<SettingsViewModel>.Instance, dataBackups: vm);
                var view = new SettingsView { DataContext = settings };
                var root = new Border { Resources = resources, Child = view, Background = (Brush)resources["PtjBackgroundBrush"] };
                root.Measure(new Size(width, 680)); root.Arrange(new Rect(0, 0, width, 680)); root.UpdateLayout(); Pump();
                var panel = Descendants(view).OfType<DataBackupsView>().Single();
                Assert.Same(vm, panel.DataContext);
                Assert.Contains(Descendants(panel).OfType<TextBlock>(), b => b.Text.Contains("excludes AI API keys"));
                Assert.Contains(Descendants(panel).OfType<TextBlock>(), b => b.Text.Contains("cannot be restored as backups"));
                Assert.Equal(Visibility.Collapsed, ((Button)panel.FindName("CancelOperation")).Visibility);
                Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting((TextBlock)panel.FindName("OperationStatus")));
                Render(root, width, dpi, $"settings-{theme}-{width}-top");
                foreach (string name in new[] { "CreateBackup", "ExportData", "InspectBackup", "ConfirmRestore" })
                {
                    var button = (Button)panel.FindName(name); Assert.True(button.Focusable && button.IsEnabled);
                    Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
                    button.BringIntoView(); Pump(); root.UpdateLayout();
                    Point corner = button.TranslatePoint(new Point(button.ActualWidth, button.ActualHeight), root);
                    Assert.InRange(corner.X, 1, width + .1); Assert.InRange(corner.Y, 1, 680.1);
                    Assert.InRange(button.TranslatePoint(new Point(), root).X, 0, width);
                }
                var restore = (Button)panel.FindName("ConfirmRestore");
                Assert.Equal(((SolidColorBrush)resources["PtjDangerBrush"]).Color, ((SolidColorBrush)restore.Background).Color);
                Assert.True(((ScrollViewer)view.FindName("SettingsScroll")).ScrollableHeight > 0);
                Render(root, width, dpi, $"settings-{theme}-{width}-restore");

                vm.RestoreCommand.Execute(null); // Fake confirmation declines, leaving the preview intact.
                var confirmation = new PersonalTradingJournal.Desktop.Dialogs.PtjDialogWindow(f.Dialogs.ConfirmationRequest!);
                var confirmationContent = (FrameworkElement)confirmation.Content;
                confirmationContent.Measure(new Size(440, double.PositiveInfinity));
                Assert.InRange(confirmationContent.DesiredSize.Height, 100, 400); // Fits a 1080p work area at 240 DPI.
                confirmation.Close();

                var maintenance = new MaintenanceWindow { DataContext = new MaintenanceViewModel(f, _ => Task.CompletedTask, () => { }), Resources = resources };
                // Detached compiled content renders at the requested DIP/DPI, independent of monitor geometry.
                var content = (UIElement)maintenance.Content; maintenance.Content = null;
                var maintenanceRoot = new Border { Resources = resources, Child = content, DataContext = maintenance.DataContext, Background = (Brush)resources["PtjBackgroundBrush"] };
                maintenanceRoot.Measure(new Size(width, 680)); maintenanceRoot.Arrange(new Rect(0, 0, width, 680)); maintenanceRoot.UpdateLayout(); Pump();
                Assert.Contains(Descendants(maintenanceRoot).OfType<TextBlock>(), b => b.Text.Contains("Normal journal use is blocked"));
                foreach (var button in Descendants(maintenanceRoot).OfType<Button>().Where(b => b.Visibility == Visibility.Visible))
                    Assert.InRange(button.TranslatePoint(new Point(button.ActualWidth, 0), maintenanceRoot).X, 1, width + .1);
                Render(maintenanceRoot, width, dpi, $"maintenance-{theme}-{width}");
                maintenance.Close();
            }
        });
    }
    [Fact]
    public async Task RecoveryMarkerShowsRealMaintenanceStartupWithoutCreatingDatabaseOrNormalServices()
    {
        if (await Child(nameof(RecoveryMarkerShowsRealMaintenanceStartupWithoutCreatingDatabaseOrNormalServices))) return;
        string root = Path.Combine(Path.GetTempPath(), "ptj-startup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        string data = Path.Combine(root, "PersonalTradingJournal"); string marker = data + ".restore-state.json";
        File.WriteAllText(marker, "synthetic invalid recovery marker");
        try
        {
            await CalendarStaTest.RunAsync(() =>
            {
                var app = new App(["--isolated-data-root", root]) { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.InitializeComponent(); app.ShowMaintenanceWindow(); Pump();
                var window = Assert.IsType<MaintenanceWindow>(app.MainWindow);
                var vm = Assert.IsType<MaintenanceViewModel>(window.DataContext);
                Assert.False(vm.MayReopen); Assert.True(window.IsVisible); Assert.False(Directory.Exists(data));
                Assert.Equal("synthetic invalid recovery marker", File.ReadAllText(marker));
                window.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task MaintenanceWindowCloseVetoPersistsUntilSafeOperationCompletes()
    {
        if (await Child(nameof(MaintenanceWindowCloseVetoPersistsUntilSafeOperationCompletes))) return;
        await CalendarStaTest.RunAsync(() =>
        {
            var pending = new TaskCompletionSource<PersonalTradingJournal.Application.Backups.JournalRestoreResult>();
            var f = new BackupUiFixture { RestoreWork = _ => pending.Task };
            var vm = new MaintenanceViewModel(f, _ => Task.CompletedTask, () => { });
            var window = new MaintenanceWindow { Resources = CalendarViewLayoutTests.SharedThemeResources("Dark"), DataContext = vm };
            bool closed = false; window.Closed += (_, _) => closed = true;
            window.Show(); Task task = vm.RunAsync(new("archive", new string('a', 64))); window.Close();
            Assert.False(closed); Assert.True(window.IsVisible); Assert.Contains("Wait", vm.Status);
            pending.SetResult(f.RestoreResult); Pump(); Assert.True(task.IsCompletedSuccessfully);
            window.Close(); Assert.True(closed);
        });
    }
    private static async Task<bool> Child(string scenario)
    {
        string variable = "PTJ_DATA_BACKUPS_" + scenario;
        if (Environment.GetEnvironmentVariable(variable) == "1") return false;
        await IsolatedTestProcess.RunSuiteAsync(typeof(DataBackupsLayoutTests), "data-backups-layout", variable,
            TimeSpan.FromSeconds(90), testCaseFilter: $"FullyQualifiedName={typeof(DataBackupsLayoutTests).FullName}.{scenario}");
        return true;
    }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var node in Descendants(child)) yield return node; }
    }
    private static void Render(Visual root, int width, int dpi, string name)
    {
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 680 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(root);
        Assert.Equal(width * dpi / 96, bitmap.PixelWidth);
        if (Environment.GetEnvironmentVariable("PTJ_BACKUP_RENDER_DIRECTORY") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
        }
    }
}
