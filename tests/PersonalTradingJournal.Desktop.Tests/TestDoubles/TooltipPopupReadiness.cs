using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

/// <summary>Observes a real tooltip popup before measuring it, without reopening or polling it.</summary>
internal sealed class TooltipPopupReadiness : IDisposable
{
    private static readonly object LogLock = new();
    private readonly ToolTip _tip;
    private readonly UIElement _owner;
    private readonly Window _window;
    private readonly string _scenario;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly Queue<string> _history = new();
    private readonly DependencyPropertyDescriptor _isOpenDescriptor;
    private DispatcherOperation? _queuedCheck;
    private DispatcherTimer? _deadline;
    private DispatcherFrame? _frame;
    private Action? _assertion;
    private ExceptionDispatchInfo? _failure;
    private bool _windowClosed;
    private bool _verified;
    private bool _disposed;

    internal TooltipPopupReadiness(ToolTip tip, UIElement owner, Window window, [CallerMemberName] string scenario = "")
    {
        _tip = tip;
        _owner = owner;
        _window = window;
        _scenario = scenario;
        _isOpenDescriptor = DependencyPropertyDescriptor.FromProperty(ToolTip.IsOpenProperty, typeof(ToolTip));
        _isOpenDescriptor.AddValueChanged(tip, OnIsOpenChanged);
        tip.Opened += OnOpened;
        tip.Closed += OnClosed;
        tip.Loaded += OnLoaded;
        tip.LayoutUpdated += OnLayoutUpdated;
        PresentationSource.AddSourceChangedHandler(tip, OnSourceChanged);
        window.Activated += OnWindowActivated;
        window.Deactivated += OnWindowDeactivated;
        window.Closed += OnWindowClosed;
        owner.MouseEnter += OnMouseEnter;
        owner.MouseLeave += OnMouseLeave;
        Record("observer attached");
    }

    internal int OpenedCount { get; private set; }
    internal int ClosedCount { get; private set; }

    internal void Verify(Action assertion, Action? request = null, TimeSpan? timeout = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _tip.Dispatcher.VerifyAccess();
        if (_assertion is not null) throw new InvalidOperationException("A popup verification is already running.");
        TimeSpan limit = timeout ?? TimeSpan.FromSeconds(5);
        if (limit < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        _assertion = assertion;
        _verified = false;
        _failure = null;
        _frame = new DispatcherFrame();
        _deadline = new DispatcherTimer(DispatcherPriority.Send, _tip.Dispatcher) { Interval = limit };
        _deadline.Tick += OnDeadline;
        _deadline.Start();
        Record("verification requested");
        try
        {
            request?.Invoke();
            // A synchronous Opened event has completed all subscribers by this point.
            // If opening is deferred, event callbacks below schedule this same check.
            CheckReadiness();
            if (_frame.Continue) Dispatcher.PushFrame(_frame);
            if (!_verified && _failure is null)
                Fail(new InvalidOperationException("Dispatcher stopped before popup placement was verified. " + Snapshot()));
        }
        catch (Exception error)
        {
            Fail(error);
        }
        finally
        {
            StopPendingWork();
            _assertion = null;
            _frame = null;
        }
        _failure?.Throw();
    }

    private void OnOpened(object sender, RoutedEventArgs e)
    {
        OpenedCount++;
        Changed("Opened");
    }

    private void OnClosed(object sender, RoutedEventArgs e)
    {
        ClosedCount++;
        Changed("Closed");
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Changed("Loaded");
    private void OnSourceChanged(object sender, SourceChangedEventArgs e) => Changed("PresentationSource changed");
    private void OnIsOpenChanged(object? sender, EventArgs e) => Changed("IsOpen changed");
    private void OnWindowActivated(object? sender, EventArgs e) => Changed("Window Activated");
    private void OnWindowDeactivated(object? sender, EventArgs e) => Changed("Window Deactivated");
    private void OnMouseEnter(object sender, MouseEventArgs e) => Changed("owner MouseEnter");
    private void OnMouseLeave(object sender, MouseEventArgs e) => Changed("owner MouseLeave");
    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _windowClosed = true;
        Changed("Window Closed");
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_assertion is not null) Changed("LayoutUpdated");
    }

