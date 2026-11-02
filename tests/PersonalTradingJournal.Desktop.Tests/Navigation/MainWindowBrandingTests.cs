using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    [Theory]
    [InlineData("Light", 96)]
    [InlineData("Dark", 96)]
    [InlineData("Light", 240)]
    [InlineData("Dark", 240)]
    public async Task SharedPageHeaderBrandingLoadsEmbeddedArtworkAndWrapsWithoutOverlap(string theme, int dpi)
    {
        if (Environment.GetEnvironmentVariable(HeaderHostVariable) != "1") { await HeaderHost.Value; return; }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            var fixture = CreateFixture();
            string originalDirectory = Environment.CurrentDirectory;
            string unrelatedDirectory = Path.Combine(Path.GetTempPath(), "ptj-branding-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(unrelatedDirectory);
            MainWindow? window = null;
            try
            {
                // A blank working directory prevents a loose source asset from satisfying the pack URI.
                Environment.CurrentDirectory = unrelatedDirectory;
                window = new MainWindow(fixture.Main);
                var root = (Grid)window.Content;
                VisualTreeHelper.SetRootDpi(root, new DpiScale(dpi / 96d, dpi / 96d));
                root.Measure(new(1000, 650)); root.Arrange(new(0, 0, 1000, 650)); root.UpdateLayout();
                var header = (Grid)window.FindName("SidebarBrandHeader");
                var surface = (Border)window.FindName("SidebarBrandSurface");
                var logo = (Image)window.FindName("SidebarBrandLogo");
                var title = (TextBlock)window.FindName("SidebarApplicationTitle");

                Assert.Equal(252, root.ColumnDefinitions[0].ActualWidth);
                Assert.Equal(72, logo.ActualWidth); Assert.Equal(72, logo.ActualHeight);
                Assert.Equal(new Thickness(20), surface.Padding);
                Assert.True(double.IsNaN(surface.Height));
                Assert.Equal(new Thickness(0, 0, 0, 1), surface.BorderThickness);
                Assert.Equal(((SolidColorBrush)app.Resources["PtjSidebarBrandBackgroundBrush"]).Color, ((SolidColorBrush)surface.Background).Color);
                Assert.Equal(((SolidColorBrush)app.Resources["PtjSidebarBrandDividerBrush"]).Color, ((SolidColorBrush)surface.BorderBrush).Color);
                Assert.NotEqual(((SolidColorBrush)app.Resources["PtjSidebarBrush"]).Color, ((SolidColorBrush)surface.Background).Color);
                Assert.True(Contrast(((SolidColorBrush)title.Foreground).Color, ((SolidColorBrush)surface.Background).Color) >= 4.5);
                Assert.Equal(Stretch.Uniform, logo.Stretch);
                Assert.Equal(BitmapScalingMode.HighQuality, RenderOptions.GetBitmapScalingMode(logo));
                Assert.False(logo.Focusable); Assert.False(logo.IsHitTestVisible);
                var peer = UIElementAutomationPeer.CreatePeerForElement(logo);
                Assert.NotNull(peer);
                Assert.False(peer.IsControlElement()); Assert.False(peer.IsContentElement());
                Assert.Equal(fixture.Main.ApplicationTitle, title.Text);
                Assert.Equal("ApplicationTitle", BindingOperations.GetBinding(title, TextBlock.TextProperty)?.Path.Path);
                Assert.Equal(TextWrapping.Wrap, title.TextWrapping);
                Assert.Equal(((SolidColorBrush)app.Resources["PtjSidebarBrandTextBrush"]).Color, ((SolidColorBrush)title.Foreground).Color);
                double logoRight = logo.TranslatePoint(new(logo.ActualWidth, 0), header).X;
                double titleLeft = title.TranslatePoint(new(), header).X;
                Assert.InRange(titleLeft - logoRight, 11.5, 12.5);
                Assert.InRange(titleLeft + title.ActualWidth, 0, header.ActualWidth + 0.5);
                var natural = new TextBlock { Text = title.Text, FontSize = title.FontSize,
                    FontFamily = title.FontFamily, FontWeight = title.FontWeight, TextWrapping = title.TextWrapping,
                    UseLayoutRounding = title.UseLayoutRounding, Language = title.Language };
                TextOptions.SetTextFormattingMode(natural, TextOptions.GetTextFormattingMode(title));
                VisualTreeHelper.SetRootDpi(natural, new DpiScale(dpi / 96d, dpi / 96d));
                natural.Measure(new(title.ActualWidth, double.PositiveInfinity));
                Assert.InRange(title.ActualHeight - natural.DesiredSize.Height, -1, 1);
                natural.Text = "Personal Trading\nJournal";
                natural.Measure(new(title.ActualWidth, double.PositiveInfinity));
                Assert.InRange(title.ActualHeight - natural.DesiredSize.Height, -1, 1);
                Assert.True(title.ActualHeight > title.FontSize * 2, "The full application name must wrap naturally.");
                Assert.InRange(title.TranslatePoint(new(0, title.ActualHeight), header).Y, 0, header.ActualHeight + 0.5);
                Assert.NotNull(window.Icon);
                Assert.IsAssignableFrom<BitmapSource>(logo.Source);

                VerifyEmbeddedAsset("ptj-logo-n26.png", "52511600342FB22C4C6282055899BC532B3B781D30AE569621415E83E63CC4EE", [1254]);
                VerifyEmbeddedAsset("ptj-app-n26.ico", "964C1CA092D69D60B6DF1445D22CB568F952A298179B3BA0B3868A5568B859D3", [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]);

                if (Environment.GetEnvironmentVariable("PTJ_HEADER_RENDER_DIRECTORY") is { Length: > 0 } directory)
                {
                    Directory.CreateDirectory(directory);
                    var sidebar = (FrameworkElement)root.Children[0];
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(sidebar.ActualWidth * dpi / 96d),
                        (int)Math.Ceiling(sidebar.ActualHeight * dpi / 96d), dpi, dpi, PixelFormats.Pbgra32);
                    bitmap.Render(sidebar);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var output = File.Create(Path.Combine(directory, $"branding-{theme}-{dpi}.png"))) encoder.Save(output);
                }

                double normalHeight = surface.ActualHeight;
                title.DataContext = new { ApplicationTitle = "Personal Trading Journal — detailed trading preparation, execution analysis, performance review and long-term learning notebook" };
                root.Measure(new(1000, 650)); root.Arrange(new(0, 0, 1000, 650)); root.UpdateLayout();
                Assert.True(surface.ActualHeight > normalHeight, "Long titles must expand the automatic header height.");
                natural.Text = title.Text;
                natural.Measure(new(title.ActualWidth, double.PositiveInfinity));
                Assert.InRange(title.ActualHeight - natural.DesiredSize.Height, -1, 1);
                var sidebarGrid = (Grid)surface.Parent;
                var navigation = sidebarGrid.Children.OfType<ScrollViewer>().Single();
                Assert.True(navigation.TranslatePoint(new(), sidebarGrid).Y >= surface.ActualHeight);
                Assert.InRange(title.TranslatePoint(new(0, title.ActualHeight), header).Y, 0, header.ActualHeight + 0.5);

                app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme == "Light" ? "Dark" : "Light");
                root.UpdateLayout();
                Assert.Equal(((SolidColorBrush)app.Resources["PtjSidebarBrandTextBrush"]).Color, ((SolidColorBrush)title.Foreground).Color);
                Assert.Equal(((SolidColorBrush)app.Resources["PtjSidebarBrandBackgroundBrush"]).Color, ((SolidColorBrush)surface.Background).Color);
                Assert.Equal(((SolidColorBrush)app.Resources["PtjSidebarBrandDividerBrush"]).Color, ((SolidColorBrush)surface.BorderBrush).Color);
                Assert.True(Contrast(((SolidColorBrush)title.Foreground).Color, ((SolidColorBrush)surface.Background).Color) >= 4.5);
            }
            finally
            {
                if (window is not null) { window.Content = null; window.Close(); }
                fixture.Main.Dispose();
                Environment.CurrentDirectory = originalDirectory;
                Directory.Delete(unrelatedDirectory);
            }
        }, shutdownDispatcher: false);
    }

    private static double Contrast(Color foreground, Color background)
    {
        static double L(Color color)
        {
            static double Channel(byte value) { double c = value / 255d; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
            return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        }
        double a = L(foreground), b = L(background);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    private static void VerifyEmbeddedAsset(string filename, string sha256, int[] sizes)
    {
        var uri = new Uri($"pack://application:,,,/PersonalTradingJournal.Desktop;component/Resources/Branding/{filename}");
        using var resource = System.Windows.Application.GetResourceStream(uri).Stream;
        using var bytes = new MemoryStream(); resource.CopyTo(bytes);
        Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(bytes.ToArray())));
        bytes.Position = 0;
        var decoder = BitmapDecoder.Create(bytes, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        Assert.Equal(sizes, decoder.Frames.Select(frame => frame.PixelWidth));
        foreach (var frame in decoder.Frames)
        {
            Assert.Equal(frame.PixelWidth, frame.PixelHeight);
            var rgba = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            byte[] pixels = new byte[rgba.PixelWidth * rgba.PixelHeight * 4];
            rgba.CopyPixels(pixels, rgba.PixelWidth * 4, 0);
            Assert.Contains(Enumerable.Range(0, rgba.PixelWidth * rgba.PixelHeight), pixel => pixels[pixel * 4 + 3] == 0);
        }
    }
}
