using System.Collections.Concurrent;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
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

        Assert.Equal("Journal", main.PageTitle);
        Assert.Same(fixture.Journal, main.CurrentContentViewModel);
        Assert.Single(fixture.Repository.ReadCalls);

        main.NavigateCommand.Execute(NavigationDestination.Notebook);
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await fixture.Journal.LoadTask;

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
        fixture.Journal.Text = "Discard this edit.";
        fixture.Dialogs.ConfirmationResult = true;

        main.NavigateCommand.Execute(NavigationDestination.Notebook);

        Assert.Equal(NavigationDestination.Notebook, main.CurrentDestination);
        Assert.NotNull(fixture.Dialogs.ConfirmationRequest);
        Assert.Empty(repository.CreateCalls);

        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await fixture.Journal.LoadTask;
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
        fixture.Journal.Text = "Unsaved note.";

        main.NavigateCommand.Execute(NavigationDestination.Journal);

        Assert.Null(fixture.Dialogs.ConfirmationRequest);
        Assert.Single(fixture.Repository.ReadCalls);
        Assert.Equal("Unsaved note.", fixture.Journal.Text);
        Assert.Equal(NavigationDestination.Journal, main.CurrentDestination);
    }

    [Fact]
    public async Task LeavingJournalCancelsReadAndReturningIgnoresItsLateResult()
    {
        var pending = new TaskCompletionSource<DailyJournalDetails?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new NavigationJournalRepository { Read = (_, _, _) => pending.Task };
        var fixture = CreateJournalFixture(repository);
        using MainWindowViewModel main = fixture.Main;
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        Task previousLoad = fixture.Journal.LoadTask;
        await repository.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        CancellationToken previousToken = Assert.Single(repository.ReadCalls).Token;

        main.NavigateCommand.Execute(NavigationDestination.Notebook);

        Assert.True(previousToken.IsCancellationRequested);
        repository.Read = (_, _, _) => Task.FromResult<DailyJournalDetails?>(null);
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await fixture.Journal.LoadTask;
        pending.SetResult(new DailyJournalDetails(
            new DailyJournalEntry(new DateOnly(2026, 9, 9), null, "Late note.", FixedTimeProvider.FixedUtcNow),
            DailyJournalAccountState.AllAccounts, null));
        await previousLoad;

        Assert.Equal(NavigationDestination.Journal, main.CurrentDestination);
        Assert.Empty(fixture.Journal.Text);
        Assert.Equal(2, repository.ReadCalls.Count);
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
        fixture.Journal.Text = "An unactivated editor cannot block another page.";

        Assert.True(main.TryCloseWindow());
        Assert.Null(fixture.Dialogs.ConfirmationRequest);
        Assert.Empty(fixture.Repository.ReadCalls);
        Assert.Equal(NavigationDestination.Dashboard, main.CurrentDestination);
    }

    private static (MainWindowViewModel Main, JournalViewModel Journal,
        NavigationJournalRepository Repository, FakeDialogService Dialogs) CreateJournalFixture(
            NavigationJournalRepository? repository = null)
    {
        repository ??= new NavigationJournalRepository();
        var dialogs = new FakeDialogService();
        var journal = new JournalViewModel(repository, new FakeTradingAccountReader(), dialogs,
            new JournalTradeContextViewModel(new FakeTradingCalendarDayReader(), new FakeTradingAccountReader()), new FixedTimeProvider());
        return (CreateFixture(journalViewModel: journal).Main, journal, repository, dialogs);
    }

    private sealed class NavigationJournalRepository : IDailyJournalRepository
    {
        public ConcurrentQueue<(DateOnly Date, Guid? AccountId, CancellationToken Token)> ReadCalls { get; } = new();
        public ConcurrentQueue<(CreateDailyJournalCommand Command, CancellationToken Token)> CreateCalls { get; } = new();
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CreateStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<DateOnly, Guid?, CancellationToken, Task<DailyJournalDetails?>>? Read { get; set; }
        public Func<CreateDailyJournalCommand, CancellationToken, Task<DailyJournalWriteResult>>? Create { get; set; }

        public Task<DailyJournalDetails?> GetAsync(DateOnly tradingDate, Guid? tradingAccountId = null,
            CancellationToken cancellationToken = default)
        {
            var read = Read;
            ReadCalls.Enqueue((tradingDate, tradingAccountId, cancellationToken));
            ReadStarted.TrySetResult();
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
}
