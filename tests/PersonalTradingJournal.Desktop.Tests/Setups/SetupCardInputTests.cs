using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Reflection;
using System.Windows.Media;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Views.Setups;

namespace PersonalTradingJournal.Desktop.Tests.Setups;

[Collection(ExclusiveWpfCollection.Name)]
[Trait(ExclusiveWpfCollection.Trait, ExclusiveWpfCollection.Value)]
public sealed class SetupCardInputTests
{
    [Fact]
    public async Task MouseAndSpaceOpenOriginalAndReturnFocusWithoutRemovingExample()
    {
        const string host = "PTJ_SETUP_CARD_INPUT_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(SetupCardInputTests), "setup-card-input", host,
                TimeSpan.FromSeconds(60), caseHangTimeout: TimeSpan.FromSeconds(30));
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown,
                Resources = CalendarViewLayoutTests.SharedThemeResources("Light") };
            var setup = new TradingSetupDetails(Guid.NewGuid(), "Retest", "Support", true, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
            var images = new SetupExampleViewTests.Images();
            var example = images.AddAsync(setup.Id, "Bounce.png", [1], "1h", default).GetAwaiter().GetResult();
            var other = images.AddAsync(setup.Id, "Another.png", [2], null, default).GetAwaiter().GetResult();
            var dialogs = new FakeDialogService();
            var vm = TradingSetupsViewModelTests.Create(new FakeTradingSetupReader { DetailsToReturn = setup }, dialog: dialogs,
                examples: images, viewer: images);
            vm.ViewCommand.ExecuteAsync(setup.Id).GetAwaiter().GetResult();
            var view = new TradingSetupsView { DataContext = vm };
            var window = NotebookMenuLayout.Window(view, 760, 680);
            try
            {
                window.Show(); window.UpdateLayout();
                var open = SetupExampleViewTests.Children(view).OfType<Button>().Single(b => b.Name == "OpenExampleCard" && AutomationProperties.GetName(b) == "Open Bounce.png, 1h");
                var back = (Button)view.FindName("BackToCards");
                Assert.Equal("Open Bounce.png, 1h", AutomationProperties.GetName(open));
                UiKeyboardProbe.Ready(open);
                Assert.Same(app.Resources["PtjTextPrimaryBrush"], ((Border)open.Template.FindName("FocusIndicator", open)).BorderBrush);
                var hwnd = ((HwndSource)PresentationSource.FromVisual(open)).Handle;
                var point = open.PointToScreen(new Point(open.ActualWidth / 2, open.ActualHeight / 2));
                var screen = new NativePoint((int)point.X, (int)point.Y);
                Assert.Equal(hwnd, WindowFromPoint(screen));
                MovePointerTo(screen);
                Until(() => open.IsMouseOver && new Rect(open.RenderSize).Contains(Mouse.GetPosition(open)));
                Assert.Equal(hwnd, WindowFromPoint(screen));
                // The mouse guard needs global foreground ownership. Ready prepares thread-local keyboard focus.
                UiKeyboardProbe.Origin("before Setup Window.Activate", open);
                var activated = window.Activate();
                UiKeyboardProbe.Origin($"after Setup Window.Activate; returned={activated}", open);
                var foreground = GetForegroundWindow();
                RetainBoundary(open, window, hwnd, foreground, screen);
                Assert.Equal(hwnd, foreground);
                Assert.Equal(2u, SendInput(2, [new() { Mouse = new() { Flags = 2 } }, new() { Mouse = new() { Flags = 4 } }], Marshal.SizeOf<NativeInput>()));
                VerifyPreview(example.Id);
                var alternate = SetupExampleViewTests.Children(view).OfType<Button>().Single(b => b.Name == "OpenExampleCard" &&
                    AutomationProperties.GetName(b) == "Open Another.png");
                UiKeyboardProbe.Space(alternate);
                VerifyPreview(other.Id);
                CloseTo(alternate);
                UiKeyboardProbe.Space(open);
                VerifyPreview(example.Id);
                CloseTo(open);

                void VerifyPreview(Guid id)
                {
                    Until(() => vm.Preview?.Card.Example.Id == id && back.IsKeyboardFocused);
                    Assert.Null(dialogs.ConfirmationRequest);
                    Assert.Equal(2, images.Rows.Count);
                    Assert.True(open.IsVisible);
                }

                void CloseTo(Button opener)
                {
                    UiKeyboardProbe.Space(back);
                    Until(() => vm.Preview is null && opener.IsKeyboardFocused);
                }
            }
            finally { window.Close(); app.Shutdown(); }
        });
    }

    // Sample before cleanup. Observations never activate a window, pump the dispatcher or repair input.
    private static void RetainBoundary(Button target, Window window, IntPtr expected, IntPtr foreground, NativePoint intended)
    {
        NotebookMouseReadinessEvidence.BestEffort(() =>
        {
            string ProcessName(IntPtr hwnd)
            {
                try
                {
                    GetWindowThreadProcessId(hwnd, out uint pid);
                    using var process = Process.GetProcessById(checked((int)pid));
                    return process.ProcessName;
                }
                catch (Exception error) { return "unavailable: " + error.GetType().Name; }
            }
            bool cursorRead = GetCursorPos(out var cursor);
            var dpi = VisualTreeHelper.GetDpi(target);
            var snapshot = new
            {
                ObservedAtUtc = DateTimeOffset.UtcNow,
                Identity = typeof(SetupCardInputTests).FullName + "." + nameof(MouseAndSpaceOpenOriginalAndReturnFocusWithoutRemovingExample),
                ChildProcessId = Environment.ProcessId,
                Source = typeof(SetupCardInputTests).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                Stage = "foreground guard before SendInput", Passed = expected == foreground,
                ExpectedWindow = NotebookMouseReadinessEvidence.NativeWindow(expected),
                AssertedForegroundWindow = NotebookMouseReadinessEvidence.NativeWindow(foreground),
                ForegroundProcess = ProcessName(foreground), ExpectedProcess = ProcessName(expected),
                WindowAtTarget = NotebookMouseReadinessEvidence.NativeWindow(WindowFromPoint(intended)),
                ForegroundAtCapture = NotebookMouseReadinessEvidence.ForegroundWindow(),
                Intended = intended.ToString(), CursorRead = cursorRead, Cursor = cursor.ToString(),
                Dpi = new { dpi.DpiScaleX, dpi.DpiScaleY }, Theme = "Light", WidthDip = 760, HeightDip = 680,
                Window = new { window.IsActive, window.IsLoaded, window.IsVisible, window.ActualWidth, window.ActualHeight },
                Target = new { target.Name, target.IsLoaded, target.IsVisible, target.IsEnabled, target.IsMouseOver,
                    target.IsKeyboardFocused, target.IsMeasureValid, target.IsArrangeValid, target.ActualWidth, target.ActualHeight },
                KeyboardFocus = Keyboard.FocusedElement?.ToString(), MouseCapture = Mouse.Captured?.ToString(),
                WpfMousePosition = Mouse.GetPosition(target).ToString(),
                Dispatcher = new { target.Dispatcher.HasShutdownStarted, target.Dispatcher.HasShutdownFinished }
            };
            string root = Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") ?? AppContext.BaseDirectory;
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "setup-input-boundary.json"),
                JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        });
    }

    private static void MovePointerTo(NativePoint point)
    {
        // Send the approach to the verified card as native input on the full virtual desktop.
        // The midpoint of the pixel maps back to the intended physical coordinate at high DPI.
        static int Normalize(int pixel, int origin, int extent) =>
            checked((int)((2L * (pixel - origin) + 1) * 65536 / (2L * extent)));
        const uint move = 0x0001, virtualDesktop = 0x4000, absolute = 0x8000;
        const int virtualLeft = 76, virtualTop = 77, virtualWidth = 78, virtualHeight = 79;
        var motion = new NativeInput { Mouse = new MouseInput
        {
            X = Normalize(point.X, GetSystemMetrics(virtualLeft), GetSystemMetrics(virtualWidth)),
            Y = Normalize(point.Y, GetSystemMetrics(virtualTop), GetSystemMetrics(virtualHeight)),
            Flags = move | virtualDesktop | absolute
        } };
        Assert.Equal(1u, SendInput(1, [motion], Marshal.SizeOf<NativeInput>()));
    }

    private static void Until(Func<bool> predicate)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate() && clock.Elapsed < TimeSpan.FromSeconds(3))
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.True(predicate(), "The expected card input or focus transition did not complete.");
    }

    [StructLayout(LayoutKind.Sequential)] private readonly record struct NativePoint(int X, int Y);
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeInput { public uint Type; public MouseInput Mouse; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int metric);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, NativeInput[] input, int size);
}
