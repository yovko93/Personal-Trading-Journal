using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    public async Task OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard(string theme, int width, int dpi)
    {
        if (Environment.GetEnvironmentVariable(Host) != "1")
        {
            string expected = string.Create(CultureInfo.InvariantCulture,
                $"{typeof(NotebookHeaderViewTests).FullName}.{nameof(OverflowTriggersToggleThroughNativeMouseAndOwnedKeyboard)}(theme: \"{theme}\", width: {width}, dpi: {dpi})");
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookHeaderViewTests), "notebook-menus", Host,
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
            var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note()); data.Notes.Add(NotebookFake.Note("Other note"));
            using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService());
            var view = new NotebookWorkspaceView { DataContext = vm, Width = width, Height = 900 };
            var outside = new Button { Content = "Outside Notebook", Height = 32 };
            var host = new DockPanel(); DockPanel.SetDock(outside, Dock.Top); host.Children.Add(outside); host.Children.Add(view);
            var window = new Window { Content = host, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false };
            string directory = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, "menu-evidence");
            Directory.CreateDirectory(directory);
            var log = new List<string>(); int mouseUps = 0;
            void Input(object sender, ProcessInputEventArgs e)
            {
                if (e.StagingItem.Input is MouseButtonEventArgs mouse)
                {
                    log.Add($"{mouse.Timestamp} {mouse.RoutedEvent.Name} {mouse.ChangedButton}/{mouse.ButtonState} handled={mouse.Handled} source={mouse.OriginalSource} captured={Mouse.Captured}");
                    if (mouse.RoutedEvent == Mouse.MouseUpEvent) mouseUps++;
                }
            }
            InputManager.Current.PostProcessInput += Input;
            try
            {
                window.Show(); Pump(vm.ActivateAsync()); vm.SelectedNote = vm.Notes[0]; Pump(vm.LoadTask); Layout();
                var notebook = (Button)view.FindName("NotebookOverflow"); var note = (Button)view.FindName("NoteOverflow");
                foreach (var button in new[] { notebook, note })
                {
                    button.ContextMenu.Opened += (_, _) => log.Add($"{Environment.TickCount} {button.Name} Opened");
                    button.ContextMenu.Closed += (_, _) => log.Add($"{Environment.TickCount} {button.Name} Closed");
                    button.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, _) => log.Add($"{Environment.TickCount} {button.Name} Click IsOpen={button.ContextMenu.IsOpen}")), true);
                }
                int writes = data.Writes;
                foreach (var button in new[] { notebook, note })
                {
                    vm.Panel = button == notebook ? 0 : 2; Layout(); UiKeyboardProbe.Ready(button);
                    Assert.Equal(Visibility.Visible, ((FrameworkElement)button.Template.FindName("ActionFocus", button)).Visibility);
                    Save(view, button.Name + "-closed", width, 900);
                    MouseClick(button); Assert.True(button.ContextMenu.IsOpen);
                    Color(button, "PtjPlanReadyPressedBrush");
                    Save(button.ContextMenu, button.Name + "-open-menu", button.ContextMenu.ActualWidth, button.ContextMenu.ActualHeight);
                    Save(view, button.Name + "-open-trigger", width, 900);
                    MouseClick(button); Assert.False(button.ContextMenu.IsOpen); Color(button, "PtjPlanReadyHoverBrush");
                    MouseClick(button); Assert.True(button.ContextMenu.IsOpen);
                    MouseClick(outside); Assert.False(button.ContextMenu.IsOpen); Assert.Same(outside, Keyboard.FocusedElement);
                    MouseClick(button); Assert.True(button.ContextMenu.IsOpen);
                    Escape(button.ContextMenu); Assert.False(button.ContextMenu.IsOpen); Assert.Same(button, Keyboard.FocusedElement);
                    UiKeyboardProbe.Space(button); Layout(); Assert.True(button.ContextMenu.IsOpen);
                    Escape(button.ContextMenu); Assert.False(button.ContextMenu.IsOpen); Assert.Same(button, Keyboard.FocusedElement);
                }
                Assert.Equal(writes, data.Writes);
                // Both triggers are simultaneously visible only in the full-width layout.
                if (width >= 1000)
                {
                    MouseClick(notebook); MouseClick(note); Assert.False(notebook.ContextMenu.IsOpen); Assert.True(note.ContextMenu.IsOpen);
                    MouseClick(notebook); Assert.False(note.ContextMenu.IsOpen); Assert.True(notebook.ContextMenu.IsOpen);
                    Escape(notebook.ContextMenu);
                }
                vm.Panel = 0; Layout(); MouseClick(notebook);
                var refresh = Assert.Single(notebook.ContextMenu.Items.OfType<MenuItem>());
                int queries = data.Queries.Count; var previousRefresh = vm.RefreshCommand.ExecutionTask; MouseClick(refresh);
                Assert.NotNull(vm.RefreshCommand.ExecutionTask); Assert.NotSame(previousRefresh, vm.RefreshCommand.ExecutionTask); Pump(vm.RefreshCommand.ExecutionTask); Layout();
                Assert.Equal(queries + 1, data.Queries.Count); Assert.False(notebook.ContextMenu.IsOpen);
                vm.Panel = 2; Layout(); MouseClick(note);
                var duplicate = (MenuItem)view.FindName("DuplicateNoteButton");
                int count = data.Notes.Count; MouseClick(duplicate); Pump(vm.DuplicateCommand.ExecutionTask!); Layout();
                Assert.Equal(count + 1, data.Notes.Count); Assert.Equal(writes + 1, data.Writes); Assert.False(note.ContextMenu.IsOpen);
                MouseClick(note); var stale = ((MenuItem)view.FindName("ArchiveNoteButton")).Command!;
                vm.SelectedNote = vm.Notes.First(n => n.Id != vm.Note!.Id); Pump(vm.LoadTask); Layout();
                Assert.False(note.ContextMenu.IsOpen); Assert.False(stale.CanExecute(null));
                writes = data.Writes; stale.Execute(null); Assert.Equal(writes, data.Writes);
                vm.Panel = 0; Layout(); var create = (Button)view.FindName("NewFolderButton");
                UiKeyboardProbe.Space(create, () => { Color(create, "PtjPlanReadyPressedBrush"); Save(view, "NewFolder-pressed-focus", width, 900); }); Layout(); Assert.True(vm.IsFolderCreationOpen);
                Assert.Same(view.FindName("NewFolderName"), Keyboard.FocusedElement);
                foreach (var button in new[] { create, notebook, note })
                {
                    // Exercise the local style's disabled state, without changing command capabilities.
                    button.SetCurrentValue(UIElement.IsEnabledProperty, false); Layout(); Color(button, "PtjSurfaceBrush");
                    Assert.Same(view.FindResource("PtjTextSecondaryBrush"), button.Foreground);
                }
                Save(view, "disabled", width, 900);
                Assert.False(app.Dispatcher.HasShutdownStarted);
            }
            finally
            {
                File.WriteAllLines(Path.Combine(directory, "input-sequence.log"), log);
                InputManager.Current.PostProcessInput -= Input;
                window.Close(); vm.Deactivate(); Pump(vm.DeactivationTask); app.Shutdown(); SynchronizationContext.SetSynchronizationContext(prior);
            }
            void Color(Button button, string resource)
            {
                Assert.Same(view.FindResource(resource), button.Background);
                var fg = ((SolidColorBrush)button.Foreground).Color; var bg = ((SolidColorBrush)button.Background).Color;
                double L(Color c) { double C(byte b) { double v=b/255d; return v<=.04045 ? v/12.92 : Math.Pow((v+.055)/1.055,2.4); } return .2126*C(c.R)+.7152*C(c.G)+.0722*C(c.B); }
                Assert.True((Math.Max(L(fg),L(bg))+.05)/(Math.Min(L(fg),L(bg))+.05)>=4.5, "Action text contrast must be at least 4.5:1.");
            }
            void Layout() { view.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); }
            void Escape(ContextMenu menu)
            {
                menu.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(menu), Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent }); Layout();
            }
            void MouseClick(FrameworkElement target)
            {
                Assert.True(target.IsVisible && target.IsEnabled);
                var point = target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2));
                Assert.True(SetCursorPos((int)point.X, (int)point.Y)); Layout();
                // Popup animation may still expose the owner HWND even after IsOpen becomes true.
                // Wait for the actual target HWND at the target point before injecting input.
                var readyFrame = new DispatcherFrame(); var readyWatch = System.Diagnostics.Stopwatch.StartNew();
                var readyTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(10) };
                bool hit = false;
                void HitTest(object? s, EventArgs e)
                {
                    point = target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2));
                    Assert.True(SetCursorPos((int)point.X, (int)point.Y));
                    bool settled = true;
                    for (DependencyObject? ancestor = target; ancestor is not null; ancestor = VisualTreeHelper.GetParent(ancestor))
                        if (ancestor is UIElement element && (element.Opacity < 1 ||
                            element.RenderTransform is TranslateTransform { HasAnimatedProperties: true } translation && (translation.X != 0 || translation.Y != 0))) settled = false;
                    hit = settled && (target is not MenuItem || target.IsMouseOver) && WindowFromPoint(new NativePoint((int)point.X, (int)point.Y)) == ((HwndSource)PresentationSource.FromVisual(target)).Handle;
                    if (hit || readyWatch.Elapsed >= TimeSpan.FromSeconds(3)) readyFrame.Continue = false;
                }
                readyTimer.Tick += HitTest; readyTimer.Start();
                try { Dispatcher.PushFrame(readyFrame); Assert.True(hit, "The intended control did not become hit-testable with settled popup animation and mouse-over."); }
                finally { readyTimer.Stop(); readyTimer.Tick -= HitTest; }
                Assert.True(SetCursorPos((int)point.X, (int)point.Y));
                log.Add($"Inject at {point}: target={target.Name}/{target.GetType().Name}; mouseOver={target.IsMouseOver}; captured={Mouse.Captured}");
                int before = mouseUps;
                SendMouse(0x0002); SendMouse(0x0004);
                var frame = new DispatcherFrame(); var deadline = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(3) };
                void Expired(object? s, EventArgs e) => frame.Continue = false;
                void Completed(object s, ProcessInputEventArgs e) { if (mouseUps > before) Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false); }
                deadline.Tick += Expired; InputManager.Current.PostProcessInput += Completed; deadline.Start();
                try { Dispatcher.PushFrame(frame); Assert.True(mouseUps > before, "Native mouse-up was not processed by this fixture."); }
                finally { deadline.Stop(); deadline.Tick -= Expired; InputManager.Current.PostProcessInput -= Completed; }
                Layout(); Assert.Equal(MouseButtonState.Released, Mouse.LeftButton);
            }
            void Save(Visual visual, string name, double w, double h)
            {
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(w * dpi / 96d), (int)Math.Ceiling(h * dpi / 96d), dpi, dpi, PixelFormats.Pbgra32); bitmap.Render(visual);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{name}.png")); encoder.Save(file);
            }
        });
    }
    [StructLayout(LayoutKind.Sequential)] private readonly record struct NativePoint(int X, int Y);
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeInput { public uint Type; public MouseInput Mouse; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
    private static void SendMouse(uint flags) => Assert.Equal(1u, SendInput(1, [new NativeInput { Mouse = new MouseInput { Flags = flags } }], Marshal.SizeOf<NativeInput>()));
}
