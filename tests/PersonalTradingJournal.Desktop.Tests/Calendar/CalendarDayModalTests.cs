using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.Views.Calendar;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

[Collection(ExclusiveWpfCollection.Name)]
[Trait(ExclusiveWpfCollection.Trait, ExclusiveWpfCollection.Value)]
public sealed partial class CalendarDayModalTests
{
    [Theory]
    [InlineData("Light", 1100, 96, "Outside")]
    [InlineData("Dark", 1400, 96, "Outside")]
    [InlineData("Light", 480, 240, "Outside")]
    [InlineData("Dark", 480, 240, "Outside")]
    [InlineData("Light", 1100, 96, "X")]
    [InlineData("Dark", 1100, 96, "Escape")]
    public async Task BackdropConsumesOneWholeClickAndAllClosePathsRestoreSelection(string theme, int width, int dpi, string close)
    {
        var vm = await CalendarSummaryFixture.CreateAsync();
        await OnSta(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var view = new CalendarView { DataContext = vm, Resources = CalendarViewLayoutTests.SharedThemeResources(theme) };
            var owner = new Window { Content = view, Width = width, Height = 760, ShowInTaskbar = false };
            Exception? failure = null;
            int closed = 0, ownerClicks = 0;
            owner.PreviewMouseUp += (_, _) => ownerClicks++;
            try
            {
                owner.Show(); Pump(); owner.UpdateLayout();
                var cell = Assert.Single(Descendants(view).OfType<CalendarDayHost>(), c => ((CalendarDayCell)c.DataContext).Date == new DateOnly(2026, 9, 5));
                using var focus = new CalendarFocusProbe(owner, cell, $"Backdrop/{theme}/{width}/{close}");
                var account = vm.SelectedAccount; string currency = vm.SelectedCurrency;
                dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        var dialog = view.DayDialog!;
                        focus.ObserveDialog(dialog);
                        dialog.Closed += (_, _) => closed++;
                        await vm.DayJournalLoadTask; dialog.UpdateLayout();
                        var backdrop = (Border)dialog.FindName("DialogBackdrop");
                        var panel = (Border)dialog.FindName("DayPanel");
                        var content = (CalendarDayDetailsView)dialog.FindName("DayContent");
                        Assert.Equal(view.ActualWidth, dialog.ActualWidth, 1);
                        Assert.Equal(view.ActualHeight, dialog.ActualHeight, 1);
                        Assert.InRange(panel.ActualWidth, 1, Math.Min(1280, dialog.ActualWidth - 32));
                        Assert.Same(dialog, Window.GetWindow(backdrop));
                        Assert.False(backdrop.IsAncestorOf(panel));
                        Assert.Same(view.Resources["PtjBackgroundBrush"], panel.Background);
                        Assert.Same(view.Resources["PtjBorderBrush"], panel.BorderBrush);
                        // The inset area belongs to the enabled modal, even with a disabled native owner.
                        Assert.Same(backdrop, dialog.InputHitTest(new Point(4, 4)));
                        var journalAction = (Button)content.FindName("DayJournalAction");
                        foreach (UIElement inside in new UIElement[] { panel, content,
                            (ScrollViewer)content.FindName("DayContentScroller"), journalAction })
                        {
                            Press(inside); Release(inside); Assert.True(dialog.IsVisible);
                        }
                        // Tooltips live in a separate popup and cannot inherit the backdrop click route.
                        var maximize = (Button)dialog.FindName("MaximizeButton");
                        var tip = new ToolTip { Content = maximize.ToolTip, PlacementTarget = maximize };
                        maximize.ToolTip = tip;
                        tip.IsOpen = true; Pump();
                        Assert.NotSame(PresentationSource.FromVisual(dialog), PresentationSource.FromVisual(tip));
                        Point tooltipCenter = dialog.PointFromScreen(tip.PointToScreen(new Point(tip.ActualWidth / 2, tip.ActualHeight / 2)));
                        Assert.True(dialog.IsPointOnOpenToolTip(tooltipCenter));
                        Press(tip); Release(tip); Assert.True(dialog.IsVisible); tip.IsOpen = false;
                        Assert.False(dialog.IsPointOnOpenToolTip(tooltipCenter));
                        // A press inside/release outside is not an outside click.
                        Press(panel); Release(backdrop); Assert.True(dialog.IsVisible);
                        // A press outside/release inside does not dismiss either.
                        Assert.True(Press(backdrop).Handled); Release(panel); Assert.True(dialog.IsVisible);
                        var other = new Window { Width = 200, Height = 100, ShowInTaskbar = false };
                        try { other.Show(); other.Activate(); Pump(); Assert.True(dialog.IsVisible); }
                        finally { other.Close(); dialog.Activate(); Pump(); }
                        Render(dialog, $"backdrop-{theme}", width, dpi);
                        if (close == "Outside")
                        {
                            Assert.True(Press(backdrop).Handled);
                            Assert.True(dialog.IsVisible); // No click-through mouse-up to Calendar.
                            Assert.True(Release(backdrop).Handled);
                        }
                        else if (close == "X") ((Button)dialog.FindName("DismissButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        else
                        {
                            var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(dialog)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                            dialog.RaiseEvent(key); Assert.True(key.Handled);
                        }
                    }
                    catch (Exception exception) { failure = exception; view.DayDialog?.Close(); }
                }));
                cell.RaiseEvent(new RoutedEventArgs(CalendarDayHost.InvokedEvent));
                Pump(); if (failure is not null) throw failure;
                Assert.Equal(1, closed); Assert.Equal(0, ownerClicks);
                Assert.Null(view.DayDialog); focus.VerifyRestored(); Assert.True(cell.IsKeyboardFocused);
                Assert.Equal(new DateOnly(2026, 9, 5), vm.SelectedDate);
                Assert.Equal(new DateOnly(2026, 9, 1), vm.SelectedMonth);
                Assert.Same(account, vm.SelectedAccount); Assert.Equal(currency, vm.SelectedCurrency);
            }
            finally { owner.Close(); }
        });
    }

    private static MouseButtonEventArgs Press(UIElement target) => RaisePointerButton(target, Mouse.PreviewMouseDownEvent);
    private static MouseButtonEventArgs Release(UIElement target) => RaisePointerButton(target, Mouse.PreviewMouseUpEvent);
    private static MouseButtonEventArgs RaisePointerButton(UIElement target, RoutedEvent routedEvent)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left) { RoutedEvent = routedEvent };
        target.RaiseEvent(args); return args;
    }

    private static readonly Lazy<Task> NativeHost = new(RunNativeHostAsync);
    [Theory]
    [InlineData("Light", 1100, 96, false)]
    [InlineData("Dark", 1100, 96, true)]
    [InlineData("Light", 480, 240, true)]
    [InlineData("Dark", 480, 240, false)]
    public async Task OwnedModalDimsCalendarClosesAndRestoresDateFocusWithoutJournalStatusService(string theme, int width, int dpi, bool empty)
    {
        CalendarViewModel vm = await CalendarSummaryFixture.CreateAsync();
        await OnSta(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var view = new CalendarView { DataContext = vm, Resources = CalendarViewLayoutTests.SharedThemeResources(theme) };
            var owner = new Window { Content = view, Width = width, Height = 760, ShowInTaskbar = false };
            Exception? failure = null;
            try
            {
                owner.Show();
                Pump(); // Let Loaded/layout/container work run before interacting with the native window.
                owner.UpdateLayout();
                var saturday = Assert.Single(Descendants(view).OfType<CalendarDayHost>(), c => ((CalendarDayCell)c.DataContext).Date == new DateOnly(2026, 9, 5));
                using var focus = new CalendarFocusProbe(owner, saturday, $"OwnedModal/{theme}/{width}/{empty}");
                var account = vm.SelectedAccount;
                string currency = vm.SelectedCurrency;
                dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        var dialog = Assert.IsType<CalendarDayDialogWindow>(view.DayDialog);
                        focus.ObserveDialog(dialog);
                        Assert.Same(owner, dialog.Owner);
                        Assert.True(dialog.IsVisible);
                        Assert.Equal(Visibility.Visible, ((Border)view.FindName("ModalShade")).Visibility);
                        await vm.DayJournalLoadTask;
                        dialog.UpdateLayout();
                        Assert.Equal(saturday.DataContext, CalendarDayDetailsTests.Cell(vm, new(2026, 9, 5)));
                        Assert.Null(view.FindName("DayDetailsPanel"));
                        var content = (CalendarDayDetailsView)dialog.FindName("DayContent");
                        Assert.Contains(Descendants(content).OfType<TextBlock>(), t => t.Text == "Day Journal");
                        Button journal = Assert.Single(Descendants(content).OfType<Button>(), b => Equals(b.Content, "Add Journal"));
                        // Day content and Add do not depend on the grid-status service.
                        Assert.True(journal.IsEnabled);
                        Assert.Same(vm.AddDayJournalCommand, journal.Command);
                        Assert.True(journal.Command.CanExecute(null));
                        Assert.Contains("No Journal", vm.DayJournalStatusMessage);
                        Assert.Equal("Add Journal", AutomationProperties.GetName(journal));
                        Render(content, theme, width, dpi);
                        if (empty) ((Button)dialog.FindName("CloseButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        else
                        {
                            var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(dialog)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                            dialog.RaiseEvent(args);
                            Assert.True(args.Handled);
                        }
                    }
                    catch (Exception exception) { failure = exception; view.DayDialog?.Close(); }
                }));
                saturday.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseUpEvent });
                Pump();
                if (failure is not null) throw failure;
                Assert.Null(view.DayDialog);
                Assert.Equal(Visibility.Collapsed, ((Border)view.FindName("ModalShade")).Visibility);
                Assert.Equal(new DateOnly(2026, 9, 5), vm.SelectedDate);
                Assert.Equal(new DateOnly(2026, 9, 1), vm.SelectedMonth);
                Assert.Same(account, vm.SelectedAccount);
                Assert.Equal(currency, vm.SelectedCurrency);
                focus.VerifyRestored(); Assert.True(saturday.IsKeyboardFocused);
            }
            finally { owner.Close(); }
        });
    }

    private static void Render(FrameworkElement content, string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable("PTJ_CALENDAR_RENDER_DIRECTORY") is not { Length: > 0 } output) return;
        content.Measure(new Size(width, 700)); content.Arrange(new Rect(0, 0, width, 700)); content.UpdateLayout();
        var image = new RenderTargetBitmap(width * dpi / 96, 700 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
        image.Render(content);
        Directory.CreateDirectory(output);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(System.IO.Path.Combine(output, $"calendar-modal-{theme}-{width}-{dpi}.png")); encoder.Save(file);
    }
    private static void Pump()
    {
        using var timing = CalendarStaTest.Timing("modal dispatcher pump");
        var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    internal static Task OnSta(Action action, [System.Runtime.CompilerServices.CallerMemberName] string scenario = "")
    {
        // Native windows own a fresh WPF lifetime independent of test ordering.
        // Application-owning resource tests are also isolated from this host.
        if (Environment.GetEnvironmentVariable("PTJ_CALENDAR_MODAL_TEST_HOST") != "1") return NativeHost.Value;
        // Preserve the native suite's process-owned renderer lifetime. Tearing down
        // the shared compositor between every native case makes Window/bitmap
        // initialization repeat; the supervised child owns final native cleanup.
        return CalendarStaTest.RunAsync(action, scenario, TimeSpan.FromSeconds(45), shutdownDispatcher: false);
    }

    private static Task RunNativeHostAsync() => IsolatedTestProcess.RunSuiteAsync(
        typeof(CalendarDayModalTests), "calendar-native", "PTJ_CALENDAR_MODAL_TEST_HOST",
        TimeSpan.FromMinutes(2), caseHangTimeout: TimeSpan.FromSeconds(45),
        testCaseFilter: "FullyQualifiedName~CalendarDayModalTests");
}
