using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Desktop.Interactions;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Instruments;
using PersonalTradingJournal.Desktop.Views.Instruments;
using PersonalTradingJournal.Domain.Instruments;

namespace PersonalTradingJournal.Desktop.Tests.Instruments;

[Collection(ExclusiveWpfCollection.Name)]
[Trait(ExclusiveWpfCollection.Trait, ExclusiveWpfCollection.Value)]
public sealed class InstrumentsViewTests
{
    [Theory]
    [InlineData("Light", 1280, 96)]
    [InlineData("Dark", 1280, 96)]
    [InlineData("Light", 480, 240)]
    [InlineData("Dark", 480, 240)]
    public async Task CompiledRowsAlignEditTargetsRowAndNativeMenusToggleWithReadableHover(string theme, int width, int dpi)
    {
        const string host = "PTJ_INSTRUMENTS_UI_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            string identity = string.Create(CultureInfo.InvariantCulture,
                $"{typeof(InstrumentsViewTests).FullName}.{nameof(CompiledRowsAlignEditTargetsRowAndNativeMenusToggleWithReadableHover)}(theme: \"{theme}\", width: {width}, dpi: {dpi})");
            await IsolatedTestProcess.RunSuiteAsync(typeof(InstrumentsViewTests), "instruments-ui", host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30),
                testCaseFilter: IsolatedTestProcess.ExactDisplayNameFilter(identity), expectedTestName: identity);
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown,
                Resources = CalendarViewLayoutTests.SharedThemeResources(theme) };
            var errors = new BindingErrors();
            PresentationTraceSources.DataBindingSource.Listeners.Add(errors);
            var (vm, reader) = CreateSyntheticModel();
            Pump(vm.EnsureLoadedAsync());
            var view = new InstrumentsView { DataContext = vm, Language = XmlLanguage.GetLanguage("en-US") };
            var outside = new Button { Content = "Outside menu", Height = 32, Style = (Style)app.Resources["PtjButtonStyle"] };
            var samples = new WrapPanel();
            string[] styles = ["PtjButtonStyle", "PtjPrimaryButtonStyle", "PtjSecondaryButtonStyle", "PtjDangerButtonStyle", "PtjEntityActionButtonStyle", "PtjIconButtonStyle"];
            foreach (string style in styles)
                samples.Children.Add(new Button { Name = style, Content = style == "PtjIconButtonStyle" ? "⋯" : "Test", Style = (Style)app.Resources[style], Margin = new Thickness(2) });
            var hostPanel = new DockPanel { Background = (Brush)app.Resources["PtjBackgroundBrush"] };
            DockPanel.SetDock(outside, Dock.Top); DockPanel.SetDock(samples, Dock.Top);
            hostPanel.Children.Add(outside); hostPanel.Children.Add(samples); hostPanel.Children.Add(view);
            var window = NotebookMenuLayout.Window(hostPanel, width, 900);
            window.Style = (Style)app.Resources["PtjWindowStyle"];
            string directory = Path.Combine(Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY")!, "ui-evidence");
            Directory.CreateDirectory(directory);
            var menus = new List<ContextMenu>();
            try
            {
                window.Show(); Layout();
                var table = (Grid)view.FindName("InstrumentTable");
                var headers = (Grid)view.FindName("InstrumentHeaders");
                var rows = Descendants(view).OfType<Grid>().Where(g => g.Name == "InstrumentRow").ToArray();
                Assert.Equal(2, rows.Length);
                Assert.Equal(new[] { "Symbol", "Display Name", "Asset Class", "Exchange", "Tick Size", "Tick Value", "Point Value", "Status", "Actions" },
                    headers.Children.OfType<TextBlock>().Select(h => h.Text));
                Assert.Equal(9, headers.ColumnDefinitions.Count);
                foreach (var row in rows)
                {
                    Assert.Equal(9, row.Children.Count);
                    Assert.Equal(9, row.ColumnDefinitions.Count);
                    foreach (FrameworkElement cell in row.Children)
                    {
                        int column = Grid.GetColumn(cell);
                        var header = headers.Children.Cast<FrameworkElement>().Single(h => Grid.GetColumn(h) == column);
                        Assert.Equal(HorizontalAlignment.Left, cell.HorizontalAlignment);
                        Assert.Equal(HorizontalAlignment.Left, header.HorizontalAlignment);
                        Assert.InRange(Math.Abs(cell.TranslatePoint(new Point(), table).X - header.TranslatePoint(new Point(), table).X), 0, 1.1);
                        Assert.True(cell.ActualWidth <= row.ColumnDefinitions[column].ActualWidth + 1);
                    }
                    Assert.DoesNotContain(Descendants(row).OfType<Button>(), b => ReferenceEquals(b.Command, vm.EditInstrumentCommand));
                    var tickValue = row.Children.OfType<TextBlock>().Single(t => Grid.GetColumn(t) == 5);
                    var binding = Assert.IsType<MultiBinding>(BindingOperations.GetMultiBinding(tickValue, TextBlock.TextProperty));
                    Assert.Equal(new[] { "TickValue", "Currency" }, binding.Bindings.Cast<Binding>().Select(b => b.Path.Path));
                    Assert.Equal(TextAlignment.Left, tickValue.TextAlignment);
                }
                var tickValues = rows.Select(r => r.Children.OfType<TextBlock>().Single(t => Grid.GetColumn(t) == 5)).ToArray();
                Assert.Equal("5.00 USD", tickValues[0].Text);
                Assert.Equal("1.23456789 EUR", tickValues[1].Text);
                var scroller = (ScrollViewer)view.FindName("InstrumentTableScroll");
                Assert.Equal(width < 1120, scroller.ScrollableWidth > 0);
                Save(view, "table-left");
                scroller.ScrollToHorizontalOffset(Math.Max(0, tickValues[0].TranslatePoint(new Point(), table).X - 16));
                Layout();
                foreach (var tickValue in tickValues)
                {
                    double left = tickValue.TranslatePoint(new Point(), scroller).X;
                    Assert.InRange(left, 0, scroller.ViewportWidth - tickValue.ActualWidth);
                }
                Save(view, "table-pricing");
                scroller.ScrollToRightEnd(); Layout(); Save(view, "table-actions");

                foreach (Button sample in samples.Children)
                {
                    Move(outside); Move(sample);
                    var overlay = Assert.IsType<Border>(sample.Template.FindName("HoverSurface", sample));
                    var chrome = Assert.IsType<Border>(sample.Template.FindName("ButtonChrome", sample));
                    Assert.Equal(.16, overlay.Opacity, 3); Assert.Equal(1, chrome.Opacity);
                    Assert.Same(app.Resources["PtjButtonHoverBorderBrush"], chrome.BorderBrush);
                    Color background = Blend(((SolidColorBrush)sample.Background).Color, ((SolidColorBrush)hostPanel.Background).Color);
                    Color hover = Blend(((SolidColorBrush)overlay.Background).Color, background, overlay.Opacity);
                    Assert.NotEqual(background, hover);
                    Assert.True(Contrast(((SolidColorBrush)sample.Foreground).Color, hover) >= 4.5, $"{theme}/{sample.Name} hover text contrast");
                    Save(sample, "hover-" + sample.Name);
                    Move(outside); Assert.Equal(0, overlay.Opacity);
                }

                var actions = Descendants(rows[1]).OfType<Button>().Single(b => b.Name == "InstrumentRowActions");
                var menu = actions.ContextMenu!; menus.Add(menu);
                Assert.True(EntityActionMenu.GetOpensContextMenu(actions));
                Assert.Equal("More instrument actions", AutomationProperties.GetName(actions));
                for (int repetition = 0; repetition < 3; repetition++)
                {
                    Click(actions); Assert.True(menu.IsOpen);
                    Click(actions); Assert.False(menu.IsOpen); // Native outside-down must not reopen it on Click.
                }
                Click(actions); Click(outside); Assert.False(menu.IsOpen);
                // WPF keyboard handlers and UI Automation still use the same button interaction.
                actions.BringIntoView(); Layout(); UiKeyboardProbe.Ready(actions);
                var source = PresentationSource.FromVisual(actions);
                actions.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Space) { RoutedEvent = Keyboard.KeyDownEvent });
                actions.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Space) { RoutedEvent = Keyboard.KeyUpEvent });
                Layout(); Assert.True(menu.IsOpen);
                menu.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(menu), Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent });
                Layout(); Assert.False(menu.IsOpen); Assert.Same(actions, Keyboard.FocusedElement);
                var invoke = (IInvokeProvider)new ButtonAutomationPeer(actions).GetPattern(PatternInterface.Invoke);
                invoke.Invoke(); Assert.True(Wait(() => menu.IsOpen));
                invoke.Invoke(); Assert.True(Wait(() => !menu.IsOpen));

                var first = (InstrumentListItem)rows[0].DataContext;
                var second = (InstrumentListItem)rows[1].DataContext;
                reader.DetailsToReturn = Details(first); Pump(vm.ViewInstrumentCommand.ExecuteAsync(first.Id)); Layout();
                Assert.Equal("USD", DetailCurrency().Text);
                reader.DetailsToReturn = Details(second);
                Click(actions); Assert.True(menu.IsOpen);
                Assert.Equal(first.Id, vm.SelectedInstrument!.Id); // Opening a different row's menu does not select it.
                var edit = menu.Items.OfType<MenuItem>().First();
                Assert.Equal("Edit", edit.Header); Assert.Same(vm.EditInstrumentCommand, edit.Command); Assert.Equal(second.Id, edit.CommandParameter);
                Assert.DoesNotContain(menu.Items.OfType<MenuItem>().Skip(1), m => Equals(m.Header, "Edit"));
                Save(menu, "edit-menu");
                Click(edit);
                Assert.True(Wait(() => vm.EditInstrumentCommand.ExecutionTask?.IsCompleted == true && vm.IsEditFormVisible));
                Assert.Equal(second.Id, vm.SelectedInstrument!.Id);
                Assert.Equal(second.Symbol, vm.EditSymbol); Assert.False(menu.IsOpen);
                Assert.Equal("EUR", vm.EditCurrency);
                Layout(); Assert.Equal("EUR", DetailCurrency().Text);
                var editCurrency = CurrencyInput("EditCurrency");
                Assert.Equal("EUR", editCurrency.Text); Assert.False(editCurrency.IsReadOnly);
                editCurrency.SetCurrentValue(TextBox.TextProperty, "GBP"); Layout();
                Assert.Equal("GBP", vm.EditCurrency);
                Save(view, "currency-edit");
                vm.CancelEditCommand.Execute(null); vm.ShowCreateFormCommand.Execute(null); Layout();
                var addCurrency = CurrencyInput("Currency");
                Assert.Equal("USD", addCurrency.Text); Assert.False(addCurrency.IsReadOnly);
                addCurrency.SetCurrentValue(TextBox.TextProperty, "CAD"); Layout();
                Assert.Equal("CAD", vm.Currency);
                Save(view, "currency-add");
                vm.CancelCreateCommand.Execute(null); vm.ShowCreateFormCommand.Execute(null); Layout();
                Assert.Equal("USD", addCurrency.Text);
                Assert.Empty(errors.Messages);
            }
            finally
            {
                foreach (var menu in menus) menu.IsOpen = false;
                PresentationTraceSources.DataBindingSource.Listeners.Remove(errors);
                window.Close(); app.Shutdown();
            }

            void Layout() { view.UpdateLayout(); window.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); }
            TextBox CurrencyInput(string path) => Descendants(view).OfType<TextBox>()
                .Single(t => BindingOperations.GetBinding(t, TextBox.TextProperty)?.Path.Path == path);
            TextBlock DetailCurrency() => Descendants(view).OfType<TextBlock>()
                .Single(t => BindingOperations.GetBinding(t, TextBlock.TextProperty)?.Path.Path == "SelectedInstrument.Currency");
            void Move(FrameworkElement target, bool requireHover = true)
            {
                target.BringIntoView(); Layout();
                var point = Center(target);
                Assert.Equal(((HwndSource)PresentationSource.FromVisual(target)).Handle, WindowFromPoint(new((int)point.X, (int)point.Y)));
                Assert.True(SetCursorPos((int)point.X, (int)point.Y));
                Assert.True(Wait(() => new Rect(target.RenderSize).Contains(Mouse.GetPosition(target)) && (!requireHover || target.IsMouseOver)),
                    "Native pointer did not reach the target with the expected capture/hover state.");
            }
            void Click(FrameworkElement target)
            {
                target.BringIntoView(); Layout();
                Point point;
                if (target is MenuItem)
                {
                    var approach = new NotebookMenuMouseApproach(target);
                    Assert.True(Wait(() => approach.TryPrepare(animationSettled: Settled(target)) && target.IsMouseOver));
                    point = approach.Position;
                }
                else { Move(target, requireHover: Mouse.Captured is not ContextMenu); point = Center(target); }
                var hwnd = ((HwndSource)PresentationSource.FromVisual(target)).Handle;
                Assert.Equal(hwnd, WindowFromPoint(new((int)point.X, (int)point.Y)));
                bool up = false;
                void Released(object sender, ProcessInputEventArgs e) { if (e.StagingItem.Input is MouseButtonEventArgs { ButtonState: MouseButtonState.Released }) up = true; }
                InputManager.Current.PostProcessInput += Released;
                try
                {
                    Assert.Equal(2u, SendInput(2, [new() { Mouse = new() { Flags = 2 } }, new() { Mouse = new() { Flags = 4 } }], Marshal.SizeOf<NativeInput>()));
                    Assert.True(Wait(() => up), "Native mouse release did not reach WPF."); Layout();
                }
                finally { InputManager.Current.PostProcessInput -= Released; }
            }
            void Save(FrameworkElement element, string suffix)
            {
                var image = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * dpi / 96d), (int)Math.Ceiling(element.ActualHeight * dpi / 96d), dpi, dpi, PixelFormats.Pbgra32);
                // Include the themed host behind transparent controls and normalize
                // the element's local origin instead of retaining its parent offset.
                var visual = new DrawingVisual();
                using (var drawing = visual.RenderOpen())
                {
                    var bounds = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
                    drawing.DrawRectangle((Brush)app.Resources["PtjBackgroundBrush"], null, bounds);
                    var offset = VisualTreeHelper.GetOffset(element);
                    drawing.DrawRectangle(new VisualBrush(element) {
                        ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(offset.X, offset.Y, bounds.Width, bounds.Height),
                        ViewportUnits = BrushMappingMode.Absolute, Viewport = bounds,
                        Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top
                    }, null, bounds);
                }
                image.Render(visual);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using var file = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}-{suffix}.png")); encoder.Save(file);
            }
        });
    }

    internal static (InstrumentsViewModel Model, FakeInstrumentReader Reader) CreateSyntheticModel()
    {
        var reader = new FakeInstrumentReader(); var store = new FakeInstrumentStore(); var deletion = new FakeInstrumentDeletionStore(); var clock = new FixedTimeProvider();
        reader.EnqueueResult([
            new(Guid.Parse("00000000-0000-0000-0000-000000000001"), "ES", "Synthetic S&P futures", AssetClass.Futures, "CME", "USD", .25m, 5m, 20m, true),
            new(Guid.Parse("00000000-0000-0000-0000-000000000002"), "MES", "Synthetic micro futures", AssetClass.Futures, "CME", "EUR", .25m, 1.23456789m, 4.93827156m, true)]);
        return (new(reader, new CreateInstrumentUseCase(store, clock), new InstrumentLifecycleUseCase(store, clock), new GetInstrumentDetailsUseCase(reader),
            new UpdateInstrumentUseCase(store, deletion, clock), new DeleteInstrumentUseCase(store, deletion), new FakeDialogService()), reader);
    }
    private static InstrumentDetails Details(InstrumentListItem item) => new(item.Id, item.Symbol, item.DisplayName, item.AssetClass, item.Exchange,
        item.Currency, item.TickSize, item.TickValue, item.PointValue, item.IsActive, FixedTimeProvider.FixedUtcNow, FixedTimeProvider.FixedUtcNow);
    private static Point Center(FrameworkElement target) => target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2));
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static bool Settled(DependencyObject target)
    {
        for (DependencyObject? item = target; item is not null; item = VisualTreeHelper.GetParent(item))
            if (item is UIElement e && (e.Opacity < 1 || e.RenderTransform is TranslateTransform { HasAnimatedProperties: true } t && (t.X != 0 || t.Y != 0))) return false;
        return true;
    }
    private static void Pump(Task task) { Assert.True(Wait(() => task.IsCompleted)); task.GetAwaiter().GetResult(); }
    private static bool Wait(Func<bool> ready)
    {
        var frame = new DispatcherFrame(); var watch = Stopwatch.StartNew();
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(10) }; bool result = false;
        void Tick(object? sender, EventArgs args) { result = ready(); if (result || watch.Elapsed >= TimeSpan.FromSeconds(3)) frame.Continue = false; }
        timer.Tick += Tick; timer.Start(); try { Dispatcher.PushFrame(frame); return result; } finally { timer.Stop(); timer.Tick -= Tick; }
    }
    private static Color Blend(Color foreground, Color background, double opacity = 1)
    { double alpha = foreground.A / 255d * opacity; return Color.FromRgb((byte)Math.Round(foreground.R * alpha + background.R * (1 - alpha)), (byte)Math.Round(foreground.G * alpha + background.G * (1 - alpha)), (byte)Math.Round(foreground.B * alpha + background.B * (1 - alpha))); }
    private static double Contrast(Color a, Color b)
    { double L(Color c) { double C(byte v) { double n = v / 255d; return n <= .04045 ? n / 12.92 : Math.Pow((n + .055) / 1.055, 2.4); } return .2126 * C(c.R) + .7152 * C(c.G) + .0722 * C(c.B); } return (Math.Max(L(a), L(b)) + .05) / (Math.Min(L(a), L(b)) + .05); }
    private sealed class BindingErrors : TraceListener
    {
        internal List<string> Messages { get; } = [];
        public override void Write(string? message) { if (message is not null) Messages.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
    [StructLayout(LayoutKind.Sequential)] private readonly record struct NativePoint(int X, int Y);
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeInput { public uint Type; public MouseInput Mouse; }
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
}
