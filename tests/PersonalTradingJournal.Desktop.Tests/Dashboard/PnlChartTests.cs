using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Desktop.Views.Dashboard;

namespace PersonalTradingJournal.Desktop.Tests.Dashboard;

public sealed class PnlChartTests
{
    [Theory]
    [InlineData(10, -30, 25)]
    [InlineData(-30, 10, 75)]
    [InlineData(20, -20, 50)]
    public void CrossingSplitsAtInterpolatedZero(int first, int last, double crossingX)
    {
        var segments = PnlChart.SplitSegment(new(0, 50 - first), first, new(100, 50 - last), last, 50);
        Assert.Equal(2, segments.Count);
        Assert.Equal(new Point(crossingX, 50), segments[0].End);
        Assert.Equal(segments[0].End, segments[1].Start);
        Assert.Equal(Math.Sign(first), segments[0].Sign);
        Assert.Equal(Math.Sign(last), segments[1].Sign);
        Assert.Equal(new Point(0, 50 - first), segments[0].Start);
        Assert.Equal(new Point(100, 50 - last), segments[1].End);
    }

    [Theory]
    [InlineData(10, 20, 1)]
    [InlineData(-10, -20, -1)]
    [InlineData(0, 10, 1)]
    [InlineData(-10, 0, -1)]
    [InlineData(0, 0, 0)]
    public void SameSideAndZeroEndpointsDoNotInventCrossings(int first, int last, int sign)
    {
        var segment = Assert.Single(PnlChart.SplitSegment(new(0, 50 - first), first, new(100, 50 - last), last, 50));
        Assert.Equal(sign, segment.Sign);
    }

    [Fact]
    public void ExtremeDecimalsDoNotOverflowRenderingInterpolation()
    {
        var segments = PnlChart.SplitSegment(new(0, 0), decimal.MaxValue, new(100, 100), decimal.MinValue, 50);
        Assert.Equal(new Point(50, 50), segments[0].End);
    }

    [Theory]
    [InlineData("Light", 96)]
    [InlineData("Light", 240)]
    [InlineData("Dark", 96)]
    [InlineData("Dark", 240)]
    public async Task ActualDrawingUsesDatedAxesThemeSignBrushesAndLeavesUnavailableGaps(string theme, int dpi)
    {
        await OnSta(() =>
        {
            PnlChart chart = Chart(theme, [Row(10), Row(-30, 2), Row(0, 3), Row(null, 4)]);
            chart.StartDate = new DateOnly(2026, 8, 31);
            Draw(chart, dpi);
            var lines = Segments(chart);
            Assert.Equal(4, lines.Length); // origin→10; 10→0→-30; -30→0
            Assert.Equal(new[] { Color(chart.PositiveBrush), Color(chart.PositiveBrush), Color(chart.NegativeBrush), Color(chart.NegativeBrush) }, lines.Select(d => Color(d.Stroke)));
            Assert.Equal(lines[1].X2, lines[2].X1);
            Assert.Equal(lines[1].Y2, lines[2].Y1);
            var zero = Assert.Single(Descendants(chart).OfType<Line>(), line => (string?)line.Tag == "ZeroBaseline");
            Assert.Equal(zero.Y1, lines[1].Y2, 8);
            Assert.Equal(lines[1].X1 + (lines[2].X2 - lines[1].X1) * .25, lines[1].X2, 8);
            Assert.Single(Descendants(chart).OfType<Line>(), line => (string?)line.Tag == "ValueAxis");
            Assert.Single(Descendants(chart).OfType<Line>(), line => (string?)line.Tag == "DateAxis");
            Assert.Equal(new[] { "2026-08-31\nStart", "2026-09-01", "2026-09-02", "2026-09-03", "2026-09-04" },
                Descendants(chart).OfType<TextBlock>().Where(t => (string?)t.Tag == "DateTick").Select(t => t.Text));
            Assert.All(Descendants(chart).OfType<TextBlock>().Where(t => (string?)t.Tag == "ValueTick"),
                tick => { Assert.Contains("USD", tick.Text); Assert.Equal(11, tick.FontSize); });
            Border[] targets = Targets(chart);
            Assert.Equal(5, targets.Length);
            Assert.Contains("Period start\nDate: 2026-08-31", (string)((ToolTip)targets[0].ToolTip).Content);
            Assert.Equal("Cumulative Realized P&L\nDate: 2026-09-02\nCumulative P&L: -30.00 USD", ((ToolTip)targets[2].ToolTip).Content);
            Assert.Contains("0.00 USD", (string)((ToolTip)targets[3].ToolTip).Content);
            Assert.Contains("Unavailable", (string)((ToolTip)targets[4].ToolTip).Content);
            Assert.All(targets, target =>
            {
                Assert.True(target.Focusable && KeyboardNavigation.GetIsTabStop(target));
                Assert.True(target.ActualWidth >= 40 && target.ActualHeight >= 24);
                Assert.NotNull(VisualTreeHelper.HitTest(target, new Point(20, 12)));
                Assert.Equal(((ToolTip)target.ToolTip).Content, AutomationProperties.GetName(target));
                Assert.Equal(100, ToolTipService.GetInitialShowDelay(target));
            });
            Assert.Empty(Descendants(targets[4]).OfType<Ellipse>());
            Assert.Equal(Color(chart.NeutralBrush), Color(Assert.Single(Descendants(targets[3]).OfType<Ellipse>()).Fill));

            chart.Points = [Row(0), Row(0, 2)];
            Draw(chart, dpi);
            Assert.All(Segments(chart), line => Assert.Equal(Color(chart.NeutralBrush), Color(line.Stroke)));
            chart.Points = [Row(10), Row(null, 2), Row(-10, 3)];
            Draw(chart, dpi);
            Assert.Single(Segments(chart));
            chart.Points = [Row(null)];
            Draw(chart, dpi);
            Assert.Empty(Segments(chart));
            Assert.Contains(Descendants(chart).OfType<TextBlock>(), t => t.Text == "Cumulative P&L values unavailable.");
            chart.Points = [];
            Draw(chart, dpi);
            Assert.Empty(Targets(chart));
            Assert.Contains(Descendants(chart).OfType<TextBlock>(), t => t.Text == "No cumulative P&L values in this scope.");
        });
    }

