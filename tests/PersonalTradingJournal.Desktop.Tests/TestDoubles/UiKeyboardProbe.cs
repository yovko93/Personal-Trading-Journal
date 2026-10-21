using System.Runtime.InteropServices;
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
        int clicks = 0;
        void Clicked(object sender, RoutedEventArgs args) { if (ReferenceEquals(args.Source, target)) clicks++; }
        target.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(Clicked), handledEventsToo: true);
        try
        {
            var source = PresentationSource.FromVisual(target);
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space) { RoutedEvent = Keyboard.KeyDownEvent });
            whilePressed?.Invoke();
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space) { RoutedEvent = Keyboard.KeyUpEvent });
            Require(clicks == 1, target, $"Space must invoke exactly one Click, observed {clicks}.");
            Write(target, "Space invoked one Click");
        }
        finally { target.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(Clicked)); }
    }

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
        return $"target={target.GetType().Name}/{target.Name}; STA={Environment.CurrentManagedThreadId}; " +
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
}
