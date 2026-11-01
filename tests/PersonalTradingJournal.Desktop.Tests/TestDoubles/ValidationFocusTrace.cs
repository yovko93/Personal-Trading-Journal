using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

/// <summary>Opt-in B1 observation only: no input, focus calls, dispatcher work or waits.
/// Private request fields are read solely to associate existing PageFocus callbacks.
/// Buffered observations are written after the assertions, including on failure.</summary>
internal sealed class ValidationFocusTrace : IDisposable
{
    internal static bool Enabled => Environment.GetEnvironmentVariable("PTJ_VALIDATION_FOCUS_TRACE") == "1";
    // Full-family verification keeps the existing shared child while retaining observations.
    internal static bool SharedSuite => Environment.GetEnvironmentVariable("PTJ_VALIDATION_FOCUS_SHARED_SUITE") == "1";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly Window _window;
    private readonly SessionPreparationViewModel _vm;
    private readonly List<object> _rows = [];
    private readonly KeyboardFocusChangedEventHandler _focusHandler;
    private readonly Dictionary<DispatcherOperation, object> _requests = [];
    private readonly string _name;

    private ValidationFocusTrace(string name, Window window, SessionPreparationViewModel vm)
    {
        _name = name; _window = window; _vm = vm;
        _focusHandler = (_, e) => Record(e.RoutedEvent.Name, new { Old = Id(e.OldFocus), New = Id(e.NewFocus), e.Handled });
        window.AddHandler(Keyboard.GotKeyboardFocusEvent, _focusHandler, true);
        window.AddHandler(Keyboard.LostKeyboardFocusEvent, _focusHandler, true);
        window.Activated += Activation; window.Deactivated += Activation;
        vm.PropertyChanged += Changed;
        window.Dispatcher.Hooks.OperationPosted += Posted;
        window.Dispatcher.Hooks.OperationStarted += Started;
        window.Dispatcher.Hooks.OperationCompleted += Completed;
        Record("attached");
    }

    internal static ValidationFocusTrace? Attach(string name, Window window, SessionPreparationViewModel vm) =>
        Enabled ? new(name, window, vm) : null;

    internal void Record(string phase, object? detail = null)
    {
        nint foreground = GetForegroundWindow();
        uint foregroundThread = GetWindowThreadProcessId(foreground, out uint foregroundProcess);
        _rows.Add(new
        {
            Utc = DateTimeOffset.UtcNow, Phase = phase, Process = Environment.ProcessId,
            Thread = Environment.CurrentManagedThreadId, Dispatcher = Id(_window.Dispatcher),
            Window = Id(_window), Hwnd = new WindowInteropHelper(_window).Handle.ToInt64(),
            _window.IsActive, _window.IsLoaded, Foreground = foreground.ToInt64(), foregroundThread, foregroundProcess,
            Keyboard = Id(Keyboard.FocusedElement), Capture = Id(Mouse.Captured),
            Application = Id(System.Windows.Application.Current),
            ApplicationDispatcher = Id(System.Windows.Application.Current?.Dispatcher),
            _vm.ValidationAttempt, FirstInvalid = Id(_vm.FirstInvalidField), Selected = Id(_vm.SelectedInstrument),
            _vm.CanEdit, _vm.IsBusy, Detail = detail
        });
    }

    private void Activation(object? sender, EventArgs e) => Record(_window.IsActive ? "activated" : "deactivated");
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SessionPreparationViewModel.ValidationAttempt) or nameof(SessionPreparationViewModel.SelectedInstrument))
            Record(e.PropertyName);
    }
    private void Posted(object? sender, DispatcherHookEventArgs e)
    {
        if (typeof(DispatcherOperation).GetField("_method", Fields)?.GetValue(e.Operation) is not Delegate { Target: { } closure } ||
            !closure.GetType().GetFields(Fields).Any(f => f.FieldType.FullName == "PersonalTradingJournal.Desktop.Interactions.PageFocus")) return;
        _requests.Add(e.Operation, closure);
        Request("request-posted", e.Operation, closure);
    }
    private void Started(object? sender, DispatcherHookEventArgs e)
    { if (_requests.TryGetValue(e.Operation, out var closure)) Request("request-started", e.Operation, closure); }
    private void Completed(object? sender, DispatcherHookEventArgs e)
    { if (_requests.Remove(e.Operation, out var closure)) Request("request-completed", e.Operation, closure); }
    private void Request(string phase, DispatcherOperation operation, object closure)
    {
        var focus = closure.GetType().GetFields(Fields).Single(f => f.FieldType.FullName == "PersonalTradingJournal.Desktop.Interactions.PageFocus").GetValue(closure)!;
        var owner = (FrameworkElement)focus.GetType().GetField("owner", Fields)!.GetValue(focus)!;
        object? Captured(string name) => closure.GetType().GetField(name, Fields)?.GetValue(closure);
        Record(phase, new
        {
            Request = Id(operation), operation.Priority, operation.Status, Owner = Id(owner),
            Version = Captured("version"), CurrentVersion = focus.GetType().GetField("_version", Fields)!.GetValue(focus),
            ContextMatches = ReferenceEquals(Captured("context"), owner.DataContext),
            InputBefore = Id(Captured("input")), LogicalBefore = Id(Captured("logicalInput")),
            LogicalNow = Id(FocusManager.GetFocusedElement(owner)), owner.IsLoaded, owner.IsVisible,
            OwnerWindow = Id(Window.GetWindow(owner)), OwnerDispatcher = Id(owner.Dispatcher),
            Targets = Descendants(owner).OfType<TextBox>().Where(t => ReferenceEquals(t.DataContext, _vm.FirstInvalidField))
                .Select(t => new { Target = Id(t), t.IsLoaded, t.IsVisible, t.IsEnabled, t.Focusable,
                    Attached = PresentationSource.FromVisual(t) is not null, Window = Id(Window.GetWindow(t)),
                    Dispatcher = Id(t.Dispatcher), t.IsKeyboardFocused, t.IsFocused }).ToArray()
        });
    }
    private static string? Id(object? value) => value is null ? null : value.GetType().Name + "#" + RuntimeHelpers.GetHashCode(value);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var next in Descendants(child)) yield return next; }
    }
    public void Dispose()
    {
        Record("trace-disposed", new { PendingObservedRequests = _requests.Count });
        _window.RemoveHandler(Keyboard.GotKeyboardFocusEvent, _focusHandler);
        _window.RemoveHandler(Keyboard.LostKeyboardFocusEvent, _focusHandler);
        _window.Activated -= Activation; _window.Deactivated -= Activation; _vm.PropertyChanged -= Changed;
        _window.Dispatcher.Hooks.OperationPosted -= Posted;
        _window.Dispatcher.Hooks.OperationStarted -= Started;
        _window.Dispatcher.Hooks.OperationCompleted -= Completed;
        string directory = Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") ?? Path.GetTempPath();
        File.WriteAllLines(Path.Combine(directory, _name + "-validation-focus.jsonl"), _rows.Select(row => JsonSerializer.Serialize(row)));
    }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
}
