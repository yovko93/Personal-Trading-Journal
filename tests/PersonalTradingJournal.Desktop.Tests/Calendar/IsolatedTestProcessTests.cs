using System.Diagnostics;
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
        IsolatedProcessResult result = await IsolatedTestProcess.RunAsync(PowerShell(
            $"[Console]::Out.WriteLine('probe stdout'); [Console]::Error.WriteLine('probe stderr'); exit {exitCode}"),
            $"exit-{exitCode}", TimeSpan.FromSeconds(15));
        Assert.Equal(exitCode, result.ExitCode);
        Assert.Contains("probe stdout", result.StandardOutput);
        Assert.Contains("probe stderr", result.StandardError);
        Assert.Contains("probe stdout", await File.ReadAllTextAsync(Path.Combine(result.ResultsDirectory, "stdout.log")));
        Assert.Contains("probe stderr", await File.ReadAllTextAsync(Path.Combine(result.ResultsDirectory, "stderr.log")));
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
            PowerShell("throw 'Must not execute'"), "not-started", TimeSpan.FromSeconds(5), cancellation.Token));
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

    private static ProcessStartInfo BlockedForegroundSta()
    {
        const string source = """
            using System;
            using System.Diagnostics;
            using System.Threading;
            public static class ForegroundStaProbe
            {
                public static void Run()
                {
                    var childStart = new ProcessStartInfo("powershell.exe");
                    childStart.Arguments = "-NoLogo -NoProfile -NonInteractive -Command \"[Threading.Thread]::Sleep(-1)\"";
                    childStart.UseShellExecute = false;
                    childStart.CreateNoWindow = true;
                    Process child = Process.Start(childStart);
                    var thread = new Thread(() =>
                    {
                        Console.WriteLine("PARENT " + Process.GetCurrentProcess().Id);
                        Console.WriteLine("CHILD " + child.Id);
                        Console.WriteLine("FOREGROUND " + Thread.CurrentThread.GetApartmentState());
                        Console.Out.Flush();
                        new ManualResetEventSlim(false).Wait();
                    });
                    thread.IsBackground = false;
                    thread.Name = "synthetic-blocked-foreground-sta";
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join();
                }
            }
            """;
        return PowerShell("Add-Type -TypeDefinition '" + source.Replace("'", "''", StringComparison.Ordinal) + "'; [ForegroundStaProbe]::Run()");
    }

    private static ProcessStartInfo PowerShell(string command)
    {
        var start = new ProcessStartInfo("powershell.exe");
        start.ArgumentList.Add("-NoLogo");
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(command);
        return start;
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
