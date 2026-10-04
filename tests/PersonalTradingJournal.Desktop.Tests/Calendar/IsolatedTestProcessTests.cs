using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed class IsolatedTestProcessTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task NormalAndFailedExitPreserveCompleteOutput(int exitCode)
    {
        ProcessStartInfo start = Probe("output", exitCode.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(Encoding.UTF8.CodePage, start.StandardOutputEncoding?.CodePage);
        Assert.Equal(Encoding.UTF8.CodePage, start.StandardErrorEncoding?.CodePage);
        IsolatedProcessResult result = await IsolatedTestProcess.RunAsync(start,
            $"exit-{exitCode}", TimeSpan.FromSeconds(15));
        Assert.Equal(exitCode, result.ExitCode);
        string stdout = ExpectedOutput("stdout", 'O'), stderr = ExpectedOutput("stderr", 'E');
        Assert.Equal(stdout, await File.ReadAllTextAsync(Path.Combine(result.ResultsDirectory, "stdout.log")));
        Assert.Equal(stderr, await File.ReadAllTextAsync(Path.Combine(result.ResultsDirectory, "stderr.log")));
        Assert.Equal(stdout[^(128 * 1024)..], result.StandardOutput);
        Assert.Equal(stderr[^(128 * 1024)..], result.StandardError);
        Assert.Contains("Exited PID", await File.ReadAllTextAsync(Path.Combine(result.ResultsDirectory, "process-supervision.log")));
        Assert.False(IsRunning(result.ProcessId));
    }

    [Fact]
    public async Task DeadlineKillsBlockedForegroundStaAndItsDescendantWithoutAnotherTestAttempt()
    {
        // The probe is synthetic managed code, not Desktop.App or a journal process.
        IsolatedProcessTimeoutException failure = await Assert.ThrowsAsync<IsolatedProcessTimeoutException>(() =>
            IsolatedTestProcess.RunAsync(BlockedForegroundSta(), "blocked-foreground-sta", TimeSpan.FromSeconds(15)));
        string output = await File.ReadAllTextAsync(Path.Combine(failure.ResultsDirectory, "stdout.log"));
        Assert.Contains("FOREGROUND STA", output); // Proves we timed out after entering the intended blocked phase.
        Assert.Single(output.Split('\n'), line => line.StartsWith("FOREGROUND STA", StringComparison.Ordinal));
        int childId = ChildId(output);
        Assert.False(IsRunning(failure.ProcessId));
        await AssertExitedAsync(childId);
        string diagnostics = await File.ReadAllTextAsync(Path.Combine(failure.ResultsDirectory, "process-supervision.log"));
        Assert.Contains("timed out", diagnostics);
        Assert.Contains("Native thread", diagnostics);
        Assert.Contains("Confirmed PID", diagnostics);
    }

    [Fact]
    public async Task CancellationAfterTheStaStartsKillsTheTreeAndPreservesAlreadyFlushedOutput()
    {
        string results = Path.Combine(AppContext.BaseDirectory, "TestResults", "cancel-probe-" + Guid.NewGuid().ToString("N"));
        using var cancellation = new CancellationTokenSource();
        Task<IsolatedProcessResult> pending = IsolatedTestProcess.RunAsync(BlockedForegroundSta(),
            "cancelled-foreground-sta", TimeSpan.FromSeconds(20), cancellation.Token, results);
        string output;
        try
        {
            output = await WaitForOutputAsync(Path.Combine(results, "stdout.log"), "FOREGROUND STA", pending);
        }
        catch
        {
            cancellation.Cancel();
            try { await pending; } catch (OperationCanceledException) { }
            throw;
        }
        finally
        {
            cancellation.Cancel();
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        int parentId = int.Parse(output.Split('\n').Single(line => line.StartsWith("PARENT ", StringComparison.Ordinal))[7..]);
        Assert.False(IsRunning(parentId));
        await AssertExitedAsync(ChildId(output));
        Assert.Contains("cancelled", await File.ReadAllTextAsync(Path.Combine(results, "process-supervision.log")));
    }

    [Fact]
    public async Task AlreadyCancelledRequestDoesNotStartAProcess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => IsolatedTestProcess.RunAsync(
            Probe("must-not-execute"), "not-started", TimeSpan.FromSeconds(5), cancellation.Token));
    }

    [Fact]
    public async Task ParentOnlyKillCannotSatisfyTheReadyDescendantCleanupAssertion()
    {
        ProcessStartInfo start = BlockedForegroundSta();
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using Process parent = Process.Start(start)!;
        Process? child = null;
        try
        {
            using var startupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            string? line;
            while ((line = await parent.StandardOutput.ReadLineAsync(startupDeadline.Token)) is not null)
            {
                if (line.StartsWith("CHILD ", StringComparison.Ordinal))
                    child = Process.GetProcessById(int.Parse(line[6..], CultureInfo.InvariantCulture));
                if (line == "FOREGROUND STA") break;
            }
            Assert.Equal("FOREGROUND STA", line);
            Assert.NotNull(child);
            parent.Kill(entireProcessTree: false); // Negative control, not the supervisor's cleanup policy.
            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<TimeoutException>(() => child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            // A failing negative control must not leave its synthetic processes.
            if (!parent.HasExited) parent.Kill(entireProcessTree: true);
            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            if (child is not null)
            {
                using (child)
                {
                    if (!child.HasExited) child.Kill(entireProcessTree: true);
                    await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
        }
    }

    [Fact]
    public async Task FirstSupervisionWriteFailureStillTerminatesTheStartedChildAndPreservesTheError()
    {
        var injected = new IOException("Injected first diagnostic write failure");
        int startedId = 0;
        StreamWriter Writer(string path) => path.EndsWith("process-supervision.log", StringComparison.Ordinal)
            ? new FaultingWriter(path, injected, text =>
            {
                Match match = Regex.Match(text, @"PID (\d+)");
                if (match.Success) startedId = int.Parse(match.Groups[1].Value);
                return true;
            }) : new StreamWriter(path) { AutoFlush = true };
        IOException failure = await Assert.ThrowsAsync<IOException>(() => IsolatedTestProcess.RunAsync(
            BlockedForegroundSta(), "first-log-failure", TimeSpan.FromSeconds(20), writerFactory: Writer));
        Assert.Same(injected, failure);
        Assert.True(startedId > 0);
        Assert.False(IsRunning(startedId));
    }

    [Fact]
    public async Task OutputPumpFailurePromptlyKillsTheBlockedStaAndChildWithoutMaskingTheIoError()
    {
        var injected = new IOException("Injected output pump failure");
        var observed = new StringBuilder();
        long failedAt = 0;
        StreamWriter Writer(string path) => path.EndsWith("stdout.log", StringComparison.Ordinal)
            ? new FaultingWriter(path, injected, text =>
            {
                observed.Append(text);
                if (!observed.ToString().Contains("FOREGROUND STA", StringComparison.Ordinal)) return false;
                failedAt = Stopwatch.GetTimestamp();
                return true;
            }) : new StreamWriter(path) { AutoFlush = true };
        IOException failure = await Assert.ThrowsAsync<IOException>(() => IsolatedTestProcess.RunAsync(
            BlockedForegroundSta(), "output-pump-failure", TimeSpan.FromSeconds(20), writerFactory: Writer));
        Assert.Same(injected, failure);
        Assert.True(failedAt > 0);
        Assert.InRange(Stopwatch.GetElapsedTime(failedAt).TotalSeconds, 0, 10);
        string output = observed.ToString();
        int parentId = int.Parse(Regex.Match(output, @"PARENT (\d+)").Groups[1].Value);
        Assert.False(IsRunning(parentId));
        await AssertExitedAsync(ChildId(output));
    }

    private sealed class FaultingWriter(string path, IOException failure, Func<string, bool> fails)
        : StreamWriter(path)
    {
        public override Task WriteLineAsync(string? value) => fails(value ?? "")
            ? Task.FromException(failure) : base.WriteLineAsync(value);

        public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default) =>
            fails(buffer.ToString()) ? Task.FromException(failure) : base.WriteAsync(buffer, cancellationToken);
    }

    private static ProcessStartInfo BlockedForegroundSta() => Probe("blocked-sta");

    private static ProcessStartInfo Probe(params string[] arguments)
    {
        string executable = Path.Combine(AppContext.BaseDirectory, "PersonalTradingJournal.TestProcessProbe.exe");
        Assert.True(File.Exists(executable), "Build the Desktop test project to deploy its synthetic process probe.");
        // The synthetic probe writes UTF-8, regardless of the runner's console
        // code page. Decode both redirected pipes using that explicit contract.
        var start = new ProcessStartInfo(executable)
        {
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        return start;
    }

    private static string ExpectedOutput(string name, char padding)
    {
        var output = new StringBuilder($"probe {name}\n");
        for (int line = 0; line < 2048; line++)
            output.Append(CultureInfo.InvariantCulture, $"{name} {line:D4}: {new string(padding, 96)}\n");
        return output.Append($"complete {name} Ω\n").ToString();
    }

    private static int ChildId(string output) => int.Parse(output.Split('\n')
        .Single(line => line.StartsWith("CHILD ", StringComparison.Ordinal))[6..]);

    private static bool IsRunning(int processId)
    {
        try { using Process process = Process.GetProcessById(processId); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }

    private static async Task AssertExitedAsync(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(process.HasExited);
        }
        catch (ArgumentException) { } // Already fully reaped by the process-tree termination.
    }

    private static async Task<string> WaitForOutputAsync(string path, string marker, Task pending)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(25));
        do
        {
            if (File.Exists(path))
            {
                using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(file);
                string output = await reader.ReadToEndAsync(deadline.Token);
                if (output.Contains(marker, StringComparison.Ordinal)) return output;
            }
            if (pending.IsCompleted) { await pending; throw new InvalidOperationException("Probe exited before its intended blocked phase."); }
        } while (await timer.WaitForNextTickAsync(deadline.Token));
        throw new InvalidOperationException("Probe output timer stopped unexpectedly.");
    }
}
