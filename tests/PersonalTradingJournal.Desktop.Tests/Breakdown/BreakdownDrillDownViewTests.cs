using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Tests.Trades;
using PersonalTradingJournal.Desktop.Views.Breakdown;
using PersonalTradingJournal.Desktop.Views.Trades;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Breakdown;

public sealed class BreakdownDrillDownViewTests
{
    [Fact]
    public async Task BoundActionsAndScopedBannerAreReachableAndWrapInBothThemes()
    {
        const string host = "PTJ_BREAKDOWN_DRILLDOWN_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(BreakdownDrillDownViewTests), "breakdown-drilldown", host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30));
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            using var f = new BreakdownFixture();
            var reader = new FakeTradeListReader();
            var trades = TradesViewModelTests.CreateViewModel(tradeListReader: reader);
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources("Light");
            app.Resources["UtcToTradingTimeConverter"] = new PersonalTradingJournal.Desktop.Converters.UtcToTradingTimeConverter();
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/PersonalTradingJournal.Desktop;component/Views/Trades/TradeEntryForm.xaml", UriKind.Relative) });
            var breakdownView = new BreakdownView { DataContext = f.Vm };
            var tradesView = new TradesView { DataContext = trades };
            var prior = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            int navigations = 0, backs = 0;
            bool available = false;
            f.Vm.OpenTradesAsync = context =>
            {
                navigations++; trades.SetBreakdownScope(context);
                TradeListItem row = new(context.Cohort.TradeIds[0], BreakdownFixture.AccountId, "Synthetic Account",
                    BreakdownFixture.InstrumentId, "TEST", TradeDirection.Long, TradeStatus.Closed,
                    new(2026, 3, 8, 12, 0, 0, TimeSpan.Zero), new(2026, 3, 8, 13, 0, 0, TimeSpan.Zero),
                    0m, 100m, 110m, 2m, 10m, 8m, "EUR", 1m);
                reader.EnqueuePage(available ? [row] : [], available ? 1 : 0);
                return trades.EnsureLoadedAsync();
            };
            trades.ReturnToBreakdownAsync = () => { backs++; return Task.CompletedTask; };
            try
            {
                Pump(f.Vm.ActivateAsync()); f.Vm.SelectedGroup = f.Vm.GroupRows[0];
                foreach (string theme in new[] { "Light", "Dark" })
                foreach (var (width, dpi) in new[] { (1280, 96), (700, 240) })
                {
                    available = width == 1280;
                    foreach (var view in new UserControl[] { breakdownView, tradesView })
                    {
                        view.Resources.MergedDictionaries.Clear(); view.Resources.MergedDictionaries.Add(CalendarViewLayoutTests.SharedThemeResources(theme));
                        VisualTreeHelper.SetRootDpi(view, new(dpi / 96d, dpi / 96d));
                        view.SetResourceReference(Control.BackgroundProperty, "PtjBackgroundBrush");
                    }
                    Layout(breakdownView);
                    var page = (ScrollViewer)breakdownView.Content;
                    var detail = (Border)breakdownView.FindName("DetailsCard");
                    page.ScrollToVerticalOffset(detail.TranslatePoint(new(), breakdownView).Y + page.VerticalOffset); Layout(breakdownView);
                    var action = Assert.Single(Descendants<Button>(detail), b => Equals(b.Content, "View trades"));
                    Assert.True(action.IsEnabled); Assert.True(action.Focusable);
                    Assert.Same(f.Vm.SelectedGroup, action.CommandParameter);
                    Invoke(action); Pump(f.Vm.ViewTradesCommand.ExecutionTask!);
                    Assert.True(trades.HasBreakdownScope); Layout(tradesView);
                    Assert.Contains(Descendants<TextBlock>(tradesView), t => t.Text.Contains($"{(available ? 1 : 0)} currently available of 1 original"));
                    if (!available) Assert.Contains(Descendants<TextBlock>(tradesView), t => t.Text.StartsWith("No original contributors"));
                    var back = Assert.Single(Descendants<Button>(tradesView), b => Equals(b.Content, "Back to Breakdown"));
                    var exit = Assert.Single(Descendants<Button>(tradesView), b => Equals(b.Content, "Exit Breakdown scope"));
                    // These off-screen arranged controls have no HWND: inspect effective
                    // layout visibility through the banner, not PresentationSource.IsVisible.
                    var banner = (Border)tradesView.FindName("BreakdownScopeBanner");
                    Assert.Equal(Visibility.Visible, banner.Visibility);
                    Assert.Equal(Visibility.Visible, back.Visibility); Assert.Equal(Visibility.Visible, exit.Visibility);
                    foreach (var button in new[] { back, exit })
                    {
                        var position = button.TranslatePoint(new(), tradesView);
                        Assert.InRange(position.X, 0, width - button.ActualWidth + 1);
                        Assert.InRange(position.Y, 0, 900 - button.ActualHeight);
                        Assert.True(button.Focusable);
                    }
                    Render(tradesView, "scope"); Render(breakdownView, "detail");
                    Invoke(back); Pump(trades.BackToBreakdownCommand.ExecutionTask!);
                    // Table row has its own exact-row command parameter, independent of selected details.
                    var table = (DataGrid)breakdownView.FindName("GroupTable");
                    page.ScrollToEnd(); Layout(breakdownView);
                    var tableAction = Assert.Single(Descendants<Button>(table), b => Equals(b.Content, "View trades"));
                    Assert.Same(f.Vm.GroupRows[0], tableAction.CommandParameter); Assert.True(tableAction.IsEnabled);
                    Render(breakdownView, "table");
                    Invoke(exit); Pump(trades.ExitBreakdownScopeCommand.ExecutionTask!); Layout(tradesView);
                    Assert.False(trades.HasBreakdownScope); Assert.Equal(Visibility.Collapsed, banner.Visibility);
                    void Layout(UserControl view) { view.Measure(new(width, 900)); view.Arrange(new(0, 0, width, 900)); view.UpdateLayout(); }
                    void Render(UserControl view, string name)
                    {
                        string directory = Path.Combine(AppContext.BaseDirectory, "breakdown-drilldown-renders"); Directory.CreateDirectory(directory);
                        var bitmap = new RenderTargetBitmap(width * dpi / 96, 900 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(view);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var output = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{name}.png")); encoder.Save(output);
                    }
                }
                Assert.Equal(4, navigations); Assert.Equal(4, backs);
            }
            finally
            {
                trades.DisposeBrowse(); breakdownView.DataContext = null; tradesView.DataContext = null;
                SynchronizationContext.SetSynchronizationContext(prior);
            }
        });
    }
    private static void Invoke(Button button)
    {
        Assert.IsAssignableFrom<IInvokeProvider>(UIElementAutomationPeer.CreatePeerForElement(button).GetPattern(PatternInterface.Invoke)).Invoke();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
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
