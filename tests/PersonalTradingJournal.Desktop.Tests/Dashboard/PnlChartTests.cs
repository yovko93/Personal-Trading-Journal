using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task ActualDrawingUsesThemeSignBrushesAndLeavesUnavailableGaps(string theme)
    {
        await OnSta(() =>
        {
            var resources = (ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(Root(),
                $"src/PersonalTradingJournal.Desktop/Resources/Themes/{theme}Theme.xaml")));
            var chart = new PnlChart { Width = 500, Height = 240, Resources = resources, Points = [Row(10), Row(-30, 2), Row(0, 3), Row(null, 4)] };
            chart.SetResourceReference(PnlChart.PositiveBrushProperty, "PtjSuccessBrush");
            chart.SetResourceReference(PnlChart.NegativeBrushProperty, "PtjDangerBrush");
            chart.SetResourceReference(PnlChart.NeutralBrushProperty, "PtjTextMutedBrush");
            var bars = Draw(chart).OfType<GeometryDrawing>().Where(d => d.Geometry is RectangleGeometry).ToArray();
            Assert.Equal(3, bars.Length); // Unknown is a gap, not a fourth zero bar.
            Assert.Equal(Color(chart.PositiveBrush), Color(bars[0].Brush));
            Assert.Equal(Color(chart.NegativeBrush), Color(bars[1].Brush));
            Assert.Equal(Color(chart.NeutralBrush), Color(bars[2].Brush));
            Assert.Equal(1, bars[2].Geometry.Bounds.Height);

            chart.IsCumulative = true;
            var drawing = Draw(chart).OfType<GeometryDrawing>().ToArray();
            var lines = drawing.Where(d => d.Geometry is LineGeometry && d.Pen.Thickness == 2).ToArray();
            Assert.Equal(4, lines.Length); // origin→10; 10→0→-30; -30→0
            Assert.Equal(new[] { Color(chart.PositiveBrush), Color(chart.PositiveBrush), Color(chart.NegativeBrush), Color(chart.NegativeBrush) }, lines.Select(d => Color(d.Pen.Brush)));
            var first = (LineGeometry)lines[1].Geometry;
            var second = (LineGeometry)lines[2].Geometry;
            Assert.Equal(first.EndPoint, second.StartPoint);
            var axis = Assert.Single(drawing, d => d.Geometry is LineGeometry && d.Pen.Thickness == 1);
            Assert.Equal(((LineGeometry)axis.Geometry).StartPoint.Y, first.EndPoint.Y, 8);
            Assert.Equal(first.StartPoint.X + (second.EndPoint.X - first.StartPoint.X) * .25, first.EndPoint.X, 8);
            Assert.Equal(Color(chart.NeutralBrush), Color(drawing.Last(d => d.Geometry is EllipseGeometry).Brush));

            chart.Points = [Row(0), Row(0, 2)];
            Assert.All(Draw(chart).OfType<GeometryDrawing>().Where(d => d.Geometry is LineGeometry), d => Assert.Equal(Color(chart.NeutralBrush), Color(d.Pen.Brush)));
            chart.Points = [Row(10), Row(null, 2), Row(-10, 3)];
            Assert.Single(Draw(chart).OfType<GeometryDrawing>(), d => d.Geometry is LineGeometry && d.Pen.Thickness == 2);
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
    private static DrawingCollection Draw(PnlChart chart)
    {
        chart.Measure(new Size(500, 240));
        chart.Arrange(new Rect(0, 0, 500, 240));
        chart.UpdateLayout();
        new RenderTargetBitmap(500, 240, 96, 96, PixelFormats.Pbgra32).Render(chart);
        return VisualTreeHelper.GetDrawing(chart).Children;
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