    [Fact]
    public async Task ManyDatesRemainReadableAndFilteringReplacesFocusValuesAndOrigin()
    {
        await OnSta(() =>
        {
            var points = Enumerable.Range(0, 40).Select(i => new DashboardChartRow(
                new DateOnly(2026, 1, 1).AddDays(i * 2), i + .123456789m, true, "USD", "Estimated coverage", 1)).ToArray();
            PnlChart chart = Chart("Dark", points);
            chart.Width = 360;
            Draw(chart, 240);
            var dates = Descendants(chart).OfType<TextBlock>().Where(t => (string?)t.Tag == "DateTick").ToArray();
            Assert.Equal(points.Length + 1, dates.Length);
            for (int i = 1; i < dates.Length; i++)
                Assert.True(dates[i - 1].TranslatePoint(new Point(dates[i - 1].ActualWidth, 0), chart).X < dates[i].TranslatePoint(new Point(), chart).X);
            ScrollViewer scroll = Assert.Single(Descendants(chart).OfType<ScrollViewer>());
            Assert.True(scroll.ScrollableWidth > 0);
            Assert.All(dates, date => Assert.True(date.TranslatePoint(new Point(0, date.ActualHeight), scroll).Y <= scroll.ViewportHeight));
            Border oldTarget = Targets(chart)[1];
            var oldTip = (ToolTip)oldTarget.ToolTip;
            Assert.Contains("+0.123456789 USD", (string)oldTip.Content);
            Assert.Contains("estimated", AutomationProperties.GetHelpText(oldTarget), StringComparison.OrdinalIgnoreCase);
            oldTarget.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, Environment.TickCount, null, oldTarget)
                { RoutedEvent = Keyboard.GotKeyboardFocusEvent });
            Assert.True(oldTip.IsOpen);
            oldTarget.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, Environment.TickCount, oldTarget, null)
                { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
            Assert.False(oldTip.IsOpen);
            scroll.ScrollToHorizontalOffset(200);
            scroll.UpdateLayout();
            Assert.True(scroll.HorizontalOffset > 0);

            chart.StartDate = new DateOnly(2026, 9, 1);
            chart.Points = [Row(-4, 2) with { Currency = "EUR" }];
            Draw(chart);
            Assert.False(oldTip.IsOpen);
            Assert.Equal(0, scroll.HorizontalOffset);
            Border[] targets = Targets(chart);
            Assert.Equal(2, targets.Length);
            Assert.Equal(chart.StartDate, ((DashboardChartRow)targets[0].Tag).Date);
            Assert.Equal(0m, ((DashboardChartRow)targets[0].Tag).Value);
            Assert.Contains("-4.00 EUR", (string)((ToolTip)targets[1].ToolTip).Content);
            Assert.DoesNotContain("USD", (string)((ToolTip)targets[1].ToolTip).Content);
            Assert.All(Segments(chart), segment => Assert.Equal(Color(chart.NegativeBrush), Color(segment.Stroke)));
        });
    }

    [Fact]
    public async Task VerticalWheelOverCumulativePlotAxisAndScrollbarMovesDashboardOnce()
    {
        await OnSta(() =>
        {
            PnlChart chart = Chart("Light", [Row(10), Row(-5, 2), Row(0, 3), Row(4, 4)]);
            chart.Width = 360;
            var content = new StackPanel();
            content.Children.Add(new Border { Height = 250 });
            content.Children.Add(chart);
            content.Children.Add(new Border { Height = 300 });
            var outer = new ScrollViewer { Width = 400, Height = 300, Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Draw(outer);
            var inner = Assert.Single(Descendants(chart).OfType<ScrollViewer>());
            outer.ScrollToVerticalOffset(200);
            outer.UpdateLayout();
            var native = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = UIElement.MouseWheelEvent };
            outer.RaiseEvent(native);
            outer.UpdateLayout();
            double nativeEnd = outer.VerticalOffset;
            Assert.True(nativeEnd > 200);
            var axis = (UIElement)VisualTreeHelper.GetParent(Assert.Single(Descendants(chart).OfType<Line>(), line => (string?)line.Tag == "ValueAxis"));
            var plot = (UIElement)VisualTreeHelper.GetParent(Assert.Single(Descendants(chart).OfType<Line>(), line => (string?)line.Tag == "DateAxis"));
            var scrollbar = Assert.Single(Descendants(chart).OfType<ScrollBar>(), bar => bar.Orientation == Orientation.Horizontal);
            foreach (UIElement surface in new UIElement[] { Targets(chart)[1], axis, plot, scrollbar })
            {
                outer.ScrollToVerticalOffset(200);
                outer.UpdateLayout();
                var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
                surface.RaiseEvent(wheel);
                if (!wheel.Handled) { wheel.RoutedEvent = UIElement.MouseWheelEvent; surface.RaiseEvent(wheel); }
                outer.UpdateLayout();
                Assert.Equal(nativeEnd, outer.VerticalOffset);
                Assert.Equal(0, inner.HorizontalOffset);
            }
            inner.ScrollToHorizontalOffset(100);
            inner.UpdateLayout();
            Assert.True(inner.HorizontalOffset > 0);
        });
    }

    [Fact]
    public void DailyRowsReuseCountsAndKeepProvenanceOutOfVisibleFields()
    {
        var facts = new[] { DashboardViewModelTests.Fact(10m, null), DashboardViewModelTests.Fact(-3m),
            DashboardViewModelTests.Fact(0m, day: 2), DashboardViewModelTests.Fact(null, null, day: 3) };
        var presentation = new DashboardCurrencyPresentation(Assert.Single(DashboardMetricCalculator.Calculate(facts).Currencies));
        Assert.Equal(new[] { 2, 1, 1 }, presentation.DailyPnl.Select(r => r.TradeCount));
        Assert.Equal(DashboardCurrencyPresentation.Money(7m, "USD"), presentation.DailyPnl[0].AmountText);
        Assert.Equal("2 Trades", presentation.DailyPnl[0].TradeCountText);
        Assert.True(presentation.DailyPnl[0].IsEstimated);
        Assert.Contains("estimated", presentation.DailyPnl[0].Description);
        Assert.Contains("Gross", presentation.DailyPnl[0].Description);
        Assert.Equal(DashboardCurrencyPresentation.Money(0m, "USD"), presentation.DailyPnl[1].AmountText);
        Assert.Equal("—", presentation.DailyPnl[2].AmountText);
        Assert.All(presentation.DailyPnl, row =>
        {
            Assert.DoesNotContain("estimated", row.AmountText);
            Assert.DoesNotContain("Complete", row.AmountText);
            Assert.DoesNotContain("Verified", row.TradeCountText);
        });
    }

    private static DashboardChartRow Row(decimal? value, int day = 1) => new(new(2026, 9, day), value, false, "USD", "", 1);
    private static Color Color(Brush brush) => Assert.IsType<SolidColorBrush>(brush).Color;
    private static PnlChart Chart(string theme, IReadOnlyList<DashboardChartRow> rows)
    {
        var chart = new PnlChart { Width = 500, Height = 260, Points = rows,
            Resources = (ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(Root(),
                $"src/PersonalTradingJournal.Desktop/Resources/Themes/{theme}Theme.xaml"))) };
        chart.SetResourceReference(PnlChart.PositiveBrushProperty, "PtjSuccessBrush");
        chart.SetResourceReference(PnlChart.NegativeBrushProperty, "PtjDangerBrush");
        chart.SetResourceReference(PnlChart.NeutralBrushProperty, "PtjTextMutedBrush");
        chart.SetResourceReference(Control.ForegroundProperty, "PtjTextSecondaryBrush");
        return chart;
    }
    private static Border[] Targets(PnlChart chart) => Descendants(chart).OfType<Border>().Where(t => t.Tag is DashboardChartRow).ToArray();
    private static Line[] Segments(PnlChart chart) => Descendants(chart).OfType<Line>().Where(t => (string?)t.Tag == "CumulativeSegment").ToArray();
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (DependencyObject nested in Descendants(child)) yield return nested;
        }
    }
    private static void Draw(FrameworkElement chart, int dpi = 96)
    {
        chart.Measure(new Size(chart.Width, chart.Height));
        chart.Arrange(new Rect(0, 0, chart.Width, chart.Height));
        chart.UpdateLayout();
        new RenderTargetBitmap((int)(chart.Width * dpi / 96), (int)(chart.Height * dpi / 96), dpi, dpi, PixelFormats.Pbgra32).Render(chart);
    }
    private static Task OnSta(Action action)
    {
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { action(); result.SetResult(); } catch (Exception error) { result.SetException(error); } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return result.Task;
    }
    private static string Root()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PersonalTradingJournal.sln"))) root = root.Parent;
        return root?.FullName ?? throw new DirectoryNotFoundException();
    }
}
