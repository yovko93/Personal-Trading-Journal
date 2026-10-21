using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

/// <summary>Readiness for deliberate test input, never a repair of post-action focus.</summary>
internal static class UiKeyboardProbe
{
    internal static void Ready(FrameworkElement target)
    {
        target.Dispatcher.VerifyAccess();
        var window = Window.GetWindow(target);
        Require(window is not null && ReferenceEquals(window.Dispatcher, target.Dispatcher), target,
            "Target must belong to a window on the current dispatcher.");
        var owner = window!;
        Require(!owner.Dispatcher.HasShutdownStarted && owner.IsLoaded && owner.IsVisible, target,
            "Input requires a loaded, visible window with a live dispatcher.");
        target.BringIntoView();
        var frame = new DispatcherFrame();
        bool ready = false;
        var deadline = new DispatcherTimer(DispatcherPriority.Send, owner.Dispatcher) { Interval = TimeSpan.FromSeconds(5) };
        deadline.Tick += Expired;
        DispatcherOperation? check = null;
        void Expired(object? sender, EventArgs args) => frame.Continue = false;
        void Check()
        {
            check = null;
            ready = owner.IsActive && GetActiveWindow() == new WindowInteropHelper(owner).Handle &&
                target.IsLoaded && target.IsVisible && target.IsEnabled && target.Focusable &&
                PresentationSource.FromVisual(target) is { } source && ReferenceEquals(source, PresentationSource.FromVisual(owner)) &&
                ReferenceEquals(Keyboard.FocusedElement, target) && target.IsKeyboardFocused &&
                (target is not ButtonBase button || button.Command is null || button.Command.CanExecute(button.CommandParameter));
            if (ready) frame.Continue = false;
        }
        void Changed(object? sender, EventArgs args)
        {
            if (frame.Continue && check is null)
                check = owner.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(Check));
        }
        owner.Activated += Changed;
        target.GotKeyboardFocus += Changed;
        deadline.Start();
        try
        {
            // Flush bindings/layout before focusing the intended input target. Activate only
            // this thread's HWND; do not steal global foreground activation from another app.
            check = owner.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                check = null;
                owner.UpdateLayout();
                SetActiveWindow(new WindowInteropHelper(owner).Handle);
                Keyboard.Focus(target);
                Changed(null, EventArgs.Empty);
            }));
            Dispatcher.PushFrame(frame);
            Require(ready, target, "Keyboard readiness was not established within five seconds.");
            Require(Keyboard.Modifiers == ModifierKeys.None && Mouse.LeftButton == MouseButtonState.Released,
                target, "Synthetic input requires released modifiers and mouse button.");
            Write(target, "ready");
        }
        finally
        {
            check?.Abort(); deadline.Stop(); deadline.Tick -= Expired;
            owner.Activated -= Changed; target.GotKeyboardFocus -= Changed;
        }
    }

    internal static void Space(ButtonBase target, Action? whilePressed = null)
    {
        Ready(target);
        using var trace = new InputTrace(target);
        int clicks = 0;
        void Clicked(object sender, RoutedEventArgs args)
        {
            if (ReferenceEquals(args.Source, target)) clicks++;
            trace.Record($"Click count={clicks}; source={Identify(args.Source)}; handled={args.Handled}");
        }
        target.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(Clicked), handledEventsToo: true);
        try
        {
            var source = (HwndSource)PresentationSource.FromVisual(target);
            Require(Mouse.Captured is null && GetCapture() == IntPtr.Zero, target,
                "Space requires no pre-existing mouse capture.");
            trace.Record("before KeyDown");
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space) { RoutedEvent = Keyboard.KeyDownEvent });
            trace.Record("after KeyDown / before pressed observation");
            Require(HasCapture(target, source), target,
                "Space key-down did not acquire managed and native capture. Check input-desktop access; a native HWND capture alone is insufficient.");
            whilePressed?.Invoke();
            trace.Record("after pressed observation / before KeyUp");
            Require(HasCapture(target, source), target, "Space capture was lost before key-up.");
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space) { RoutedEvent = Keyboard.KeyUpEvent });
            trace.Record("after KeyUp");
            WaitForRelease(target, source, trace);
            Require(clicks == 1, target, $"Space must invoke exactly one Click, observed {clicks}.");
            Write(target, "Space invoked one Click");
        }
        finally { target.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(Clicked)); }
    }

    private static bool HasCapture(ButtonBase target, HwndSource source) =>
        target.IsMouseCaptured && ReferenceEquals(Mouse.Captured, target) && GetCapture() == source.Handle;

    private static void WaitForRelease(ButtonBase target, HwndSource source, InputTrace trace)
    {
        // ButtonBase clears IsPressed through its real LostMouseCapture handler.
        // Wait for that input processing, never manufacture a lost-capture event,
        // clear the property, move focus, or use fixture cleanup to complete it.
        var frame = new DispatcherFrame();
        bool released = false;
        DispatcherOperation? check = null;
        var deadline = new DispatcherTimer(DispatcherPriority.Send, target.Dispatcher) { Interval = TimeSpan.FromSeconds(5) };
        var pressed = DependencyPropertyDescriptor.FromProperty(ButtonBase.IsPressedProperty, target.GetType());
        void Check()
        {
            check = null;
            released = !target.IsPressed && !target.IsMouseCaptured &&
                !ReferenceEquals(Mouse.Captured, target) && GetCapture() != source.Handle;
            if (released) frame.Continue = false;
        }
        void Changed(object? sender, EventArgs e)
        {
            if (frame.Continue && check is null)
                check = target.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(Check));
        }
        void Expired(object? sender, EventArgs e) => frame.Continue = false;
        pressed.AddValueChanged(target, Changed);
        target.LostMouseCapture += Changed;
        deadline.Tick += Expired;
        deadline.Start();
        try
        {
            trace.Record("release completion wait begins");
            Changed(null, EventArgs.Empty);
            Dispatcher.PushFrame(frame);
            trace.Record("release completion check / ContextIdle returned");
            Require(released, target, "Space did not complete native/managed capture release and clear IsPressed within five seconds.");
        }
        finally
        {
            check?.Abort(); deadline.Stop(); deadline.Tick -= Expired;
            target.LostMouseCapture -= Changed; pressed.RemoveValueChanged(target, Changed);
        }
    }

    internal static void Observe(FrameworkElement target, string stage) => Write(target, stage);

    private sealed class InputTrace : IDisposable
    {
        private readonly ButtonBase _target;
        private readonly Window _window;
        private readonly Stopwatch _elapsed = Stopwatch.StartNew();
        private readonly DependencyPropertyDescriptor _pressed;
        private readonly Dictionary<DispatcherOperation, DispatcherPriority> _pending = [];
        internal InputTrace(ButtonBase target)
        {
            _target = target; _window = Window.GetWindow(target);
            _pressed = DependencyPropertyDescriptor.FromProperty(ButtonBase.IsPressedProperty, target.GetType());
            _pressed.AddValueChanged(target, PressedChanged);
            target.AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler(Key), true);
            target.AddHandler(Keyboard.KeyUpEvent, new KeyEventHandler(Key), true);
            target.AddHandler(Mouse.GotMouseCaptureEvent, new MouseEventHandler(Capture), true);
            target.AddHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler(Capture), true);
            target.AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Focus), true);
            target.AddHandler(Keyboard.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Focus), true);
            _window.Activated += Activated; _window.Deactivated += Deactivated;
            target.Dispatcher.Hooks.OperationPosted += Posted;
            target.Dispatcher.Hooks.OperationCompleted += Completed;
            target.Dispatcher.Hooks.OperationAborted += Completed;
            Record("observers attached");
        }
        internal void Record(string phase)
        {
            string pending;
            lock (_pending) pending = string.Join(",", _pending.Values.GroupBy(p => p).Select(g => $"{g.Key}:{g.Count()}"));
            Write(_target, $"input {_elapsed.Elapsed.TotalMilliseconds:F3}ms {phase}; observed pending=[{pending}]");
        }
        private void Posted(object? sender, DispatcherHookEventArgs e) { lock (_pending) _pending[e.Operation] = e.Operation.Priority; }
        private void Completed(object? sender, DispatcherHookEventArgs e) { lock (_pending) _pending.Remove(e.Operation); }
        private void PressedChanged(object? sender, EventArgs e) => Record("IsPressed changed");
        private void Key(object sender, KeyEventArgs e) => Record($"{e.RoutedEvent.Name} key={e.Key}; handled={e.Handled}; source={Identify(e.OriginalSource)}; eventIsDown={e.IsDown}");
        private void Capture(object sender, MouseEventArgs e) => Record($"{e.RoutedEvent.Name}; source={Identify(e.OriginalSource)}; handled={e.Handled}");
        private void Focus(object sender, KeyboardFocusChangedEventArgs e) => Record($"{e.RoutedEvent.Name}; {Identify(e.OldFocus)} -> {Identify(e.NewFocus)}");
        private void Activated(object? sender, EventArgs e) => Record("window Activated");
        private void Deactivated(object? sender, EventArgs e) => Record("window Deactivated");
        public void Dispose()
        {
            Record("observer detached; fixture cleanup has not run");
            _pressed.RemoveValueChanged(_target, PressedChanged);
            _target.RemoveHandler(Keyboard.KeyDownEvent, new KeyEventHandler(Key));
            _target.RemoveHandler(Keyboard.KeyUpEvent, new KeyEventHandler(Key));
            _target.RemoveHandler(Mouse.GotMouseCaptureEvent, new MouseEventHandler(Capture));
            _target.RemoveHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler(Capture));
            _target.RemoveHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Focus));
            _target.RemoveHandler(Keyboard.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Focus));
            _window.Activated -= Activated; _window.Deactivated -= Deactivated;
            _target.Dispatcher.Hooks.OperationPosted -= Posted;
            _target.Dispatcher.Hooks.OperationCompleted -= Completed;
            _target.Dispatcher.Hooks.OperationAborted -= Completed;
        }
    }

    private static string Identify(object? value) => value is null ? "null" :
        $"{value.GetType().Name}#{RuntimeHelpers.GetHashCode(value):X}" + (value is FrameworkElement e ? $"/{e.Name}" : "");

    internal static Task SpaceCommand(ButtonBase target)
    {
        var command = target.Command as IAsyncRelayCommand ?? throw new InvalidOperationException("Expected an async command.");
        Task? previous = command.ExecutionTask;
        Space(target);
        try { return RequireNewExecution(previous, command.ExecutionTask); }
        catch (InvalidOperationException) { Write(target, "FAILED: no fresh command execution"); throw; }
    }

    // The fixture holds asynchronous reads until after input so a new execution cannot
    // be confused with a cached, synchronously completed task from a previous invocation.
    internal static Task RequireNewExecution(Task? previous, Task? current) =>
        current is not null && !ReferenceEquals(previous, current) ? current :
            throw new InvalidOperationException("Keyboard input did not start a fresh command ExecutionTask.");

    private static void Require(bool condition, FrameworkElement target, string reason)
    {
        if (condition) return;
        string detail = Describe(target);
        Write(target, "FAILED: " + reason);
        throw new InvalidOperationException(reason + " " + detail);
    }

    private static string Describe(FrameworkElement target)
    {
        var owner = Window.GetWindow(target);
        return $"target={target.GetType().Name}/{target.Name}#{RuntimeHelpers.GetHashCode(target):X}; STA={Environment.CurrentManagedThreadId}; " +
            $"owner={Identify(owner)}; sameDispatcher={ReferenceEquals(owner?.Dispatcher, target.Dispatcher)}; " +
            $"pressed={(target as ButtonBase)?.IsPressed}; clickMode={(target as ButtonBase)?.ClickMode}; " +
            $"captured={Identify(Mouse.Captured)}; isMouseCaptured={target.IsMouseCaptured}; nativeCapture=0x{GetCapture():X}; " +
            $"spaceDown={Keyboard.IsKeyDown(Key.Space)}; leftMouse={Mouse.LeftButton}; " +
            $"mouseSource={Identify(Mouse.PrimaryDevice.ActiveSource)}; keyboardSource={Identify(Keyboard.PrimaryDevice.ActiveSource)}; " +
            $"window loaded={owner?.IsLoaded},visible={owner?.IsVisible},active={owner?.IsActive}; " +
            $"target loaded={target.IsLoaded},visible={target.IsVisible},enabled={target.IsEnabled},focusable={target.Focusable}; " +
            $"source={PresentationSource.FromVisual(target)?.GetType().Name}; keyboard={Keyboard.FocusedElement}; " +
            $"keyboardFocused={target.IsKeyboardFocused}; commandReady={(target as ButtonBase)?.Command?.CanExecute((target as ButtonBase)?.CommandParameter)}; " +
            $"active=0x{GetActiveWindow():X}; foreground=0x{GetForegroundWindow():X}; nativeFocus=0x{GetFocus():X}; modifiers={Keyboard.Modifiers}";
    }

    private static void Write(FrameworkElement target, string stage)
    {
        string text = $"{DateTimeOffset.UtcNow:O} {stage}; {Describe(target)}";
        Console.WriteLine(text);
        if (Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") is { Length: > 0 } directory)
            File.AppendAllText(Path.Combine(directory, "keyboard-readiness.log"), text + Environment.NewLine);
    }

    [DllImport("user32.dll")] private static extern IntPtr SetActiveWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetFocus();
    [DllImport("user32.dll")] private static extern IntPtr GetCapture();
}
