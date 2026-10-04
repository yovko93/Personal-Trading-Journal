using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.Views.Calendar;
using PersonalTradingJournal.Desktop.Views.Dashboard;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed class CalendarDayPerformanceChartTests
{
    [Theory]
    [InlineData("Light", 9, 9, "05:10:34", "UTC-4", 96)]
    [InlineData("Dark", 9, 9, "05:10:34", "UTC-4", 240)]
    [InlineData("Light", 12, 10, "05:10:34", "UTC-5", 240)]
    [InlineData("Dark", 12, 10, "05:10:34", "UTC-5", 96)]
    [InlineData("Light", 9, 8, "04:00:00", "UTC-4", 96)]
    [InlineData("Dark", 12, 9, "04:00:00", "UTC-5", 240)]
    public async Task AxisShowsClockSecondsWhilePointDetailsRetainExplicitOffset(string theme, int month,
        int utcHour, string expected, string offset, int dpi)
    {
        await CalendarDayModalTests.OnSta(() =>
        {
            var instant = new DateTimeOffset(2026, month, 5, utcHour,
                expected == "04:00:00" ? 0 : 10, expected == "04:00:00" ? 0 : 34, TimeSpan.Zero).AddTicks(1234567);
            var point = new CalendarDayPerformancePoint(instant, 100m, false, 2, "USD");
            var chart = Chart(theme, 960, new("USD", [point]));
            Draw(chart, dpi);
            Assert.Equal(expected, point.TimeText);
            string[] labels = Descendants(chart).OfType<TextBlock>().Select(t => t.Text).ToArray();
            Assert.Contains(expected, labels);
            Assert.DoesNotContain(labels, text => text.Contains("UTC", StringComparison.Ordinal) || text.Contains("-04:00", StringComparison.Ordinal) || text.Contains("-05:00", StringComparison.Ordinal));
            var target = Assert.Single(Targets(chart));
            Assert.Equal(instant, ((CalendarDayPerformancePoint)target.Tag).ClosedAtUtc);
            Assert.Contains($"{expected}.1234567 {offset} New York", AutomationProperties.GetName(target));
            Assert.Equal(point.Description, ((ToolTip)Assert.Single(Regions(chart)).ToolTip).Content);
            Assert.Equal(100m, point.Value);
            Assert.Equal(2, point.TradeCount);
            Render(chart, $"{theme}-clock-{month}-{utcHour}", 960, dpi);
        });
    }

    [Theory]
    [InlineData("Light", 960, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 960, 96)]
    [InlineData("Dark", 480, 240)]
    public async Task FullHeightRegionsHandOffOneGuideAndRetainKeyboardFocusWithSubtleThemeDrawing(string theme, int width, int dpi)
    {
        await CalendarDayModalTests.OnSta(() =>
        {
            var chart = Chart(theme, width, new("USD", [Point(0, 100), Point(1, -40), Point(30, 0), Point(60, 50)]));
            var next = new Button { Content = "After chart" };
            var root = new StackPanel(); root.Children.Add(chart); root.Children.Add(next);
            var window = new Window { Content = root, Width = width + 40, Height = 340, ShowInTaskbar = false,
                Background = chart.Background,
                // This case scripts routed hover and real keyboard focus. Do not let
                // the desktop cursor introduce an unrelated MouseEnter during a pump.
                // VisualTreeHelper.HitTest below still checks the actual region geometry.
                IsHitTestVisible = false };
            try
            {
                window.Show(); Pump(); Draw(chart, dpi);
                Assert.True(next.Focus()); Pump();
                Border[] targets = Targets(chart), regions = Regions(chart);
                Line guide = Guide(chart);
                Assert.False(chart.IsHitTestVisible);
                Assert.False(chart.IsMouseOver || chart.IsKeyboardFocusWithin);
                Assert.Equal(4, regions.Length);
                Assert.Equal(Visibility.Collapsed, guide.Visibility);
                var plot = (Canvas)VisualTreeHelper.GetParent(regions[0]);
                double bottom = guide.Y2;
                for (int i = 0; i < regions.Length; i++)
                {
                    Border region = regions[i];
                    Assert.Equal(16, Canvas.GetTop(region));
                    Assert.Equal(bottom - 16, region.Height);
                    Assert.False(region.Focusable || KeyboardNavigation.GetIsTabStop(region));
                    Assert.Equal(100, ToolTipService.GetInitialShowDelay(region));
                    Assert.False(((ToolTip)region.ToolTip).IsHitTestVisible);
                    Assert.Same(targets[i], region.DataContext);
                    double x = Center(targets[i]);
                    foreach (double y in new[] { 17d, bottom - 1 })
                        Assert.Same(region, HitRegion(plot, x, y));
                    if (i == 0) Assert.Equal(0, Canvas.GetLeft(region));
                    else
                    {
                        double boundary = (Center(targets[i - 1]) + x) / 2;
                        Assert.Equal(boundary, Canvas.GetLeft(region), 6);
                        Assert.Equal(boundary, Canvas.GetLeft(regions[i - 1]) + regions[i - 1].Width, 6);
                        Assert.Same(regions[i - 1], HitRegion(plot, boundary - .01, bottom / 2));
                        Assert.Same(region, HitRegion(plot, boundary + .01, bottom / 2));
                    }
                }
                Enter(regions[1]);
                AssertGuideAt(chart, targets[1]);
                Assert.Equal(9, Marker(targets[1]).Width);
                Assert.Equal(6, Marker(targets[0]).Width);
                Move(regions[1]);
                ToolTip hover = (ToolTip)regions[1].ToolTip;
                Assert.Equal(PlacementMode.Relative, hover.Placement);
                Assert.Same(window, hover.PlacementTarget);
                Assert.Equal(((CalendarDayPerformancePoint)targets[1].Tag).Description, hover.Content);
                Assert.Contains("-40 USD", (string)hover.Content);
                hover.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.True(hover.DesiredSize.Width <= 310); // Exact prose wraps inside a narrow modal.
                Enter(regions[2]); // Zero is a genuine point with its own region/guide.
                Leave(regions[1]); // A delayed leave from the previous region must not clear the new guide.
                AssertGuideAt(chart, targets[2]);
                Assert.Equal(6, Marker(targets[1]).Width);
                Assert.Equal(9, Marker(targets[2]).Width);
                Leave(regions[2]);
                Assert.Equal(Visibility.Collapsed, guide.Visibility);

                Assert.True(targets[0].Focus()); Pump();
                AssertGuideAt(chart, targets[0]);
                var keyboard = (ToolTip)targets[0].ToolTip;
                Assert.True(keyboard.IsOpen);
                Assert.Equal(PlacementMode.Bottom, keyboard.Placement);
                Assert.Same(targets[0], keyboard.PlacementTarget);
                Render(chart, theme, width, dpi);
                chart.Width = width - 40; Draw(chart, dpi);
                targets = Targets(chart); regions = Regions(chart); guide = Guide(chart);
                Assert.True(targets[0].IsKeyboardFocused);
                AssertGuideAt(chart, targets[0]);
                Assert.False(keyboard.IsOpen);
                keyboard = (ToolTip)targets[0].ToolTip;
                Assert.True(keyboard.IsOpen);
                Enter(regions[3]); AssertGuideAt(chart, targets[3]);
                plot.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseLeaveEvent });
                AssertGuideAt(chart, targets[0]); // Pointer leaving preserves the keyboard-owned point.
                Assert.True(next.Focus()); Pump();
                Assert.Equal(Visibility.Collapsed, guide.Visibility);
                Assert.False(keyboard.IsOpen);

                var steps = Descendants(chart).OfType<Line>().Where(l => Equals(l.Tag, "ClosureStep")).ToArray();
                Assert.All(steps, line => { Assert.Equal(1.25, line.StrokeThickness); Assert.Equal(1, line.Opacity); });
                var fills = Descendants(chart).OfType<Rectangle>().Where(r => Equals(r.Tag, "ClosureFill")).ToArray();
                Assert.Equal(2, fills.Length); // Prior +100 and -40 only; zero has no invented area.
                Assert.Equal(new[] { chart.PositiveBrush, chart.NegativeBrush }, fills.Select(r => r.Fill));
                Assert.All(fills, fill => { Assert.Equal(.08, fill.Opacity); Assert.False(fill.IsHitTestVisible); });
                Assert.All(steps, line => Assert.True(Contrast((SolidColorBrush)line.Stroke, (SolidColorBrush)chart.Background) >= 3));
                Assert.All(Descendants(chart).OfType<TextBlock>().Where(t => !string.IsNullOrEmpty(t.Text)),
                    label => Assert.True(Contrast((SolidColorBrush)label.Foreground, (SolidColorBrush)chart.Background) >= 4.5));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task GuideAndRegionsStayInPlotCoordinatesAcrossScrollResizeAndScopeChanges()
    {
        await CalendarDayModalTests.OnSta(() =>
        {
            var chart = Chart("Dark", 360, new("USD", Enumerable.Range(0, 12).Select(i => Point(i, i % 2 == 0 ? i : -i)).ToArray()));
            Draw(chart, 240);
            var scroll = Descendants(chart).OfType<ScrollViewer>().Single();
            scroll.ScrollToHorizontalOffset(150); scroll.UpdateLayout();
            Assert.True(scroll.HorizontalOffset > 0);
            Border point = Targets(chart)[8], region = Regions(chart)[8];
            Enter(region); AssertGuideAt(chart, point);
            Line guide = Guide(chart);
            Assert.Equal(point.TranslatePoint(new(point.Width / 2, 0), scroll).X,
                guide.TranslatePoint(new(guide.X1, 0), scroll).X, 6);
            Point position = region.TranslatePoint(new(region.Width / 2, 2), scroll);
            Assert.Same(region, FindRegion(VisualTreeHelper.HitTest(scroll, position)?.VisualHit));
            double originalX = guide.X1;
            chart.Width = 960; Draw(chart, 240);
            Border replacement = Targets(chart)[8];
            AssertGuideAt(chart, replacement);
            Assert.NotEqual(originalX, Guide(chart).X1);
            Assert.False(((ToolTip)region.ToolTip).IsOpen);
            Enter(Regions(chart)[8]);
            chart.Series = new("EUR", [Point(0, -7, "EUR")]); Draw(chart);
            Assert.Equal(0, scroll.HorizontalOffset);
            Assert.Equal(Visibility.Collapsed, Guide(chart).Visibility);
            Assert.Contains("EUR", (string)((ToolTip)Regions(chart).Single().ToolTip).Content);
            Assert.DoesNotContain("USD", (string)((ToolTip)Regions(chart).Single().ToolTip).Content);
            chart.Series = new("EUR", []); Draw(chart);
            Assert.Empty(Regions(chart)); Assert.Empty(Targets(chart));
            Assert.DoesNotContain(Descendants(chart).OfType<Line>(), l => Equals(l.Tag, "ActivePointGuide"));
        });
    }

    [Fact]
    public async Task CoincidentClosuresShareOneTargetPerCurrencyAndChartsDoNotShareActiveState()
    {
        DateOnly date = new(2026, 9, 5);
        var a = CalendarDayDetailsTests.Row(date, 30, 30) with
            { ClosedAtUtc = new DateTimeOffset(2026, 9, 5, 16, 0, 0, TimeSpan.Zero).AddTicks(1234567) };
        var b = a with { Id = Guid.NewGuid(), GrossPnL = -10, NetPnL = -10 };
        var euro = a with { Id = Guid.NewGuid(), Currency = "EUR", GrossPnL = 7, NetPnL = 7 };
        var series = CalendarDayPerformance.From([a, euro, b]);
        await CalendarDayModalTests.OnSta(() =>
        {
            var usd = Chart("Light", 700, series.Single(s => s.Currency == "USD"));
            var eur = Chart("Light", 700, series.Single(s => s.Currency == "EUR"));
            Draw(usd); Draw(eur);
            Border target = Assert.Single(Targets(usd)), region = Assert.Single(Regions(usd));
            Assert.Equal(2, ((CalendarDayPerformancePoint)target.Tag).TradeCount);
            Assert.Equal(20m, ((CalendarDayPerformancePoint)target.Tag).Value);
            Assert.Contains("20 USD", (string)((ToolTip)region.ToolTip).Content);
            Assert.Contains("12:00:00.1234567 UTC-4 New York", (string)((ToolTip)region.ToolTip).Content);
            Enter(region); AssertGuideAt(usd, target);
            Assert.Equal(Visibility.Collapsed, Guide(eur).Visibility);
            Leave(region);
            Enter(Assert.Single(Regions(eur))); AssertGuideAt(eur, Targets(eur).Single());
            Assert.Contains("7 EUR", (string)((ToolTip)Regions(eur).Single().ToolTip).Content);
            Assert.Equal(Visibility.Collapsed, Guide(usd).Visibility);
        });
    }

    [Fact]
    public async Task ZeroHasGuideButUnavailableClosureDoesNotInventPointOrFillAcrossGap()
    {
        await CalendarDayModalTests.OnSta(() =>
        {
            var chart = Chart("Light", 600, new("USD", [Point(0, 0), Point(1, null), Point(2, null)]));
            Draw(chart);
            Enter(Regions(chart)[0]); AssertGuideAt(chart, Targets(chart)[0]);
            Enter(Regions(chart)[1]);
            Assert.Equal(Visibility.Collapsed, Guide(chart).Visibility);
            Assert.Contains("Unavailable USD", (string)((ToolTip)Regions(chart)[1].ToolTip).Content);
            Assert.DoesNotContain(Descendants(chart).OfType<Rectangle>(), r => Equals(r.Tag, "ClosureFill"));
            Assert.DoesNotContain(Descendants(chart).OfType<Line>(), l => Equals(l.Tag, "ClosureStep"));
            Assert.Empty(Descendants(Targets(chart)[1]).OfType<Ellipse>());
        });
    }

    private static CalendarDayPerformancePoint Point(int minute, decimal? value, string currency = "USD") =>
        new(new DateTimeOffset(2026, 9, 5, 15, 0, 0, TimeSpan.Zero).AddMinutes(minute), value, false, 1, currency);
    private static DayPerformanceChart Chart(string theme, int width, CalendarDayPerformance series)
    {
        var resources = CalendarViewLayoutTests.SharedThemeResources(theme);
        return new() { Width = width, Height = 260, Resources = resources, Series = series,
            Background = (Brush)resources["PtjSurfaceBrush"], PositiveBrush = (Brush)resources["PtjSuccessBrush"],
            NegativeBrush = (Brush)resources["PtjDangerBrush"], NeutralBrush = (Brush)resources["PtjTextMutedBrush"] };
    }
    private static void Draw(FrameworkElement chart, int dpi = 96)
    {
        chart.Measure(new(chart.Width, chart.Height)); chart.Arrange(new(0, 0, chart.Width, chart.Height)); chart.UpdateLayout();
        new RenderTargetBitmap((int)chart.Width * dpi / 96, (int)chart.Height * dpi / 96, dpi, dpi, PixelFormats.Pbgra32).Render(chart);
    }
    private static void Render(DayPerformanceChart chart, string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable("PTJ_CALENDAR_RENDER_DIRECTORY") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 260 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(chart);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(System.IO.Path.Combine(directory, $"calendar-day-chart-{theme}-{width}-{dpi}.png")); encoder.Save(file);
    }
    private static double Contrast(SolidColorBrush foreground, SolidColorBrush background)
    {
        static double Luminance(Color c)
        {
            static double Linear(byte n) { double v = n / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
            return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        }
        double a = Luminance(foreground.Color), b = Luminance(background.Color);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
    private static Border[] Targets(DependencyObject chart) => Descendants(chart).OfType<Border>().Where(b => b.Tag is CalendarDayPerformancePoint).ToArray();
    private static Border[] Regions(DependencyObject chart) => Descendants(chart).OfType<Border>().Where(b => Equals(b.Tag, "DayPerformanceHoverRegion")).ToArray();
    private static Line Guide(DependencyObject chart) => Descendants(chart).OfType<Line>().Single(l => Equals(l.Tag, "ActivePointGuide"));
    private static Ellipse Marker(Border target) => Descendants(target).OfType<Ellipse>().Single();
    private static double Center(Border target) => Canvas.GetLeft(target) + target.Width / 2;
    private static void AssertGuideAt(DayPerformanceChart chart, Border target)
    {
        Line guide = Guide(chart); Assert.Equal(Visibility.Visible, guide.Visibility);
        Assert.Equal(Center(target), guide.X1, 6); Assert.Equal(guide.X1, guide.X2);
        Assert.Equal(16, guide.Y1); Assert.Equal(chart.Height - 52, guide.Y2);
    }
    private static void Enter(UIElement target) => target.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
    private static void Leave(UIElement target) => target.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseLeaveEvent });
    private static void Move(UIElement target) => target.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseMoveEvent });
    private static Border? HitRegion(Canvas plot, double x, double y) => FindRegion(VisualTreeHelper.HitTest(plot, new(x, y))?.VisualHit);
    private static Border? FindRegion(DependencyObject? hit)
    {
        while (hit is not null) { if (hit is Border b && Equals(b.Tag, "DayPerformanceHoverRegion")) return b; hit = VisualTreeHelper.GetParent(hit); }
        return null;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static void Pump()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
            new Action(() => frame.Continue = false)); System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
}
