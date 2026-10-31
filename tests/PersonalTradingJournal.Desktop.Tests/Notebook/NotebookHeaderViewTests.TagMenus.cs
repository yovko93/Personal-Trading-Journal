using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Desktop.Views.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed partial class NotebookHeaderViewTests
{
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 480, 240)]
    public async Task TagMenuRoutedToggleDismissalAndCapturedOwnership(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            string expected = string.Create(CultureInfo.InvariantCulture,
                $"{typeof(NotebookHeaderViewTests).FullName}.{nameof(TagMenuRoutedToggleDismissalAndCapturedOwnership)}(theme: \"{theme}\", width: {width}, dpi: {dpi})");
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookHeaderViewTests), "notebook-tag-menus", Host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30),
                testCaseFilter: IsolatedTestProcess.ExactDisplayNameFilter(expected), expectedTestName: expected);
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown,
                Resources = CalendarViewLayoutTests.SharedThemeResources(theme) };
            var prior = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var data = new NotebookFake(); data.Tags.AddRange([new(Guid.NewGuid(), "Alpha", 1), new(Guid.NewGuid(), "Beta", 1)]);
            data.Notes.Add(NotebookFake.Note());
            using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService { ConfirmationResult = true });
            var view = new NotebookWorkspaceView { DataContext = vm };
            var outside = new Button { Content = "Outside Notebook", Height = 32 };
            int outsideClicks = 0; outside.Click += (_, _) => outsideClicks++;
            var host = new DockPanel(); DockPanel.SetDock(outside, Dock.Top); host.Children.Add(outside); host.Children.Add(view);
            var window = new Window { Content = host, Width = width, Height = 950, ShowInTaskbar = false };
            var trace = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, "input-sequence.log");
            try
            {
                window.Show(); Pump(vm.ActivateAsync()); vm.Panel = 0; Layout();
                Button Trigger(string name) => SwitchDescendants<Button>((ItemsControl)view.FindName("TagFilters"))
                    .Single(b => AutomationProperties.GetName(b) == $"Manage global tag: {name}");
                // The lower popup must not cover the next native target. Switch upward.
                var a = Trigger("Beta"); var b = Trigger("Alpha");
                Click(a); var first = a.ContextMenu!; Assert.True(first.IsOpen);
                Click(a); Assert.False(a.ContextMenu?.IsOpen == true); // Fails on the old unconditional reopen.
                Click(a); var menu = a.ContextMenu!; Assert.True(menu.IsOpen);
                // Actual outside-down dismissal precedes the trigger's native release/click.
                Click(a); Assert.False(a.ContextMenu?.IsOpen == true);
                Click(a); Assert.True(a.ContextMenu!.IsOpen);
                Click(b); Assert.False(a.ContextMenu?.IsOpen == true); Assert.True(b.ContextMenu!.IsOpen);
                var oldDelete = (MenuItem)b.ContextMenu.Items[1];
                ((MenuItem)b.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Layout();
                Assert.True(vm.IsTagRenameOpen); Assert.Equal("Alpha", vm.TagName);
                vm.TagName = "Alpha renamed"; Pump(vm.RenameTagCommand.ExecuteAsync(null)); Layout();
                Assert.Equal(1, data.Writes); Assert.Contains(data.Tags, t => t.Name == "Alpha renamed");
                // Establish readiness on the trigger before requesting keyboard opening,
                // then exercise keyboard opening/navigation rather than assigning item focus.
                UiKeyboardProbe.Ready(a);
                a.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(a), 44, Key.Apps) { RoutedEvent = Keyboard.PreviewKeyDownEvent }); Layout();
                var current = a.ContextMenu!;
                MenuReady(current);
                var focusedItem = (MenuItem)current.Items[0];
                PressMenuKey(Key.Down); Assert.Same(focusedItem, Keyboard.FocusedElement);
                PressMenuKey(Key.Down); Assert.Same(current.Items[1], Keyboard.FocusedElement);
                PressMenuKey(Key.Up); Assert.Same(focusedItem, Keyboard.FocusedElement);
                PressMenuKey(Key.Escape);
                Assert.False(current.IsOpen);
                Assert.Same(a, Keyboard.FocusedElement);
                oldDelete.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(view.TagMenuTask); Assert.Equal(1, data.Writes);
                a.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(a), 45, Key.Apps) { RoutedEvent = Keyboard.PreviewKeyDownEvent }); Layout(); Assert.True(a.ContextMenu!.IsOpen);
                // Ordinary outside dismissal must not suppress a later unrelated click.
                Click(outside); Assert.False(a.ContextMenu!.IsOpen);
                Assert.InRange(outsideClicks, 0, 1);
                // Keyboard menu dismissal can consume the click. If delivered, the
                // outside action owns focus; otherwise WPF restores the opener.
                Assert.Same(outsideClicks == 1 ? outside : a, Keyboard.FocusedElement);
                File.AppendAllText(trace, $"{DateTimeOffset.UtcNow:O} outside-dismissed; deliveredClicks={outsideClicks}; focused={Keyboard.FocusedElement}\n");
                Click(a); Assert.True(a.ContextMenu.IsOpen);
                var stale = (MenuItem)a.ContextMenu.Items[1]; data.Tags.RemoveAll(t => t.Name == "Beta");
                Pump(vm.RefreshCommand.ExecuteAsync(null)); Layout(); Assert.False(current.IsOpen); Assert.False(a.ContextMenu?.IsOpen == true);
                stale.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(view.TagMenuTask); Assert.Equal(1, data.Writes);
                b = Trigger("Alpha renamed"); Click(b); var delete = (MenuItem)b.ContextMenu!.Items[1];
                delete.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(view.TagMenuTask); Layout();
                delete.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(view.TagMenuTask);
                Assert.Equal(2, data.Writes); Assert.Empty(data.Tags); Assert.Empty(vm.SelectedTagFilters);
                Assert.All(data.Queries, q => Assert.Empty(q.TagIds));
                // Shared tracking must retain the note-menu's direct toggle path.
                vm.SelectedNote = Assert.Single(vm.Notes); Pump(vm.LoadTask); vm.Panel = 2; Layout();
                var note = (Button)view.FindName("NoteOverflow"); Click(note); Assert.True(note.ContextMenu.IsOpen);
                Click(note); Assert.False(note.ContextMenu.IsOpen); Assert.False(vm.IsEditing);
                view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                Assert.Null(b.ContextMenu); Assert.False(note.ContextMenu.IsOpen);
            }
            finally { window.Close(); vm.Deactivate(); Pump(vm.DeactivationTask); app.Shutdown(); SynchronizationContext.SetSynchronizationContext(prior); }
            void Layout() { view.Measure(new Size(width, 900)); view.Arrange(new Rect(0, 0, width, 900)); view.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); }
            void Click(Button button)
            {
                Assert.True(button.IsVisible && button.IsEnabled); button.BringIntoView(); Layout();
                var point = button.PointToScreen(new Point(button.ActualWidth / 2, button.ActualHeight / 2));
                Assert.True(SetCursorPos((int)point.X, (int)point.Y));
                var source = Assert.IsType<HwndSource>(PresentationSource.FromVisual(button));
                var frame = new DispatcherFrame(); var watch = System.Diagnostics.Stopwatch.StartNew();
                var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(10) }; bool hit = false;
                void Check(object? sender, EventArgs e)
                {
                    point = button.PointToScreen(new Point(button.ActualWidth / 2, button.ActualHeight / 2));
                    Assert.True(SetCursorPos((int)point.X, (int)point.Y));
                    hit = WindowFromPoint(new NativePoint((int)point.X, (int)point.Y)) == source.Handle;
                    if (hit || watch.Elapsed >= TimeSpan.FromSeconds(3)) frame.Continue = false;
                }
                timer.Tick += Check; timer.Start();
                try { Dispatcher.PushFrame(frame); }
                finally { timer.Stop(); timer.Tick -= Check; }
                File.AppendAllText(trace, $"{DateTimeOffset.UtcNow:O} target={AutomationProperties.GetName(button)}; point={point}; expected={DescribeWindow(source.Handle)}; hit={DescribeWindow(WindowFromPoint(new NativePoint((int)point.X, (int)point.Y)))}; foreground={DescribeWindow(GetForegroundWindow())}; activeMouseSource={Mouse.PrimaryDevice.ActiveSource}; hitReady={hit}\n");
                Assert.True(hit, "Intended control does not own the native target coordinates.");
                int ups = 0;
                void Input(object sender, ProcessInputEventArgs e) { if (e.StagingItem.Input.RoutedEvent == Mouse.MouseUpEvent) { ups++; Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false); } }
                frame = new DispatcherFrame(); var deadline = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(3) };
                void Expired(object? sender, EventArgs e) => frame.Continue = false;
                InputManager.Current.PostProcessInput += Input; deadline.Tick += Expired; deadline.Start();
                try { SendMouse(0x0002); SendMouse(0x0004); Dispatcher.PushFrame(frame); Assert.True(ups > 0, "Native mouse-up was not processed."); }
                finally { deadline.Stop(); deadline.Tick -= Expired; InputManager.Current.PostProcessInput -= Input; }
                Layout(); Assert.Equal(MouseButtonState.Released, Mouse.LeftButton);
            }
            void PressMenuKey(Key key)
            {
                var target = Assert.IsAssignableFrom<UIElement>(Keyboard.FocusedElement);
                target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), Environment.TickCount, key) { RoutedEvent = Keyboard.KeyDownEvent }); Layout();
            }
            void MenuReady(ContextMenu menu)
            {
                var frame = new DispatcherFrame();
                var deadline = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(5) };
                void Check(object? sender, RoutedEventArgs e) { if (menu.IsOpen && menu.IsLoaded && menu.IsKeyboardFocusWithin) frame.Continue = false; }
                void Expired(object? sender, EventArgs e) => frame.Continue = false;
                menu.Opened += Check; menu.GotKeyboardFocus += Check; deadline.Tick += Expired;
                try { deadline.Start(); Check(null, new()); if (frame.Continue) Dispatcher.PushFrame(frame); }
                finally { deadline.Stop(); deadline.Tick -= Expired; menu.Opened -= Check; menu.GotKeyboardFocus -= Check; }
                Assert.True(menu.IsOpen && menu.IsLoaded && menu.IsKeyboardFocusWithin,
                    $"Keyboard menu readiness: open={menu.IsOpen}; loaded={menu.IsLoaded}; within={menu.IsKeyboardFocusWithin}; captured={Mouse.Captured}; mouseSource={Mouse.PrimaryDevice.ActiveSource}; focused={Keyboard.FocusedElement}");
                File.AppendAllText(trace, $"{DateTimeOffset.UtcNow:O} keyboard-ready; captured={Mouse.Captured}; nativeCapture={GetCapture():X}; menuSource={((HwndSource)PresentationSource.FromVisual(menu)).Handle:X}; focusScope={FocusManager.GetFocusScope(menu)}; logicalFocus={FocusManager.GetFocusedElement(menu)}; focused={Keyboard.FocusedElement}\n");
            }
        });
    }
}
