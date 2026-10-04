using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Views.Calendar;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

/// <summary>Verifies modal focus restoration without repairing the selected element's focus.</summary>
internal sealed class CalendarFocusProbe : IDisposable
{
    private static readonly object LogLock = new();
    private readonly Window _owner;
    private readonly CalendarDayHost _cell;
    private readonly string _scenario;
    private readonly Queue<string> _history = new();
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private Window? _dialog;
    private DispatcherOperation? _check;
    private DispatcherTimer? _deadline;
    private DispatcherFrame? _frame;
    private string? _failure;
    private bool _verified, _ownerClosed, _disposed;

    internal CalendarFocusProbe(Window owner, CalendarDayHost cell, [CallerMemberName] string scenario = "")
    {
        _owner = owner; _cell = cell; _scenario = scenario;
        owner.Activated += OnActivated;
        owner.Deactivated += OnDeactivated;
        owner.GotKeyboardFocus += OnKeyboardFocus;
        owner.LostKeyboardFocus += OnKeyboardFocus;
        owner.Closed += OnOwnerClosed;
        Capture("observer attached");
    }

    internal void ObserveDialog(Window dialog)
    {
        if (_dialog is not null) throw new InvalidOperationException("A dialog is already observed.");
        _dialog = dialog;
        dialog.Activated += OnActivated;
        dialog.Deactivated += OnDeactivated;
        dialog.Closed += OnDialogClosed;
        Capture("dialog observed");
    }

    internal void Capture(string stage) => Write(stage);

