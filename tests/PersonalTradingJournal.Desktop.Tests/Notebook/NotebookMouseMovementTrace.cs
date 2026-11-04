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
    private readonly Queue<string> _rows = new();
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
            Add("attached");
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
        if (_rows.Count == 64) _rows.Dequeue();
        _rows.Enqueue(row);
    });

    internal static object? CaptureMode() => typeof(MouseDevice).GetProperty("CapturedMode",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(Mouse.PrimaryDevice) ?? "unavailable";

    public void Dispose()
    {
        Add("detaching");
        NotebookMouseReadinessEvidence.BestEffort(() => { if (_subscribed) InputManager.Current.PostProcessInput -= Processed; });
        NotebookMouseReadinessEvidence.BestEffort(() => _source?.RemoveHook(NativeMessage));
        NotebookMouseReadinessEvidence.BestEffort(() => File.AppendAllLines(Path.Combine(_directory, "mouse-movement.log"), _rows));
    }

    [StructLayout(LayoutKind.Sequential)] private readonly record struct NativePoint(int X, int Y);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
}
