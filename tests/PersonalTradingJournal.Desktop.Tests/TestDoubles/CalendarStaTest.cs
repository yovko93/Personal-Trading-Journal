using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

/// <summary>Phase breadcrumbs and a bounded STA lifetime inside a supervised test process.</summary>
internal static class CalendarStaTest
{
    [ThreadStatic] private static State? _current;
    private static string? _timedOutScenario;

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
        if (Volatile.Read(ref _timedOutScenario) is { } previous)
            throw new InvalidOperationException($"Calendar test host already timed out in {previous}; refusing another STA. See child diagnostics.");
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = new State(scenario);
        var thread = new Thread(() =>
        {
            _current = state;
            try
            {
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
                _current = null;
                if (state.Error is { } failure) done.TrySetException(failure); else done.TrySetResult();
            }
        }) { IsBackground = true, Name = "Calendar layout: " + scenario };
        state.Thread = thread;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try { await done.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(30), timeoutProvider ?? TimeProvider.System); }
        catch (TimeoutException error) when (!done.Task.IsFaulted ||
            done.Task.Exception!.InnerExceptions.All(failure => !ReferenceEquals(failure, error)))
        {
            Interlocked.CompareExchange(ref _timedOutScenario, scenario, null);
            state.Write("TIMEOUT; STA alive=" + thread.IsAlive + "; thread state=" + thread.ThreadState);
            // WPF/native work cannot safely be forcibly aborted as a managed thread. The
            // enclosing process supervisor terminates this process if it cannot exit.
            throw new TimeoutException($"Calendar STA timed out in phase '{state.Phase}' ({scenario}); " +
                $"thread {thread.ManagedThreadId}, alive={thread.IsAlive}. See calendar-sta-phases.log.", error);
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
            Console.WriteLine(line);
            if (Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                lock (typeof(CalendarStaTest)) File.AppendAllText(Path.Combine(output, "calendar-sta-phases.log"), line + Environment.NewLine);
            }
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
