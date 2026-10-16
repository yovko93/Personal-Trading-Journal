using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Breakdown;
using PersonalTradingJournal.Desktop.Views.Breakdown;

namespace PersonalTradingJournal.Desktop.Tests.Breakdown;

public sealed class BreakdownTableViewTests
{
    [Fact]
    public async Task BoundTableSortSelectionCoverageVirtualizationAndReachabilityInBothThemes()
    {
        const string host = "PTJ_BREAKDOWN_TABLE_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(BreakdownTableViewTests), "breakdown-table", host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30));
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            using var f = new BreakdownFixture();
            f.Reader.MakeResult = q =>
            {
                var rows = Enumerable.Range(1, 80).Select(n => BreakdownPresentationTests.Group(n, n % 3 == 0 ? null : -n * 11.25m)
                    with { Label = n == 1 ? "Long synthetic Instrument label with retained historical metadata · inactive" : "Instrument " + n }).ToArray();
                for (int i = 0; i < rows.Length; i++)
                {
                    var metrics = rows[i].Metrics;
                    if (metrics.Selected.Total is not null)
                        rows[i] = rows[i] with { Metrics = metrics with { Selected = metrics.Selected with
                        { WinRatePercent = 0m, ProfitFactor = new(ProfitFactorStatus.Defined, 0m), Losses = 1 } } };
                }
                rows[0] = rows[0] with { Metrics = rows[0].Metrics with { FinancialSources = new(BreakdownCalculationMode.NetPreferred, 1, 0, 1, 0, 0) } };
                var summary = BreakdownPresentationTests.Metrics(80, 54, null) with { FinancialSources = new(BreakdownCalculationMode.NetPreferred, 80, 53, 1, 0, 26) };
                return new(q, summary, rows);
            };
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
                    view.Resources.MergedDictionaries.Clear();
                    view.Resources.MergedDictionaries.Add(CalendarViewLayoutTests.SharedThemeResources(theme));
                    VisualTreeHelper.SetRootDpi(view, new(dpi / 96d, dpi / 96d));
                    Layout();
                    var table = (DataGrid)view.FindName("GroupTable");
                    var page = (ScrollViewer)view.Content;
                    page.ScrollToEnd(); Layout();
                    Assert.True(table.IsReadOnly); Assert.True(table.EnableRowVirtualization);
                    Assert.Equal(80, table.Items.Count);
                    Assert.InRange(Descendants<DataGridRow>(table).Count(), 1, 79);
                    Assert.Equal("Breakdown group comparison", AutomationProperties.GetName(table));
                    Assert.Equal(7, table.Columns.Count);
                    int reads = f.Reader.Calls.Count;
                    table.SelectedItem = f.Vm.GroupRows.Single(r => r.Key == BreakdownPresentationTests.Key(2));
                    Assert.Equal(BreakdownPresentationTests.Key(2), f.Vm.SelectedGroupKey);
                    var header = Descendants<DataGridColumnHeader>(table).Single(h => h.Column?.SortMemberPath == "Pnl");
                    Assert.True(header.Focusable);
                    Assert.True(table.CanUserSortColumns); Assert.True(header.Column.CanUserSort);
                    var tablePeer = UIElementAutomationPeer.CreatePeerForElement(table);
                    var presenter = Assert.Single(Descendants<DataGridColumnHeadersPresenter>(table));
                    var headersPeer = Assert.IsType<DataGridColumnHeadersPresenterAutomationPeer>(UIElementAutomationPeer.CreatePeerForElement(presenter));
                    var headerPeer = Assert.Single(headersPeer.GetChildren(), p => p.GetName() == "P&L");
                    Assert.IsAssignableFrom<IInvokeProvider>(headerPeer.GetPattern(PatternInterface.Invoke)).Invoke();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); Layout();
                    Assert.Equal(BreakdownSortColumn.Pnl, f.Vm.SortColumn);
                    Assert.Equal(f.Vm.SelectedGroup, table.SelectedItem);
                    Assert.Equal(reads, f.Reader.Calls.Count);
                    Pump(f.Vm.RefreshAsync()); Layout();
                    Assert.Equal(BreakdownPresentationTests.Key(2), f.Vm.SelectedGroupKey);
                    Assert.Equal(f.Vm.SelectedGroup, table.SelectedItem);
                    Assert.Same(f.Vm.Result, f.Vm.Presentation!.Source);
                    // Return to first row for long label/fallback rendering; sort state stays intact.
                    var longRow = f.Vm.GroupRows.Single(r => r.Key == BreakdownPresentationTests.Key(1));
                    var inner = Descendants<ScrollViewer>(table).First();
                    inner.ScrollToVerticalOffset(f.Vm.GroupRows.ToList().IndexOf(longRow)); Layout();
                    inner.ScrollToHorizontalOffset(0); Layout();
                    if (width == 700) Assert.True(inner.ScrollableWidth > 0,
                        $"Horizontal extent {inner.ExtentWidth}, viewport {inner.ViewportWidth}, table {table.ActualWidth}, view {view.ActualWidth}, columns {string.Join(",", table.Columns.Select(c => c.ActualWidth))}.");
                    Assert.True(Descendants<TextBlock>(table).Any(t => t.Text == longRow.Label),
                        $"Target row not realized: viewport {width}, offset {inner.VerticalOffset}, container {table.ItemContainerGenerator.ContainerFromItem(longRow)?.GetType().Name ?? "none"}.");
                    Render("left");
                    inner.ScrollToHorizontalOffset(inner.ScrollableWidth); Layout();
                    Assert.InRange(inner.HorizontalOffset - inner.ScrollableWidth, -1, 1);
                    Assert.Contains(Descendants<TextBlock>(table), t => t.Text.Contains("Gross fallback"));
                    Render("right");
                    var unavailable = f.Vm.GroupRows.First(r => r.Source.Metrics.Selected.Total is null);
                    inner.ScrollToVerticalOffset(f.Vm.GroupRows.ToList().IndexOf(unavailable)); Layout();
                    Pump(Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle).Task); Layout();
                    Assert.Contains(Descendants<TextBlock>(table), t => t.Text.Contains("1 unavailable"));
                    Render("unavailable");
                    Assert.Equal(0, page.ScrollableWidth);
                    void Layout() { view.Measure(new(width, 800)); view.Arrange(new(0, 0, width, 800)); view.UpdateLayout(); }
                    void Render(string side)
                    {
                        string directory = Path.Combine(AppContext.BaseDirectory, "breakdown-table-renders"); Directory.CreateDirectory(directory);
                        var bitmap = new RenderTargetBitmap(width * dpi / 96, 800 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(view);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var output = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{side}.png")); encoder.Save(output);
                    }
                }
            }
            finally { view.DataContext = null; SynchronizationContext.SetSynchronizationContext(prior); }
        });
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