    private void Changed(string name)
    {
        Record(name);
        if (_disposed || _assertion is null || _queuedCheck is not null) return;
        // Do not measure midway through Opened: the production follower may be
        // another event subscriber that still has to apply its placement offsets.
        _queuedCheck = _tip.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
        {
            _queuedCheck = null;
            CheckReadiness();
        }));
    }

    private void CheckReadiness()
    {
        if (_disposed || _assertion is null || _frame is not { Continue: true }) return;
        if (_windowClosed || ClosedCount > 0 || (OpenedCount > 0 && !_tip.IsOpen))
        {
            Fail(new InvalidOperationException("Tooltip closed before popup placement could be measured. " + Snapshot()));
            return;
        }
        if (OpenedCount == 0 || !_tip.IsOpen || PresentationSource.FromVisual(_tip) is null ||
            !_tip.IsMeasureValid || !_tip.IsArrangeValid || _tip.ActualWidth <= 0 || _tip.ActualHeight <= 0)
            return;
        try
        {
            Write("ready; measuring popup");
            // Readiness and the physical-pixel assertions are one STA operation:
            // no idle pump can dismiss the popup between the guard and PointToScreen.
            _assertion();
            _verified = true;
            _frame.Continue = false;
        }
        catch (Exception error) { Fail(error); }
    }

    private void OnDeadline(object? sender, EventArgs e) => Fail(new TimeoutException(
        "Tooltip did not become ready within the bounded popup deadline. " + Snapshot()));

    private void Fail(Exception error)
    {
        _failure ??= ExceptionDispatchInfo.Capture(error);
        Write("FAILED: " + error);
        if (_frame is not null) _frame.Continue = false;
    }

    private void Record(string name)
    {
        if (_history.Count == 64) _history.Dequeue();
        _history.Enqueue($"{_elapsed.Elapsed.TotalMilliseconds:F1}ms {name}; {Snapshot()}");
    }

    private string Snapshot()
    {
        string pointer;
        try { pointer = Mouse.GetPosition(_owner).ToString(); }
        catch (InvalidOperationException error) { pointer = "unavailable: " + error.Message; }
        return $"IsOpen={_tip.IsOpen}; tipSource={Source(_tip)}; Opened={OpenedCount}; Closed={ClosedCount}; " +
            $"placement={_tip.Placement}; target={_tip.PlacementTarget?.GetType().Name ?? "null"}; " +
            $"targetSource={(_tip.PlacementTarget is Visual target ? Source(target) : "null")}; " +
            $"offsets={_tip.HorizontalOffset},{_tip.VerticalOffset}; tipSize={_tip.ActualWidth}x{_tip.ActualHeight}; " +
            $"layoutValid={_tip.IsMeasureValid}/{_tip.IsArrangeValid}; windowSource={Source(_window)}; " +
            $"windowState={_window.WindowState}; active={_window.IsActive}; visible={_window.IsVisible}; " +
            $"windowClosed={_windowClosed}; windowBounds={_window.Left},{_window.Top},{_window.ActualWidth}x{_window.ActualHeight}; " +
            $"ownerHovered={_owner.IsMouseOver}; pointerInOwner={pointer}; " +
            $"dpi={PresentationSource.FromVisual(_window)?.CompositionTarget?.TransformToDevice}; " +
            $"workarea={SystemParameters.WorkArea}";
    }

    private static string Source(Visual visual) => PresentationSource.FromVisual(visual) is { } source
        ? source.GetType().Name + "/" + (source.RootVisual?.GetType().Name ?? "no root") : "null";

    private void Write(string message)
    {
        Record(message);
        string text = $"{DateTimeOffset.UtcNow:O} {_scenario}; STA={Environment.CurrentManagedThreadId}" +
            Environment.NewLine + string.Join(Environment.NewLine, _history);
        Console.WriteLine(text);
        if (Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") is not { Length: > 0 } output) return;
        try
        {
            Directory.CreateDirectory(output);
            lock (LogLock) File.AppendAllText(Path.Combine(output, "chart-popup-readiness.log"), text + Environment.NewLine);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine("Could not write optional popup diagnostics: " + error.Message);
        }
    }

    private void StopPendingWork()
    {
        _queuedCheck?.Abort();
        _queuedCheck = null;
        if (_deadline is not null)
        {
            _deadline.Stop();
            _deadline.Tick -= OnDeadline;
            _deadline = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopPendingWork();
        _isOpenDescriptor.RemoveValueChanged(_tip, OnIsOpenChanged);
        _tip.Opened -= OnOpened;
        _tip.Closed -= OnClosed;
        _tip.Loaded -= OnLoaded;
        _tip.LayoutUpdated -= OnLayoutUpdated;
        PresentationSource.RemoveSourceChangedHandler(_tip, OnSourceChanged);
        _window.Activated -= OnWindowActivated;
        _window.Deactivated -= OnWindowDeactivated;
        _window.Closed -= OnWindowClosed;
        _owner.MouseEnter -= OnMouseEnter;
        _owner.MouseLeave -= OnMouseLeave;
        if (_frame is { Continue: true }) Fail(new ObjectDisposedException(nameof(TooltipPopupReadiness)));
    }
}
