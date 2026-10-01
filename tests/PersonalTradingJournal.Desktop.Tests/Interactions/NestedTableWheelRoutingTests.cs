using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using PersonalTradingJournal.Desktop.Interactions;

namespace PersonalTradingJournal.Desktop.Tests.Interactions;

public sealed class NestedTableWheelRoutingTests
{
    [Theory]
    [InlineData("Trades")]
    [InlineData("Recent Trades")]
    public async Task WheelOverHeaderRowActionAndEmptySpaceMovesPageExactlyOnce(string section)
    {
        await OnSta(() =>
        {
            var header = new TextBlock { Text = section, Height = 30 };
            var action = new Button { Content = "View", Width = 50, Height = 30 };
            var row = new Border { Height = 60, Child = action };
            var empty = new Border { Height = 60, Background = Brushes.Transparent };
            var table = new StackPanel { MinWidth = 900 };
            table.Children.Add(header);
            table.Children.Add(row);
            table.Children.Add(empty);
            var horizontal = new ScrollViewer
            {
                Width = 300, Height = 170, Content = table,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            NestedTableWheelRouting.SetForwardVerticalWheel(horizontal, true);
            var pageContent = new StackPanel();
            pageContent.Children.Add(new Border { Height = 180 });
            pageContent.Children.Add(horizontal);
            pageContent.Children.Add(new Border { Height = 300 });
            var page = new ScrollViewer
            {
                Width = 340, Height = 260, Content = pageContent,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            Layout(page, 340, 260);
            Assert.True(page.ScrollableHeight > 250);
            Assert.True(horizontal.ScrollableWidth > 0);
            var horizontalBar = Assert.Single(Descendants(horizontal).OfType<ScrollBar>(),
                bar => bar.Orientation == Orientation.Horizontal);

            page.ScrollToVerticalOffset(120);
            page.UpdateLayout();
            double start = page.VerticalOffset;
            RaiseWheel(page, UIElement.MouseWheelEvent, -120);
            page.UpdateLayout();
            double nativeDistance = page.VerticalOffset - start;
            Assert.True(nativeDistance > 0);

            foreach (UIElement surface in new UIElement[] { header, row, action, empty, horizontalBar })
            {
                page.ScrollToVerticalOffset(start);
                page.UpdateLayout();
                RaiseWheel(surface, UIElement.PreviewMouseWheelEvent, -120);
                page.UpdateLayout();
                Assert.Equal(start + nativeDistance, page.VerticalOffset);
                Assert.Equal(0, horizontal.HorizontalOffset);
            }

            page.ScrollToVerticalOffset(start);
            page.UpdateLayout();
            RaiseWheel(action, UIElement.PreviewMouseWheelEvent, 120);
            page.UpdateLayout();
            Assert.Equal(start - nativeDistance, page.VerticalOffset);

            page.ScrollToVerticalOffset(0);
            page.UpdateLayout();
            RaiseWheel(empty, UIElement.PreviewMouseWheelEvent, 120);
            page.UpdateLayout();
            Assert.Equal(0, page.VerticalOffset);

            horizontal.ScrollToHorizontalOffset(120);
            horizontal.UpdateLayout();
            Assert.True(horizontal.HorizontalOffset > 0);
        });
    }

    [Fact]
    public async Task BoundaryPassesWheelToNextScrollableVerticalAncestor()
    {
        await OnSta(() =>
        {
            var horizontal = new ScrollViewer
            {
                Width = 250, Height = 120, Content = new Border { Width = 600, Height = 100 },
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            NestedTableWheelRouting.SetForwardVerticalWheel(horizontal, true);
            var pageContent = new StackPanel();
            pageContent.Children.Add(horizontal);
            pageContent.Children.Add(new Border { Height = 300 });
            var page = new ScrollViewer
            {
                Width = 280, Height = 200, Content = pageContent,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            var outerContent = new StackPanel();
            outerContent.Children.Add(page);
            outerContent.Children.Add(new Border { Height = 300 });
            var outer = new ScrollViewer
            {
                Width = 300, Height = 160, Content = outerContent,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            Layout(outer, 300, 160);
            page.ScrollToVerticalOffset(page.ScrollableHeight);
            page.UpdateLayout();
            outer.ScrollToVerticalOffset(40);
            outer.UpdateLayout();
            double pageEnd = page.VerticalOffset, outerStart = outer.VerticalOffset;
            RaiseWheel(horizontal, UIElement.PreviewMouseWheelEvent, -120);
            outer.UpdateLayout();
            Assert.Equal(pageEnd, page.VerticalOffset);
            Assert.True(outer.VerticalOffset > outerStart);
        });
    }

    [Fact]
    public void OnlyTheTwoHorizontalTradeTablesOptIntoWheelRouting()
    {
        string root = Root();
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XNamespace interactions = "clr-namespace:PersonalTradingJournal.Desktop.Interactions";
        XDocument trades = XDocument.Load(Path.Combine(root, "src/PersonalTradingJournal.Desktop/Views/Trades/TradesView.xaml"));
        XDocument dashboard = XDocument.Load(Path.Combine(root, "src/PersonalTradingJournal.Desktop/Views/Dashboard/DashboardView.xaml"));
        XElement tradeTable = Assert.Single(trades.Descendants(p + "ScrollViewer"),
            element => (string?)element.Attribute(x + "Name") == "TradeListScrollViewer");
        XElement recentTable = Assert.Single(dashboard.Descendants(p + "ScrollViewer"),
            element => element.Attribute(interactions + "NestedTableWheelRouting.ForwardVerticalWheel") is not null &&
                element.Descendants(p + "ItemsControl").Any(item =>
                (string?)item.Attribute("ItemsSource") == "{Binding RecentTrades}"));
        foreach (XElement table in new[] { tradeTable, recentTable })
        {
            Assert.Equal("True", (string?)table.Attribute(interactions + "NestedTableWheelRouting.ForwardVerticalWheel"));
            Assert.Equal("Auto", (string?)table.Attribute("HorizontalScrollBarVisibility"));
            Assert.Equal("Disabled", (string?)table.Attribute("VerticalScrollBarVisibility"));
        }
        Assert.Equal(1, trades.Descendants().Count(element =>
            element.Attribute(interactions + "NestedTableWheelRouting.ForwardVerticalWheel") is not null));
        Assert.Equal(1, dashboard.Descendants().Count(element =>
            element.Attribute(interactions + "NestedTableWheelRouting.ForwardVerticalWheel") is not null));
    }

    private static void RaiseWheel(UIElement source, RoutedEvent routedEvent, int delta) =>
        source.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta)
        {
            RoutedEvent = routedEvent,
            Source = source,
        });

    private static void Layout(FrameworkElement element, int width, int height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (DependencyObject nested in Descendants(child)) yield return nested;
        }
    }

    private static Task OnSta(Action action)
    {
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); result.SetResult(); }
            catch (Exception error) { result.SetException(error); }
        });
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
