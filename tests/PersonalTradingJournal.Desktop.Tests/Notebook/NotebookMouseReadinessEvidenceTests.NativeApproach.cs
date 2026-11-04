using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed partial class NotebookMouseReadinessEvidenceTests
{
    [Fact]
    public async Task OneDistinctNativeApproachRecoversCachedHoverAndClicksExactlyOnce()
    {
        const string host = "PTJ_NOTEBOOK_MENU_APPROACH_HOST";
        if (Environment.GetEnvironmentVariable(host) != "1")
        {
            string identity = $"{typeof(NotebookMouseReadinessEvidenceTests).FullName}.{nameof(OneDistinctNativeApproachRecoversCachedHoverAndClicksExactlyOnce)}";
            await IsolatedTestProcess.RunSuiteAsync(typeof(NotebookMouseReadinessEvidenceTests), "notebook-mouse-approach", host,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30),
                testCaseFilter: $"FullyQualifiedName={identity}", expectedTestName: identity);
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources[SystemParameters.MenuPopupAnimationKey] = PopupAnimation.None;
            var anchor = new Button { Content = "Disposable fixture", Width = 240, Height = 40,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var item = new MenuItem { Name = "DuplicateNoteButton", Header = "Duplicate synthetic item", Width = 220 };
            var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom, StaysOpen = true };
            menu.Items.Add(item);
            var window = NotebookMenuLayout.Window(anchor, 480, 280);
            var rows = new List<object>();
            string directory = EvidenceDirectory();
            Directory.CreateDirectory(directory);
            HwndSource? source = null;
            bool blockGlobalHit = false;
            int moves = 0, nativeMoves = 0, barrierHits = 0, clicks = 0, nativeDown = 0, nativeUp = 0, requests = 0;
            IntPtr Hook(IntPtr hwnd, int message, IntPtr w, IntPtr l, ref bool handled)
            {
                if (message == 0x200) nativeMoves++;
                if (message == 0x201) nativeDown++;
                if (message == 0x202) nativeUp++;
                if (blockGlobalHit && message == 0x84)
                { barrierHits++; handled = true; return new IntPtr(-1); }
                return IntPtr.Zero;
            }
            void Record(string phase)
            {
                Assert.True(GetCursorPos(out var cursor));
                rows.Add(new { phase, utc = DateTimeOffset.UtcNow, processId = Environment.ProcessId,
                    cursor = new { cursor.X, cursor.Y }, position = Mouse.GetPosition(item).ToString(), hover = item.IsMouseOver,
                    over = Mouse.DirectlyOver?.GetType().Name, captured = Mouse.Captured?.GetType().Name,
                    nativeHit = WindowFromPoint(cursor).ToString("X"), popup = source?.Handle.ToString("X"),
                    moves, nativeMoves, barrierHits, requests, clicks, nativeDown, nativeUp });
            }
            try
            {
                window.Show(); window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                var owner = new WindowInteropHelper(window).Handle;
                var origin = anchor.PointToScreen(new Point(10, 10));
                Assert.Equal(owner, WindowFromPoint(new((int)origin.X, (int)origin.Y)));
                Assert.True(SetCursorPos((int)origin.X, (int)origin.Y));
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                menu.IsOpen = true; menu.UpdateLayout();
                Assert.True(WaitNativeApproach(() => item.IsLoaded));
                source = Assert.IsType<HwndSource>(PresentationSource.FromVisual(item));
                Point Center() { var p = item.PointToScreen(new Point(item.ActualWidth / 2, item.ActualHeight / 2)); return new Point((int)p.X, (int)p.Y); }
                var center = Center();
                Assert.True(WaitNativeApproach(() => { center = Center(); return WindowFromPoint(new((int)center.X, (int)center.Y)) == source.Handle; }));
                Assert.Same(menu, Mouse.Captured);
                Assert.Equal("SubTree", NotebookMouseMovementTrace.CaptureMode()?.ToString());
                source.AddHook(Hook);
                menu.AddHandler(Mouse.MouseMoveEvent, new MouseEventHandler((_, _) => moves++), true);
                item.Click += (_, _) => clicks++;
                using var movement = new NotebookMouseMovementTrace(item, directory);
                var approach = new NotebookMenuMouseApproach(item, (point, succeeded) => { requests++; movement.CursorRequested(point, succeeded); });
                // Explicitly model an initial global-hit miss without touching capture.
                blockGlobalHit = true;
                Assert.True(SetCursorPos((int)center.X, (int)center.Y));
                Assert.True(WaitNativeApproach(() => moves > 0 && new Rect(item.RenderSize).Contains(Mouse.GetPosition(item))));
                Assert.True(barrierHits > 0); Assert.False(item.IsMouseOver);
                Record("controlled-initial-miss");
                blockGlobalHit = false;
                Assert.True(approach.HitsTarget(center));
                Assert.Same(menu, Mouse.Captured);
                // Exercise the original readiness behavior for its unchanged three seconds.
                bool originalReady = WaitNativeApproach(() =>
                {
                    Assert.Equal(source.Handle, WindowFromPoint(new((int)center.X, (int)center.Y)));
                    Assert.True(SetCursorPos((int)center.X, (int)center.Y));
                    return item.IsMouseOver;
                });
                Record("original-readiness-deadline");
                Assert.False(originalReady);
                Assert.True(nativeMoves > 1);
                Assert.Same(menu, Mouse.Captured);
                // The real acceptance helper, not a copy of its movement implementation.
                Assert.True(WaitNativeApproach(() => approach.TryPrepare(animationSettled: true) && item.IsMouseOver && approach.HitsTarget(approach.Position)));
                Assert.Equal(1, requests);
                Assert.Equal(new Point(center.X + 2, center.Y), approach.Position);
                Assert.Same(menu, Mouse.Captured);
                Record("one-distinct-approach-established-hover");
                Assert.True(GetCursorPos(out var positioned));
                Assert.Equal(new NativePoint((int)approach.Position.X, (int)approach.Position.Y), positioned);
                Assert.Equal(MouseButtonState.Released, Mouse.LeftButton);
                Assert.Equal(2u, SendInput(2, [new() { Mouse = new() { Flags = 2 } }, new() { Mouse = new() { Flags = 4 } }], Marshal.SizeOf<ApproachInput>()));
                Assert.True(WaitNativeApproach(() => clicks == 1 && nativeUp == 1));
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                Assert.Equal(1, clicks); Assert.Equal(1, nativeDown); Assert.Equal(1, nativeUp);
                Assert.False(menu.IsOpen);
                Record("exactly-one-native-operation");
            }
            finally
            {
                blockGlobalHit = false;
                if (source is not null) source.RemoveHook(Hook);
                NotebookMouseReadinessEvidence.BestEffort(() => File.WriteAllText(Path.Combine(directory, "controlled-approach.json"),
                    JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true })));
                menu.IsOpen = false; window.Close(); app.Shutdown();
            }
        });
    }

    private static bool WaitNativeApproach(Func<bool> ready)
    {
        var frame = new DispatcherFrame(); var watch = System.Diagnostics.Stopwatch.StartNew();
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(10) };
        bool result = false;
        void Tick(object? sender, EventArgs args) { result = ready(); if (result || watch.Elapsed >= TimeSpan.FromSeconds(3)) frame.Continue = false; }
        timer.Tick += Tick; timer.Start();
        try { Dispatcher.PushFrame(frame); return result; }
        finally { timer.Stop(); timer.Tick -= Tick; }
    }

    [StructLayout(LayoutKind.Sequential)] private struct ApproachMouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct ApproachInput { public uint Type; public ApproachMouseInput Mouse; }
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, ApproachInput[] inputs, int size);
}