    internal void VerifyRestored()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _owner.Dispatcher.VerifyAccess();
        if (_frame is not null) throw new InvalidOperationException("Focus verification is already running.");
        Capture("before activation: verify production logical focus");
        Require(ReferenceEquals(FocusManager.GetFocusedElement(_owner), _cell),
            "Calendar did not restore the selected date's logical focus; the test will not repair it.");
        Require(_cell.IsVisible && _cell.IsEnabled && _cell.Focusable && PresentationSource.FromVisual(_cell) is not null,
            "The selected date is not a connected, visible, enabled and focusable target.");
        Require(!_ownerClosed && _owner.IsVisible && _owner.WindowState != WindowState.Minimized,
            "The Calendar owner must be visible and restored before testing keyboard focus.");
        _verified = false; _failure = null;
        _frame = new DispatcherFrame();
        _deadline = new DispatcherTimer(DispatcherPriority.Send, _owner.Dispatcher) { Interval = TimeSpan.FromSeconds(5) };
        _deadline.Tick += OnDeadline;
        _deadline.Start();
        try
        {
            // Activate this STA's test-owned HWND once, without asking Windows to
            // steal global foreground activation. Window.Activate can return false
            // on a hidden desktop even when this window already has keyboard focus.
            // WPF must restore the retained element itself; never call cell.Focus.
            IntPtr previous = SetActiveWindow(new WindowInteropHelper(_owner).Handle);
            Capture($"SetActiveWindow previous=0x{previous:X}");
            Check();
            if (_frame.Continue) Dispatcher.PushFrame(_frame);
            Require(_verified, _failure ?? "Dispatcher stopped before the selected date regained keyboard focus.");
            Require(ReferenceEquals(Keyboard.FocusedElement, _cell) && _cell.IsKeyboardFocused,
                "The selected date lost keyboard focus before verification completed.");
            Capture("verified selected date keyboard focus");
        }
        finally { StopPendingWork(); _frame = null; }
    }

    private void OnActivated(object? sender, EventArgs e) => Changed(ReferenceEquals(sender, _owner) ? "owner Activated" : "dialog Activated");
    private void OnDeactivated(object? sender, EventArgs e) => Changed(ReferenceEquals(sender, _owner) ? "owner Deactivated" : "dialog Deactivated");
    private void OnKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => Changed($"{e.RoutedEvent.Name}: {Identify(e.OldFocus)} -> {Identify(e.NewFocus)}");
    private void OnDialogClosed(object? sender, EventArgs e) => Changed("dialog Closed");
    private void OnOwnerClosed(object? sender, EventArgs e) { _ownerClosed = true; Changed("owner Closed"); }
    private void OnDeadline(object? sender, EventArgs e)
    {
        _failure = "Selected date did not regain keyboard focus within the bounded five-second activation deadline.";
        if (_frame is not null) _frame.Continue = false;
    }

    private void Changed(string stage)
    {
        Record(stage);
        if (_disposed || _frame is not { Continue: true } || _check is not null) return;
        // Activation subscribers and native focus restoration finish before this check.
        _check = _owner.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => { _check = null; Check(); }));
    }

    private void Check()
    {
        if (_disposed || _frame is not { Continue: true }) return;
        if (_ownerClosed) { _failure = "Calendar owner closed before keyboard focus was restored."; _frame.Continue = false; }
        else if (_owner.IsActive && GetActiveWindow() == new WindowInteropHelper(_owner).Handle && _cell.IsKeyboardFocused)
        {
            _verified = true;
            _frame.Continue = false;
        }
    }

    private void Require(bool condition, string message)
    {
        if (condition) return;
        Write("FAILED: " + message);
        throw new InvalidOperationException(message + Environment.NewLine + string.Join(Environment.NewLine, _history));
    }

    private void Record(string stage)
    {
        if (_history.Count == 64) _history.Dequeue();
        _history.Enqueue($"{_elapsed.Elapsed.TotalMilliseconds:F1}ms {stage}; owner={DescribeWindow(_owner)}; " +
            $"dialog={(_dialog is null ? "unobserved" : DescribeWindow(_dialog))}; " +
            $"keyboard={Identify(Keyboard.FocusedElement)}; logical={Identify(FocusManager.GetFocusedElement(_owner))}; " +
            $"cell={Identify(_cell)}; visible={_cell.IsVisible}; enabled={_cell.IsEnabled}; focusable={_cell.Focusable}; " +
            $"keyboardFocused={_cell.IsKeyboardFocused}; source={PresentationSource.FromVisual(_cell)?.GetType().Name ?? "null"}; " +
            $"size={_cell.ActualWidth:F2}x{_cell.ActualHeight:F2}; " +
            $"native foreground=0x{GetForegroundWindow():X}; active=0x{GetActiveWindow():X}; focus=0x{GetFocus():X}");
    }

    private static string DescribeWindow(Window window) => $"{window.GetType().Name}[HWND=0x{new WindowInteropHelper(window).Handle:X}," +
        $"active={window.IsActive},visible={window.IsVisible},enabled={window.IsEnabled},state={window.WindowState}]";
    private static string Identify(object? value) => value is null ? "null" :
        $"{value.GetType().Name}#{RuntimeHelpers.GetHashCode(value):X}" + (value is FrameworkElement e ? $"({e.Name})" : "");

    private void Write(string stage)
    {
        Record(stage);
        string text = $"{DateTimeOffset.UtcNow:O} {_scenario}; STA={Environment.CurrentManagedThreadId}" +
            Environment.NewLine + string.Join(Environment.NewLine, _history);
        Console.WriteLine(text);
        if (Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") is not { Length: > 0 } output) return;
        try
        {
            Directory.CreateDirectory(output);
            lock (LogLock) File.AppendAllText(Path.Combine(output, "calendar-focus.log"), text + Environment.NewLine);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Console.WriteLine("Could not write optional focus diagnostics: " + error.Message); }
    }

    private void StopPendingWork()
    {
        _check?.Abort(); _check = null;
        if (_deadline is null) return;
        _deadline.Stop(); _deadline.Tick -= OnDeadline; _deadline = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; StopPendingWork();
        _owner.Activated -= OnActivated; _owner.Deactivated -= OnDeactivated; _owner.Closed -= OnOwnerClosed;
        _owner.GotKeyboardFocus -= OnKeyboardFocus; _owner.LostKeyboardFocus -= OnKeyboardFocus;
        if (_dialog is not null)
        { _dialog.Activated -= OnActivated; _dialog.Deactivated -= OnDeactivated; _dialog.Closed -= OnDialogClosed; }
        if (_frame is not null) _frame.Continue = false;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr SetActiveWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetFocus();
}
