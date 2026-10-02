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
using PersonalTradingJournal.Desktop.Tests.TestDoubles;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed class CalendarViewLayoutTests
{
    [Theory]
    [InlineData("Light", 1100, 96)]
    [InlineData("Dark", 1100, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task FilterControlsUseSharedThemeKeyboardNamesBindingAndWrapAboveGrid(string theme, int width, int dpi)
    {
        CalendarViewModel vm = await CalendarSummaryFixture.CreateAsync();
        await OnSta(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
            // This component host has no Application.Resources. Flatten the unchanged production
            // resource declarations so detached Popup templates have their shared resources.
            DirectoryInfo? repository = new(AppContext.BaseDirectory);
            while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "PersonalTradingJournal.sln"))) repository = repository.Parent;
            string resourcePath = Path.Combine(repository!.FullName, "src/PersonalTradingJournal.Desktop/Resources");
            var declarations = new[] { $"Themes/{theme}Theme", "Typography", "Spacing", "Icons", "Controls" }
                .Select(file => System.Xml.Linq.XDocument.Load(Path.Combine(resourcePath, file + ".xaml")).Root!).ToArray();
            var combined = new System.Xml.Linq.XElement(declarations[^1]);
            combined.RemoveNodes();
            foreach (var declaration in declarations) combined.Add(declaration.Elements());
            combined.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns + "system", "clr-namespace:System;assembly=System.Runtime");
            foreach (string prefix in new[] { "converters", "validation" })
            {
                var attribute = combined.Attribute(System.Xml.Linq.XNamespace.Xmlns + prefix)!;
                string oldNamespace = attribute.Value;
                attribute.Value += ";assembly=PersonalTradingJournal.Desktop";
                foreach (var element in combined.Descendants().Where(e => e.Name.NamespaceName == oldNamespace))
                    element.Name = System.Xml.Linq.XNamespace.Get(attribute.Value) + element.Name.LocalName;
            }
            var resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(combined.ToString());
            var view = new CalendarView { DataContext = vm, Resources = resources };
            view.SetResourceReference(Control.BackgroundProperty, "PtjBackgroundBrush");
            void Layout()
            {
                view.Measure(new Size(width, 980));
                view.Arrange(new Rect(0, 0, width, 980));
                view.UpdateLayout();
            }
            Layout();
            var account = (ComboBox)view.FindName("CalendarAccountSelector");
            var currency = (ComboBox)view.FindName("CalendarCurrencySelector");
            var grid = (ScrollViewer)view.FindName("CalendarScroller");
            Assert.Equal("Calendar account", AutomationProperties.GetName(account));
            Assert.Equal("Calendar currency", AutomationProperties.GetName(currency));
            Assert.Same(vm.SelectedAccount, account.SelectedItem);
            Assert.Equal("All currencies", currency.SelectedItem);
            Assert.Same(view.Resources["PtjComboBoxStyle"], account.Style);
            Assert.Same(account.Style, currency.Style);
            Assert.All(new[] { account, currency }, combo =>
            {
                Assert.True(combo.Focusable);
                Assert.True(KeyboardNavigation.GetIsTabStop(combo));
                var origin = combo.TransformToAncestor(view).Transform(new Point());
                Assert.InRange(origin.X + combo.ActualWidth, 1, width);
                Assert.True(origin.Y + combo.ActualHeight <= grid.TransformToAncestor(view).Transform(new Point()).Y);
                Assert.Equal(((SolidColorBrush)view.Resources["PtjTextPrimaryBrush"]).Color, ((SolidColorBrush)combo.Foreground).Color);
            });
            double ay = account.TransformToAncestor(view).Transform(new Point()).Y;
            double cy = currency.TransformToAncestor(view).Transform(new Point()).Y;
            if (width == 480) Assert.True(cy > ay + account.ActualHeight);
            else Assert.Equal(ay, cy, 1);
            currency.SelectedItem = "EUR";
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            var frame = new System.Windows.Threading.DispatcherFrame();
            _ = vm.LoadTask.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)),
                CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            vm.LoadTask.GetAwaiter().GetResult();
            Layout();
            Assert.Equal("EUR", vm.SelectedCurrency);
            Assert.Equal("EUR", currency.SelectedItem);
            Assert.Single(vm.MonthData!.Currencies);
            Assert.All(vm.Weeks.SelectMany(w => w.Days).SelectMany(d => d.WeeklySummaries), s => Assert.Equal("EUR", s.Currency));
            if (width < 840) Assert.True(grid.ScrollableWidth > 0);
            var bitmap = new RenderTargetBitmap(width * dpi / 96, 980 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(view);
            if (Environment.GetEnvironmentVariable("PTJ_CALENDAR_RENDER_DIRECTORY") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(output, $"calendar-filters-{theme}-{width}-{dpi}.png"));
                encoder.Save(file);
            }
        });
    }

    [Theory]
    [InlineData("Light", 1100, 96, Key.Enter)]
    [InlineData("Dark", 1100, 96, Key.Space)]
    [InlineData("Light", 640, 240, Key.Space)]
    [InlineData("Dark", 640, 240, Key.Enter)]
    public async Task DaySelectionByKeyboardAndMouseRendersResponsiveDetailsAndExactViewTargets(string theme, int width, int dpi, Key key)
    {
        var reader = new FakeTradingCalendarDayReader
        {
            Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date,
                [CalendarDayDetailsTests.Row(q.Date, -285m, null), CalendarDayDetailsTests.Row(q.Date, 10m, 9m) with { Currency = "EUR" }], ct))
        };
        CalendarViewModel vm = await CalendarSummaryFixture.CreateAsync(reader);
        await OnSta(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
            var view = new CalendarView { DataContext = vm };
            view.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"/PersonalTradingJournal.Desktop;component/Resources/Themes/{theme}Theme.xaml", UriKind.Relative),
            });
            foreach (string name in new[] { "PtjButtonStyle", "PtjIconButtonStyle", "PtjSecondaryButtonStyle" })
                view.Resources[name] = new Style(typeof(Button));
            foreach (string name in new[] { "PtjSectionTitleTextStyle", "PtjCaptionTextStyle", "PtjBodyTextStyle" })
                view.Resources[name] = new Style(typeof(TextBlock));
            view.Resources["PtjComboBoxStyle"] = new Style(typeof(ComboBox));
            view.SetResourceReference(Control.BackgroundProperty, "PtjBackgroundBrush");
            void Layout()
            {
                var frame = new System.Windows.Threading.DispatcherFrame();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() => frame.Continue = false));
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                view.Measure(new Size(width, 980));
                view.Arrange(new Rect(0, 0, width, 980));
                view.UpdateLayout();
            }
            void FinishLoad()
            {
                var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                var frame = new System.Windows.Threading.DispatcherFrame();
                _ = vm.DayLoadTask.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)),
                    CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                vm.DayLoadTask.GetAwaiter().GetResult();
            }
            Layout();
            Border Cell(int month, int day) => Assert.Single(Descendants(view).OfType<Border>(), b => b.Focusable &&
                b.DataContext is CalendarDayCell cell && cell.Date == new DateOnly(2026, month, day));
            Border saturday = Cell(9, 5);
            Assert.True(KeyboardNavigation.GetIsTabStop(saturday));
            Assert.Contains("Enter or Space", AutomationProperties.GetHelpText(saturday));
            var keyEvent = new KeyEventArgs(Keyboard.PrimaryDevice, new TestPresentationSource(view), 0, key)
                { RoutedEvent = Keyboard.KeyDownEvent };
            saturday.RaiseEvent(keyEvent);
            Assert.True(keyEvent.Handled);
            FinishLoad();
            Layout();
            Assert.Equal(new DateOnly(2026, 9, 5), vm.SelectedDate);
            Assert.True(((CalendarDayCell)saturday.DataContext).IsSelected);
            Border panel = (Border)view.FindName("DayDetailsPanel");
            Assert.Equal(Visibility.Visible, panel.Visibility);
            Assert.True(panel.ActualWidth <= width);
            Assert.Contains(Descendants(panel).OfType<TextBlock>(), t => t.Text == "2 closed Trades");
            Assert.Contains(Descendants(panel).OfType<TextBlock>(), t => t.Text == "Estimated");
            Button[] views = Descendants(panel).OfType<Button>().Where(b => b.Content?.ToString() == "View").ToArray();
            Assert.Equal(2, views.Length);
            Assert.All(views, b =>
            {
                Assert.Same(vm.ViewTradeCommand, b.Command);
                Assert.Contains(vm.DayTrades, row => ReferenceEquals(row.Trade, b.CommandParameter));
                Assert.True(KeyboardNavigation.GetIsTabStop(b));
                Assert.True(b.ActualWidth >= 48);
            });
            var scroller = (ScrollViewer)view.FindName("CalendarPageScroller");
            scroller.ScrollToBottom();
            Layout();
            Assert.True(scroller.VerticalOffset > 0);
            var bitmap = new RenderTargetBitmap(width * dpi / 96, 980 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(view);
            if (Environment.GetEnvironmentVariable("PTJ_CALENDAR_RENDER_DIRECTORY") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(output, $"calendar-details-{theme}-{width}-{dpi}.png"));
                encoder.Save(file);
            }
            Border adjacent = Cell(8, 31);
            var mouse = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = UIElement.MouseLeftButtonUpEvent };
            adjacent.RaiseEvent(mouse);
            Assert.True(mouse.Handled);
            FinishLoad();
            Layout();
            Assert.Equal(new DateOnly(2026, 8, 31), vm.SelectedDate);
            Assert.False(((CalendarDayCell)saturday.DataContext).IsSelected);
            Assert.Equal(2, reader.Calls.Count);
        });
    }

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
            view.Resources["PtjComboBoxStyle"] = new Style(typeof(ComboBox));
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
                var vm = new CalendarViewModel(new EmptyReader(), new FixedClock(new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero)), new FakeTradingCalendarDayReader(), new FakeTradingAccountReader());
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
                view.Resources["PtjComboBoxStyle"] = new Style(typeof(ComboBox));
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

    private sealed class TestPresentationSource(Visual visual) : PresentationSource
    {
        public override Visual RootVisual { get; set; } = visual;
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }
}
