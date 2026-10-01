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
                Assert.All(saturdayAreas, area => Assert.True(area.ActualHeight >= 48));
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
