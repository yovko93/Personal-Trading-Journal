using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Desktop.Views.Dashboard;

namespace PersonalTradingJournal.Desktop.Tests.Dashboard;

public sealed class DailyPnlChartTests
{
    [Theory]
    [InlineData("Light", 96)]
    [InlineData("Light", 240)]
    [InlineData("Dark", 96)]
    [InlineData("Dark", 240)]
    public async Task RenderedDailyAxesBarsAndFocusTargetsFollowExactDatedValues(string theme, int dpi)
    {
        await OnSta(() =>
        {
            DailyPnlChart chart = Chart(theme, 520, 260,
                Row(12.345m, 1, 2), Row(-7m, 2, 3), Row(0m, 3, 4), Row(null, 4, 5));
            Draw(chart, 520, 260, dpi);
            var targets = Targets(chart);
            Assert.Equal(4, targets.Length);
            Assert.Equal(new[] { new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2),
                new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 4) }, targets.Select(t => ((DashboardChartRow)t.Tag).Date));
            Assert.All(targets, target =>
            {
                Assert.True(target.Focusable);
                Assert.True(KeyboardNavigation.GetIsTabStop(target));
                Assert.True(target.ActualWidth >= 40 && target.ActualHeight >= 24);
                Assert.NotNull(VisualTreeHelper.HitTest(target, new Point(target.ActualWidth / 2, target.ActualHeight / 2)));
                Assert.Equal(((ToolTip)target.ToolTip).Content, AutomationProperties.GetName(target));
                Assert.Contains("Trades", AutomationProperties.GetHelpText(target));
                Assert.Equal(100, ToolTipService.GetInitialShowDelay(target));
                Assert.Equal(2000, ToolTipService.GetBetweenShowDelay(target));
            });
            Assert.Equal("Day Profit\nDate: 2026-09-01\nTrades Count: 2\nProfit: +12.345 USD", ((ToolTip)targets[0].ToolTip).Content);
            Assert.Equal("Day Profit\nDate: 2026-09-02\nTrades Count: 3\nProfit: -7.00 USD", ((ToolTip)targets[1].ToolTip).Content);
            Assert.Equal("Day Profit\nDate: 2026-09-03\nTrades Count: 4\nProfit: 0.00 USD", ((ToolTip)targets[2].ToolTip).Content);
            Assert.Equal("Day Profit\nDate: 2026-09-04\nTrades Count: 5\nProfit: Unavailable", ((ToolTip)targets[3].ToolTip).Content);

            var lines = Descendants(chart).OfType<Line>().ToArray();
            var zero = Assert.Single(lines, line => (string?)line.Tag == "ZeroBaseline");
            Assert.Equal(2, zero.StrokeThickness);
            Assert.True(zero.Y1 > 16 && zero.Y1 < 260 - 38);
            Assert.Single(lines, line => (string?)line.Tag == "ValueAxis");
            Assert.Single(lines, line => (string?)line.Tag == "DateAxis");
            var dates = Descendants(chart).OfType<TextBlock>().Where(t => (string?)t.Tag == "DateTick").ToArray();
            Assert.Equal(new[] { "2026-09-01", "2026-09-02", "2026-09-03", "2026-09-04" }, dates.Select(t => t.Text));
            Assert.All(dates, date => Assert.True(date.TranslatePoint(new Point(), chart).Y > zero.TranslatePoint(new Point(), chart).Y));
            Assert.Contains(Descendants(chart).OfType<TextBlock>(), t => (string?)t.Tag == "ValueTick" && t.Text.Contains("USD"));
            var bars = targets.SelectMany(Descendants).OfType<Rectangle>().ToArray();
            Assert.Equal(3, bars.Length); // An unavailable value is a gap, not a zero bar.
            Assert.Equal(Color(chart.PositiveBrush), Color(bars[0].Fill));
            Assert.Equal(Color(chart.NegativeBrush), Color(bars[1].Fill));
            Assert.Equal(Color(chart.NeutralBrush), Color(bars[2].Fill));
            Assert.Equal(5, bars[2].Height);
        });
    }

    [Fact]
    public async Task ManyDatesScrollWithoutOverlappingOrInventingMissingDays()
    {
        await OnSta(() =>
        {
            var rows = Enumerable.Range(0, 40).Select(i => new DashboardChartRow(
                new DateOnly(2026, 1, 1).AddDays(i * 2), i - 20, false, "USD", "Complete", 1)).ToArray();
            DailyPnlChart chart = Chart("Light", 360, 260, rows);
            Draw(chart, 360, 260, 240);
            var dates = Descendants(chart).OfType<TextBlock>().Where(t => (string?)t.Tag == "DateTick").ToArray();
            Assert.Equal(rows.Select(r => r.Date.ToString("yyyy-MM-dd")), dates.Select(t => t.Text));
            for (int i = 1; i < dates.Length; i++)
                Assert.True(dates[i - 1].TranslatePoint(new Point(dates[i - 1].ActualWidth, 0), chart).X <
                            dates[i].TranslatePoint(new Point(), chart).X);
            ScrollViewer scroll = Assert.Single(Descendants(chart).OfType<ScrollViewer>());
            Assert.Equal(ScrollBarVisibility.Auto, scroll.HorizontalScrollBarVisibility);
            Assert.True(scroll.ScrollableWidth > 0);
            Assert.Equal(rows.Length, Targets(chart).Length);
        });
    }

    [Theory]
    [InlineData(10)]
    [InlineData(-10)]
    [InlineData(0)]
    public async Task ZeroDayKeepsAVisibleFocusableTargetAtEitherAxisEdge(int otherValue)
    {
        await OnSta(() =>
        {
            DailyPnlChart chart = Chart("Light", 400, 260, Row(otherValue, 1), Row(0, 2));
            Draw(chart, 400, 260, 96);
            Border zero = Targets(chart)[1];
            Assert.True(zero.ActualHeight >= 24);
            Assert.True(zero.Focusable && KeyboardNavigation.GetIsTabStop(zero));
            Rectangle marker = Assert.Single(Descendants(zero).OfType<Rectangle>());
            Assert.Equal(5, marker.Height);
            Assert.NotNull(VisualTreeHelper.HitTest(zero, new Point(zero.ActualWidth / 2, zero.ActualHeight / 2)));
        });
    }

    [Fact]
    public async Task EmptyAndCurrencyChangesReplaceAllTargetsAndTooltips()
    {
        await OnSta(() =>
        {
            var facts = new[] { DashboardViewModelTests.Fact(10m),
                DashboardViewModelTests.Fact(-3m) with { Currency = "EUR" } };
            var presentations = DashboardMetricCalculator.Calculate(facts).Currencies
                .ToDictionary(source => source.Currency, source => new DashboardCurrencyPresentation(source));
            DailyPnlChart chart = Chart("Dark", 400, 260, presentations["USD"].DailyPnl.ToArray());
            Draw(chart, 400, 260, 96);
            Border oldTarget = Assert.Single(Targets(chart));
            Assert.Contains("USD", (string)((ToolTip)oldTarget.ToolTip).Content);
            chart.Points = presentations["EUR"].DailyPnl;
            Draw(chart, 400, 260, 96);
            Border newTarget = Assert.Single(Targets(chart));
            Assert.NotSame(oldTarget, newTarget);
            Assert.Contains("EUR", (string)((ToolTip)newTarget.ToolTip).Content);
            Assert.DoesNotContain("USD", (string)((ToolTip)newTarget.ToolTip).Content);
            chart.Points = [];
            Draw(chart, 400, 260, 96);
            Assert.Empty(Targets(chart));
            Assert.Contains(Descendants(chart).OfType<TextBlock>(), t => t.Text == "No daily P&L values in this scope.");
        });
    }

    [Fact]
    public async Task TooltipUsesTheSameFilteredDailyCountAndAmountAsItsBar()
    {
        await OnSta(() =>
        {
            var metrics = DashboardMetricCalculator.Calculate([
                DashboardViewModelTests.Fact(12m), DashboardViewModelTests.Fact(-12m),
                DashboardViewModelTests.Fact(8m, day: 2)]);
            var presentation = new DashboardCurrencyPresentation(Assert.Single(metrics.Currencies));
            DailyPnlChart chart = Chart("Light", 400, 260, presentation.DailyPnl.ToArray());
            Draw(chart, 400, 260, 96);
            Border[] targets = Targets(chart);
            Assert.Equal(2, targets.Length);
            Assert.Equal(2, ((DashboardChartRow)targets[0].Tag).TradeCount);
            Assert.Equal(0m, ((DashboardChartRow)targets[0].Tag).Value);
            Assert.Equal("Day Profit\nDate: 2026-09-01\nTrades Count: 2\nProfit: 0.00 USD",
                ((ToolTip)targets[0].ToolTip).Content);
            Assert.Equal("Day Profit\nDate: 2026-09-02\nTrades Count: 1\nProfit: +8.00 USD",
                ((ToolTip)targets[1].ToolTip).Content);
        });
    }

    private static DailyPnlChart Chart(string theme, int width, int height, params DashboardChartRow[] rows)
    {
        var resources = (ResourceDictionary)XamlReader.Parse(File.ReadAllText(System.IO.Path.Combine(Root(),
            $"src/PersonalTradingJournal.Desktop/Resources/Themes/{theme}Theme.xaml")));
        var chart = new DailyPnlChart { Width = width, Height = height, Resources = resources, Points = rows };
        chart.SetResourceReference(DailyPnlChart.PositiveBrushProperty, "PtjSuccessBrush");
        chart.SetResourceReference(DailyPnlChart.NegativeBrushProperty, "PtjDangerBrush");
        chart.SetResourceReference(DailyPnlChart.NeutralBrushProperty, "PtjTextMutedBrush");
        chart.SetResourceReference(Control.ForegroundProperty, "PtjTextSecondaryBrush");
        return chart;
    }

    private static DashboardChartRow Row(decimal? value, int day, int tradeCount = 1) =>
        new(new DateOnly(2026, 9, day), value, false, "USD", "Complete", tradeCount);
    private static Border[] Targets(DailyPnlChart chart) => Descendants(chart).OfType<Border>()
        .Where(border => border.Tag is DashboardChartRow).ToArray();
    private static Color Color(Brush? brush) => Assert.IsType<SolidColorBrush>(brush).Color;
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (DependencyObject nested in Descendants(child)) yield return nested;
        }
    }
    private static void Draw(FrameworkElement element, int width, int height, int dpi)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
        new RenderTargetBitmap(width * dpi / 96, height * dpi / 96, dpi, dpi, PixelFormats.Pbgra32).Render(element);
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
        while (root is not null && !File.Exists(System.IO.Path.Combine(root.FullName, "PersonalTradingJournal.sln"))) root = root.Parent;
        return root?.FullName ?? throw new DirectoryNotFoundException();
    }
}
