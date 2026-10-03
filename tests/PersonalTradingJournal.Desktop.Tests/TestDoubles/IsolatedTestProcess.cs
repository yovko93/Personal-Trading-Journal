using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

/// <summary>
/// Supervises a test-only child process. An in-process task timeout cannot stop a
/// blocked WPF/foreground STA thread; this boundary can terminate that entire host.
/// </summary>
internal static class IsolatedTestProcess
{
    private static readonly TimeSpan TerminationTimeout = TimeSpan.FromSeconds(10);

    internal static async Task RunSuiteAsync(Type suiteType, string suiteName, string hostVariable,
        TimeSpan timeout, CancellationToken cancellationToken = default, TimeSpan? caseHangTimeout = null,
        string? testCaseFilter = null)
    {
        ArgumentNullException.ThrowIfNull(suiteType);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostVariable);
        if (testCaseFilter is not null) ArgumentException.ThrowIfNullOrWhiteSpace(testCaseFilter);
        string results = CreateResultsDirectory(suiteName);
        var start = new ProcessStartInfo("dotnet");
        start.ArgumentList.Add("vstest");
        start.ArgumentList.Add(suiteType.Assembly.Location);
        start.ArgumentList.Add("/TestCaseFilter:" + (testCaseFilter ?? $"FullyQualifiedName~{suiteType.FullName}"));
        start.ArgumentList.Add($"/Logger:trx;LogFileName={SafeName(suiteName)}.trx");
        start.ArgumentList.Add($"/ResultsDirectory:{results}");
        if (caseHangTimeout is { } hang)
        {
            if (hang <= TimeSpan.Zero || hang >= timeout)
                throw new ArgumentOutOfRangeException(nameof(caseHangTimeout), "The case dump watchdog must fit within the outer process deadline.");
            // The case and STA watchdogs can race. Dumps are best-effort evidence,
            // not a retry or replacement for the phase log and process deadline.
            start.ArgumentList.Add("/Blame:CollectHangDump;HangDumpType=Mini;TestTimeout=" +
                hang.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "s");
        }
        start.Environment[hostVariable] = "1";
        IsolatedProcessResult result = await RunAsync(start, suiteName, timeout, cancellationToken, results);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Isolated test suite '{suiteName}' failed with exit {result.ExitCode}. " +
                $"Individual child cases and complete output: {result.ResultsDirectory}.{Environment.NewLine}" +
                DisplayTail(result.StandardOutput) + Environment.NewLine + DisplayTail(result.StandardError));
    }

    internal static async Task<IsolatedProcessResult> RunAsync(ProcessStartInfo start, string diagnosticName,
        TimeSpan timeout, CancellationToken cancellationToken = default, string? resultsDirectory = null,
        Func<string, StreamWriter>? writerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (timeout <= TimeSpan.Zero || timeout == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout), "A finite positive process deadline is required.");
        cancellationToken.ThrowIfCancellationRequested();
        // Validate/create the deadline before starting anything: an invalid timer
        // duration must not leave a newly started child outside the cleanup scope.
        using var deadline = new CancellationTokenSource(timeout);
        using var stopped = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        string results = resultsDirectory ?? CreateResultsDirectory(diagnosticName);
        Directory.CreateDirectory(results);
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.Environment["PTJ_TEST_RESULTS_DIRECTORY"] = results;
        string stdoutPath = Path.Combine(results, "stdout.log"), stderrPath = Path.Combine(results, "stderr.log");
        string supervisionPath = Path.Combine(results, "process-supervision.log");
        writerFactory ??= path => new StreamWriter(path, append: false, Encoding.UTF8) { AutoFlush = true };
        await using var stdoutFile = writerFactory(stdoutPath);
        await using var stderrFile = writerFactory(stderrPath);
        await using var supervision = writerFactory(supervisionPath);
        using var process = new Process { StartInfo = start };
        Task<string>? stdout = null, stderr = null;
        IsolatedProcessResult? result = null;
        var failures = new List<Exception>();
        void RecordFailure(Exception error)
        {
            if (!failures.Any(existing => ReferenceEquals(existing, error))) failures.Add(error);
        }
        var outputFailure = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!process.Start()) throw new InvalidOperationException($"Could not start isolated test '{diagnosticName}'.");
        try
        {
            // Every operation after Start belongs to this cleanup scope, including
            // the very first log write and creating the asynchronous output pumps.
            try
            {
                await supervision.WriteLineAsync($"Started '{diagnosticName}', PID {process.Id}, deadline {timeout}. " +
                    $"UTC {DateTimeOffset.UtcNow:O}. Child phase breadcrumbs and TRX belong in {results}.");
                stdout = CopyOutputAsync(process.StandardOutput, stdoutFile, outputFailure);
                stderr = CopyOutputAsync(process.StandardError, stderrFile, outputFailure);
                Task exit = process.WaitForExitAsync();
                Task finished = await Task.WhenAny(exit, outputFailure.Task).WaitAsync(stopped.Token);
                if (finished == outputFailure.Task) ExceptionDispatchInfo.Capture(await outputFailure.Task).Throw();
                await exit;
                await Task.WhenAll(stdout, stderr).WaitAsync(TerminationTimeout);
                await supervision.WriteLineAsync($"Exited PID {process.Id} with code {process.ExitCode}, UTC {DateTimeOffset.UtcNow:O}.");
                result = new(process.Id, process.ExitCode, await stdout, await stderr, results);
            }
            catch (OperationCanceledException) when (stopped.IsCancellationRequested)
            {
                string reason = cancellationToken.IsCancellationRequested ? "cancelled" : "timed out";
                await supervision.WriteLineAsync($"Process {reason}, UTC {DateTimeOffset.UtcNow:O}. " +
                    "The thread snapshot below is not a managed stack; see phase logs or a collected VSTest dump for stacks.");
                await WriteThreadSnapshotAsync(process, supervision);
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException($"Isolated test '{diagnosticName}' cancelled; process-tree cleanup is required before return. " +
                        $"Diagnostics: {results}.", cancellationToken);
                throw new IsolatedProcessTimeoutException($"Isolated test '{diagnosticName}' exceeded {timeout}; " +
                    $"PID {process.Id} requires process-tree termination before return. Diagnostics: {results}.", process.Id, results);
            }
        }
        catch (Exception error) { RecordFailure(error); }
        finally
        {
            // Cleanup must never depend on a writable diagnostic stream. Otherwise
            // an IOException in logging could abandon the very STA host it reports.
            try { await TerminateAsync(process); }
            catch (Exception error) { RecordFailure(error); }
            try
            {
                Task[] pumps = new Task?[] { stdout, stderr }.OfType<Task>().ToArray();
                await Task.WhenAll(pumps).WaitAsync(TerminationTimeout);
            }
            catch (Exception error) { RecordFailure(error); }
            try { await supervision.WriteLineAsync($"Confirmed PID {process.Id} exited after cleanup, UTC {DateTimeOffset.UtcNow:O}."); }
            catch (Exception error) { RecordFailure(error); }
        }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Isolated test failure and additional cleanup/diagnostic errors.", failures);
        return result ?? throw new InvalidOperationException("The isolated test ended without a result or failure.");
    }

    private static async Task TerminateAsync(Process process)
    {
        if (!process.HasExited)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) when (process.HasExited) { }
        }
        // This is cleanup, never an extra attempt to run a failing test.
        await process.WaitForExitAsync().WaitAsync(TerminationTimeout);
    }

    private static async Task WriteThreadSnapshotAsync(Process process, StreamWriter diagnostics)
    {
        try
        {
            foreach (ProcessThread thread in process.Threads)
            {
                using (thread)
                    await diagnostics.WriteLineAsync($"Native thread {thread.Id}: {thread.ThreadState}" +
                        (thread.ThreadState == System.Diagnostics.ThreadState.Wait ? $", wait {thread.WaitReason}" : ""));
            }
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            // Exiting processes can lose their thread snapshot. Always continue termination.
            await diagnostics.WriteLineAsync($"Thread snapshot unavailable: {error.GetType().Name}.");
        }
    }

    private static async Task<string> CopyOutputAsync(StreamReader source, StreamWriter target,
        TaskCompletionSource<Exception> outputFailure)
    {
        // Keep full artifacts, but a bounded tail for the parent failure message.
        const int maximumTail = 128 * 1024;
        char[] buffer = new char[4096];
        var tail = new StringBuilder();
        int read;
        try
        {
            while ((read = await source.ReadAsync(buffer.AsMemory())) != 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read));
                await target.FlushAsync();
                tail.Append(buffer, 0, read);
                if (tail.Length > maximumTail) tail.Remove(0, tail.Length - maximumTail);
            }
            return tail.ToString();
        }
        catch (Exception error) { outputFailure.TrySetResult(error); throw; }
    }

    private static string CreateResultsDirectory(string name) => Path.Combine(
        Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") ?? Path.Combine(AppContext.BaseDirectory, "TestResults"),
        $"{SafeName(name)}-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{Guid.NewGuid():N}");

    private static string SafeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return string.Concat(name.Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character : '_'));
    }

    private static string DisplayTail(string value) => value.Length <= 8192 ? value : "[See complete artifact output.]\n" + value[^8192..];
}

internal sealed record IsolatedProcessResult(int ProcessId, int ExitCode, string StandardOutput,
    string StandardError, string ResultsDirectory);

internal sealed class IsolatedProcessTimeoutException(string message, int processId, string resultsDirectory)
    : TimeoutException(message)
{
    internal int ProcessId { get; } = processId;
    internal string ResultsDirectory { get; } = resultsDirectory;
}
