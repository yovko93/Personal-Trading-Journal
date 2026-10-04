using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

/// <summary>Phase breadcrumbs and a bounded STA lifetime inside a supervised test process.</summary>
internal static class CalendarStaTest
{
    [ThreadStatic] private static State? _current;
    private static string? _timedOutScenario;
    private static readonly CancellationTokenSource Poisoned = new();
    private static readonly Lazy<NativeDispatcherHost> NativeHost = new(() => new());

    internal static IDisposable Phase(string name)
    {
        State state = _current ?? throw new InvalidOperationException("Calendar phases require the STA harness.");
        state.Phase = name;
        state.Write("start");
        return new PhaseScope(state, name);
    }

    internal static async Task RunAsync(Action action, [CallerMemberName] string scenario = "", TimeSpan? timeout = null,
        TimeProvider? timeoutProvider = null, bool shutdownDispatcher = true)
    {
        // This helper is only used inside a supervised child. Do not accumulate
        // another blocked WPF thread after a case has poisoned that host.
        ThrowIfPoisoned();
        var state = new State(scenario);
        Task work;
        if (!shutdownDispatcher)
        {
            NativeDispatcherHost host = NativeHost.Value;
            state.Thread = host.Thread;
            state.Phase = "waiting for native dispatcher";
            work = host.RunAsync(state, action);
        }
        else
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try { ExecuteAction(state, action, shutdownDispatcher: true); done.TrySetResult(); }
                catch (Exception error) { done.TrySetException(error); }
            }) { IsBackground = true, Name = "Calendar layout: " + scenario };
            state.Thread = thread;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            work = done.Task;
        }
        try { await work.WaitAsync(timeout ?? TimeSpan.FromSeconds(30), timeoutProvider ?? TimeProvider.System); }
        catch (TimeoutException error) when (!work.IsFaulted ||
            work.Exception!.InnerExceptions.All(failure => !ReferenceEquals(failure, error)))
        {
            Interlocked.CompareExchange(ref _timedOutScenario, scenario, null);
            Poisoned.Cancel();
            state.Write("TIMEOUT; STA alive=" + state.Thread!.IsAlive + "; thread state=" + state.Thread.ThreadState);
            // WPF/native work cannot safely be forcibly aborted as a managed thread. The
            // enclosing process supervisor terminates this process if it cannot exit.
            throw new TimeoutException($"Calendar STA timed out in phase '{state.Phase}' ({scenario}); " +
                $"thread {state.Thread.ManagedThreadId}, alive={state.Thread.IsAlive}. See calendar-sta-phases.log.", error);
        }
    }

    private static void ThrowIfPoisoned()
    {
        if (Volatile.Read(ref _timedOutScenario) is { } previous)
            throw new InvalidOperationException($"Calendar test host already timed out in {previous}; refusing another STA. See child diagnostics.");
    }

    private static void ExecuteAction(State state, Action action, bool shutdownDispatcher)
    {
        SynchronizationContext? previousContext = SynchronizationContext.Current;
        _current = state;
        try
        {
            ThrowIfPoisoned();
            state.Write("STA started");
            using (Phase("Dispatcher acquisition")) state.Dispatcher = Dispatcher.CurrentDispatcher;
            state.Write($"Host: {RuntimeInformation.FrameworkDescription}; WPF=" +
                FileVersionInfo.GetVersionInfo(typeof(System.Windows.Window).Assembly.Location).ProductVersion +
                $"; logical CPUs={Environment.ProcessorCount}; UserInteractive={Environment.UserInteractive}; " +
                $"station={UserObjectName(GetProcessWindowStation())}; desktop={UserObjectName(GetThreadDesktop(GetCurrentThreadId()))}");
            state.Phase = "test action";
            state.Write("start");
            action();
            state.Write("assertions completed");
        }
        catch (Exception error) { state.Error = error; }
        finally
        {
            try
            {
                state.Phase = shutdownDispatcher ? "dispatcher cleanup" : "native renderer lifetime owned by child process";
                if (shutdownDispatcher && state.Dispatcher is { HasShutdownStarted: false } dispatcher) dispatcher.InvokeShutdown();
                state.Write("STA finished");
            }
            catch (Exception error) { state.Error ??= error; }
            SynchronizationContext.SetSynchronizationContext(previousContext);
            _current = null;
        }
        if (state.Error is { } failure) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class NativeDispatcherHost
    {
        private readonly TaskCompletionSource<Dispatcher> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SemaphoreSlim _action = new(1, 1);
        internal Thread Thread { get; }

        internal NativeDispatcherHost()
        {
            Thread = new Thread(() =>
            {
                try
                {
                    Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                    ExceptionDispatchInfo? fatal = null;
                    dispatcher.UnhandledException += (_, args) =>
                    {
                        // Without an Application/handler, an exception can escape
                        // through the native window callback without unwinding Run.
                        // Handle it only long enough to unwind the pump, then throw
                        // the original error outside that native callback boundary.
                        fatal ??= ExceptionDispatchInfo.Capture(args.Exception);
                        args.Handled = true;
                        dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                    };
                    _ready.TrySetResult(dispatcher);
                    Dispatcher.Run();
                    fatal?.Throw();
                    throw new InvalidOperationException("Calendar native dispatcher exited unexpectedly.");
                }
                catch (Exception error)
                {
                    // Before startup, return the error to the waiting case. The
                    // test host can log a background unhandled exception without
                    // exiting, so explicitly fail this supervised child after a
                    // fatal pump error, preserving the original exception/stack.
                    if (!_ready.TrySetException(error))
                        Environment.FailFast("The native test dispatcher failed after startup.", error);
                }
            }) { IsBackground = true, Name = "Calendar native renderer (child-owned)" };
            Thread.SetApartmentState(ApartmentState.STA);
            Thread.Start();
        }

        internal async Task RunAsync(State state, Action action)
        {
            try
            {
                // Acquire outside the dispatcher: a nested ShowDialog/PushFrame
                // must never dispatch a second test while the first still runs.
                await _action.WaitAsync(Poisoned.Token).ConfigureAwait(false);
                try
                {
                    ThrowIfPoisoned();
                    Dispatcher dispatcher = await _ready.Task.WaitAsync(Poisoned.Token).ConfigureAwait(false);
                    await dispatcher.InvokeAsync(() => ExecuteAction(state, action, shutdownDispatcher: false),
                        DispatcherPriority.Normal, Poisoned.Token).Task.ConfigureAwait(false);
                }
                finally { _action.Release(); }
            }
            catch (OperationCanceledException) when (Poisoned.IsCancellationRequested)
            {
                ThrowIfPoisoned();
                throw;
            }
        }
    }

    private sealed class State(string scenario)
    {
        internal volatile string Phase = "STA startup";
        internal Thread? Thread;
        internal Dispatcher? Dispatcher;
        internal Exception? Error;
        private readonly Stopwatch _elapsed = Stopwatch.StartNew();
        internal void Write(string status)
        {
            var application = System.Windows.Application.Current;
            string app = application is null ? "null" : $"thread={application.Dispatcher.Thread.ManagedThreadId}," +
                $"alive={application.Dispatcher.Thread.IsAlive},shutdown={application.Dispatcher.HasShutdownStarted}/{application.Dispatcher.HasShutdownFinished}";
            string line = $"{DateTimeOffset.UtcNow:O} {scenario}; {status}; phase={Phase}; " +
                $"elapsed={_elapsed.Elapsed}; STA={Thread?.ManagedThreadId}; Application={app}";
            // Sample only lifetime boundaries so the phase breadcrumbs do not
            // themselves add repeated process/thread enumeration to native work.
            if (status is "STA started" or "STA finished" || status.StartsWith("TIMEOUT;", StringComparison.Ordinal))
                line += ProcessDiagnostics();
            Console.WriteLine(line);
            if (Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                lock (typeof(CalendarStaTest)) File.AppendAllText(Path.Combine(output, "calendar-sta-phases.log"), line + Environment.NewLine);
            }
        }
    }

    private static string ProcessDiagnostics()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            ThreadPool.GetAvailableThreads(out int workers, out int completionPorts);
            return $"; PID={Environment.ProcessId}; process CPU={process.TotalProcessorTime}; " +
                $"native threads={process.Threads.Count}; working set={process.WorkingSet64}; private bytes={process.PrivateMemorySize64}; " +
                $"managed bytes={GC.GetTotalMemory(forceFullCollection: false)}; GC counts={GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}; " +
                $"pool threads={ThreadPool.ThreadCount}; pool pending={ThreadPool.PendingWorkItemCount}; " +
                $"pool available={workers}/{completionPorts}";
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            // Diagnostics must not turn an otherwise passing native case into a
            // failure when Windows cannot provide a process performance counter.
            return $"; PID={Environment.ProcessId}; process diagnostics unavailable={error.GetType().Name}";
        }
    }

    private sealed class PhaseScope(State state, string phase) : IDisposable
    {
        public void Dispose() { state.Phase = phase; state.Write("end"); }
    }

    private static string UserObjectName(IntPtr handle)
    {
        var name = new StringBuilder(256);
        return GetUserObjectInformation(handle, 2, name, name.Capacity * sizeof(char), out _) ? name.ToString() : "unavailable";
    }
    [DllImport("user32.dll")] private static extern IntPtr GetProcessWindowStation();
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint threadId);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetUserObjectInformationW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder value, int length, out int needed);
}
