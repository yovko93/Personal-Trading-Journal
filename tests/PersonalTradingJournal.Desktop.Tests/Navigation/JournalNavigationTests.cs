using System.Collections.Concurrent;
using System.Diagnostics;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Journals;
using Xunit.Abstractions;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    private readonly ITestOutputHelper _journalOutput;
    private const string JournalRaceHostVariable = "PTJ_JOURNAL_NAVIGATION_RACE_HOST";
    // Keep the controlled concurrent reads independent of unrelated WPF tests'
    // shared ThreadPool. The child still runs all 16 flows concurrently, with the
    // same five-second operation guards and normal solution-level concurrency.
    private static readonly Lazy<Task> JournalRaceHost = new(() => IsolatedTestProcess.RunSuiteAsync(
        typeof(MainWindowViewModelTests), "journal-navigation-races", JournalRaceHostVariable,
        TimeSpan.FromSeconds(30), testCaseFilter:
        $"FullyQualifiedName={typeof(MainWindowViewModelTests).FullName}.{nameof(LeavingJournalCancelsReadAndReturningIgnoresItsLateResult)}|" +
        $"FullyQualifiedName={typeof(MainWindowViewModelTests).FullName}.{nameof(ConcurrentJournalNavigationsKeepFreshStateWhenCancelledReadsFinishLate)}"));

    public MainWindowViewModelTests(ITestOutputHelper journalOutput) => _journalOutput = journalOutput;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task AnswersWithoutFreeformTextProtectPageAndWindowNavigation(int question)
    {
        var fixture = CreateJournalFixture();
        using MainWindowViewModel main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await fixture.Journal.LoadTask;
        fixture.Journal.OpenEditorCommand.Execute(null);
        switch (question)
        {
            case 1: fixture.Journal.WentWell = "I followed the plan."; break;
            case 2: fixture.Journal.NeedsImprovement = "More patience."; break;
            case 3: fixture.Journal.NextTradingDay = "Wait for confirmation."; break;
        }
        Assert.Empty(fixture.Journal.Text);
        Assert.True(fixture.Journal.IsDirty);
        main.NavigateCommand.Execute(NavigationDestination.Notebook);
        Assert.Equal(NavigationDestination.Journal, main.CurrentDestination);
        Assert.False(main.TryCloseWindow());
        Assert.True(fixture.Journal.IsDirty);
        Assert.Single(fixture.Repository.ReadCalls);
        fixture.Dialogs.ConfirmationResult = true;
        main.NavigateCommand.Execute(NavigationDestination.Notebook);
        Assert.Equal(NavigationDestination.Notebook, main.CurrentDestination);
        Assert.Empty(fixture.Repository.CreateCalls);
    }

    [Fact]
    public async Task JournalNavigationRetainsViewModelAndLoadsFreshOnEachVisit()
    {
        var fixture = CreateJournalFixture();
        using MainWindowViewModel main = fixture.Main;

        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await fixture.Journal.LoadTask;
        fixture.Journal.OpenEditorCommand.Execute(null);

        Assert.Equal("Journal", main.PageTitle);
        Assert.Same(fixture.Journal, main.CurrentContentViewModel);
        Assert.Single(fixture.Repository.ReadCalls);

        main.NavigateCommand.Execute(NavigationDestination.Notebook);
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await fixture.Journal.LoadTask;
        fixture.Journal.OpenEditorCommand.Execute(null);

        Assert.Same(fixture.Journal, main.CurrentContentViewModel);
        Assert.Equal(2, fixture.Repository.ReadCalls.Count);
    }

    [Fact]
    public async Task JournalKeepEditingPreservesContentDestinationAndSidebarState()
    {
        var fixture = CreateJournalFixture();
        using MainWindowViewModel main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await fixture.Journal.LoadTask;
        fixture.Journal.OpenEditorCommand.Execute(null);
        fixture.Journal.Text = "Keep this unsaved note.";
        NavigationSectionViewModel analysis = main.NavigationSections.Single(section => section.Title == "ANALYSIS");
        analysis.ToggleCommand.Execute(null);

        main.NavigateCommand.Execute(NavigationDestination.Mistakes);

        Assert.Equal(NavigationDestination.Journal, main.CurrentDestination);
        Assert.Same(fixture.Journal, main.CurrentContentViewModel);
        Assert.Equal("Keep this unsaved note.", fixture.Journal.Text);
        Assert.Equal(NavigationDestination.Journal, Assert.Single(main.AllNavigationItems, item => item.IsSelected).Destination);
        Assert.False(analysis.IsExpanded);
        Assert.NotNull(fixture.Dialogs.ConfirmationRequest);
        Assert.Single(fixture.Repository.ReadCalls);
        Assert.Empty(fixture.Repository.CreateCalls);
    }

    [Fact]
    public async Task JournalDiscardAllowsNavigationAndReloadsSavedContentOnReturn()
    {
        var persisted = new DailyJournalDetails(
            new DailyJournalEntry(new DateOnly(2026, 9, 9), null, "Saved note.", FixedTimeProvider.FixedUtcNow),
            DailyJournalAccountState.AllAccounts, null);
        var repository = new NavigationJournalRepository { Read = (_, _, _) => Task.FromResult<DailyJournalDetails?>(persisted) };
        var fixture = CreateJournalFixture(repository);
        using MainWindowViewModel main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await fixture.Journal.LoadTask;
        fixture.Journal.OpenEditorCommand.Execute(null);
        fixture.Journal.Text = "Discard this edit.";
        fixture.Dialogs.ConfirmationResult = true;

        main.NavigateCommand.Execute(NavigationDestination.Notebook);

        Assert.Equal(NavigationDestination.Notebook, main.CurrentDestination);
        Assert.NotNull(fixture.Dialogs.ConfirmationRequest);
        Assert.Empty(repository.CreateCalls);

        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await fixture.Journal.LoadTask;
        fixture.Journal.OpenEditorCommand.Execute(null);
        Assert.Same(fixture.Journal, main.CurrentContentViewModel);
        Assert.Equal("Saved note.", fixture.Journal.Text);
        Assert.Equal(2, repository.ReadCalls.Count);
    }

    [Fact]
    public async Task ClickingActiveJournalDoesNotPromptOrReloadDirtyEditor()
    {
        var fixture = CreateJournalFixture();
        using MainWindowViewModel main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await fixture.Journal.LoadTask;
        fixture.Journal.OpenEditorCommand.Execute(null);
        fixture.Journal.Text = "Unsaved note.";

        main.NavigateCommand.Execute(NavigationDestination.Journal);

        Assert.Null(fixture.Dialogs.ConfirmationRequest);
        Assert.Single(fixture.Repository.ReadCalls);
        Assert.Equal("Unsaved note.", fixture.Journal.Text);
        Assert.Equal(NavigationDestination.Journal, main.CurrentDestination);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task LeavingJournalCancelsReadAndReturningIgnoresItsLateResult(bool lateFailure) =>
        Environment.GetEnvironmentVariable(JournalRaceHostVariable) == "1"
            ? AssertLateJournalReadIgnoredAsync(lateFailure, "single navigation")
            : JournalRaceHost.Value;

    [Fact]
    public Task ConcurrentJournalNavigationsKeepFreshStateWhenCancelledReadsFinishLate() =>
        Environment.GetEnvironmentVariable(JournalRaceHostVariable) == "1"
            ? Task.WhenAll(Enumerable.Range(0, 16).Select(index =>
                AssertLateJournalReadIgnoredAsync(index % 2 == 1, $"parallel navigation {index}", logSuccess: false)))
            : JournalRaceHost.Value;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JournalReadWaitsForAccountsAndCancellationCanPreventRepositoryInvocation(bool cancel)
    {
        var trace = new NavigationReadTrace();
        var accounts = new GatedNavigationAccountReader();
        var repository = new NavigationJournalRepository { Trace = trace.Record };
        var fixture = CreateJournalFixture(repository, accounts);
        using MainWindowViewModel main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        Task load = fixture.Journal.LoadTask;
        Exception? failure = null;
        try
        {
            await WaitForJournalPhaseAsync(accounts.Started.Task, load, "account read", fixture.Journal, repository, trace);
            Assert.False(load.IsCompleted);
            Assert.True(fixture.Journal.IsLoading);
            Assert.False(repository.ReadStarted.Task.IsCompleted);
            Assert.Empty(repository.ReadCalls);

            if (cancel)
            {
                main.NavigateCommand.Execute(NavigationDestination.Notebook);
                Assert.True(accounts.Token.IsCancellationRequested);
                await load.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Empty(repository.ReadCalls);
                Assert.False(repository.ReadStarted.Task.IsCompleted);
                Assert.False(fixture.Journal.IsLoading);
                Assert.Equal(NavigationDestination.Notebook, main.CurrentDestination);
            }
            else
            {
                accounts.Release.TrySetResult([]);
                await WaitForJournalPhaseAsync(repository.ReadStarted.Task, load, "repository read", fixture.Journal, repository, trace);
                await load.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Single(repository.ReadCalls);
                Assert.Null(fixture.Journal.ErrorMessage);
                Assert.True(fixture.Journal.ShowCompactReview);
                Assert.False(fixture.Journal.IsEditorOpen);
                fixture.Journal.OpenEditorCommand.Execute(null);
                Assert.True(fixture.Journal.CanEdit);
            }
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            accounts.Release.TrySetResult([]);
            main.Dispose();
            await DrainJournalLoadsAsync([load], failure, trace);
            foreach (string line in trace.Events) _journalOutput.WriteLine(line);
        }
    }

    [Fact]
    public async Task CompletedLoadWithoutRepositoryInvocationReportsTheFailedPhase()
    {
        var accounts = new FakeTradingAccountReader();
        accounts.EnqueueException(new IOException("Synthetic account read failure."));
        var fixture = CreateJournalFixture(accountReader: accounts);
        using MainWindowViewModel main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await fixture.Journal.LoadTask.WaitAsync(TimeSpan.FromSeconds(5));

        var error = await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => WaitForJournalPhaseAsync(
            fixture.Repository.ReadStarted.Task, fixture.Journal.LoadTask, "repository read", fixture.Journal,
            fixture.Repository, new NavigationReadTrace()));

        Assert.Contains("load completed before repository read", error.Message);
        Assert.Contains("calls=0", error.Message);
        Assert.Contains("Journal could not be loaded", error.Message);
    }

    private async Task AssertLateJournalReadIgnoredAsync(bool lateFailure, string scenario, bool logSuccess = true)
    {
        var trace = new NavigationReadTrace();
        var pending = new TaskCompletionSource<DailyJournalDetails?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new NavigationJournalRepository { Read = (_, _, _) => pending.Task, Trace = trace.Record };
        var fixture = CreateJournalFixture(repository, new TracedNavigationAccountReader(trace));
        using MainWindowViewModel main = fixture.Main;
        trace.Record($"{scenario}: before first navigation; lateFailure={lateFailure}");
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        Task previousLoad = fixture.Journal.LoadTask;
        Task currentLoad = Task.CompletedTask;
        Exception? failure = null;
        trace.Record($"after first navigation: destination={main.CurrentDestination}, load={previousLoad.Status}, loading={fixture.Journal.IsLoading}");
        try
        {
            await WaitForJournalPhaseAsync(repository.ReadStarted.Task, previousLoad, "repository read", fixture.Journal, repository, trace);
            CancellationToken previousToken = Assert.Single(repository.ReadCalls).Token;
            Assert.False(previousLoad.IsCompleted);

            main.NavigateCommand.Execute(NavigationDestination.Notebook);

            Assert.True(previousToken.IsCancellationRequested);
            Assert.False(previousLoad.IsCompleted);
            var review = new DailyReviewAnswers("Followed the plan.", "Wait longer.", "Use the checklist.");
            var fresh = new DailyJournalDetails(DailyJournalEntry.Rehydrate(Guid.NewGuid(),
                new DateOnly(2026, 9, 9), null, "Fresh note.", false, 7,
                FixedTimeProvider.FixedUtcNow, FixedTimeProvider.FixedUtcNow, review),
                DailyJournalAccountState.AllAccounts, null);
            repository.Read = (_, _, _) => Task.FromResult<DailyJournalDetails?>(fresh);
            main.NavigateCommand.Execute(NavigationDestination.Journal);
            currentLoad = fixture.Journal.LoadTask;
            await currentLoad.WaitAsync(TimeSpan.FromSeconds(5));
            AssertFreshJournalState();
            Assert.False(previousLoad.IsCompleted);
            Assert.Equal(2, repository.ReadCalls.Count);
            Assert.False(repository.ReadCalls.Last().Token.IsCancellationRequested);

            if (lateFailure) pending.SetException(new IOException("Synthetic late journal read failure."));
            else pending.SetResult(new DailyJournalDetails(
                new DailyJournalEntry(new DateOnly(2026, 9, 9), null, "Late note.", FixedTimeProvider.FixedUtcNow),
                DailyJournalAccountState.AllAccounts, null));
            await previousLoad.WaitAsync(TimeSpan.FromSeconds(5));

            AssertFreshJournalState();
            Assert.Equal(2, repository.ReadCalls.Count);

            void AssertFreshJournalState()
            {
                Assert.Equal(NavigationDestination.Journal, main.CurrentDestination);
                Assert.Equal("Fresh note.", fixture.Journal.Text);
                Assert.Equal(review.WentWell, fixture.Journal.WentWell);
                Assert.Equal(review.NeedsImprovement, fixture.Journal.NeedsImprovement);
                Assert.Equal(review.NextTradingDay, fixture.Journal.NextTradingDay);
                Assert.Equal(7L, fixture.Journal.Revision);
                Assert.True(fixture.Journal.IsCompleted);
                Assert.False(fixture.Journal.IsLoading);
                Assert.False(fixture.Journal.IsDirty);
                Assert.Null(fixture.Journal.ErrorMessage);
            }
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            pending.TrySetResult(null);
            main.Dispose();
            await DrainJournalLoadsAsync([previousLoad, currentLoad], failure, trace);
            if (logSuccess || failure is not null)
                foreach (string line in trace.Events) _journalOutput.WriteLine(line);
        }
    }

    private static async Task WaitForJournalPhaseAsync(Task signal, Task load, string phase, JournalViewModel journal,
        NavigationJournalRepository repository, NavigationReadTrace trace)
    {
        try
        {
            // A load that ended without reaching the expected fake must fail immediately.
            // The existing five-second bound still detects queued or stuck work.
            await Task.WhenAny(signal, load).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(signal.IsCompletedSuccessfully, $"Journal load completed before {phase}; {State()}");
        }
        finally { trace.Record($"finished waiting for {phase}: {State()}"); }

        string State() => $"signal={signal.Status}, load={load.Status}, calls={repository.ReadCalls.Count}, " +
            $"loading={journal.IsLoading}, error={journal.ErrorMessage ?? "<none>"}";
    }

    private static async Task DrainJournalLoadsAsync(Task[] loads, Exception? originalFailure, NavigationReadTrace trace)
    {
        try { await Task.WhenAll(loads).WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception cleanupError) when (originalFailure is not null)
        {
            // Keep the triggering assertion, while reporting bounded cleanup trouble.
            trace.Record($"load cleanup after {originalFailure.GetType().Name} failed: {cleanupError}");
        }
    }

    [Fact]
    public async Task SavingJournalBlocksNavigationAndWindowCloseUntilSaveCompletes()
    {
        var pending = new TaskCompletionSource<DailyJournalWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new NavigationJournalRepository { Create = (_, _) => pending.Task };
        var fixture = CreateJournalFixture(repository);
        using MainWindowViewModel main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await fixture.Journal.LoadTask;
        fixture.Journal.OpenEditorCommand.Execute(null);
        fixture.Journal.Text = "Save this note.";
        Task save = fixture.Journal.SaveCommand.ExecuteAsync(null);
        await repository.CreateStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        main.NavigateCommand.Execute(NavigationDestination.Notebook);

        Assert.True(fixture.Journal.IsSaving);
        Assert.Equal(NavigationDestination.Journal, main.CurrentDestination);
        Assert.Equal(NavigationDestination.Journal, Assert.Single(main.AllNavigationItems, item => item.IsSelected).Destination);
        Assert.False(main.TryCloseWindow());
        Assert.Null(fixture.Dialogs.ConfirmationRequest);
        var request = Assert.Single(repository.CreateCalls);
        Assert.False(request.Token.IsCancellationRequested);

        pending.SetResult(new DailyJournalWriteResult(DailyJournalWriteStatus.Created,
            new DailyJournalDetails(new DailyJournalEntry(request.Command.TradingDate,
                request.Command.TradingAccountId, request.Command.Text, request.Command.IsDraft,
                FixedTimeProvider.FixedUtcNow), DailyJournalAccountState.AllAccounts, null)));
        await save;

        Assert.True(main.TryCloseWindow());
        Assert.Null(fixture.Dialogs.ConfirmationRequest);
    }

    [Fact]
    public async Task WindowCloseUsesJournalDiscardDecision()
    {
        var fixture = CreateJournalFixture();
        using MainWindowViewModel main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await fixture.Journal.LoadTask;
        fixture.Journal.OpenEditorCommand.Execute(null);
        fixture.Journal.Text = "Keep the window open.";

        Assert.False(main.TryCloseWindow());
        Assert.Equal("Keep the window open.", fixture.Journal.Text);
        Assert.Equal(NavigationDestination.Journal, main.CurrentDestination);
        Assert.NotNull(fixture.Dialogs.ConfirmationRequest);

        fixture.Dialogs.ConfirmationResult = true;
        Assert.True(main.TryCloseWindow());
        Assert.Empty(fixture.Repository.CreateCalls);
    }

    [Fact]
    public async Task AcceptedWindowCloseCancelsAnActiveJournalRead()
    {
        var pending = new TaskCompletionSource<DailyJournalDetails?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new NavigationJournalRepository { Read = (_, _, _) => pending.Task };
        var fixture = CreateJournalFixture(repository);
        using MainWindowViewModel main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        Task load = fixture.Journal.LoadTask;
        await repository.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        CancellationToken token = Assert.Single(repository.ReadCalls).Token;

        Assert.True(main.TryCloseWindow());
        Assert.True(token.IsCancellationRequested);
        pending.SetResult(null);
        await load;
    }

    [Fact]
    public void WindowCloseOutsideJournalDoesNotConsultOrActivateJournal()
    {
        var fixture = CreateJournalFixture();
        using MainWindowViewModel main = fixture.Main;
        fixture.Journal.OpenEditorCommand.Execute(null);
        fixture.Journal.Text = "An unactivated editor cannot block another page.";

        Assert.True(main.TryCloseWindow());
        Assert.Null(fixture.Dialogs.ConfirmationRequest);
        Assert.Empty(fixture.Repository.ReadCalls);
        Assert.Equal(NavigationDestination.Dashboard, main.CurrentDestination);
    }

    private static (MainWindowViewModel Main, JournalViewModel Journal,
        NavigationJournalRepository Repository, FakeDialogService Dialogs) CreateJournalFixture(
            NavigationJournalRepository? repository = null, ITradingAccountReader? accountReader = null,
            TimeProvider? clock = null, IDailyJournalHistoryReader? historyReader = null)
    {
        repository ??= new NavigationJournalRepository();
        var dialogs = new FakeDialogService();
        var journal = new JournalViewModel(repository, accountReader ?? new FakeTradingAccountReader(), dialogs,
            new JournalTradeContextViewModel(new FakeTradingCalendarDayReader(), new FakeTradingAccountReader()), clock ?? new FixedTimeProvider(), historyReader);
        return (CreateFixture(journalViewModel: journal).Main, journal, repository, dialogs);
    }

    private sealed class NavigationJournalRepository : IDailyJournalRepository
    {
        public Task<DailyJournalWriteResult> DeleteAsync(DeleteDailyJournalCommand command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ConcurrentQueue<(DateOnly Date, Guid? AccountId, CancellationToken Token)> ReadCalls { get; } = new();
        public ConcurrentQueue<(CreateDailyJournalCommand Command, CancellationToken Token)> CreateCalls { get; } = new();
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CreateStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<DateOnly, Guid?, CancellationToken, Task<DailyJournalDetails?>>? Read { get; set; }
        public Func<CreateDailyJournalCommand, CancellationToken, Task<DailyJournalWriteResult>>? Create { get; set; }
        public Action<string>? Trace { get; init; }

        public Task<DailyJournalDetails?> GetAsync(DateOnly tradingDate, Guid? tradingAccountId = null,
            CancellationToken cancellationToken = default)
        {
            Trace?.Invoke($"repository GetAsync entered: cancelled={cancellationToken.IsCancellationRequested}");
            var read = Read;
            ReadCalls.Enqueue((tradingDate, tradingAccountId, cancellationToken));
            ReadStarted.TrySetResult();
            Trace?.Invoke("repository read-start signal completed");
            return read?.Invoke(tradingDate, tradingAccountId, cancellationToken) ?? Task.FromResult<DailyJournalDetails?>(null);
        }

        public Task<IReadOnlyList<DailyJournalRevision>> GetHistoryAsync(Guid journalId,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DailyJournalRevision>>([]);

        public Task<DailyJournalWriteResult> CreateAsync(CreateDailyJournalCommand command,
            CancellationToken cancellationToken = default)
        {
            CreateCalls.Enqueue((command, cancellationToken));
            CreateStarted.TrySetResult();
            return Create?.Invoke(command, cancellationToken) ?? throw new InvalidOperationException("The navigation test must not create a journal.");
        }

        public Task<DailyJournalWriteResult> UpdateAsync(UpdateDailyJournalCommand command,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The navigation test must not update a journal.");
    }

    private sealed class TracedNavigationAccountReader(NavigationReadTrace trace) : ITradingAccountReader
    {
        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            trace.Record($"account GetAllAsync entered inside Task.Run: cancelled={cancellationToken.IsCancellationRequested}");
            return Task.FromResult<IReadOnlyList<AccountListItem>>([]);
        }

        public Task<TradingAccountDetails?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TradingAccountDetails?>(null);
    }

    private sealed class GatedNavigationAccountReader : ITradingAccountReader
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<IReadOnlyList<AccountListItem>> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token { get; private set; }

        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            Started.TrySetResult();
            return Release.Task.WaitAsync(cancellationToken);
        }

        public Task<TradingAccountDetails?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TradingAccountDetails?>(null);
    }

    private sealed class NavigationReadTrace
    {
        private readonly Stopwatch _elapsed = Stopwatch.StartNew();
        public ConcurrentQueue<string> Events { get; } = new();

        public void Record(string phase)
        {
            ThreadPool.GetAvailableThreads(out int availableWorkers, out int availableIo);
            ThreadPool.GetMaxThreads(out int maxWorkers, out int maxIo);
            Events.Enqueue($"{_elapsed.Elapsed.TotalMilliseconds:F3} ms: {phase}; thread={Environment.CurrentManagedThreadId}, " +
                $"poolThread={Thread.CurrentThread.IsThreadPoolThread}, context={SynchronizationContext.Current?.GetType().FullName ?? "<none>"}, " +
                $"scheduler={TaskScheduler.Current.Id}, poolThreads={ThreadPool.ThreadCount}, busyWorkers={maxWorkers - availableWorkers}, " +
                $"availableWorkers={availableWorkers}, busyIo={maxIo - availableIo}, pending={ThreadPool.PendingWorkItemCount}, completed={ThreadPool.CompletedWorkItemCount}");
        }
    }
}
