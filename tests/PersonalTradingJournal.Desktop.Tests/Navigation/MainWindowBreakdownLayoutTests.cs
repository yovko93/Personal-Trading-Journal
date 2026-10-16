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
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.Breakdown;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Desktop.Views.Breakdown;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    [Theory]
    [InlineData("Light", 1586, 96)]
    [InlineData("Dark", 1586, 96)]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 1280, 96)]
    [InlineData("Light", 1000, 240)]
    [InlineData("Dark", 1000, 240)]
    public async Task SharedPageHeaderBreakdownReferenceCompositionAndRetainedStates(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(HeaderHostVariable) != "1") { await HeaderHost.Value; return; }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            using var f = new BreakdownFixture();
            f.Accounts.Rows = [BreakdownFixture.Account(BreakdownFixture.AccountId) with { Currency = "USD" }];
            f.Reader.MakeResult = q => Composition(q);
            f.Vm.Grouping = BreakdownGrouping.TradingSetup;
            f.Vm.Period = DashboardPeriod.Month;
            var fixture = CreateFixture(breakdown: f.Vm);
            var window = new MainWindow(fixture.Main);
            var prior = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            try
            {
                fixture.Main.NavigateCommand.Execute(NavigationDestination.Breakdown); Pump(f.Vm.LoadTask);
                f.Vm.SortGroups(Desktop.ViewModels.Breakdown.BreakdownSortColumn.Pnl);
                f.Vm.SortGroups(Desktop.ViewModels.Breakdown.BreakdownSortColumn.Pnl);
                f.Vm.SelectedGroup = f.Vm.GroupRows.Single(r => r.Label == "FVG");
                var view = new BreakdownView { DataContext = f.Vm };
                var area = (Grid)window.FindName("PageContentArea");
                // Use the real compiled shell and page without starting App or any database service.
                ((ContentControl)((Border)area.Children[1]).Child).Content = view;
                var root = (FrameworkElement)window.Content;
                VisualTreeHelper.SetRootDpi(root, new(dpi / 96d, dpi / 96d));
                Layout();
                var title = (TextBlock)window.FindName("PageTitleText");
                Assert.Equal("Breakdown Analytics", title.Text);
                Assert.Equal(TextAlignment.Left, title.TextAlignment);
                Assert.Equal(AutomationHeadingLevel.Level1, AutomationProperties.GetHeadingLevel(title));
                Assert.Equal(Visibility.Visible, ((TextBlock)window.FindName("BreakdownSubtitle")).Visibility);
                Assert.InRange(title.TranslatePoint(new(), area).X, 31.9, 32.1);
                var toggle = (ToggleButton)window.FindName("PageThemeToggle");
                Assert.True(title.TranslatePoint(new(title.ActualWidth, 0), area).X <= toggle.TranslatePoint(new(), area).X);
                var more = (Expander)view.FindName("MoreFilters");
                Assert.False(more.IsExpanded);
                Assert.False(((TextBox)view.FindName("CurrencyFilter")).IsVisible);
                Assert.Equal(Visibility.Collapsed, ((TextBlock)view.FindName("OperationStatus")).Visibility);
                var cards = (UniformGrid)view.FindName("KpiCards");
                Assert.Equal(4, cards.Children.Count);
                Assert.Equal(width >= 1280 ? 4 : 2, cards.Columns);
                foreach (Border card in cards.Children) Assert.InRange(card.ActualHeight, 80, 100);
                var chips = (ListBox)view.FindName("GroupingFilter");
                Assert.Equal(7, chips.Items.Count);
                Assert.IsAssignableFrom<ISelectionProvider>(UIElementAutomationPeer.CreatePeerForElement(chips).GetPattern(PatternInterface.Selection));
                var chart = (Border)view.FindName("ChartCard");
                var details = (Border)view.FindName("DetailsCard");
                var table = (DataGrid)view.FindName("GroupTable");
                var page = (ScrollViewer)view.Content;
                Assert.Equal(0, page.ScrollableWidth);
                if (width >= 1280)
                {
                    Assert.InRange(chart.ActualWidth / (chart.ActualWidth + details.ActualWidth), .64, .69);
                    Assert.Equal(chart.ActualHeight, details.ActualHeight);
                    Assert.InRange(table.TranslatePoint(new(), root).Y, 0, 760);
                    Assert.InRange(table.TranslatePoint(new(table.ActualWidth, 0), root).X, 0, width);
                    Assert.True(table.ActualHeight > 100);
                }
                else Assert.Equal(1, Grid.GetRow(details));
                Render("four-groups");
                if (width == 1000)
                {
                    page.ScrollToEnd(); Layout(); Render("four-groups-bottom");
                    Assert.Equal(page.ScrollableHeight, page.VerticalOffset);
                    var action = (Button)view.FindName("SelectedTradesButton");
                    Assert.InRange(action.TranslatePoint(new(), root).Y, 0, Height() - action.ActualHeight);
                    page.ScrollToTop(); Layout();
                }
                else if (width == 1586)
                    Assert.InRange(table.ActualWidth - table.Columns.Sum(c => c.ActualWidth), 0, 22);
                int reads = f.Reader.Calls.Count;
                var originalTitle = f.Vm.ChartHeading;
                chips.SelectedValue = BreakdownGrouping.Direction; Layout();
                Assert.True(f.Vm.HasUnappliedChanges);
                Assert.Equal(originalTitle, f.Vm.ChartHeading); Assert.Equal(reads, f.Reader.Calls.Count);
                chips.SelectedValue = BreakdownGrouping.TradingSetup;
                ((Button)view.FindName("MoreFiltersButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout();
                Assert.True(more.IsExpanded);
                Assert.Equal("Expanded", AutomationProperties.GetItemStatus((Button)view.FindName("MoreFiltersButton")));
                more.IsExpanded = false;
                f.Reader.MakeResult = _ => throw new IOException("Synthetic read failure");
                var result = f.Vm.Result; Pump(f.Vm.RefreshAsync()); Layout();
                Assert.Same(result, f.Vm.Result); Assert.Equal(Visibility.Visible, ((TextBlock)view.FindName("OperationStatus")).Visibility);
                Assert.NotNull(f.Vm.ErrorMessage); Render("retained-error");
                f.Reader.MakeResult = q => Composition(q, single: true);
                f.Vm.Grouping = BreakdownGrouping.Instrument;
                Pump(f.Vm.ApplyAsync()); f.Vm.SelectedGroup = f.Vm.GroupRows[0]; Layout();
                Assert.Equal("Net/Gross", f.Vm.Presentation!.Summary.Basis);
                Assert.Contains("Net/Gross", f.Vm.ChartHeading);
                Assert.Single(f.Vm.GroupRows); Render("single-MNQ");
                f.Reader.MakeResult = q => BreakdownFixture.Snapshot(q, unavailable: true);
                Pump(f.Vm.RefreshAsync()); f.Vm.SelectedGroup = f.Vm.GroupRows[0]; Layout();
                Assert.Equal("—", f.Vm.Presentation!.Summary.Pnl); Render("unavailable");
                f.Reader.MakeResult = q => BreakdownMetricCalculator.Calculate(q, []);
                Pump(f.Vm.RefreshAsync()); Layout();
                Assert.Empty(f.Vm.GroupRows); Assert.Null(f.Vm.SelectedDetails); Render("empty");
                fixture.Main.NavigateCommand.Execute(NavigationDestination.Dashboard); Layout();
                Assert.Equal(TextAlignment.Center, title.TextAlignment);
                Assert.Equal(Visibility.Collapsed, ((TextBlock)window.FindName("BreakdownSubtitle")).Visibility);

                int Height() => width == 1280 ? 800 : 992;
                void Layout() { root.Measure(new(width, Height())); root.Arrange(new(0, 0, width, Height())); root.UpdateLayout(); }
                void Render(string state)
                {
                    string directory = Path.Combine(AppContext.BaseDirectory, "breakdown-alignment-renders");
                    Directory.CreateDirectory(directory);
                    var bitmap = new RenderTargetBitmap(width * dpi / 96, Height() * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
                    bitmap.Render(root);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{state}.png")); encoder.Save(file);
                }
            }
            finally { window.Content = null; window.Close(); fixture.Main.Dispose(); SynchronizationContext.SetSynchronizationContext(prior); }
        }, shutdownDispatcher: false);
    }

    private static BreakdownAnalyticsSnapshot Composition(BreakdownQuery query, bool single = false)
    {
        var facts = new List<BreakdownTradeFact>();
        var close = new DateTimeOffset(2026, 3, 8, 16, 0, 0, TimeSpan.Zero);
        var groups = new[] { ("FVG", 48, 29, 1920m), ("Liquidity sweep", 36, 22, 1140m), ("CRT", 24, 12, -240m), ("Unclassified", 12, 3, -360m) };
        foreach (var (label, count, wins, total) in groups)
        {
            Guid? setup = label == "Unclassified" ? null : new Guid(Array.FindIndex(groups, g => g.Item1 == label) + 1, 0, 0, new byte[8]);
            decimal positive = (total + 60m * (count - wins)) / wins;
            decimal remaining = total;
            for (int n = 0; n < count; n++)
            {
                decimal value = n == count - 1 ? remaining : n < wins ? positive : -60m;
                remaining -= value;
                var fact = new TradeAnalyticsFact(Guid.NewGuid(), TradeStatus.Closed, close.AddMinutes(-10), close, query.Currency,
                    setup, value + 2, single && n % 2 == 0 ? null : 2m, single && n % 2 == 0 ? null : value);
                facts.Add(new(fact, BreakdownFixture.AccountId, BreakdownFixture.InstrumentId, TradeDirection.Long)
                { InstrumentName = "MNQ", InstrumentIsActive = true, SetupName = label == "Unclassified" ? null : label, SetupIsActive = true });
            }
        }
        return BreakdownMetricCalculator.Calculate(query, facts);
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
}
