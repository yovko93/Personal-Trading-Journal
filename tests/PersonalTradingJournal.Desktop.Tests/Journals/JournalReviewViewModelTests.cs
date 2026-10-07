using System.Collections.Concurrent;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalReviewViewModelTests
{
    private static readonly DateOnly Day = new(2026, 9, 9);
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly DailyReviewAnswers Ready = new("I followed my plan.", "Improve patience.", "Wait for confirmation.");

    [Fact]
    public async Task AnswersAloneCannotCompleteButCancelPreservesThemAsDraft()
    {
        var repository = new Repository();
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        const string exact = "  αβ\r\n\tПреглед 🧭\n  ";
        vm.WentWell = exact;
        vm.NeedsImprovement = " ";
        Assert.True(vm.IsDirty);
        Assert.False(vm.CanComplete);
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.True(vm.CompleteReviewCommand.CanExecute(null));
        Assert.Equal(0, vm.TradeContext.ClosedTradeCount);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(repository.Creates);
        Assert.True(vm.IsEditorOpen);
        Assert.Contains("Journal text is required", vm.JournalTextValidation);
        Assert.Equal(exact, vm.WentWell);
        await vm.SaveDraftAndCloseCommand.ExecuteAsync(null);

        var command = Assert.Single(repository.Creates);
        Assert.Equal("", command.Text);
        Assert.Equal(new DailyReviewAnswers(exact, " ", ""), command.Review);
        Assert.True(command.IsDraft);
        Assert.True(vm.IsDraft);
        Assert.False(vm.IsDirty);
        Assert.Equal(exact, vm.WentWell);
        Assert.False(vm.IsEditorOpen);
        Assert.True(vm.ShowCompactReview);
    }

    [Theory]
    [InlineData(0, "  \t\r\n")]
    [InlineData(1, "... 🧭")]
    [InlineData(2, "")]
    public async Task CompletionRejectsContentWithoutAnyMeaningfulFieldAndLeavesLocalDraft(int answer, string value)
    {
        var repository = new Repository();
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        SetAnswer(vm, answer, value);
        await vm.CompleteReviewCommand.ExecuteAsync(null);

        Assert.Contains("Journal text is required", vm.ErrorMessage);
        Assert.True(vm.IsDraft);
        Assert.Equal(value.Length > 0, vm.IsDirty);
        Assert.True(vm.CanEdit);
        Assert.False(vm.CanComplete);
        Assert.Empty(repository.Creates);
        Assert.Empty(repository.Updates);
        SetAnswer(vm, answer, "План 2");
        Assert.NotNull(vm.JournalTextValidation);
        vm.Text = "План 2";
        Assert.Null(vm.ErrorMessage);
        Assert.True(vm.CanComplete);
    }

    [Fact]
    public async Task CompletingNewZeroTradeDayAtomicallyCreatesCompletedReviewAndLocksContent()
    {
        var repository = new Repository();
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        SetReady(vm);
        Assert.True(vm.CanComplete);
        await vm.CompleteReviewCommand.ExecuteAsync(null);

        var command = Assert.Single(repository.Creates);
        Assert.False(command.IsDraft);
        Assert.Equal(Ready, command.Review);
        Assert.Equal(Day, command.TradingDate);
        Assert.Null(command.TradingAccountId);
        Assert.Equal("Journal", command.Text);
        Assert.True(vm.IsCompleted);
        Assert.True(vm.IsReadOnly);
        Assert.False(vm.IsEditorOpen);
        Assert.True(vm.ShowCompactReview);
        Assert.True(vm.CanReadContent);
        Assert.False(vm.CanEdit);
        Assert.False(vm.IsDirty);
        Assert.Contains("Completed review", vm.StatusText);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.False(vm.CompleteReviewCommand.CanExecute(null));
        Assert.True(vm.ReopenReviewCommand.CanExecute(null));
        vm.OpenEditorCommand.Execute(null);
        vm.Text = "blocked edit";
        vm.WentWell = vm.NeedsImprovement = vm.NextTradingDay = "blocked answer";
        await vm.SaveCommand.ExecuteAsync(null);
        vm.OpenEditorCommand.Execute(null);
        await vm.CompleteReviewCommand.ExecuteAsync(null);
        Assert.Equal("Journal", vm.Text);
        Assert.Equal(Ready.WentWell, vm.WentWell);
        Assert.Equal(Ready.NeedsImprovement, vm.NeedsImprovement);
        Assert.Equal(Ready.NextTradingDay, vm.NextTradingDay);
        Assert.Empty(repository.Updates);
    }

    [Fact]
    public async Task ExistingDraftCompletionUsesExactLoadedRevisionAndSelectedAccountScope()
    {
        var account = Account();
        var original = Details("optional notes", accountId: account.Id, revision: 7);
        var repository = new Repository { Journal = original };
        var vm = Create(repository, accounts: [account]);
        vm.SelectedAccount = new(account.Id, account.Name);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        SetReady(vm);
        await vm.CompleteReviewCommand.ExecuteAsync(null);

        var command = Assert.Single(repository.Updates);
        Assert.Equal(original.Entry.Id, command.JournalId);
        Assert.Equal(7, command.ExpectedRevision);
        Assert.False(command.IsDraft);
        Assert.Equal("optional notes", command.Text);
        Assert.Equal(Ready, command.Review);
        Assert.Equal(account.Id, vm.SelectedAccount.Id);
        Assert.Equal(Day.ToDateTime(TimeOnly.MinValue), vm.SelectedDate);
        Assert.Equal(8, vm.Revision);
        Assert.Empty(repository.Creates);
    }

    [Fact]
    public async Task ExplicitReopenIsLocalAndSaveWritesOneRevisionFromLoadedCompletedContent()
    {
        var original = Details("notes", draft: false, revision: 8, review: Ready);
        var repository = new Repository { Journal = original };
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        Assert.True(vm.IsCompleted);
        Assert.False(vm.CanEdit);
        await vm.ReopenReviewCommand.ExecuteAsync(null);

        Assert.Empty(repository.Updates);
        Assert.False(vm.IsDraft);
        Assert.True(vm.IsCompleted);
        Assert.True(vm.CanEdit);
        Assert.True(vm.IsEditorOpen);
        Assert.False(vm.IsReadOnly);
        Assert.False(vm.IsDirty);
        Assert.Equal(8, vm.Revision);
        vm.WentWell = "Revised reflection";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(8, repository.Updates.Last().ExpectedRevision);
        Assert.True(repository.Updates.Last().ReopenCompleted);
        Assert.Equal("Revised reflection", repository.Updates.Last().Review!.WentWell);
        Assert.False(repository.Updates.Last().IsDraft);
        Assert.Equal(9, vm.Revision);
    }

    [Fact]
    public async Task LegacyCompletedJournalCanReopenAndSaveMeaningfulFreeformWithoutAnswers()
    {
        var repository = new Repository { Journal = Details("", draft: false, review: Ready) };
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        Assert.True(vm.IsCompleted);
        Assert.True(vm.CanReadContent);
        Assert.True(vm.IsReadOnly);
        Assert.Equal(Ready.WentWell, vm.WentWell);
        await vm.ReopenReviewCommand.ExecuteAsync(null);
        Assert.Equal("", vm.Text);
        Assert.True(vm.IsCompleted);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Empty(repository.Updates); // Reopening is local; re-completing requires Journal text.
        Assert.Contains("Journal text is required", vm.JournalTextValidation);
        vm.Text = "legacy reviewed";
        await vm.CompleteReviewCommand.ExecuteAsync(null);
        Assert.Single(repository.Updates);
        Assert.True(vm.IsCompleted);
        Assert.Null(vm.ErrorMessage);
    }

    [Theory]
    [InlineData("save", DailyJournalWriteStatus.Conflict)]
    [InlineData("complete", DailyJournalWriteStatus.Conflict)]
    [InlineData("reopen", DailyJournalWriteStatus.Conflict)]
    [InlineData("complete", DailyJournalWriteStatus.AlreadyExists)]
    public async Task ConflictsPreserveAllLocalFieldsAndBlockEveryWriteUntilExplicitReload(string action, DailyJournalWriteStatus status)
    {
        var repository = new Repository
        {
            Journal = status == DailyJournalWriteStatus.AlreadyExists ? null
                : Details("saved", draft: action != "reopen", review: Ready),
            WriteBehavior = (_, _) => Task.FromResult(new DailyJournalWriteResult(status, Details("remote", draft: false, revision: 9, review: Ready))),
        };
        var dialogs = new Dialogs();
        var vm = Create(repository, dialogs);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        if (action == "reopen") { await vm.ReopenReviewCommand.ExecuteAsync(null); vm.Text = "local reopened notes"; }
        if (action != "reopen")
        {
            vm.Text = "local notes";
            vm.WentWell = "local success";
            vm.NeedsImprovement = "local improvement";
            vm.NextTradingDay = "local plan";
        }
        var expected = (vm.Text, vm.WentWell, vm.NeedsImprovement, vm.NextTradingDay);
        await Execute(vm, action);

        Assert.Equal(expected, (vm.Text, vm.WentWell, vm.NeedsImprovement, vm.NextTradingDay));
        Assert.Contains("Refresh", vm.ErrorMessage);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.False(vm.CompleteReviewCommand.CanExecute(null));
        Assert.False(vm.ReopenReviewCommand.CanExecute(null));
        Assert.Equal(action == "reopen", vm.IsCompleted);
        await vm.SaveCommand.ExecuteAsync(null);
        vm.OpenEditorCommand.Execute(null);
        await vm.CompleteReviewCommand.ExecuteAsync(null);
        await vm.ReopenReviewCommand.ExecuteAsync(null);
        Assert.Equal(1, repository.Creates.Count + repository.Updates.Count);

        repository.Journal = Details("new remote notes", draft: false, revision: 9, review: new("remote 1", "remote 2", "remote 3"));
        if (vm.IsDirty)
        {
            await vm.ReloadCommand.ExecuteAsync(null);
            Assert.Equal(expected, (vm.Text, vm.WentWell, vm.NeedsImprovement, vm.NextTradingDay));
        }
        dialogs.ConfirmResult = true;
        await vm.ReloadCommand.ExecuteAsync(null);
        Assert.Equal("new remote notes", vm.Text);
        Assert.Equal("remote 1", vm.WentWell);
        Assert.Equal("remote 2", vm.NeedsImprovement);
        Assert.Equal("remote 3", vm.NextTradingDay);
        Assert.Equal(9, vm.Revision);
        Assert.True(vm.IsCompleted);
        Assert.False(vm.IsDirty);
        Assert.True(vm.ReopenReviewCommand.CanExecute(null));
        Assert.Null(vm.ErrorMessage);
    }

    [Theory]
    [InlineData("save")]
    [InlineData("complete")]
    [InlineData("reopen")]
    public async Task AllReviewCommandsShareOneSubmissionGuard(string action)
    {
        var started = Signal();
        var result = new TaskCompletionSource<DailyJournalWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Repository
        {
            Journal = action == "reopen" ? Details("notes", draft: false, review: Ready) : null,
            WriteBehavior = (_, _) => { started.TrySetResult(); return result.Task; },
        };
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        if (action != "reopen") SetReady(vm);
        else { await vm.ReopenReviewCommand.ExecuteAsync(null); vm.Text = "edited notes"; }
        Task writing = Execute(vm, action);
        await Wait(started.Task);
        Assert.True(vm.IsSaving);
        Assert.False(vm.CanEdit);
        Assert.False(vm.TryLeave());
        DateTime? selected = vm.SelectedDate;
        vm.SelectedDate = selected!.Value.AddDays(1);
        Assert.Equal(selected, vm.SelectedDate);
        vm.OpenEditorCommand.Execute(null);
        await vm.SaveCommand.ExecuteAsync(null);
        vm.OpenEditorCommand.Execute(null);
        await vm.CompleteReviewCommand.ExecuteAsync(null);
        await vm.ReopenReviewCommand.ExecuteAsync(null);
        Assert.Equal(1, repository.Creates.Count + repository.Updates.Count);
        result.SetResult(new(DailyJournalWriteStatus.Updated, Details("notes", draft: action != "complete", revision: 2, review: Ready)));
        await writing;
        Assert.False(vm.IsSaving);
        Assert.Equal(action == "complete", vm.IsCompleted);
    }

    [Theory]
    [InlineData("save", false)]
    [InlineData("complete", false)]
    [InlineData("save", true)]
    [InlineData("complete", true)]
    public async Task FailureOrCancelledWriteKeepsEveryUnsavedAnswerAndDraft(string action, bool cancel)
    {
        var started = Signal();
        var repository = new Repository
        {
            WriteBehavior = async (_, token) =>
            {
                started.TrySetResult();
                if (!cancel) throw new IOException("private failure");
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException();
            },
        };
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        vm.Text = "local notes";
        SetReady(vm);
        Task writing = Execute(vm, action);
        await Wait(started.Task);
        if (cancel) vm.CancelOperationCommand.Execute(null);
        await writing;

        Assert.Equal("local notes", vm.Text);
        Assert.Equal(Ready.WentWell, vm.WentWell);
        Assert.Equal(Ready.NeedsImprovement, vm.NeedsImprovement);
        Assert.Equal(Ready.NextTradingDay, vm.NextTradingDay);
        Assert.True(vm.IsDirty);
        Assert.True(vm.IsDraft);
        Assert.False(vm.IsCompleted);
        Assert.True(vm.CanEdit);
        Assert.True(vm.CompleteReviewCommand.CanExecute(null));
        Assert.Contains(cancel ? "cancelled" : "could not be saved", vm.ErrorMessage);
        Assert.DoesNotContain("private failure", vm.ErrorMessage);
    }

    [Fact]
    public async Task CommittedCompletionIsAcceptedEvenIfCancellationWasRequestedBeforeResultDelivery()
    {
        var started = Signal();
        var result = new TaskCompletionSource<DailyJournalWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Repository { WriteBehavior = (_, _) => { started.TrySetResult(); return result.Task; } };
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        SetReady(vm);
        Task writing = vm.CompleteReviewCommand.ExecuteAsync(null);
        await Wait(started.Task);
        vm.CancelOperationCommand.Execute(null);
        result.SetResult(new(DailyJournalWriteStatus.Created, Details("", draft: false, review: Ready)));
        await writing;

        Assert.True(vm.IsCompleted);
        Assert.False(vm.IsDirty);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal(Ready.NextTradingDay, vm.NextTradingDay);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrCancelledDraftSaveOfReopenedCompletedKeepsLocalFields(bool cancel)
    {
        var started = Signal();
        var repository = new Repository
        {
            Journal = Details("completed notes", draft: false, revision: 4, review: Ready),
            WriteBehavior = async (_, token) =>
            {
                started.TrySetResult();
                if (!cancel) throw new IOException("private failure");
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException();
            },
        };
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        await vm.ReopenReviewCommand.ExecuteAsync(null);
        vm.Text = "edited completed notes";
        Task writing = vm.SaveDraftAndCloseCommand.ExecuteAsync(null);
        await Wait(started.Task);
        if (cancel) vm.CancelOperationCommand.Execute(null);
        await writing;

        Assert.True(vm.IsCompleted);
        Assert.False(vm.IsReadOnly);
        Assert.Equal(4, vm.Revision);
        Assert.Equal("edited completed notes", vm.Text);
        Assert.Equal(Ready.WentWell, vm.WentWell);
        Assert.Equal(Ready.NeedsImprovement, vm.NeedsImprovement);
        Assert.Equal(Ready.NextTradingDay, vm.NextTradingDay);
        Assert.True(vm.IsDirty);
        Assert.False(vm.ReopenReviewCommand.CanExecute(null));
        Assert.Contains(cancel ? "cancelled" : "could not be saved", vm.ErrorMessage);
    }

    [Fact]
    public async Task CommittedTradeContextRefreshCannotReplaceUnsavedReviewAnswersOrJournalScope()
    {
        var repository = new Repository { Journal = Details("saved notes") };
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        SetReady(vm);
        vm.TradeContext.OnDataCommitted();
        await vm.TradeContext.LoadTask;

        Assert.Equal("saved notes", vm.Text);
        Assert.Equal(Ready.WentWell, vm.WentWell);
        Assert.Equal(Ready.NeedsImprovement, vm.NeedsImprovement);
        Assert.Equal(Ready.NextTradingDay, vm.NextTradingDay);
        Assert.True(vm.IsDirty);
        Assert.Equal(Day.ToDateTime(TimeOnly.MinValue), vm.SelectedDate);
        Assert.Null(vm.SelectedAccount.Id);
        Assert.Single(repository.Reads);
        Assert.Empty(repository.Creates);
        Assert.Empty(repository.Updates);
    }

    [Fact]
    public async Task AnswerOnlyUnsavedChangesProtectDatePageAndReloadWhileHistoryFilterStaysIndependent()
    {
        var repository = new Repository();
        var dialogs = new Dialogs();
        var vm = Create(repository, dialogs);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        vm.NeedsImprovement = "precious unsaved answer";
        DateTime? selected = vm.SelectedDate;
        vm.SelectedDate = selected!.Value.AddDays(1);
        vm.SelectedAccount = new(Guid.NewGuid(), "Another account");
        Assert.False(vm.TryLeave());
        await vm.ReloadCommand.ExecuteAsync(null);

        Assert.Equal(selected, vm.SelectedDate);
        Assert.NotNull(vm.SelectedAccount.Id);
        Assert.Null(vm.EditorAccount.Id);
        Assert.Equal("precious unsaved answer", vm.NeedsImprovement);
        Assert.Equal(3, dialogs.Requests.Count);
        Assert.Single(repository.Reads);
        Assert.True(vm.IsDirty);
        dialogs.ConfirmResult = true;
        vm.SelectedDate = selected.Value.AddDays(1);
        await vm.LoadTask;
        vm.OpenEditorCommand.Execute(null);
        Assert.Equal("", vm.Text);
        Assert.Equal("", vm.WentWell);
        Assert.Equal("", vm.NeedsImprovement);
        Assert.Equal("", vm.NextTradingDay);
        Assert.False(vm.IsDirty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task OversizedAnswerStaysLocalAndBlocksBothDraftSaveAndCompletion(int answer)
    {
        var repository = new Repository();
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        SetReady(vm);
        SetAnswer(vm, answer, new string('a', DailyReviewAnswers.MaximumAnswerLength + 1));
        Assert.Contains("100,000", vm.ErrorMessage);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.False(vm.CompleteReviewCommand.CanExecute(null));
        await vm.SaveCommand.ExecuteAsync(null);
        vm.OpenEditorCommand.Execute(null);
        await vm.CompleteReviewCommand.ExecuteAsync(null);
        Assert.Empty(repository.Creates);
        SetAnswer(vm, answer, new string('a', DailyReviewAnswers.MaximumAnswerLength));
        Assert.Null(vm.ErrorMessage);
        Assert.True(vm.CanComplete);
        vm.OpenEditorCommand.Execute(null);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Single(repository.Creates);
    }

    [Fact]
    public async Task LateOldScopeCannotReplaceCurrentReviewAnswersOrCompletionState()
    {
        var started = Signal();
        var old = new TaskCompletionSource<DailyJournalDetails?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Repository
        {
            ReadBehavior = (date, _, _) => date == Day ? StartOld() : Task.FromResult<DailyJournalDetails?>(
                Details("current", date: Day.AddDays(1), review: new("current 1", "current 2", "current 3"))),
        };
        Task<DailyJournalDetails?> StartOld() { started.TrySetResult(); return old.Task; }
        var vm = Create(repository);
        Task loading = vm.ActivateAsync();
        await Wait(started.Task);
        vm.SelectedDate = Day.AddDays(1).ToDateTime(TimeOnly.MinValue);
        await vm.LoadTask;
        vm.OpenEditorCommand.Execute(null);
        vm.NextTradingDay = "unsaved new-scope plan";
        old.SetResult(Details("old completed", draft: false, review: Ready));
        await loading;

        Assert.Equal("current", vm.Text);
        Assert.Equal("current 1", vm.WentWell);
        Assert.Equal("current 2", vm.NeedsImprovement);
        Assert.Equal("unsaved new-scope plan", vm.NextTradingDay);
        Assert.True(vm.IsDraft);
        Assert.True(vm.IsDirty);
    }

    private static JournalViewModel Create(Repository repository, Dialogs? dialogs = null, IReadOnlyList<AccountListItem>? accounts = null) =>
        new(repository, new Accounts(accounts ?? []), dialogs ?? new Dialogs(),
            new JournalTradeContextViewModel(new FakeTradingCalendarDayReader(), new Accounts(accounts ?? [])), new Clock());

    private static void SetReady(JournalViewModel vm)
    {
        vm.OpenEditorCommand.Execute(null);
        if (vm.Text.Length == 0) vm.Text = "Journal";
        vm.WentWell = Ready.WentWell;
        vm.NeedsImprovement = Ready.NeedsImprovement;
        vm.NextTradingDay = Ready.NextTradingDay;
    }

    private static void SetAnswer(JournalViewModel vm, int answer, string value)
    {
        vm.OpenEditorCommand.Execute(null);
        if (answer == 0) vm.WentWell = value;
        else if (answer == 1) vm.NeedsImprovement = value;
        else vm.NextTradingDay = value;
    }

    private static Task Execute(JournalViewModel vm, string action) => action switch
    {
        "save" => vm.SaveCommand.ExecuteAsync(null),
        "complete" => vm.CompleteReviewCommand.ExecuteAsync(null),
        "reopen" => vm.SaveDraftAndCloseCommand.ExecuteAsync(null),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    private static DailyJournalDetails Details(string text, bool draft = true, long revision = 1,
        Guid? accountId = null, DateOnly? date = null, Guid? id = null, DailyReviewAnswers? review = null) =>
        new(DailyJournalEntry.Rehydrate(id ?? Guid.NewGuid(), date ?? Day, accountId, text, draft, revision, Now, Now, review),
            accountId.HasValue ? DailyJournalAccountState.Active : DailyJournalAccountState.AllAccounts,
            accountId.HasValue ? "Account" : null);

    private static AccountListItem Account() => new(Guid.NewGuid(), "Historical", TradingAccountType.Personal,
        null, null, "USD", null, false);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task Wait(Task task) => task.WaitAsync(TimeSpan.FromSeconds(10));

    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Dialogs : IDialogService
    {
        public bool ConfirmResult { get; set; }
        public List<ConfirmationDialogRequest> Requests { get; } = [];
        public bool Confirm(ConfirmationDialogRequest request) { Requests.Add(request); return ConfirmResult; }
        public void ShowInformation(InformationDialogRequest request) => throw new NotSupportedException();
    }
    private sealed class Accounts(IReadOnlyList<AccountListItem> items) : ITradingAccountReader
    {
        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(items);
        public Task<TradingAccountDetails?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Repository : IDailyJournalRepository
    {
        public Task<DailyJournalWriteResult> DeleteAsync(DeleteDailyJournalCommand command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public DailyJournalDetails? Journal { get; set; }
        public Func<object, CancellationToken, Task<DailyJournalWriteResult>>? WriteBehavior { get; init; }
        public Func<DateOnly, Guid?, CancellationToken, Task<DailyJournalDetails?>>? ReadBehavior { get; init; }
        public ConcurrentQueue<(DateOnly Date, Guid? Account)> Reads { get; } = new();
        public ConcurrentQueue<CreateDailyJournalCommand> Creates { get; } = new();
        public ConcurrentQueue<UpdateDailyJournalCommand> Updates { get; } = new();
        public Task<DailyJournalDetails?> GetAsync(DateOnly tradingDate, Guid? tradingAccountId = null, CancellationToken cancellationToken = default)
        {
            Reads.Enqueue((tradingDate, tradingAccountId));
            if (ReadBehavior is not null) return ReadBehavior(tradingDate, tradingAccountId, cancellationToken);
            return Task.FromResult(Journal?.Entry.TradingDate == tradingDate && Journal.Entry.TradingAccountId == tradingAccountId ? Journal : null);
        }
        public Task<DailyJournalWriteResult> CreateAsync(CreateDailyJournalCommand command, CancellationToken cancellationToken = default)
        {
            Creates.Enqueue(command);
            if (WriteBehavior is not null) return WriteBehavior(command, cancellationToken);
            Journal = Details(command.Text, command.IsDraft, accountId: command.TradingAccountId, date: command.TradingDate, review: command.Review);
            return Task.FromResult(new DailyJournalWriteResult(DailyJournalWriteStatus.Created, Journal));
        }
        public Task<DailyJournalWriteResult> UpdateAsync(UpdateDailyJournalCommand command, CancellationToken cancellationToken = default)
        {
            Updates.Enqueue(command);
            if (WriteBehavior is not null) return WriteBehavior(command, cancellationToken);
            var entry = Journal!.Entry;
            var changed = entry.Text != command.Text || entry.IsDraft != command.IsDraft || entry.Review != command.Review;
            Journal = Details(command.Text, command.IsDraft, command.ExpectedRevision + (changed ? 1 : 0), entry.TradingAccountId,
                entry.TradingDate, entry.Id, command.Review);
            return Task.FromResult(new DailyJournalWriteResult(changed ? DailyJournalWriteStatus.Updated : DailyJournalWriteStatus.Unchanged, Journal));
        }
        public Task<IReadOnlyList<DailyJournalRevision>> GetHistoryAsync(Guid journalId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("M14.4 does not provide history navigation.");
    }
}
