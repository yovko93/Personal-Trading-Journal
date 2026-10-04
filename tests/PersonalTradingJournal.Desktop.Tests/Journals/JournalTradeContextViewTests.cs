using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Interactions;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Desktop.Views.Journals;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalTradeContextViewTests
{
    [Fact]
    public async Task ReadOnlyRowsAlignWrapScrollAndExposeCurrenciesAndReferenceStatesInBothThemes()
    {
        var contexts = new List<JournalTradeContextViewModel>();
        var cases = new[] { ("Light", 1100, 96), ("Dark", 1100, 96), ("Light", 480, 240), ("Dark", 480, 240) };
        foreach (var _ in cases) contexts.Add(await CreateAsync());
        var empty = await CreateAsync(empty: true);
        var failed = await CreateAsync(fail: true);
        await OnSta(() =>
        {
            _ = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            for (int i = 0; i < cases.Length; i++)
            {
                var (theme, width, dpi) = cases[i];
                CheckLayout(contexts[i], theme, width, dpi);
            }
            var (emptyView, _) = Layout(empty, "Light", 480);
            Assert.Equal(Visibility.Visible, ((TextBlock)emptyView.FindName("ContextEmpty")).Visibility);
            Assert.Empty(((ItemsControl)emptyView.FindName("ContextRows")).Items);
            Assert.Contains("still save", ((TextBlock)emptyView.FindName("ContextEmpty")).Text);
            var (errorView, _) = Layout(failed, "Dark", 480);
            Assert.Equal(Visibility.Collapsed, ((TextBlock)errorView.FindName("ContextEmpty")).Visibility);
            Assert.NotEmpty(((TextBlock)errorView.FindName("ContextError")).Text);
            Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting((TextBlock)errorView.FindName("ContextError")));
            Assert.True(((Button)errorView.FindName("RefreshContext")).IsEnabled);
            Assert.Empty(((ItemsControl)errorView.FindName("ContextRows")).Items);
            empty.Deactivate();
            failed.Deactivate();
        });
    }

    private static void CheckLayout(JournalTradeContextViewModel vm, string theme, int width, int dpi)
    {
        using var phase = CalendarStaTest.Phase($"Journal context {theme} {width} DIP / {dpi} DPI");
        var (view, outer) = Layout(vm, theme, width);
        var rows = (ItemsControl)view.FindName("ContextRows");
        var header = (Grid)view.FindName("ContextHeader");
        var table = (StackPanel)view.FindName("ContextTable");
        var scroller = (ScrollViewer)view.FindName("ContextScroller");
        Assert.Equal(5, rows.Items.Count);
        Assert.Equal(2, ((ItemsControl)view.FindName("ContextSummaries")).Items.Count);
        Assert.Equal(vm.CountText, ((TextBlock)view.FindName("ContextCount")).Text);
        Assert.Equal(Visibility.Collapsed, ((TextBlock)view.FindName("ContextEmpty")).Visibility);
        Assert.Equal(new[] { "Time (New York)", "Instrument", "Account", "Net P&L", "Size", "Direction", "Setup", "Trading Mistakes" },
            header.Children.OfType<TextBlock>().OrderBy(Grid.GetColumn).Select(t => t.Text));
        Assert.True(NestedTableWheelRouting.GetForwardVerticalWheel(scroller));
        Assert.Equal(ScrollBarVisibility.Auto, scroller.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Disabled, scroller.VerticalScrollBarVisibility);
        Assert.DoesNotContain(Descendants(rows), e => e is TextBox or Button); // No edit, View/navigation or mutable input.
        var grids = Descendants(rows).OfType<Grid>().Where(g => g.Name == "ContextRowGrid").ToArray();
        Assert.Equal(5, grids.Length);
        foreach (Grid grid in grids)
        {
            var row = (CalendarTradePresentation)grid.DataContext;
            var border = Assert.IsType<JournalTradeContextRow>(VisualTreeHelper.GetParent(grid));
            Assert.True(border.Focusable);
            Assert.Equal(row.AccessibleName, AutomationProperties.GetName(border));
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(border);
            Assert.NotNull(peer);
            Assert.Equal(row.AccessibleName, peer.GetName());
            Assert.Equal(AutomationControlType.Group, peer.GetAutomationControlType());
            Assert.Null(peer.GetPattern(PatternInterface.Invoke));
            Assert.Null(peer.GetPattern(PatternInterface.Value));
            Assert.Equal(header.ColumnDefinitions.Select(c => Math.Round(c.ActualWidth, 2)), grid.ColumnDefinitions.Select(c => Math.Round(c.ActualWidth, 2)));
            Assert.Equal(header.TranslatePoint(new Point(), table).X, grid.TranslatePoint(new Point(), table).X, 1);
            Assert.Contains(Descendants(grid).OfType<TextBlock>(), t => t.Text == row.AccountText && Equals(t.ToolTip, row.AccountText));
            Assert.Contains(Descendants(grid).OfType<TextBlock>(), t => t.Text == row.SetupText && Equals(t.ToolTip, row.SetupText));
            Assert.Contains(Descendants(grid).OfType<TextBlock>(), t => t.Text == row.MistakesText && Equals(t.ToolTip, row.MistakesText));
            TextBlock amount = Assert.Single(Descendants(grid).OfType<TextBlock>(), t => t.Text == row.AmountText);
            string brush = row.Amount > 0 ? "PtjSuccessBrush" : row.Amount < 0 ? "PtjDangerBrush" : "PtjTextPrimaryBrush";
            Assert.Equal(((SolidColorBrush)System.Windows.Application.Current.Resources[brush]).Color, ((SolidColorBrush)amount.Foreground).Color);
            TextBlock estimated = Assert.Single(Descendants(grid).OfType<TextBlock>(), t => t.Text == "Estimated");
            Assert.Equal(row.IsEstimated ? Visibility.Visible : Visibility.Collapsed, estimated.Visibility);
        }
        Assert.Contains(vm.Rows, r => r.SetupText.Contains("(inactive)") && r.MistakesText.Contains("Unavailable Mistake"));
        Assert.Contains(vm.Rows, r => r.AccountText.Contains("(inactive)") && r.InstrumentText.Contains("(inactive)"));
        Assert.Contains(vm.Rows, r => r.AccountText == "Unavailable Account" && r.InstrumentText == "Unavailable Instrument");
        Assert.Contains(vm.Rows, r => r.SetupText == "None assigned" && r.MistakesText == "None assigned");
        if (width < 600)
        {
            Assert.True(scroller.ScrollableWidth > 0);
            scroller.ScrollToRightEnd();
            Flush();
            Assert.Equal(scroller.ScrollableWidth, scroller.HorizontalOffset, 1);
        }
        else Assert.Equal(0, scroller.ScrollableWidth);
        scroller.ScrollToLeftEnd();
        Flush();
        var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        scroller.RaiseEvent(wheel);
        Flush();
        Assert.True(wheel.Handled);
        Assert.True(outer.VerticalOffset > 0);
        outer.ScrollToTop();
        Flush();
        Render(outer, theme, width, dpi);
        vm.Deactivate();
    }

    private static (JournalTradeContextView View, ScrollViewer Outer) Layout(JournalTradeContextViewModel vm, string theme, int width)
    {
        System.Windows.Application.Current.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
        var view = new JournalTradeContextView { DataContext = vm };
        var stack = new StackPanel();
        stack.Children.Add(view);
        stack.Children.Add(new Border { Height = 700 }); // The actual parent Journal page has a vertically scrolling editor.
        var outer = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        outer.SetResourceReference(Control.BackgroundProperty, "PtjBackgroundBrush");
        outer.Measure(new Size(width, 680));
        outer.Arrange(new Rect(0, 0, width, 680));
        outer.UpdateLayout();
        Flush();
        return (view, outer);
    }

    private static async Task<JournalTradeContextViewModel> CreateAsync(bool empty = false, bool fail = false)
    {
        DateOnly date = new(2026, 9, 9);
        var reader = new FakeTradingCalendarDayReader { Handler = (query, ct) => fail
            ? Task.FromException<TradingCalendarDayDetails>(new InvalidOperationException("Synthetic read failure"))
            : Task.FromResult(empty ? TradingCalendarDayDetails.Create(date, []) : Sample(date)) };
        var vm = new JournalTradeContextViewModel(reader, new FakeTradingAccountReader());
        await vm.ActivateAsync(date, null);
        return vm;
    }

    private static TradingCalendarDayDetails Sample(DateOnly date)
    {
        DateTimeOffset close = new(2026, 9, 9, 16, 0, 0, TimeSpan.Zero);
        TradeListItem Row(decimal? gross, decimal? net, string currency, int minute) => new(Guid.NewGuid(), Guid.NewGuid(),
            "Historical Account with a readable long name", Guid.NewGuid(), "MNQ", TradeDirection.Long, TradeStatus.Closed,
            close.AddHours(-1), close.AddMinutes(minute), 0m, 100m, 105m, net.HasValue ? 0m : null, gross, net, currency, 2m);
        TradeListItem[] rows = [Row(6.5m, 6.5m, "USD", 4), Row(-285m, null, "USD", 3), Row(0m, 0m, "USD", 2),
            Row(null, null, "USD", 1), Row(20m, 20m, "EUR", 0)];
        return TradingCalendarDayDetails.Create(date, rows) with
        {
            Classifications = new Dictionary<Guid, CalendarTradeClassification>
            {
                [rows[0].Id] = new(Guid.NewGuid(), "A long historical Setup whose full name must remain readable", false,
                    [new(Guid.NewGuid(), "Early entry", false), new(Guid.NewGuid(), null, null)])
            },
            References = rows.ToDictionary(r => r.Id, r => r.Id == rows[3].Id ? new CalendarTradeReferenceState(null, null) : new(false, false))
        };
    }

    private static void Flush() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void Render(Visual visual, string theme, int width, int dpi)
    {
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 680 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        if (Environment.GetEnvironmentVariable("PTJ_JOURNAL_RENDER_DIRECTORY") is { Length: > 0 } path)
        {
            Directory.CreateDirectory(path);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(path, $"journal-context-{theme}-{width}-{dpi}.png"));
            encoder.Save(file);
        }
    }
    private static readonly Lazy<Task> Host = new(() => IsolatedTestProcess.RunSuiteAsync(
        typeof(JournalTradeContextViewTests), "journal-context", "PTJ_JOURNAL_CONTEXT_TEST_HOST",
        TimeSpan.FromMinutes(2), caseHangTimeout: TimeSpan.FromSeconds(30)));
    private static Task OnSta(Action action) => Environment.GetEnvironmentVariable("PTJ_JOURNAL_CONTEXT_TEST_HOST") == "1"
        ? CalendarStaTest.RunAsync(action, nameof(ReadOnlyRowsAlignWrapScrollAndExposeCurrenciesAndReferenceStatesInBothThemes)) : Host.Value;
}
