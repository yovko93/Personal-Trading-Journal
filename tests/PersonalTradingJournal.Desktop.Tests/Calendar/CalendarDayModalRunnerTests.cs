using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Tests.Trades;
using PersonalTradingJournal.Desktop.Views.Calendar;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed partial class CalendarDayModalTests
{
    [Fact]
    public async Task NativeThemeResourcesReuseUnchangedStylesOnTheirOwningStaWithDistinctThemes()
    {
        await OnSta(() =>
        {
            var light = CalendarViewLayoutTests.SharedThemeResources("Light");
            var dark = CalendarViewLayoutTests.SharedThemeResources("Dark");
            Assert.Same(light, CalendarViewLayoutTests.SharedThemeResources("Light"));
            Assert.Same(dark, CalendarViewLayoutTests.SharedThemeResources("Dark"));
            Assert.NotSame(light, dark);
            Assert.NotSame(light["PtjButtonStyle"], dark["PtjButtonStyle"]);
            Assert.NotEqual(((SolidColorBrush)light["PtjBackgroundBrush"]).Color,
                ((SolidColorBrush)dark["PtjBackgroundBrush"]).Color);
            foreach (var resources in new[] { light, dark })
            {
                var style = Assert.IsType<Style>(resources["PtjButtonStyle"]);
                // WPF detaches a sealed Style from its dispatcher so it can be shared.
                // An unsealed style must still belong to this STA.
                Assert.True(style.CheckAccess());
                if (!style.IsSealed) Assert.Same(System.Windows.Threading.Dispatcher.CurrentDispatcher, style.Dispatcher);
                var first = new Button { Resources = resources, Style = style };
                var second = new Button { Resources = resources, Style = style };
                first.Measure(new Size(200, 60)); second.Measure(new Size(200, 60));
                Assert.Same(first.Style, second.Style);
                Assert.Same(resources["PtjBackgroundBrush"], CalendarViewLayoutTests.SharedThemeResources(
                    ReferenceEquals(resources, light) ? "Light" : "Dark")["PtjBackgroundBrush"]);
            }
            Task.Run(() => Assert.Throws<InvalidOperationException>(() =>
                CalendarViewLayoutTests.SharedThemeResources("Light"))).GetAwaiter().GetResult();
        });
    }

    [Theory]
    [InlineData("Light", 480, false)]
    [InlineData("Dark", 480, true)]
    [InlineData("Light", 1044, true)]
    [InlineData("Dark", 1044, false)]
    [InlineData("Light", 1280, true)]
    [InlineData("Dark", 1280, false)]
    [InlineData("Light", 1900, false)]
    [InlineData("Dark", 1900, true)]
    public async Task DetachedTableChecksWideAndNarrowLayoutRegardlessOfRunnerDesktop(string theme, int width, bool longName)
    {
        var date = new DateOnly(2026, 9, 5);
        var row = CalendarDayDetailsTests.Row(date, -285, null) with { TradingAccountName = longName
            ? "An unusually long historical account name that remains available in its tooltip" : "P 21" };
        var reader = new FakeTradingCalendarDayReader { Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [row], ct)) };
        var details = new FakeTradeDetailReader(); details.EnqueueResult(TradesViewModelTests.CreateEditableTradeDetail(row, null));
        var editor = TradesViewModelTests.CreateViewModel(tradeDetailReader: details);
        var vm = await CalendarSummaryFixture.CreateAsync(reader, editor);
        await vm.SelectDayCommand.ExecuteAsync(CalendarDayDetailsTests.Cell(vm, date));
        await vm.ViewTradeCommand.ExecuteAsync(row);
        await OnSta(() =>
        {
            // Measure/arrange the actual compiled view, without Show(), so a 1,024-pixel
            // hosted desktop cannot silently shrink the wide-layout coverage.
            var content = new CalendarDayDetailsView { DataContext = vm,
                Resources = CalendarViewLayoutTests.SharedThemeResources(theme), UseLayoutRounding = true };
            content.FontFamily = (FontFamily)content.Resources["PtjFontFamily"];
            TextOptions.SetTextFormattingMode(content, TextFormattingMode.Display);
            content.Measure(new Size(width, 900)); content.Arrange(new Rect(0, 0, width, 900));
            Pump(); content.UpdateLayout();
            var header = (Grid)content.FindName("DayTradesHeader");
            var scroll = (ScrollViewer)content.FindName("DayTradesScroller");
            var table = (StackPanel)content.FindName("DayTradesTable");
            var grid = Descendants(content).OfType<Grid>().Single(g => g.Name == "DayTradeRow");
            Assert.Equal(width, content.ActualWidth);
            Assert.True(scroll.ViewportWidth > width - 70, "The detached viewport must actually exercise the requested width.");
            AssertContentSizedAccount(header, [grid]);
            Assert.Equal(150, header.ColumnDefinitions[3].ActualWidth, 1);
            Assert.Equal(table.ActualWidth - 16, header.ColumnDefinitions.Sum(c => c.ActualWidth), 1);
            for (int i = 0; i < 9; i++) Assert.Equal(header.ColumnDefinitions[i].ActualWidth, grid.ColumnDefinitions[i].ActualWidth, 1);
            var action = grid.Children.OfType<Button>().Single();
            if (width >= 1280)
            {
                Assert.Equal(0, scroll.ScrollableWidth);
                Assert.Equal(scroll.ViewportWidth, table.ActualWidth, 1);
                Assert.InRange(scroll.ViewportWidth - action.TransformToAncestor(scroll).Transform(new Point()).X - action.ActualWidth, 7, 9);
                Assert.True(header.ColumnDefinitions[6].ActualWidth > 150);
                Assert.True(header.ColumnDefinitions[7].ActualWidth > 170);
            }
            else
            {
                Assert.True(scroll.ScrollableWidth > 0);
                scroll.ScrollToRightEnd(); Pump(); content.UpdateLayout();
                double left = action.TransformToAncestor(scroll).Transform(new Point()).X;
                Assert.True(left >= 0 && left + action.ActualWidth <= scroll.ViewportWidth + 1);
            }
            Assert.Single(vm.DayTrades, trade => trade.IsExpanded && trade.Trade.Id == row.Id);
            Assert.Same(editor, Descendants(content).OfType<CalendarInlineTradeView>().Single().DataContext);
        });
    }

    private static void AssertContentSizedAccount(Grid header, Grid[] rows)
    {
        var column = header.ColumnDefinitions[2];
        TextBlock[] names = [header.Children.OfType<TextBlock>().Single(t => Grid.GetColumn(t) == 2),
            .. rows.Select(r => r.Children.OfType<TextBlock>().Single(t => Grid.GetColumn(t) == 2))];
        // Display-mode text/ellipsis measurement changes with actual host DPI. A content-sized
        // column must match that measured demand, not always occupy its 160-DIP maximum.
        double expected = Math.Clamp(names.Max(t => t.DesiredSize.Width), column.MinWidth, column.MaxWidth);
        double pixel = 1 / VisualTreeHelper.GetDpi(header).DpiScaleX;
        Assert.InRange(column.ActualWidth, column.MinWidth - pixel, column.MaxWidth + pixel);
        Assert.True(Math.Abs(expected - column.ActualWidth) <= pixel,
            $"Account column must follow measured content: expected {expected:F2}, actual {column.ActualWidth:F2} DIPs.");
        Assert.All(rows, r => Assert.Equal(column.ActualWidth, r.ColumnDefinitions[2].ActualWidth, 1));
    }

    // Process-local simulation of a constrained hosted runner; never change the desktop display.
    private static void ApplyRunnerWindowConstraint(Window window)
    {
        if (Environment.GetEnvironmentVariable("PTJ_CALENDAR_TEST_MAX_WINDOW_WIDTH") is { } value)
        {
            double maximum = double.Parse(value, CultureInfo.InvariantCulture);
            if (!double.IsFinite(maximum) || maximum < 400)
                throw new ArgumentOutOfRangeException(nameof(value), "Test window limit must be at least 400 DIPs.");
            window.MaxWidth = maximum;
        }
    }

    private static void WriteLayoutDiagnostics(Window window, Grid header, ScrollViewer scroll, Grid[] rows, string scenario)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(header);
        string measurement = $"{scenario}; screen={SystemParameters.PrimaryScreenWidth:F2}x{SystemParameters.PrimaryScreenHeight:F2}; " +
            $"workArea={SystemParameters.WorkArea}; requested={window.Width:F2}; actual={window.ActualWidth:F2}; " +
            $"dpi={dpi.PixelsPerInchX:F2}/{dpi.PixelsPerInchY:F2}; viewport={scroll.ViewportWidth:F2}; " +
            $"extent={scroll.ExtentWidth:F2}; scrollable={scroll.ScrollableWidth:F2}; " +
            $"columns={string.Join(",", header.ColumnDefinitions.Select(c => c.ActualWidth.ToString("F2", CultureInfo.InvariantCulture)))}; " +
            $"Account desired={string.Join(",", rows.Select(r => r.Children.OfType<TextBlock>().Single(t => Grid.GetColumn(t) == 2).DesiredSize.Width))}";
        Console.WriteLine(measurement);
        if (Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "calendar-layout.txt"), measurement + Environment.NewLine);
        }
    }
}
