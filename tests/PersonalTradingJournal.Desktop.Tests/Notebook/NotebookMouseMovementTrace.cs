using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

// Read-only, bounded observations. Never consumes input, pumps, activates, moves
// the pointer, changes capture or writes to the test runner's progress stream.
internal sealed class NotebookMouseMovementTrace : IDisposable
{
    private readonly FrameworkElement _target;
    private readonly string _directory;
    private readonly TransitionBuffer _rows = new();
    private HwndSource? _source;
    private bool _subscribed;

    internal NotebookMouseMovementTrace(FrameworkElement target, string directory)
    {
        _target = target; _directory = directory;
        NotebookMouseReadinessEvidence.BestEffort(() =>
        {
            _source = PresentationSource.FromVisual(target) as HwndSource;
            _source?.AddHook(NativeMessage);
            InputManager.Current.PostProcessInput += Processed;
            _subscribed = true;
            Add($"attached hwnd=0x{(_source?.Handle ?? IntPtr.Zero):X}; nativeHook={_source is not null}; processedHook={_subscribed}");
        });
    }

    private IntPtr NativeMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message is 0x0200 or 0x0201 or 0x0202 or 0x0215)
            Add($"native hwnd=0x{hwnd:X} message=0x{message:X} client={(short)(lParam.ToInt64() & 0xffff)},{(short)((lParam.ToInt64() >> 16) & 0xffff)} lParam=0x{lParam:X}");
        return IntPtr.Zero;
    }

    private void Processed(object sender, ProcessInputEventArgs args)
    {
        if (args.StagingItem.Input is MouseEventArgs mouse &&
            (mouse.RoutedEvent == Mouse.MouseMoveEvent || mouse.RoutedEvent == Mouse.PreviewMouseMoveEvent ||
             mouse.RoutedEvent == Mouse.GotMouseCaptureEvent || mouse.RoutedEvent == Mouse.LostMouseCaptureEvent ||
             mouse is MouseButtonEventArgs))
            Add($"processed event={mouse.RoutedEvent.Name} timestamp={mouse.Timestamp} source={Describe(mouse.OriginalSource)} handled={mouse.Handled}");
    }

    private static string Describe(object? value) => value is FrameworkElement element
        ? $"{element.GetType().Name}#{element.Name}" : value?.GetType().Name ?? "null";

    private void Add(string observation) => NotebookMouseReadinessEvidence.BestEffort(() =>
    {
        bool cursorRead = GetCursorPos(out var cursor);
        string row = string.Create(CultureInfo.InvariantCulture,
            $"{DateTimeOffset.UtcNow:O} target={_target.Name}; {observation}; nativeCursor={cursorRead}/{cursor}; managedPosition={Mouse.GetPosition(_target)}; over={Describe(Mouse.DirectlyOver)}; capture={Describe(Mouse.Captured)}; captureMode={CaptureMode()}; hover={_target.IsMouseOver}");
        _rows.Add(row);
    });

    // Observe the caller's existing SetCursorPos result; never issue input here.
    internal void CursorRequested(Point point, bool succeeded) =>
        Add($"cursor-request point={point}; succeeded={succeeded}");

    // A rolling tail alone lost the onset of CI #116's failure. Retain the
    // beginning and deadline, within the original 64-observation memory bound.
    internal sealed class TransitionBuffer
    {
        private readonly List<string> _first = new();
        private readonly Queue<string> _last = new();
        internal int Total { get; private set; }
        internal void Add(string row)
        {
            Total++;
            if (_first.Count < 16) _first.Add(row);
            else
            {
                if (_last.Count == 48) _last.Dequeue();
                _last.Enqueue(row);
            }
        }
        internal IEnumerable<string> Snapshot() => _first
            .Concat(new[] { $"trace-retention total={Total}; omittedMiddle={Total - _first.Count - _last.Count}; first={_first.Count}; last={_last.Count}" })
            .Concat(_last);
    }

    internal static object? CaptureMode() => typeof(MouseDevice).GetProperty("CapturedMode",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(Mouse.PrimaryDevice) ?? "unavailable";

    public void Dispose()
    {
        Add("detaching");
        NotebookMouseReadinessEvidence.BestEffort(() => { if (_subscribed) InputManager.Current.PostProcessInput -= Processed; });
        NotebookMouseReadinessEvidence.BestEffort(() => _source?.RemoveHook(NativeMessage));
        NotebookMouseReadinessEvidence.BestEffort(() => File.AppendAllLines(Path.Combine(_directory, "mouse-movement.log"), _rows.Snapshot()));
    }

    [StructLayout(LayoutKind.Sequential)] private readonly record struct NativePoint(int X, int Y);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
}
