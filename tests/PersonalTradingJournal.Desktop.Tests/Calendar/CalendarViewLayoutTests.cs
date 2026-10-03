using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Desktop.Interactions;
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
    public async Task OutcomeHoverRestoresBackgroundWithoutChangingSelectionMarkersOrTooltipTargets(string theme, int width, int dpi)
    {
        CalendarViewModel vm = await CalendarSummaryFixture.CreateAsync();
        await vm.SelectDayCommand.ExecuteAsync(CalendarDayDetailsTests.Cell(vm, new(2026, 9, 9)));
        await OnSta(() =>
        {
            var view = new CalendarView { DataContext = vm, Resources = SharedThemeResources(theme) };
            view.SetResourceReference(Control.BackgroundProperty, "PtjBackgroundBrush");
            view.Measure(new Size(width, 800));
            view.Arrange(new Rect(0, 0, width, 800));
            view.UpdateLayout();
            // Drive WPF's read-only hover state deterministically in the detached component.
            // Raising MouseEnter alone does not update IsMouseOver; this is not live pointer evidence.
            var key = (DependencyPropertyKey)typeof(UIElement).GetField("IsMouseOverPropertyKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
            var focusKey = (DependencyPropertyKey)typeof(UIElement).GetField("IsKeyboardFocusedPropertyKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
            Color Brush(string name) => ((SolidColorBrush)view.Resources[name]).Color;
            foreach (CalendarDayHost cell in Descendants(view).OfType<CalendarDayHost>())
            {
                var day = (CalendarDayCell)cell.DataContext;
                Color original = ((SolidColorBrush)cell.Background).Color;
                Brush outline = cell.BorderBrush;
                Thickness thickness = cell.BorderThickness;
                Border marker = Assert.Single(Descendants(cell).OfType<Border>(), b => b.Name == "DateFocusMarker");
                Border selection = Assert.Single(Descendants(cell).OfType<Border>(), b => b.Name == "DateSelectionMarker");
                Border today = Assert.Single(Descendants(cell).OfType<Border>(), b => b.Name == "TodayMarker");
                Brush markerOutline = selection.BorderBrush, todayBackground = today.Background;
                string hoverKey = day.DailyOutcome == Desktop.Converters.PnLOutcome.Positive ? "PtjCalendarSuccessHoverBrush"
                    : day.DailyOutcome == Desktop.Converters.PnLOutcome.Negative ? "PtjCalendarDangerHoverBrush" : "PtjCalendarHoverBrush";
                cell.SetValue(key, true);
                view.UpdateLayout();
                Assert.Equal(Brush(hoverKey), ((SolidColorBrush)cell.Background).Color);
                Assert.NotEqual(original, ((SolidColorBrush)cell.Background).Color);
                Assert.Same(outline, cell.BorderBrush);
                Assert.Equal(thickness, cell.BorderThickness);
                Assert.Same(markerOutline, selection.BorderBrush);
                Assert.Same(todayBackground, today.Background);
                if (day.IsToday)
                {
                    cell.SetValue(focusKey, true);
                    view.UpdateLayout();
                    Assert.Equal(Brush("PtjTextPrimaryBrush"), ((SolidColorBrush)marker.BorderBrush).Color);
                    Assert.Equal(Brush("PtjAccentBrush"), ((SolidColorBrush)selection.BorderBrush).Color);
                    Assert.Equal(Brush("PtjCalendarTodayBrush"), ((SolidColorBrush)today.Background).Color);
                }
                Assert.Equal(day.IsSelected ? new Thickness(2) : new Thickness(0, 0, 1, 1), thickness);
                Assert.Equal(day.DateTooltip, marker.ToolTip);
                Assert.Null(cell.ToolTip);
                Assert.All(Descendants(cell).OfType<FrameworkElement>().Where(e => e.ToolTip is not null), e => Assert.Same(marker, e));
                // Actual hit targets in the compiled tree: amount, count, weekly text and empty space
                // still resolve to the hovered host, but none can find a tooltip on its ancestors.
                var points = Descendants(cell).OfType<TextBlock>().Where(t => t.IsVisible && t.ActualWidth > 0 &&
                        (t.DataContext is CalendarPnlSummary || t.Text.StartsWith("Week", StringComparison.Ordinal)))
                    .Select(t => t.TransformToAncestor(cell).Transform(new Point(t.ActualWidth / 2, t.ActualHeight / 2)))
                    .Append(new Point(12, cell.ActualHeight - 12));
                foreach (Point point in points)
                {
                    DependencyObject? hit = VisualTreeHelper.HitTest(cell, point)?.VisualHit;
                    Assert.NotNull(hit);
                    bool foundCell = false;
                    for (DependencyObject? target = hit; target is not null; target = VisualTreeHelper.GetParent(target))
                    {
                        if (target is FrameworkElement element) Assert.Null(element.ToolTip);
                        if (ReferenceEquals(target, cell)) foundCell = true;
                    }
                    Assert.True(foundCell);
                }
                TextBlock number = Assert.Single(Descendants(today).OfType<TextBlock>());
                if (!day.IsInDisplayedMonth)
                    Assert.Equal(Brush("PtjTextMutedBrush"), ((SolidColorBrush)number.Foreground).Color);
                if (day.IsSaturday) Assert.Equal(Brush("PtjCalendarHoverBrush"), ((SolidColorBrush)cell.Background).Color);
                if (day.IsToday || day.Date == new DateOnly(2026, 9, 1) || day.Date == new DateOnly(2026, 9, 5))
                {
                    var bitmap = new RenderTargetBitmap(width * dpi / 96, 800 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
                    bitmap.Render(view);
                    if (Environment.GetEnvironmentVariable("PTJ_CALENDAR_RENDER_DIRECTORY") is { Length: > 0 } output)
                    {
                        Directory.CreateDirectory(output);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var file = File.Create(Path.Combine(output, $"calendar-hover-{theme}-{width}-{dpi}-{day.Date.Day}.png"));
                        encoder.Save(file);
                    }
                }
                cell.SetValue(key, false);
                if (day.IsToday) cell.SetValue(focusKey, false);
                view.UpdateLayout();
                Assert.Equal(original, ((SolidColorBrush)cell.Background).Color);
            }
            var scroller = (ScrollViewer)view.FindName("CalendarScroller");
            if (width < 840)
            {
                scroller.ScrollToHorizontalOffset(100);
                view.UpdateLayout();
                Assert.Equal(100, scroller.HorizontalOffset);
                CalendarDayHost selected = Assert.Single(Descendants(view).OfType<CalendarDayHost>(), c => ((CalendarDayCell)c.DataContext).IsSelected);
                selected.SetValue(key, true);
                view.UpdateLayout();
                Assert.Equal(Brush("PtjCalendarDangerHoverBrush"), ((SolidColorBrush)selected.Background).Color);
                selected.SetValue(key, false);
            }
            // Hover shades retain normal-text contrast in both palettes, including neutral dates.
            foreach (var (text, background) in new[] { ("PtjSuccessBrush", "PtjCalendarSuccessHoverBrush"),
                ("PtjDangerBrush", "PtjCalendarDangerHoverBrush"), ("PtjTextSecondaryBrush", "PtjCalendarHoverBrush"),
                ("PtjTextMutedBrush", "PtjCalendarHoverBrush") })
            {
                double a = Luminance(Brush(text)), b = Luminance(Brush(background));
                Assert.True((Math.Max(a, b) + .05) / (Math.Min(a, b) + .05) >= 4.5, $"{theme}: {text}/{background}");
            }
        });
        static double Luminance(Color color)
        {
            static double Channel(byte value) => value / 255d <= .04045 ? value / 255d / 12.92 : Math.Pow((value / 255d + .055) / 1.055, 2.4);
            return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        }
    }

    [Theory]
    [InlineData("Light", 1100, 96, 9)]
    [InlineData("Dark", 1100, 96, 9)]
    [InlineData("Light", 480, 240, 9)]
    [InlineData("Dark", 480, 240, 9)]
    [InlineData("Light", 1100, 96, 5)]
    [InlineData("Dark", 1100, 96, 5)]
    [InlineData("Light", 480, 240, 5)]
    [InlineData("Dark", 480, 240, 5)]
    public async Task MonthlyHeaderAndCenteredMarkersKeepTodaySelectionAndFocusDistinct(string theme, int width, int dpi, int selectedDay)
    {
        CalendarViewModel vm = await CalendarSummaryFixture.CreateAsync();
        await vm.SelectDayCommand.ExecuteAsync(CalendarDayDetailsTests.Cell(vm, new(2026, 9, selectedDay)));
        await OnSta(() =>
        {
            var view = new CalendarView { DataContext = vm, Resources = SharedThemeResources(theme) };
            view.SetResourceReference(Control.BackgroundProperty, "PtjBackgroundBrush");
            view.Measure(new Size(width, 800));
            view.Arrange(new Rect(0, 0, width, 800));
            view.UpdateLayout();
            Color Brush(string key) => ((SolidColorBrush)view.Resources[key]).Color;
            var header = (StackPanel)view.FindName("MonthlyPnlHeader");
            Assert.Equal(0, Grid.GetRow(header));
            TextBlock title = Assert.Single(Descendants(header).OfType<TextBlock>(), t => t.Text == "Monthly P/L");
            Assert.Equal(TextAlignment.Center, title.TextAlignment);
            Assert.Equal(width / 2d, title.TransformToAncestor(view).Transform(new Point(title.ActualWidth / 2, 0)).X, 1);
            foreach (TextBlock amount in Descendants(header).OfType<TextBlock>().Where(t => t.DataContext is CalendarMonthlyPnlSummary))
            {
                var summary = (CalendarMonthlyPnlSummary)amount.DataContext;
                Assert.Equal(summary.AmountText, amount.Text);
                Assert.Equal(summary.Description, AutomationProperties.GetName(amount));
                Assert.Equal(summary.Description, amount.ToolTip);
                string key = summary.Amount > 0 ? "PtjSuccessBrush" : summary.Amount < 0 ? "PtjDangerBrush" : "PtjTextPrimaryBrush";
                Assert.Equal(Brush(key), ((SolidColorBrush)amount.Foreground).Color);
            }
            foreach (CalendarDayHost cell in Descendants(view).OfType<CalendarDayHost>())
            {
                var day = (CalendarDayCell)cell.DataContext;
                Border marker = Assert.Single(Descendants(cell).OfType<Border>(), b => b.Name == "DateSelectionMarker");
                Border today = Assert.Single(Descendants(marker).OfType<Border>(), b => b.Name == "TodayMarker");
                TextBlock number = Assert.Single(Descendants(today).OfType<TextBlock>());
                Assert.Equal(day.DayNumber, number.Text);
                Assert.Equal(HorizontalAlignment.Center, marker.HorizontalAlignment);
                Assert.InRange(Math.Abs(marker.TransformToAncestor(cell).Transform(new Point(marker.ActualWidth / 2, 0)).X
                    - cell.ActualWidth / 2), 0, 1);
                Assert.InRange(marker.TransformToAncestor(cell).Transform(new Point()).Y, 0, 14);
                Assert.Equal(new CornerRadius(12), today.CornerRadius);
                Assert.Equal(day.IsToday ? Brush("PtjCalendarTodayBrush") : Colors.Transparent,
                    ((SolidColorBrush)today.Background).Color);
                Assert.Equal(day.IsSelected ? Brush("PtjAccentBrush") : Colors.Transparent,
                    ((SolidColorBrush)marker.BorderBrush).Color);
                Assert.Equal(day.IsToday ? Brush("PtjOnCalendarTodayBrush") : day.IsInDisplayedMonth
                    ? Brush("PtjTextPrimaryBrush") : Brush("PtjTextMutedBrush"), ((SolidColorBrush)number.Foreground).Color);
                Assert.Equal(day.IsSelected ? new Thickness(2) : new Thickness(0, 0, 1, 1), cell.BorderThickness);
                Assert.Equal(Brush(day.IsSelected ? "PtjAccentBrush" : "PtjBorderBrush"), ((SolidColorBrush)cell.BorderBrush).Color);
                // Focus remains a separate outer marker ring, even when a click also focuses a selected cell.
                Border focusMarker = Assert.Single(Descendants(cell).OfType<Border>(), b => b.Name == "DateFocusMarker");
                Assert.Equal(new Thickness(1), focusMarker.BorderThickness);
                Assert.True(focusMarker.ActualWidth > marker.ActualWidth);
                var focus = Assert.Single(focusMarker.Style.Triggers.OfType<DataTrigger>());
                Assert.Equal("True", focus.Value.ToString());
                var focusBinding = Assert.IsType<System.Windows.Data.Binding>(focus.Binding);
                Assert.Equal("IsKeyboardFocused", focusBinding.Path.Path);
                Assert.Equal(typeof(CalendarDayHost), focusBinding.RelativeSource.AncestorType);
                Assert.Contains(focus.Setters.OfType<Setter>(), s => s.Property == Border.BorderBrushProperty &&
                    s.Value is DynamicResourceExtension resource && Equals(resource.ResourceKey, "PtjTextPrimaryBrush"));
                Assert.DoesNotContain(cell.Style.Triggers.OfType<Trigger>(), t => t.Property == UIElement.IsKeyboardFocusedProperty);
            }
            var scroll = (ScrollViewer)view.FindName("CalendarScroller");
            if (width < 840)
            {
                scroll.ScrollToHorizontalOffset(100);
                view.UpdateLayout();
                Assert.Equal(100, scroll.HorizontalOffset);
            }
            var bitmap = new RenderTargetBitmap(width * dpi / 96, 800 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(view);
            if (Environment.GetEnvironmentVariable("PTJ_CALENDAR_RENDER_DIRECTORY") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(output, $"calendar-markers-{theme}-{width}-{dpi}-{selectedDay}.png"));
                encoder.Save(file);
            }
        });
    }

    [Theory]
    [InlineData("Light", 1100, 96)]
    [InlineData("Dark", 1100, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task HitTestingScopesDateHoverToMarkerNotDailyWeeklyOrEmptyCellAreas(string theme, int width, int dpi)
    {
        CalendarViewModel vm = await CalendarSummaryFixture.CreateAsync();
        await OnSta(() =>
        {
            var view = new CalendarView { DataContext = vm, Resources = SharedThemeResources(theme) };
            view.SetResourceReference(Control.BackgroundProperty, "PtjBackgroundBrush");
            view.Measure(new Size(width, 980));
            view.Arrange(new Rect(0, 0, width, 980));
            view.UpdateLayout();
            foreach (CalendarDayHost cell in Descendants(view).OfType<CalendarDayHost>())
            {
                var day = (CalendarDayCell)cell.DataContext;
                Border marker = Assert.Single(Descendants(cell).OfType<Border>(), b => b.Name == "DateFocusMarker");
                Assert.Null(cell.ToolTip);
                Assert.Equal(Colors.Transparent, ((SolidColorBrush)marker.Background).Color);
                Assert.Equal(day.DateTooltip, marker.ToolTip);
                Assert.Equal(day.DailyAccessibleDescription, AutomationProperties.GetHelpText(marker));
                Assert.Contains(day.DailyAccessibleDescription, AutomationProperties.GetHelpText(cell));
                Assert.DoesNotContain("Estimated", day.DateTooltip, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("commission", day.DateTooltip, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("fees", day.DateTooltip, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("coverage", day.DateTooltip, StringComparison.OrdinalIgnoreCase);
                // Use the compiled visual tree's real hit targets, including transparent marker padding.
                foreach (Point point in new[] { new Point(3, 18), new Point(18, 18), new Point(32, 18) })
                    Assert.Same(marker, TooltipOwnerAt(cell, marker.TransformToAncestor(cell).Transform(point)));
                Assert.Null(TooltipOwnerAt(cell, new Point(12, cell.ActualHeight - 12)));
                foreach (TextBlock text in Descendants(cell).OfType<TextBlock>().Where(t =>
                    t.IsVisible && t.ActualWidth > 0 && (t.DataContext is CalendarPnlSummary || t.Text.StartsWith("Week", StringComparison.Ordinal) || t.Text == "0 Trades")))
                {
                    Assert.Null(TooltipOwnerAt(cell, text.TransformToAncestor(cell).Transform(new Point(text.ActualWidth / 2, text.ActualHeight / 2))));
                    Assert.Null(text.ToolTip);
                }
                Assert.All(Descendants(cell).OfType<FrameworkElement>().Where(e => e.ToolTip is not null), e => Assert.Same(marker, e));
            }
            var estimated = Assert.Single(Descendants(view).OfType<CalendarDayHost>(), c => ((CalendarDayCell)c.DataContext).Date == new DateOnly(2026, 9, 4));
            Assert.Contains("commission/fees unknown", AutomationProperties.GetHelpText(estimated));
            var saturday = Assert.Single(Descendants(view).OfType<CalendarDayHost>(), c => ((CalendarDayCell)c.DataContext).Date == new DateOnly(2026, 9, 5));
            Assert.Contains("7.00 USD; 1 Trade", ((CalendarDayCell)saturday.DataContext).DateTooltip);
            Assert.DoesNotContain("-205", ((CalendarDayCell)saturday.DataContext).DateTooltip);
            Assert.Contains(Descendants(saturday).OfType<TextBlock>(), t => t.Text == "-205.00 USD");
            Assert.Contains(Descendants(saturday).OfType<TextBlock>(), t => t.Text == "7 Trades");
            var scroller = (ScrollViewer)view.FindName("CalendarScroller");
            if (width < 840)
            {
                scroller.ScrollToHorizontalOffset(100);
                view.UpdateLayout();
                Assert.Equal(100, scroller.HorizontalOffset);
                Border marker = Assert.Single(Descendants(estimated).OfType<Border>(), b => b.Name == "DateFocusMarker");
                Assert.Same(marker, TooltipOwnerAt(estimated, marker.TransformToAncestor(estimated).Transform(new Point(18, 18))));
            }
            var bitmap = new RenderTargetBitmap(width * dpi / 96, 980 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(view);
        });

        static FrameworkElement? TooltipOwnerAt(CalendarDayHost cell, Point position)
        {
            DependencyObject? hit = VisualTreeHelper.HitTest(cell, position)?.VisualHit;
            Assert.NotNull(hit);
            // WPF can find a tooltip on an ancestor even when the hit child has no tooltip.
            for (DependencyObject? current = hit; current is not null; current = VisualTreeHelper.GetParent(current))
                if (current is FrameworkElement element && ToolTipService.GetToolTip(element) is not null && ToolTipService.GetIsEnabled(element)) return element;
            return null;
        }
    }

    [Theory]
    [InlineData("Light", 1100, 96)]
    [InlineData("Dark", 1100, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task RefreshPreservesDateContainersAndAutomationAndMonthWheelRouting(string theme, int width, int dpi)
    {
        var reader = new FakeTradingCalendarDayReader
        {
            Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date,
                [CalendarDayDetailsTests.Row(q.Date, -285m, null)], ct)),
        };
        CalendarViewModel vm = await CalendarSummaryFixture.CreateAsync(reader);
        await OnSta(() =>
        {
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
            var view = new CalendarView { DataContext = vm, Resources = SharedThemeResources(theme) };
            view.SetResourceReference(Control.BackgroundProperty, "PtjBackgroundBrush");
            void Layout()
            {
                var frame = new System.Windows.Threading.DispatcherFrame();
                dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => frame.Continue = false));
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                view.Measure(new Size(width, 600));
                view.Arrange(new Rect(0, 0, width, 600));
                view.UpdateLayout();
            }
            void Finish(Task task)
            {
                var frame = new System.Windows.Threading.DispatcherFrame();
                _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)),
                    CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                task.GetAwaiter().GetResult();
            }
            Layout();
            CalendarDayHost cell = Assert.Single(Descendants(view).OfType<CalendarDayHost>(),
                b => ((CalendarDayCell)b.DataContext).Date == new DateOnly(2026, 9, 5));
            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(cell)!;
            Assert.Equal(System.Windows.Automation.Peers.AutomationControlType.Button, peer.GetAutomationControlType());
            Assert.Contains("Monday", peer.GetName());
            Assert.Contains("Sunday", peer.GetName());
            Assert.Contains("Enter or Space", peer.GetHelpText());
            var invoke = Assert.IsAssignableFrom<System.Windows.Automation.Provider.IInvokeProvider>(
                peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke));
            invoke.Invoke();
            Layout();
            Finish(vm.DayLoadTask);
            Layout();
            Assert.Equal(new DateOnly(2026, 9, 5), vm.SelectedDate);
            Assert.Contains("Selected", peer.GetItemStatus());
            Assert.Contains("Summary loaded", peer.GetItemStatus());
            Button refresh = Assert.Single(Descendants(view).OfType<Button>(), b => ReferenceEquals(b.Command, vm.RefreshCommand));
            Assert.True(refresh.Focusable);
            Assert.Contains("selected day", AutomationProperties.GetName(refresh));
            Finish(vm.RefreshCommand.ExecuteAsync(null));
            Layout();
            Assert.Same(cell, Assert.Single(Descendants(view).OfType<CalendarDayHost>(), b => ReferenceEquals(b.DataContext, cell.DataContext)));
            Assert.Contains("selected", peer.GetName());
            Assert.All(Descendants(view).OfType<ComboBox>(), c => Assert.True(c.Focusable));
            var page = (ScrollViewer)view.FindName("CalendarPageScroller");
            var horizontal = (ScrollViewer)view.FindName("CalendarScroller");
            double start = Math.Min(120, page.ScrollableHeight / 2);
            page.ScrollToVerticalOffset(start);
            Layout();
            Wheel(page, UIElement.MouseWheelEvent, -120);
            Layout();
            double step = page.VerticalOffset - start;
            Assert.True(step > 0);
            foreach (UIElement surface in new UIElement[] { cell, cell.Child, horizontal })
            {
                page.ScrollToVerticalOffset(start);
                Layout();
                MouseWheelEventArgs args = Wheel(surface, UIElement.PreviewMouseWheelEvent, -120);
                Layout();
                Assert.True(args.Handled);
                Assert.Equal(start + step, page.VerticalOffset);
                Assert.Equal(0, horizontal.HorizontalOffset);
                page.ScrollToVerticalOffset(start);
                Layout();
                Wheel(surface, UIElement.PreviewMouseWheelEvent, 120);
                Layout();
                Assert.Equal(Math.Max(0, start - step), page.VerticalOffset);
            }
            page.ScrollToTop();
            Layout();
            Assert.False(Wheel(cell, UIElement.PreviewMouseWheelEvent, 120).Handled);
            page.ScrollToBottom();
            Layout();
            Assert.False(Wheel(cell, UIElement.PreviewMouseWheelEvent, -120).Handled);
            if (width < 840)
            {
                horizontal.ScrollToHorizontalOffset(100);
                Layout();
                Assert.Equal(100, horizontal.HorizontalOffset);
                page.ScrollToVerticalOffset(start);
                Layout();
                Wheel(cell, UIElement.PreviewMouseWheelEvent, -120);
                Layout();
                Assert.Equal(100, horizontal.HorizontalOffset);
            }
            page.ScrollToBottom();
            Layout();
            var bitmap = new RenderTargetBitmap(width * dpi / 96, 600 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(view);
            if (Environment.GetEnvironmentVariable("PTJ_CALENDAR_RENDER_DIRECTORY") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(output, $"calendar-refresh-{theme}-{width}-{dpi}.png"));
                encoder.Save(file);
            }
        });

        static MouseWheelEventArgs Wheel(UIElement target, RoutedEvent routedEvent, int delta)
        {
            var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, delta) { RoutedEvent = routedEvent };
            target.RaiseEvent(args);
            return args;
        }
    }

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
            var view = new CalendarView { DataContext = vm, Resources = SharedThemeResources(theme) };
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
            Assert.Null(view.FindName("DayDetailsPanel"));
            var panel = new CalendarDayDetailsView { DataContext = vm, Resources = SharedThemeResources(theme) };
            panel.Measure(new Size(width, 650));
            panel.Arrange(new Rect(0, 0, width, 650));
            panel.UpdateLayout();
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
            var scroller = (ScrollViewer)panel.FindName("DayContentScroller");
            ScrollViewer table = Assert.Single(Descendants(panel).OfType<ScrollViewer>(), s =>
                NestedTableWheelRouting.GetForwardVerticalWheel(s) && s.Content is StackPanel);
            foreach (UIElement surface in new UIElement[] { views[0], table,
                Descendants(panel).OfType<Border>().First(b => b.Tag is CalendarDayPerformancePoint) })
            {
                scroller.ScrollToTop();
                panel.UpdateLayout();
                var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120)
                    { RoutedEvent = UIElement.PreviewMouseWheelEvent };
                surface.RaiseEvent(wheel);
                Assert.True(wheel.Handled);
                panel.UpdateLayout();
                Assert.True(scroller.VerticalOffset > 0);
                Assert.Equal(0, table.VerticalOffset);
            }
            if (table.ScrollableWidth > 0)
            {
                table.ScrollToHorizontalOffset(100);
                panel.UpdateLayout();
                Assert.True(table.HorizontalOffset > 0);
            }
            scroller.ScrollToBottom();
            panel.UpdateLayout();
            Assert.True(scroller.VerticalOffset > 0);
            var bitmap = new RenderTargetBitmap(width * dpi / 96, 650 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(panel);
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
                { RoutedEvent = Mouse.MouseUpEvent };
            adjacent.Child.RaiseEvent(mouse); // Bubble from the cell body, not only the marker.
            Assert.True(mouse.Handled);
            FinishLoad();
            Layout();
            Assert.Equal(new DateOnly(2026, 8, 31), vm.SelectedDate);
            Assert.False(((CalendarDayCell)saturday.DataContext).IsSelected);
            Assert.Equal(2, reader.Calls.Count);
            TextBlock weekLabel = Assert.Single(Descendants(saturday).OfType<TextBlock>(), t => t.Text == "Week 1");
            var weeklyClick = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = Mouse.MouseUpEvent };
            weekLabel.RaiseEvent(weeklyClick);
            FinishLoad();
            Layout();
            Assert.True(weeklyClick.Handled);
            Assert.Equal(new DateOnly(2026, 9, 5), vm.SelectedDate);
            TextBlock dailyAmount = Assert.Single(Descendants(Cell(9, 4)).OfType<TextBlock>(),
                t => t.DataContext is CalendarPnlSummary s && t.Text == s.AmountText);
            var amountClick = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = Mouse.MouseUpEvent };
            dailyAmount.RaiseEvent(amountClick);
            FinishLoad();
            Layout();
            Assert.True(amountClick.Handled);
            Assert.Equal(new DateOnly(2026, 9, 4), vm.SelectedDate);
            Assert.Equal(4, reader.Calls.Count);
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
            Assert.Equal(Brush("PtjBorderBrush"), ((SolidColorBrush)Cell(9, 9).BorderBrush).Color);
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
                - saturday.ActualHeight / 2), 0, 21);
            Assert.Contains(Descendants(saturday).OfType<TextBlock>(), t => t.Text == "Week 1");
            Assert.Contains(Descendants(weekly).OfType<TextBlock>(), t => t.Text == "-205.00 USD");
            Assert.Contains(Descendants(weekly).OfType<TextBlock>(), t => t.Text == "7 Trades");
            Border quietSaturday = Cell(9, 12);
            Border quietWeek = Assert.Single(Descendants(quietSaturday).OfType<Border>(),
                b => AutomationProperties.GetName(b) == "Weekly summary area");
            Assert.InRange(Math.Abs(quietWeek.TransformToAncestor(quietSaturday).Transform(new Point(0, quietWeek.ActualHeight / 2)).Y
                - quietSaturday.ActualHeight / 2), 0, 21);
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
                    Assert.Equal(((SolidColorBrush)view.Resources["PtjBorderBrush"]).Color,
                        ((SolidColorBrush)today.BorderBrush).Color);
                }
                var scroller = Assert.IsType<ScrollViewer>(view.FindName("CalendarScroller"));
                if (width < 700) Assert.True(scroller.ExtentWidth > scroller.ViewportWidth);
                Assert.Equal("Previous calendar month", AutomationProperties.GetName(
                    Assert.Single(Descendants(view).OfType<Button>(), b => AutomationProperties.GetName(b) == "Previous calendar month")));
                Assert.Contains(Descendants(view).OfType<TextBlock>(), t => t.Text == "Saturday");
        });
    }

    internal static ResourceDictionary SharedThemeResources(string theme)
    {
        // Detached component hosts have no Application.Resources: flatten the unchanged shared
        // declarations so Popup templates resolve them without cross-thread global resources.
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "PersonalTradingJournal.sln"))) repository = repository.Parent;
        string resourcePath = Path.Combine(repository!.FullName, "src/PersonalTradingJournal.Desktop/Resources");
        var declarations = new[] { $"Themes/{theme}Theme", "Typography", "Spacing", "Icons", "Controls" }
            .Select(file => System.Xml.Linq.XDocument.Load(Path.Combine(resourcePath, file + ".xaml")).Root!).ToArray();
        var combined = new System.Xml.Linq.XElement(declarations[^1]);
        combined.RemoveNodes();
        foreach (var declaration in declarations) combined.Add(declaration.Elements());
        combined.Add(System.Xml.Linq.XDocument.Load(Path.Combine(resourcePath, "../Views/Trades/TradeEntryForm.xaml")).Root!.Elements());
        combined.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns + "system", "clr-namespace:System;assembly=System.Runtime");
        foreach (string prefix in new[] { "converters", "validation" })
        {
            var attribute = combined.Attribute(System.Xml.Linq.XNamespace.Xmlns + prefix)!;
            string oldNamespace = attribute.Value;
            attribute.Value += ";assembly=PersonalTradingJournal.Desktop";
            foreach (var element in combined.Descendants().Where(e => e.Name.NamespaceName == oldNamespace))
                element.Name = System.Xml.Linq.XNamespace.Get(attribute.Value) + element.Name.LocalName;
            foreach (var attached in combined.Descendants().SelectMany(e => e.Attributes()).Where(a => a.Name.NamespaceName == oldNamespace).ToArray())
            {
                attached.Parent!.SetAttributeValue(System.Xml.Linq.XNamespace.Get(attribute.Value) + attached.Name.LocalName, attached.Value);
                attached.Remove();
            }
        }
        return (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(combined.ToString());
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
