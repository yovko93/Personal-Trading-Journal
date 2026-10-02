using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.Views.Calendar;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed class CalendarViewLayoutTests
{
    [Theory]
    [InlineData("Light", 1100, 96)]
    [InlineData("Dark", 1100, 96)]
    [InlineData("Light", 640, 240)]
    [InlineData("Dark", 640, 240)]
    public async Task PopulatedGridRendersSignsAndCenteredWeeklyOnlySaturdayAcrossThemes(string theme, int width, int dpi)
    {
        CalendarViewModel vm = await CalendarSummaryFixture.CreateAsync();
        await OnSta(() =>
        {
            var view = new CalendarView { DataContext = vm };
            view.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"/PersonalTradingJournal.Desktop;component/Resources/Themes/{theme}Theme.xaml", UriKind.Relative),
            });
            foreach (string key in new[] { "PtjButtonStyle", "PtjIconButtonStyle", "PtjSecondaryButtonStyle" })
                view.Resources[key] = new Style(typeof(Button));
            foreach (string key in new[] { "PtjSectionTitleTextStyle", "PtjCaptionTextStyle", "PtjBodyTextStyle" })
                view.Resources[key] = new Style(typeof(TextBlock));
            view.SetResourceReference(Control.BackgroundProperty, "PtjBackgroundBrush");
            view.Measure(new Size(width, 980));
            view.Arrange(new Rect(0, 0, width, 980));
            view.UpdateLayout();
            var bitmap = new RenderTargetBitmap(width * dpi / 96, 980 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(view);
            if (Environment.GetEnvironmentVariable("PTJ_CALENDAR_RENDER_DIRECTORY") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(output, $"calendar-{theme}-{width}-{dpi}.png"));
                encoder.Save(file);
            }

            Border Cell(int month, int day) => Assert.Single(Descendants(view).OfType<Border>(), b => b.Focusable &&
                b.DataContext is CalendarDayCell c && c.Date == new DateOnly(2026, month, day));
            Color Brush(string key) => ((SolidColorBrush)view.Resources[key]).Color;
            Assert.Equal(Brush("PtjSuccessSurfaceBrush"), ((SolidColorBrush)Cell(9, 1).Background).Color);
            Assert.Equal(Brush("PtjDangerSurfaceBrush"), ((SolidColorBrush)Cell(9, 2).Background).Color);
            Assert.Equal(Brush("PtjSurfaceBrush"), ((SolidColorBrush)Cell(9, 3).Background).Color);
            Assert.Equal(Brush("PtjSurfaceBrush"), ((SolidColorBrush)Cell(8, 31).Background).Color);
            Assert.Equal(Brush("PtjSurfaceBrush"), ((SolidColorBrush)Cell(9, 5).Background).Color);
            Assert.Equal(Brush("PtjAccentBrush"), ((SolidColorBrush)Cell(9, 9).BorderBrush).Color);
            Assert.Equal(1d, Cell(9, 9).Opacity);
            foreach (TextBlock amount in Descendants(view).OfType<TextBlock>().Where(t =>
                         t.DataContext is CalendarPnlSummary summary && t.Text == summary.AmountText))
            {
                var summary = (CalendarPnlSummary)amount.DataContext;
                string key = summary.Amount > 0 ? "PtjSuccessBrush" : summary.Amount < 0 ? "PtjDangerBrush" : "PtjTextPrimaryBrush";
                Assert.Equal(Brush(key), ((SolidColorBrush)amount.Foreground).Color);
                Assert.Equal(TextWrapping.Wrap, amount.TextWrapping);
                Assert.True(amount.ActualHeight > 0);
            }
            Border saturday = Cell(9, 5);
            Border weekly = Assert.Single(Descendants(saturday).OfType<Border>(),
                b => AutomationProperties.GetName(b) == "Weekly summary area");
            ItemsControl dailyItems = Assert.Single(Descendants(saturday).OfType<ItemsControl>(),
                i => ReferenceEquals(i.ItemsSource, ((CalendarDayCell)saturday.DataContext).DailySummaries));
            Assert.Equal(Visibility.Collapsed, ((StackPanel)VisualTreeHelper.GetParent(dailyItems)).Visibility);
            Assert.Equal(0d, weekly.BorderThickness.Top);
            Assert.InRange(Math.Abs(weekly.TransformToAncestor(saturday).Transform(new Point(0, weekly.ActualHeight / 2)).Y
                - saturday.ActualHeight / 2), 0, 12);
            Assert.Contains(Descendants(saturday).OfType<TextBlock>(), t => t.Text == "Week 1");
            Assert.Contains(Descendants(weekly).OfType<TextBlock>(), t => t.Text == "-205.00 USD");
            Assert.Contains(Descendants(weekly).OfType<TextBlock>(), t => t.Text == "7 Trades");
            Border quietSaturday = Cell(9, 12);
            Border quietWeek = Assert.Single(Descendants(quietSaturday).OfType<Border>(),
                b => AutomationProperties.GetName(b) == "Weekly summary area");
            Assert.InRange(Math.Abs(quietWeek.TransformToAncestor(quietSaturday).Transform(new Point(0, quietWeek.ActualHeight / 2)).Y
                - quietSaturday.ActualHeight / 2), 0, 12);
            Assert.Contains(Descendants(quietWeek).OfType<TextBlock>(), t => t.Text == "Week 2");
            Assert.Contains(Descendants(quietWeek).OfType<TextBlock>(), t => t.Text == "3 Trades");
            Assert.Contains(Descendants(Cell(9, 7)).OfType<TextBlock>(), t => t.Text == "— USD");
            Assert.Contains(Descendants(Cell(9, 3)).OfType<TextBlock>(), t => t.Text == "1 Trade");
            Assert.DoesNotContain(Descendants(Cell(9, 10)).OfType<TextBlock>(), t => t.DataContext is CalendarPnlSummary);
            Assert.Equal(2, Descendants(Cell(9, 8)).OfType<TextBlock>().Count(t =>
                t.DataContext is CalendarPnlSummary s && t.Text == s.AmountText));
            var scroll = (ScrollViewer)view.FindName("CalendarScroller");
            if (width < 840) Assert.True(scroll.ScrollableWidth > 0);
        });
    }

    [Theory]
    [InlineData("Dark", 2021, 2, 4, 960, 96)]
    [InlineData("Light", 2026, 9, 5, 960, 96)]
    [InlineData("Dark", 2026, 8, 6, 640, 240)]
    [InlineData("Light", 2026, 8, 6, 640, 240)]
    public async Task CompiledGridKeepsSevenAlignedColumnsAndFocusableDaysAcrossThemesAndSizes(
        string theme, int year, int month, int rows, int width, int dpi)
    {
        await OnSta(() =>
        {
                var vm = new CalendarViewModel(new EmptyReader(), new FixedClock(new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero)));
                while (vm.SelectedMonth.Year * 12 + vm.SelectedMonth.Month < year * 12 + month)
                    vm.NextCommand.Execute(null);
                while (vm.SelectedMonth.Year * 12 + vm.SelectedMonth.Month > year * 12 + month)
                    vm.PreviousCommand.Execute(null);
                var view = new CalendarView { DataContext = vm };
                foreach (string file in new[] { $"Themes/{theme}Theme" })
                    view.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri($"/PersonalTradingJournal.Desktop;component/Resources/{file}.xaml", UriKind.Relative),
                    });
                foreach (string key in new[] { "PtjButtonStyle", "PtjIconButtonStyle", "PtjSecondaryButtonStyle" })
                    view.Resources[key] = new Style(typeof(Button));
                foreach (string key in new[] { "PtjSectionTitleTextStyle", "PtjCaptionTextStyle", "PtjBodyTextStyle" })
                    view.Resources[key] = new Style(typeof(TextBlock));
                view.Measure(new Size(width, 760));
                view.Arrange(new Rect(0, 0, width, 760));
                view.UpdateLayout();
                new RenderTargetBitmap(width * dpi / 96, 760 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32).Render(view);

                var dayCells = Descendants(view).OfType<Border>()
                    .Where(b => b.DataContext is CalendarDayCell && b.Focusable)
                    .ToArray();
                Assert.Equal(rows * 7, dayCells.Length);
                Assert.All(dayCells, cell =>
                {
                    Assert.True(cell.Focusable);
                    Assert.True(KeyboardNavigation.GetIsTabStop(cell));
                    Assert.True(cell.ActualWidth >= 90);
                    Assert.True(cell.ActualHeight >= 112);
                });
                var columns = Descendants(view).OfType<UniformGrid>().ToArray();
                Assert.Equal(rows + 1, columns.Length); // One weekday header and one grid per week.
                Assert.All(columns, column => Assert.Equal(7, column.Columns));
                Assert.All(columns.Skip(1), column => Assert.Equal(columns[0].ActualWidth, column.ActualWidth, 1));
                var saturdayAreas = Descendants(view).OfType<Border>()
                    .Where(b => AutomationProperties.GetName(b) == "Weekly summary area" && b.Visibility == Visibility.Visible)
                    .ToArray();
                Assert.Equal(rows, saturdayAreas.Length);
                Assert.All(saturdayAreas, area => Assert.True(area.ActualHeight > 0));
                if (dayCells.Any(cell => ((CalendarDayCell)cell.DataContext).IsToday))
                {
                    Border today = Assert.Single(dayCells, cell => ((CalendarDayCell)cell.DataContext).IsToday);
                    Assert.Equal(((SolidColorBrush)view.Resources["PtjAccentBrush"]).Color,
                        ((SolidColorBrush)today.BorderBrush).Color);
                }
                var scroller = Assert.IsType<ScrollViewer>(view.FindName("CalendarScroller"));
                if (width < 700) Assert.True(scroller.ExtentWidth > scroller.ViewportWidth);
                Assert.Equal("Previous calendar month", AutomationProperties.GetName(
                    Assert.Single(Descendants(view).OfType<Button>(), b => AutomationProperties.GetName(b) == "Previous calendar month")));
                Assert.Contains(Descendants(view).OfType<TextBlock>(), t => t.Text == "Saturday");
        });
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
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { action(); done.SetResult(); } catch (Exception e) { done.SetException(e); } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return done.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    private sealed class EmptyReader : ITradingCalendarReader
    {
        public Task<TradingCalendarMonth> GetAsync(TradingCalendarQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TradingCalendarMonth(query.MonthStart, query.GridStart, query.GridEnd, [], []));
    }

    private sealed class FixedClock(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }
}
