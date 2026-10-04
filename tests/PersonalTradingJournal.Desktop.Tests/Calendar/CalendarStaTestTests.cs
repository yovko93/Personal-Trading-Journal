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
    public async Task NativeModeReusesBackgroundDispatcherUntilItsChildExits()
    {
        if (await RunInChild()) return;
        Dispatcher? dispatcher = null;
        Thread? worker = null;
        await CalendarStaTest.RunAsync(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            worker = Thread.CurrentThread;
            Assert.True(worker.IsBackground);
            Assert.Contains("Calendar native renderer", worker.Name);
        }, shutdownDispatcher: false);
        await CalendarStaTest.RunAsync(() =>
        {
            Assert.Same(worker, Thread.CurrentThread);
            Assert.Same(dispatcher, Dispatcher.CurrentDispatcher);
        }, shutdownDispatcher: false);
        Assert.True(worker!.IsAlive);
        Assert.True(worker.IsBackground);
        Assert.False(dispatcher!.HasShutdownStarted);
        // The enclosing child exits after this assertion. Its renderer/dispatcher
        // cannot be retained by the parent or the next isolated suite.
    }

    [Fact]
    public async Task NativeActionsCannotReenterEachOtherThroughANestedDispatcherFrame()
    {
        if (await RunInChild()) return;
        var entered = new TaskCompletionSource<(Dispatcher Dispatcher, DispatcherFrame Frame)>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool secondRan = false, ranInsideFirst = false;
        Thread? firstThread = null;
        Task first = CalendarStaTest.RunAsync(() =>
        {
            firstThread = Thread.CurrentThread;
            var frame = new DispatcherFrame();
            entered.SetResult((Dispatcher.CurrentDispatcher, frame));
            Dispatcher.PushFrame(frame);
        }, shutdownDispatcher: false);
        var running = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task second = CalendarStaTest.RunAsync(() =>
        {
            Assert.Same(firstThread, Thread.CurrentThread);
            secondRan = true;
        }, shutdownDispatcher: false);
        await running.Dispatcher.InvokeAsync(() =>
        {
            ranInsideFirst = secondRan;
            running.Frame.Continue = false;
        }, DispatcherPriority.Background).Task;
        await Task.WhenAll(first, second);
        Assert.False(ranInsideFirst);
        Assert.True(secondRan);
    }

    [Fact]
    public async Task NativeActionFailurePreservesExceptionAndReleasesTheDispatcherForTheNextCase()
    {
        if (await RunInChild()) return;
        var expected = new TimeoutException("synthetic native action exception, not a harness deadline");
        Dispatcher? dispatcher = null;
        SynchronizationContext? originalContext = null;
        var actual = await Assert.ThrowsAsync<TimeoutException>(() => CalendarStaTest.RunAsync(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            originalContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            throw expected;
        }, shutdownDispatcher: false));
        Assert.Same(expected, actual);
        await CalendarStaTest.RunAsync(() =>
        {
            Assert.Same(dispatcher, Dispatcher.CurrentDispatcher);
            Assert.IsType<DispatcherSynchronizationContext>(SynchronizationContext.Current);
            Assert.IsType<DispatcherSynchronizationContext>(originalContext);
        }, shutdownDispatcher: false);
    }

    [Fact]
    public async Task NativeUnhandledDispatcherCallbackFailsItsChildWithTheOriginalError()
    {
        const string marker = "synthetic unhandled native dispatcher callback";
        if (Environment.GetEnvironmentVariable(HostVariable) != "1")
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                IsolatedTestProcess.RunSuiteAsync(typeof(CalendarStaTestTests), "calendar-sta-unhandled-native",
                    HostVariable, TimeSpan.FromSeconds(30),
                    testCaseFilter: $"FullyQualifiedName={typeof(CalendarStaTestTests).FullName}.{nameof(NativeUnhandledDispatcherCallbackFailsItsChildWithTheOriginalError)}"));
            Assert.Contains(marker, failure.Message);
            Assert.Contains(nameof(InvalidOperationException), failure.Message);
            Assert.Contains("Test host process crashed", failure.Message);
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            Dispatcher native = Dispatcher.CurrentDispatcher;
            void Record(string stage)
            {
                if (Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") is { Length: > 0 } output)
                    File.AppendAllText(System.IO.Path.Combine(output, "native-fatal-probe.log"), stage + Environment.NewLine);
            }
            native.UnhandledExceptionFilter += (_, args) =>
                Record($"filter requested catch={args.RequestCatch}");
            native.UnhandledException += (_, args) => Record($"unhandled event handled for pump unwind={args.Handled}");
            // BeginInvoke deliberately represents an unhandled dispatcher event,
            // unlike InvokeAsync's ordinary action failure returned through Task.
            native.BeginInvoke(DispatcherPriority.Normal,
                new Action(() => { Record("callback entered"); throw new InvalidOperationException(marker); }));
        }, shutdownDispatcher: false);
        // Keep the case open until the unhandled callback terminates its child.
        // A swallowed callback instead fails this bounded wait without the marker.
        await new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            .Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task NativeTimeoutCancelsQueuedCasesAndPoisonsFurtherDispatchWithoutRunningTheirActions()
    {
        if (await RunInChild()) return;
        using var release = new ManualResetEventSlim(false);
        var clock = new DeadlineClock();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher? dispatcher = null;
        bool queuedRan = false, laterRan = false;
        Task pending = CalendarStaTest.RunAsync(() =>
        {
            using var phase = CalendarStaTest.Phase("synthetic blocked native operation");
            dispatcher = Dispatcher.CurrentDispatcher;
            entered.SetResult();
            release.Wait();
            released.SetResult();
        }, timeoutProvider: clock, shutdownDispatcher: false);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task queued = CalendarStaTest.RunAsync(() => queuedRan = true, shutdownDispatcher: false);
            (await clock.Created.Task.WaitAsync(TimeSpan.FromSeconds(5))).Fire();
            var timeout = await Assert.ThrowsAsync<TimeoutException>(() => pending);
            Assert.Contains("synthetic blocked native operation", timeout.Message);
            var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => queued);
            Assert.Contains("refusing another STA", refused.Message);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CalendarStaTest.RunAsync(() => laterRan = true, shutdownDispatcher: false));
            Assert.False(queuedRan);
            Assert.False(laterRan);
        }
        finally
        {
            release.Set();
            await released.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // Flush past any formerly queued normal-priority callback, proving
            // cancellation cannot leave an action that runs after the timeout.
            await dispatcher!.InvokeAsync(() => Assert.False(queuedRan), DispatcherPriority.ApplicationIdle)
                .Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
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
