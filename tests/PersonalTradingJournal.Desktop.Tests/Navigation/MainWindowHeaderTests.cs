using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    private const string HeaderHostVariable = "PTJ_SHELL_HEADER_HOST";
    private static readonly Lazy<Task> HeaderHost = new(() => IsolatedTestProcess.RunSuiteAsync(
        typeof(MainWindowViewModelTests), "shell-header", HeaderHostVariable, TimeSpan.FromSeconds(90),
        testCaseFilter: $"FullyQualifiedName~{typeof(MainWindowViewModelTests).FullName}.SharedPageHeader",
        caseHangTimeout: TimeSpan.FromSeconds(30)));

    [Theory]
    [InlineData("Light", 1280, 96, false)]
    [InlineData("Dark", 1280, 96, false)]
    [InlineData("Light", 1600, 96, false)]
    [InlineData("Dark", 1600, 96, false)]
    [InlineData("Light", 1000, 240, false)]
    [InlineData("Dark", 1000, 240, false)]
    [InlineData("Light", 1000, 240, true)]
    [InlineData("Dark", 1000, 240, true)]
    public async Task SharedPageHeaderCentersIndependentlyOfToggleAndWrapsWithoutClipping(
        string theme, int width, int dpi, bool longTitle)
    {
        if (Environment.GetEnvironmentVariable(HeaderHostVariable) != "1") { await HeaderHost.Value; return; }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            var fixture = CreateFixture();
            var window = new MainWindow(fixture.Main);
            try
            {
                // Compiled shell with fake services, no App startup, DB, native activation or screen-size assumptions.
                var root = (FrameworkElement)window.Content;
                VisualTreeHelper.SetRootDpi(root, new DpiScale(dpi / 96d, dpi / 96d));
                var title = (TextBlock)window.FindName("PageTitleText");
                var treatment = (Border)window.FindName("PageTitleTreatment");
                var header = (Border)window.FindName("PageHeader");
                var content = (Grid)window.FindName("PageContentArea");
                var toggle = (ToggleButton)window.FindName("PageThemeToggle");
                if (longTitle)
                    title.DataContext = new { PageTitle = "Daily Review — historical account evidence and saved coaching analyses" };
                Layout();
                CheckBounds();
                Assert.InRange(header.ActualHeight, 60, longTitle ? 180 : 90);
                Assert.Equal(AutomationHeadingLevel.Level1, AutomationProperties.GetHeadingLevel(title));
                Assert.Equal(title.Text, new TextBlockAutomationPeer(title).GetName());
                Assert.Equal(TextWrapping.Wrap, title.TextWrapping);
                Assert.Equal(TextAlignment.Center, title.TextAlignment);
                Assert.True(title.FontSize >= 24);
                var natural = new TextBlock { Text = title.Text, FontSize = title.FontSize,
                    FontFamily = title.FontFamily, FontWeight = title.FontWeight, TextWrapping = title.TextWrapping,
                    UseLayoutRounding = title.UseLayoutRounding, Language = title.Language };
                TextOptions.SetTextFormattingMode(natural, TextOptions.GetTextFormattingMode(title));
                VisualTreeHelper.SetRootDpi(natural, new DpiScale(dpi / 96d, dpi / 96d));
                natural.Measure(new(title.ActualWidth, double.PositiveInfinity));
                Assert.InRange(title.ActualHeight - natural.DesiredSize.Height, -1, 1);
                if (longTitle) Assert.True(title.ActualHeight > 40, "Long title must wrap, not clip or shrink.");
                Assert.Equal(((SolidColorBrush)app.Resources["PtjAccentBrush"]).Color, ((SolidColorBrush)title.Foreground).Color);
                Assert.Equal(((SolidColorBrush)app.Resources["PtjPageHeaderSurfaceBrush"]).Color, ((SolidColorBrush)treatment.Background).Color);
                Assert.True(toggle.IsEnabled && toggle.Focusable);
                var togglePeer = new ToggleButtonAutomationPeer(toggle);
                Assert.Equal(fixture.Main.ThemeToggleToolTip, togglePeer.GetName());
                bool wasLight = fixture.Main.IsLightTheme;
                Assert.Same(fixture.Main.ToggleThemeCommand, toggle.Command);
                Assert.True(toggle.Command.CanExecute(null));
                toggle.Command.Execute(null);
                Assert.NotEqual(wasLight, fixture.Main.IsLightTheme);
                SaveRender(root, theme, width, dpi, longTitle);

                // A larger control must not move the centered title or collide with its wrapping.
                toggle.Width = 100;
                Layout();
                CheckBounds();
                // Shared DynamicResources must also follow a theme change in the existing shell.
                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme == "Light" ? "Dark" : "Light");
                Layout();
                Assert.Equal(((SolidColorBrush)app.Resources["PtjAccentBrush"]).Color, ((SolidColorBrush)title.Foreground).Color);
                Assert.Equal(((SolidColorBrush)app.Resources["PtjPageHeaderSurfaceBrush"]).Color, ((SolidColorBrush)treatment.Background).Color);

                void Layout()
                {
                    root.Measure(new(width, 650)); root.Arrange(new(0, 0, width, 650)); root.UpdateLayout();
                }
                void CheckBounds()
                {
                    double center = treatment.TranslatePoint(new(treatment.ActualWidth / 2, 0), content).X;
                    Assert.InRange(Math.Abs(center - content.ActualWidth / 2), 0, 0.6);
                    double textCenter = title.TranslatePoint(new(title.ActualWidth / 2, 0), content).X;
                    Assert.InRange(Math.Abs(textCenter - content.ActualWidth / 2), 0, 0.6);
                    double titleRight = treatment.TranslatePoint(new(treatment.ActualWidth, 0), content).X;
                    double toggleLeft = toggle.TranslatePoint(new(), content).X;
                    Assert.True(titleRight + 10 <= toggleLeft, $"Title right {titleRight}; toggle left {toggleLeft}");
                    Assert.InRange(toggle.TranslatePoint(new(toggle.ActualWidth, 0), content).X, 0, content.ActualWidth - 30);
                    Assert.InRange(treatment.TranslatePoint(new(), content).X, 32, content.ActualWidth);
                    Assert.True(root.DesiredSize.Width <= width, "Header must not create horizontal overflow.");
                }
            }
            finally { window.Content = null; window.Close(); fixture.Main.Dispose(); }
        }, shutdownDispatcher: false);
    }

    [Fact]
    public async Task SharedPageHeaderUsesTheSameAccessibleTitleForEveryDestination()
    {
        if (Environment.GetEnvironmentVariable(HeaderHostVariable) != "1") { await HeaderHost.Value; return; }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources("Light");
            var fixture = CreateFixture();
            var window = new MainWindow(fixture.Main);
            try
            {
                var root = (FrameworkElement)window.Content;
                var title = (TextBlock)window.FindName("PageTitleText");
                foreach (var destination in Enum.GetValues<NavigationDestination>())
                {
                    fixture.Main.NavigateCommand.Execute(destination);
                    root.Measure(new(1000, 650)); root.Arrange(new(0, 0, 1000, 650)); root.UpdateLayout();
                    Assert.Equal(fixture.Main.PageTitle, title.Text);
                    Assert.False(string.IsNullOrWhiteSpace(title.Text));
                    Assert.Equal(AutomationHeadingLevel.Level1, AutomationProperties.GetHeadingLevel(title));
                }
            }
            finally { window.Content = null; window.Close(); fixture.Main.Dispose(); }
        }, shutdownDispatcher: false);
    }

    private static void SaveRender(FrameworkElement root, string theme, int width, int dpi, bool longTitle)
    {
        if (Environment.GetEnvironmentVariable("PTJ_HEADER_RENDER_DIRECTORY") is not { Length: > 0 } path) return;
        Directory.CreateDirectory(path);
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 650 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(path, $"header-{theme}-{width}-{dpi}-{(longTitle ? "long" : "dashboard")}.png"));
        encoder.Save(file);
    }
}
