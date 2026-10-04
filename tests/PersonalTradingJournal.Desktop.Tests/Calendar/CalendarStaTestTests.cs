using System.Windows.Threading;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed class CalendarStaTestTests
{
    private const string HostVariable = "PTJ_CALENDAR_STA_PROBE_HOST";

    [Fact]
    public async Task ImportResourceApplicationCannotPoisonLaterNativeTooltipWindows()
    {
        // This composite runs a supervised Import child, then two real Popup
        // checks. Use the existing suite budget, not a single STA's 30 seconds:
        // VSTest startup/exit must not time out an already-passing child TRX.
        if (await RunInChild(timeout: TimeSpan.FromMinutes(2))) return;
        await new Import.TopstepDesktopRoutingTests().CompiledImportViewLoadsRealResourcesAndMaterializesTopstepReviewControls();
        // The old import test signalled completion just before its Application
        // shutdown. Wait for its owner thread so the regression is not a race.
        if (System.Windows.Application.Current is { } application)
            Assert.True(application.Dispatcher.Thread.Join(TimeSpan.FromSeconds(5)));
        await new Dashboard.ChartTooltipFollowerTests().OpenPopupAppearsBesidePointerAndMovesAfterHorizontalScroll();
        await new Dashboard.ChartTooltipFollowerTests().OpenPopupFlipsBesidePointerNearWindowRightAndBottomEdges();
        Assert.Null(System.Windows.Application.Current);
    }

    [Fact]
    public async Task SuccessfulActionShutsDownItsNamedBackgroundDispatcher()
    {
        if (await RunInChild()) return;
        Dispatcher? dispatcher = null;
        await CalendarStaTest.RunAsync(() =>
        {
            using var phase = CalendarStaTest.Phase("synthetic successful action");
            Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
            Assert.True(Thread.CurrentThread.IsBackground);
            Assert.Contains(nameof(SuccessfulActionShutsDownItsNamedBackgroundDispatcher), Thread.CurrentThread.Name);
            dispatcher = Dispatcher.CurrentDispatcher;
        });
        Assert.NotNull(dispatcher);
        Assert.True(dispatcher.HasShutdownFinished);
    }

    [Fact]
    public async Task AssertionFailureIsPreservedAfterDispatcherCleanup()
    {
        if (await RunInChild()) return;
        Dispatcher? dispatcher = null;
        var expected = new InvalidOperationException("synthetic assertion failure");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => CalendarStaTest.RunAsync(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            using var phase = CalendarStaTest.Phase("synthetic failing assertion");
            throw expected;
        }));
        Assert.Same(expected, actual);
        Assert.True(dispatcher!.HasShutdownFinished);
    }

    [Fact]
    public async Task TimeoutExceptionFromActionIsNotMistakenForAnExpiredHarnessDeadline()
    {
        if (await RunInChild()) return;
        var expected = new TimeoutException("synthetic action exception, not a harness timeout");
        var actual = await Assert.ThrowsAsync<TimeoutException>(() => CalendarStaTest.RunAsync(() => throw expected));
        Assert.Same(expected, actual);
        bool nextAction = false;
        await CalendarStaTest.RunAsync(() => nextAction = true);
        Assert.True(nextAction);
    }

    [Fact]
    public async Task NativeModeKeepsDispatcherLifetimeWithinItsChildWithoutLeavingForegroundWork()
    {
        if (await RunInChild()) return;
        Dispatcher? dispatcher = null;
        Thread? worker = null;
        await CalendarStaTest.RunAsync(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            worker = Thread.CurrentThread;
            Assert.True(worker.IsBackground);
            Assert.Contains("Calendar layout:", worker.Name);
        }, shutdownDispatcher: false);
        Assert.True(worker!.Join(TimeSpan.FromSeconds(5)));
        Assert.False(dispatcher!.HasShutdownStarted);
        // The enclosing child exits after this assertion. Its renderer/dispatcher
        // cannot be retained by the parent or the next isolated suite.
    }

    [Fact]
    public async Task TimeoutNamesBlockedPhaseAndCannotAccumulateAnotherSta()
    {
        if (await RunInChild()) return;
        using var release = new ManualResetEventSlim(false);
        var clock = new DeadlineClock();
        var ready = new TaskCompletionSource<Thread>(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread? thread = null;
        Task pending = CalendarStaTest.RunAsync(() =>
        {
            using var phase = CalendarStaTest.Phase("synthetic blocked operation");
            ready.SetResult(Thread.CurrentThread);
            release.Wait();
        }, timeoutProvider: clock);
        try
        {
            thread = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // Trigger the normal 30-second deadline only after the intended
            // blocked phase is reached; cold WPF startup speed is not an assertion.
            (await clock.Created.Task.WaitAsync(TimeSpan.FromSeconds(5))).Fire();
            var error = await Assert.ThrowsAsync<TimeoutException>(() => pending);
            Assert.Contains("synthetic blocked operation", error.Message);
            Assert.Contains(thread.ManagedThreadId.ToString(), error.Message);
            Assert.True(thread.IsBackground);
            bool ranAnother = false;
            var subsequent = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CalendarStaTest.RunAsync(() => ranAnother = true));
            Assert.Contains("refusing another STA", subsequent.Message);
            Assert.False(ranAnother);
        }
        finally
        {
            release.Set();
            // This probe is cooperatively releasable; real stuck native work is
            // bounded by IsolatedTestProcess's independently tested kill boundary.
            if (thread is not null) Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        }
    }

    private static async Task<bool> RunInChild([System.Runtime.CompilerServices.CallerMemberName] string scenario = "",
        TimeSpan? timeout = null)
    {
        if (Environment.GetEnvironmentVariable(HostVariable) == "1") return false;
        await IsolatedTestProcess.RunSuiteAsync(typeof(CalendarStaTestTests), "calendar-sta-" + scenario,
            HostVariable, timeout ?? TimeSpan.FromSeconds(30),
            testCaseFilter: $"FullyQualifiedName={typeof(CalendarStaTestTests).FullName}.{scenario}");
        return true;
    }

    private sealed class DeadlineClock : TimeProvider
    {
        internal TaskCompletionSource<DeadlineTimer> Created { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Assert.Equal(TimeSpan.FromSeconds(30), dueTime);
            var timer = new DeadlineTimer(callback, state);
            Created.SetResult(timer);
            return timer;
        }
    }

    private sealed class DeadlineTimer(TimerCallback callback, object? state) : ITimer
    {
        private bool _disposed;
        internal void Fire() { Assert.False(_disposed); callback(state); }
        public bool Change(TimeSpan dueTime, TimeSpan period) => !_disposed;
        public void Dispose() => _disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
