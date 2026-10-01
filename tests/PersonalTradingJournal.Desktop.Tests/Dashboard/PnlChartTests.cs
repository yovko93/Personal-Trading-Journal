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

    [Fact]
    public void HoverRangesDivideDenseNeighborsAndCapSparseGaps()
    {
        double[] dense = [20, 50, 80];
        Assert.Equal(new PnlChart.HoverRange(0, 35), PnlChart.GetHoverRange(dense, 0, 100));
        Assert.Equal(new PnlChart.HoverRange(35, 65), PnlChart.GetHoverRange(dense, 1, 100));
        Assert.Equal(new PnlChart.HoverRange(65, 100), PnlChart.GetHoverRange(dense, 2, 100));

        double[] sparse = [20, 220];
        Assert.Equal(new PnlChart.HoverRange(0, 68), PnlChart.GetHoverRange(sparse, 0, 260));
        Assert.Equal(new PnlChart.HoverRange(172, 260), PnlChart.GetHoverRange(sparse, 1, 260));
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

    [Theory]
    [InlineData("Light", 96)]
    [InlineData("Light", 240)]
    [InlineData("Dark", 96)]
    [InlineData("Dark", 240)]
    public async Task OnlyHoveredOrFocusedCumulativePointHasAlignedGuide(string theme, int dpi)
    {
        await OnSta(() =>
        {
            PnlChart chart = Chart(theme, [Row(10), Row(-30, 2), Row(-20, 3), Row(5, 4)]);
            chart.Width = 360;
            Draw(chart, dpi);
            Line guide = Guide(chart);
            Assert.Equal(Visibility.Collapsed, guide.Visibility);
            Assert.Equal(Color(chart.NeutralBrush), Color(guide.Stroke));
            Border[] targets = Targets(chart);

            // The second dated point lies beyond a positive-to-negative zero crossing.
            Border loss = targets[2];
            loss.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                { RoutedEvent = UIElement.MouseEnterEvent });
            AssertGuideAt(guide, loss, chart);
            Assert.Equal(10, Assert.Single(Descendants(loss).OfType<Ellipse>()).Width);
            Assert.Equal(6, Assert.Single(Descendants(targets[1]).OfType<Ellipse>()).Width);
            Assert.Contains("-30.00 USD", (string)((ToolTip)loss.ToolTip).Content);
            Assert.Equal(6, Segments(chart).Length); // Highlighting does not redraw the signed line.

            Border next = targets[3];
            next.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                { RoutedEvent = UIElement.MouseEnterEvent });
            AssertGuideAt(guide, next, chart);
            Assert.Equal(6, Assert.Single(Descendants(loss).OfType<Ellipse>()).Width);
            Assert.Equal(10, Assert.Single(Descendants(next).OfType<Ellipse>()).Width);
            next.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                { RoutedEvent = UIElement.MouseLeaveEvent });
            Assert.Equal(Visibility.Collapsed, guide.Visibility);

            loss.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, Environment.TickCount, null, loss)
                { RoutedEvent = Keyboard.GotKeyboardFocusEvent });
            AssertGuideAt(guide, loss, chart);
            Assert.True(((ToolTip)loss.ToolTip).IsOpen);
            loss.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, Environment.TickCount, loss, null)
                { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
            Assert.Equal(Visibility.Collapsed, guide.Visibility);
            Assert.False(((ToolTip)loss.ToolTip).IsOpen);

            loss.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                { RoutedEvent = UIElement.MouseEnterEvent });
            chart.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                { RoutedEvent = UIElement.MouseLeaveEvent });
            Assert.Equal(Visibility.Collapsed, guide.Visibility);

            chart.Points = [Row(null)];
            Draw(chart, dpi);
            Border unavailable = Targets(chart)[1];
            unavailable.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                { RoutedEvent = UIElement.MouseEnterEvent });
            Assert.Equal(Visibility.Collapsed, Guide(chart).Visibility);
            Assert.Contains("Unavailable", (string)((ToolTip)unavailable.ToolTip).Content);
        });
    }

    [Theory]
    [InlineData("Light", 96)]
    [InlineData("Light", 240)]
    [InlineData("Dark", 96)]
    [InlineData("Dark", 240)]
    public async Task FullHeightHoverRegionsSelectTheNearestPlottedPointAndSwitchAtMidpoints(string theme, int dpi)
    {
        await OnSta(() =>
        {
            PnlChart chart = Chart(theme, [Row(10), Row(0, 2), Row(-5, 3), Row(null, 4)]);
            chart.Width = 360;
            Draw(chart, dpi);
            Border[] targets = Targets(chart);
            Border[] regions = HoverRegions(chart);
            Assert.Equal(4, regions.Length); // Origin and three plotted dates; no unavailable-value region.
            Canvas plot = (Canvas)VisualTreeHelper.GetParent(regions[0]);
            double bottom = Assert.Single(Descendants(chart).OfType<Line>(), line => (string?)line.Tag == "DateAxis").Y1;
            Assert.All(regions, region =>
            {
                Assert.Equal(16, Canvas.GetTop(region));
                Assert.Equal(bottom - 16, region.Height);
                Assert.False(region.Focusable || KeyboardNavigation.GetIsTabStop(region));
                Assert.Equal(100, ToolTipService.GetInitialShowDelay(region));
            });

            Border zero = RegionFor(chart, targets[2]);
            double zeroX = Canvas.GetLeft(targets[2]) + targets[2].Width / 2;
            foreach (double x in new[] { zeroX - 20, zeroX + 20 })
                foreach (double y in new[] { 17d, bottom - 1 })
                    Assert.Same(zero, HitRegion(plot, x, y));
            zero.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                { RoutedEvent = UIElement.MouseEnterEvent });
            AssertGuideAt(Guide(chart), targets[2], chart);
            Assert.Contains("2026-09-02", (string)((ToolTip)zero.ToolTip).Content);
            Assert.Contains("0.00 USD", (string)((ToolTip)zero.ToolTip).Content);

            Border next = RegionFor(chart, targets[3]);
            double midpoint = (zeroX + Canvas.GetLeft(targets[3]) + targets[3].Width / 2) / 2;
            Assert.Same(zero, HitRegion(plot, midpoint - .5, bottom / 2));
            Assert.Same(next, HitRegion(plot, midpoint + .5, bottom / 2));
            next.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                { RoutedEvent = UIElement.MouseEnterEvent });
            AssertGuideAt(Guide(chart), targets[3], chart);
            Assert.Contains("-5.00 USD", (string)((ToolTip)next.ToolTip).Content);
            next.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                { RoutedEvent = UIElement.MouseLeaveEvent });
            Assert.Equal(Visibility.Collapsed, Guide(chart).Visibility);

            Border origin = RegionFor(chart, targets[0]);
            origin.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                { RoutedEvent = UIElement.MouseEnterEvent });
            AssertGuideAt(Guide(chart), targets[0], chart);
            origin.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                { RoutedEvent = UIElement.MouseLeaveEvent });
            Assert.Equal(Visibility.Collapsed, Guide(chart).Visibility);
        });
    }

    [Fact]
    public async Task GuideUsesScrollablePlotCoordinatesAndRealignsAfterResizeButClearsForNewScope()
    {
        await OnSta(() =>
        {
            PnlChart chart = Chart("Dark", Enumerable.Range(1, 8).Select(i => Row(i % 2 == 0 ? -i : i, i)).ToArray());
            chart.Width = 360;
            Draw(chart, 240);
            ScrollViewer scroll = Assert.Single(Descendants(chart).OfType<ScrollViewer>());
            scroll.ScrollToHorizontalOffset(300);
            scroll.UpdateLayout();
            Border point = Targets(chart)[5];
            Border region = RegionFor(chart, point);
            Assert.Same(region, HitRegion((Canvas)VisualTreeHelper.GetParent(region),
                Canvas.GetLeft(point) + point.Width / 2, 17));
            point.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                { RoutedEvent = UIElement.MouseEnterEvent });
            Line guide = Guide(chart);
            AssertGuideAt(guide, point, chart);
            double firstX = guide.X1;
            Assert.Equal(point.TranslatePoint(new Point(point.Width / 2, 0), scroll).X,
                guide.TranslatePoint(new Point(guide.X1, 0), scroll).X, 6);
            Assert.True(scroll.HorizontalOffset > 0);
            Point visibleRegionCenter = region.TranslatePoint(new Point(region.Width / 2, region.Height / 2), scroll);
            Assert.True(visibleRegionCenter.X > 0 && visibleRegionCenter.X < scroll.ViewportWidth);
            Assert.Same(region, FindRegion(VisualTreeHelper.HitTest(scroll, visibleRegionCenter)?.VisualHit));

            chart.Width = 950;
            Draw(chart, 240);
            Border resizedPoint = Targets(chart)[5];
            Border resizedRegion = RegionFor(chart, resizedPoint);
            Line resizedGuide = Guide(chart);
            AssertGuideAt(resizedGuide, resizedPoint, chart);
            Assert.NotEqual(firstX, resizedGuide.X1);
            Assert.Same(resizedRegion, HitRegion((Canvas)VisualTreeHelper.GetParent(resizedRegion),
                Canvas.GetLeft(resizedPoint) + resizedPoint.Width / 2, 17));
            Assert.Equal(resizedPoint.TranslatePoint(new Point(resizedPoint.Width / 2, 0), scroll).X,
                resizedGuide.TranslatePoint(new Point(resizedGuide.X1, 0), scroll).X, 6);

            chart.Points = [Row(3)];
            Draw(chart, 240);
            Assert.Equal(Visibility.Collapsed, Guide(chart).Visibility);
            Assert.Single(Segments(chart));
            Assert.Equal(2, HoverRegions(chart).Length);
            Assert.Contains("USD", (string)((ToolTip)HoverRegions(chart)[1].ToolTip).Content);
            chart.Points = [Row(-4) with { Currency = "EUR" }];
            Draw(chart, 240);
            Assert.Contains("EUR", (string)((ToolTip)HoverRegions(chart)[1].ToolTip).Content);
            Assert.DoesNotContain("USD", (string)((ToolTip)HoverRegions(chart)[1].ToolTip).Content);
        });
    }

    private static void AssertGuideAt(Line guide, Border target, PnlChart chart)
    {
        Assert.Equal(Visibility.Visible, guide.Visibility);
        Assert.Equal(Canvas.GetLeft(target) + target.Width / 2, guide.X1, 6);
        Assert.Equal(guide.X1, guide.X2);
        Assert.Equal(16, guide.Y1);
        Assert.Equal(Assert.Single(Descendants(chart).OfType<Line>(), line => (string?)line.Tag == "DateAxis").Y1, guide.Y2);
        Assert.False(guide.IsHitTestVisible);
        Assert.Single(Descendants(chart).OfType<Line>(), line => (string?)line.Tag == "ActivePointGuide");
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
            foreach (UIElement surface in new UIElement[] { Targets(chart)[1], HoverRegions(chart)[1], axis, plot, scrollbar })
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
    private static Border[] HoverRegions(PnlChart chart) => Descendants(chart).OfType<Border>()
        .Where(region => region.Tag as string == "CumulativeHoverRegion").ToArray();
    private static Border RegionFor(PnlChart chart, Border target) =>
        Assert.Single(HoverRegions(chart), region => ReferenceEquals(region.DataContext, target));
    private static Border? HitRegion(Canvas plot, double x, double y) =>
        FindRegion(VisualTreeHelper.HitTest(plot, new Point(x, y))?.VisualHit);
    private static Border? FindRegion(DependencyObject? hit)
    {
        while (hit is not null)
        {
            if (hit is Border region && region.Tag as string == "CumulativeHoverRegion") return region;
            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }
    private static Line Guide(PnlChart chart) => Assert.Single(Descendants(chart).OfType<Line>(), line => (string?)line.Tag == "ActivePointGuide");
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
