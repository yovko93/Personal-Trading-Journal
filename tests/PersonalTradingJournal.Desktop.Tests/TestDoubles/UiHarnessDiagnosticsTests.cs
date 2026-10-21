using System.Text.Json;
using System.Windows.Controls;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

public sealed class UiHarnessDiagnosticsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnstartedOrStaleCommandCannotSupplyAnExecutionTask(bool stale)
    {
        var prior = new TaskCompletionSource(); prior.SetResult();
        var error = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = UiKeyboardProbe.RequireNewExecution(prior.Task, stale ? prior.Task : null);
        });
        Assert.Contains("fresh command ExecutionTask", error.Message);
    }

    [Fact]
    public async Task FreshExecutionPreservesTheActualFailure()
    {
        var prior = new TaskCompletionSource(); prior.SetResult();
        var current = new TaskCompletionSource();
        var expected = new IOException("synthetic command failure");
        current.SetException(expected);
        Task actual = UiKeyboardProbe.RequireNewExecution(prior.Task, current.Task);
        Assert.Same(current.Task, actual);
        Assert.Same(expected, await Assert.ThrowsAsync<IOException>(() => actual));
    }

    [Fact]
    public async Task DisconnectedKeyboardTargetFailsBeforeInputWithStateDiagnostics() =>
        await CalendarStaTest.RunAsync(() =>
        {
            var error = Assert.Throws<InvalidOperationException>(() => UiKeyboardProbe.Ready(new Button { Name = "Detached" }));
            Assert.Contains("Target must belong to a window", error.Message);
            Assert.Contains("Button/Detached", error.Message);
            Assert.Contains("keyboardFocused=False", error.Message);
        });

    [Fact]
    public async Task PendingTaskIsNamedAtCreationAndRetainsIdentityThroughCompletion()
    {
        var records = new List<string>();
        var trace = new UiCaseTrace("synthetic fixture", records.Add);
        var pending = new TaskCompletionSource<int>();
        Task<int> observed = trace.Await(() => pending.Task, "fixture load", line: 42);
        Assert.False(observed.IsCompleted);
        Assert.Contains(records, r => r.Contains("\"State\":\"creating task\""));
        var created = records.Select(r => JsonDocument.Parse(r).RootElement.Clone())
            .Single(r => r.GetProperty("State").GetString() == "created");
        Assert.Equal("fixture load", created.GetProperty("Phase").GetString());
        Assert.Equal(pending.Task.Id, created.GetProperty("TaskId").GetInt32());
        Assert.Equal("WaitingForActivation", created.GetProperty("TaskStatus").GetString());
        Assert.Equal(42, created.GetProperty("Line").GetInt32());
        pending.SetResult(17);
        Assert.Equal(17, await observed);
        var completed = records.Select(r => JsonDocument.Parse(r).RootElement.Clone())
            .Single(r => r.GetProperty("State").GetString() == "await ended");
        Assert.Equal(pending.Task.Id, completed.GetProperty("TaskId").GetInt32());
        Assert.Equal("RanToCompletion", completed.GetProperty("TaskStatus").GetString());
        Assert.True(completed.GetProperty("Milliseconds").GetDouble() >= created.GetProperty("Milliseconds").GetDouble());
        Assert.True(completed.GetProperty("Utc").GetDateTimeOffset() >= created.GetProperty("Utc").GetDateTimeOffset());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrCancelledOperationKeepsItsOutcomeAndDiagnosticBoundary(bool cancel)
    {
        var records = new List<string>();
        var trace = new UiCaseTrace("synthetic operation", records.Add);
        var pending = new TaskCompletionSource();
        Task observed = trace.Await(() => pending.Task, "pending save");
        if (cancel) pending.SetCanceled(); else pending.SetException(new IOException("synthetic storage failure"));
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => observed);
        else await Assert.ThrowsAsync<IOException>(() => observed);
        Assert.Contains(records, r => r.Contains(cancel ? "Canceled" : "Faulted") && r.Contains("await ended"));
        Assert.Contains(records, r => r.Contains("\"State\":\"end\""));
    }
}
