using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Breakdown;
using PersonalTradingJournal.Desktop.Views.Breakdown;

namespace PersonalTradingJournal.Desktop.Tests.Breakdown;

public sealed class BreakdownChartViewTests
{
    [Fact]
    public async Task BoundChartSharesSelectionScrollsAndStacksWithAccessibleValuesInBothThemes()
    {
        const string host = "PTJ_BREAKDOWN_CHART_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(BreakdownChartViewTests), "breakdown-chart", host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30));
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            using var f = new BreakdownFixture();
            f.Reader.MakeResult = q => new(q, BreakdownPresentationTests.Metrics(60, 59, null) with { FinancialSources = new(BreakdownCalculationMode.NetPreferred, 60, 58, 1, 0, 1) },
                Enumerable.Range(1, 60).Select(n =>
                {
                    decimal? value = n switch { 1 => 120m, 2 => -80m, 3 => 0m, 4 => null, _ => n };
                    var row = BreakdownPresentationTests.Group(n, value) with { Label = n == 1 ? "Long synthetic instrument label · historical metadata retained and readable" : "Instrument " + n };
                    var m = row.Metrics;
                    return row with { Metrics = m with { Selected = m.Selected with { WinRatePercent = value is null ? null : value > 0 ? 100m : 0m,
                        Wins = value > 0 ? 1 : 0, Losses = value < 0 ? 1 : 0, BreakEvens = value == 0 ? 1 : 0,
                        AverageWin = new(value > 0 ? Application.Analytics.AveragePnlStatus.Defined : Application.Analytics.AveragePnlStatus.NoWins, value > 0 ? value : null),
                        AverageLoss = new(value < 0 ? Application.Analytics.AveragePnlStatus.Defined : Application.Analytics.AveragePnlStatus.NoLosses, value < 0 ? -value : null),
                        ProfitFactor = new(value is null ? Application.Analytics.ProfitFactorStatus.IncompleteCoverage : value > 0 ? Application.Analytics.ProfitFactorStatus.NoLosses : value == 0 ? Application.Analytics.ProfitFactorStatus.AllBreakEven : Application.Analytics.ProfitFactorStatus.Defined, value < 0 ? 0m : null) },
                        FinancialSources = new(BreakdownCalculationMode.NetPreferred, 1, n is 2 or 4 ? 0 : 1, n == 2 ? 1 : 0, 0, n == 4 ? 1 : 0) } };
                }).ToArray());
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources("Light");
            var view = new BreakdownView { DataContext = f.Vm };
            var prior = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            try
            {
                Pump(f.Vm.ActivateAsync());
                foreach (string theme in new[] { "Light", "Dark" })
                foreach (var (width, dpi) in new[] { (1280, 96), (700, 240) })
                {
                    view.Resources.MergedDictionaries.Clear(); view.Resources.MergedDictionaries.Add(CalendarViewLayoutTests.SharedThemeResources(theme));
                    VisualTreeHelper.SetRootDpi(view, new(dpi / 96d, dpi / 96d));
                    var page = (ScrollViewer)view.Content;
                    var chart = (ListBox)view.FindName("ComparisonChart");
                    var table = (DataGrid)view.FindName("GroupTable");
                    var details = (Border)view.FindName("DetailsCard");
                    var layout = (Grid)view.FindName("ComparisonLayout");
                    Layout();
                    Assert.Equal(width < 1000 ? 1 : 0, Grid.GetRow(details));
                    Assert.Equal(60, chart.Items.Count); Assert.InRange(Descendants<ListBoxItem>(chart).Count(), 1, 59);
                    Assert.NotEmpty(AutomationProperties.GetName(chart));
                    Assert.IsAssignableFrom<ISelectionProvider>(UIElementAutomationPeer.CreatePeerForElement(chart).GetPattern(PatternInterface.Selection));
                    if (f.Vm.SelectedGroup is null)
                    {
                        page.ScrollToVerticalOffset(layout.TranslatePoint(new(0, 0), view).Y + page.VerticalOffset); Layout();
                        Render("unselected");
                    }
                    chart.SelectedIndex = 1; Layout();
                    Assert.Equal(f.Vm.SelectedGroup, table.SelectedItem);
                    Assert.Equal(BreakdownPresentationTests.Key(2), f.Vm.SelectedDetails!.Group.Key);
                    table.SelectedItem = f.Vm.GroupRows[59]; Layout();
                    Assert.Equal(59, chart.SelectedIndex);
                    var inner = Descendants<ScrollViewer>(chart).First();
                    Assert.True(inner.VerticalOffset > 0);
                    var metric = (ComboBox)view.FindName("ChartMetricSelector");
                    int calls = f.Reader.Calls.Count;
                    metric.SelectedValue = BreakdownChartMetric.WinRate; Layout();
                    Assert.Equal(BreakdownChartMetric.WinRate, f.Vm.Chart!.Metric); Assert.Equal(100, f.Vm.Chart.Maximum);
                    metric.SelectedValue = BreakdownChartMetric.Pnl;
                    chart.SelectedIndex = 0; Layout();
                    Assert.Equal(calls, f.Reader.Calls.Count);
                    double offset = layout.TranslatePoint(new(0, 0), view).Y + page.VerticalOffset;
                    page.ScrollToVerticalOffset(offset); Layout();
                    Assert.InRange(chart.ActualWidth, 200, width);
                    Assert.InRange(details.TranslatePoint(new(details.ActualWidth, 0), view).X, 0, width + 1);
                    Assert.Contains(Descendants<ListBoxItem>(chart), item => AutomationProperties.GetName(item).Contains("Gross fallback"));
                    Assert.Contains(Descendants<TextBlock>(chart), t => t.Text.Contains("Unavailable"));
                    var longLabel = Assert.Single(Descendants<TextBlock>(chart), t => t.Text == f.Vm.GroupRows[0].Label);
                    Assert.Equal(TextWrapping.Wrap, longLabel.TextWrapping);
                    Assert.True(longLabel.ActualHeight > longLabel.FontSize * 1.5, "Long chart labels must occupy multiple lines rather than clip.");
                    Assert.All(Descendants<ListBoxItem>(chart), item => { Assert.True(item.Focusable); Assert.NotEmpty(AutomationProperties.GetName(item)); });
                    Render("selected");
                    var savedOffset = page.VerticalOffset;
                    table.SelectedItem = f.Vm.GroupRows[59]; Layout();
                    // A shorter selected label can reduce the outer extent; only normal end clamping is allowed.
                    Assert.Equal(Math.Min(savedOffset, page.ScrollableHeight), page.VerticalOffset);
                    table.SelectedItem = f.Vm.GroupRows[0]; Layout();
                    if (width == 700)
                    {
                        page.ScrollToVerticalOffset(details.TranslatePoint(new(0, 0), view).Y + page.VerticalOffset); Layout(); Render("details");
                    }
                    void Layout() { view.Measure(new(width, 900)); view.Arrange(new(0, 0, width, 900)); view.UpdateLayout(); }
                    void Render(string state)
                        => SaveRender(view, theme, width, dpi, state);
                }
                f.Reader.MakeResult = q => new(q, BreakdownPresentationTests.Metrics(0, 0, null), []);
                Pump(f.Vm.RefreshAsync()); view.Measure(new(700, 900)); view.Arrange(new(0, 0, 700, 900)); view.UpdateLayout();
                Assert.Empty(((ListBox)view.FindName("ComparisonChart")).Items);
                Assert.Null(f.Vm.SelectedDetails); Assert.Contains(Descendants<TextBlock>(view), t => t.Text == "No matching groups to compare.");
                Assert.Equal(Visibility.Collapsed, ((StackPanel)view.FindName("DetailsFields")).Visibility);
                Assert.InRange(((Border)view.FindName("DetailsCard")).ActualHeight, 1, 150);
                SaveRender(view, "Dark", 700, 240, "empty");
            }
            finally { view.DataContext = null; SynchronizationContext.SetSynchronizationContext(prior); }
        });
    }
    private static void SaveRender(BreakdownView view, string theme, int width, int dpi, string state)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "breakdown-chart-renders"); Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 900 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(output);
    }
    private static void Pump(Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            _ = task.ContinueWith(_ => frame.Continue = false, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) yield return found;
            foreach (var item in Descendants<T>(child)) yield return item;
        }
    }
}
