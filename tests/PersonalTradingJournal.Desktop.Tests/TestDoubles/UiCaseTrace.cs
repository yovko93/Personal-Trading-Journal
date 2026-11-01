using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Threading;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

/// <summary>Boundary-only diagnostics. Does not change pumps, priorities or deadlines.</summary>
internal sealed class UiCaseTrace(string scenario, Action<string>? sink = null)
{
    private readonly long _start = Stopwatch.GetTimestamp();
    private int _sequence;

    internal void Write(string phase, string state, Task? task = null, int line = 0)
    {
        string json = JsonSerializer.Serialize(new
        {
            Utc = DateTimeOffset.UtcNow, Milliseconds = Stopwatch.GetElapsedTime(_start).TotalMilliseconds,
            Scenario = scenario, Phase = phase, State = state, Line = line,
            Process = Environment.ProcessId, Thread = Environment.CurrentManagedThreadId,
            TaskId = task?.Id, TaskStatus = task?.Status.ToString(),
        });
        if (sink is not null) { sink(json); return; }
        Console.WriteLine("UI phase: " + json);
        if (Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") is { Length: > 0 } directory)
        {
            lock (typeof(UiCaseTrace)) File.AppendAllText(Path.Combine(directory, "ui-case-phases.jsonl"), json + Environment.NewLine);
        }
    }

    internal IDisposable Phase(string name, [CallerLineNumber] int line = 0)
    {
        string phase = $"{Interlocked.Increment(ref _sequence)}: {name}";
        Write(phase, "begin", line: line);
        return new Boundary(() => Write(phase, "end", line: line));
    }

    internal async Task<T> Await<T>(Func<Task<T>> create,
        [CallerArgumentExpression(nameof(create))] string name = "", [CallerLineNumber] int line = 0)
    {
        using var phase = Phase(name, line);
        Write(name, "creating task", line: line);
        Task<T> task = create();
        Write(name, "created", task, line);
        try { return await task; }
        finally { Write(name, "await ended", task, line); }
    }

    internal async Task Await(Func<Task> create,
        [CallerArgumentExpression(nameof(create))] string name = "", [CallerLineNumber] int line = 0)
    {
        using var phase = Phase(name, line);
        Write(name, "creating task", line: line);
        Task task = create();
        Write(name, "created", task, line);
        try { await task; }
        finally { Write(name, "await ended", task, line); }
    }

    internal void Pump(Func<Task> create,
        [CallerArgumentExpression(nameof(create))] string name = "", [CallerLineNumber] int line = 0)
    {
        using var phase = Phase(name, line);
        Write(name, "creating task", line: line);
        Task task = create();
        Write(name, "created / entering original dispatcher pump", task, line);
        try
        {
            var frame = new DispatcherFrame(); var dispatcher = Dispatcher.CurrentDispatcher;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
            if (!task.IsCompleted) Dispatcher.PushFrame(frame);
            task.GetAwaiter().GetResult();
        }
        finally { Write(name, "pump ended", task, line); }
        using (Phase("post-task ApplicationIdle", line))
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private sealed class Boundary(Action end) : IDisposable
    {
        private int _disposed;
        public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) end(); }
    }
}
