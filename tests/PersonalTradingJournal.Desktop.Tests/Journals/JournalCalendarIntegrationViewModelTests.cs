using System.Collections.Concurrent;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalCalendarIntegrationViewModelTests
{
    private static readonly DateOnly Day = new(2026, 9, 9);
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly DailyReviewAnswers Ready = new("Followed plan", "Patience", "Wait for signal");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TargetedScopeIsExplicitAndReadsOnceWithoutIntermediateScope(bool accountSpecific)
    {
        var repository = new Repository();
        Guid? accountId = accountSpecific ? Guid.NewGuid() : null;
        var account = new AccountListItem(accountId ?? Guid.NewGuid(), "Historical", TradingAccountType.Personal,
            null, null, "USD", null, false);
        var vm = Create(repository, accounts: [account]);
        DateOnly selected = new(2026, 11, 1);

        Assert.True(vm.TryOpenScope(selected, accountId, "Historical (inactive)"));
        Assert.Empty(repository.Reads);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);

        Assert.Equal((selected, accountId), Assert.Single(repository.Reads));
        Assert.Equal(accountId, vm.SelectedAccount.Id);
        Assert.Equal(selected.ToDateTime(TimeOnly.MinValue), vm.SelectedDate);
        Assert.Equal(accountSpecific ? "Historical (inactive)" : "All accounts", vm.SelectedAccount.Name);
        Assert.True(vm.SelectedAccount.IsAvailable);
        Assert.True(vm.CanEdit);
        Assert.Empty(repository.Creates);
    }

    [Fact]
    public async Task ChangedDateAndAccountUseOneDiscardDecisionAndOneExactRead()
    {
        var repository = new Repository();
        var dialogs = new Dialogs();
        var vm = Create(repository, dialogs);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        vm.Text = "Local freeform";
        vm.WentWell = "Local answer";
        Guid requestedAccount = Guid.NewGuid();
        DateOnly requestedDate = Day.AddDays(1);

        Assert.False(vm.TryOpenScope(requestedDate, requestedAccount, "Other"));
        Assert.Equal(Day.ToDateTime(TimeOnly.MinValue), vm.SelectedDate);
        Assert.Null(vm.SelectedAccount.Id);
        Assert.Equal("Local freeform", vm.Text);
        Assert.Equal("Local answer", vm.WentWell);
        Assert.Single(repository.Reads);
        Assert.Single(dialogs.Requests);

        dialogs.Result = true;
        Assert.True(vm.TryOpenScope(requestedDate, requestedAccount, "Other"));
        await vm.LoadTask;
        vm.OpenEditorCommand.Execute(null);
        Assert.Equal(2, dialogs.Requests.Count);
        Assert.Equal(2, repository.Reads.Count);
        Assert.Equal((requestedDate, (Guid?)requestedAccount), repository.Reads.Last());
        Assert.Equal(requestedAccount, vm.SelectedAccount.Id);
        Assert.False(vm.SelectedAccount.IsAvailable);
        Assert.Contains("unavailable", vm.ScopeMessage);
        Assert.Empty(repository.Creates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameScopeCalendarReentryKeepsInactiveDirtyDraftAndConflict(bool conflict)
    {
        var repository = new Repository { Journal = Details("saved") };
        if (conflict) repository.Write = (_, _) => Task.FromResult(new DailyJournalWriteResult(DailyJournalWriteStatus.Conflict, Details("remote", revision: 2)));
        var dialogs = new Dialogs();
        var vm = Create(repository, dialogs);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        vm.Text = "Precious local draft";
        vm.NextTradingDay = "Precious local plan";
        if (conflict) await vm.SaveCommand.ExecuteAsync(null);
        string? error = vm.ErrorMessage;
        vm.Deactivate();

        Assert.True(vm.TryOpenScope(Day, null, "Not an implicit account"));
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);

        Assert.Single(repository.Reads);
        Assert.Empty(dialogs.Requests);
        Assert.Equal("Precious local draft", vm.Text);
        Assert.Equal("Precious local plan", vm.NextTradingDay);
        Assert.True(vm.IsDirty);
        Assert.True(vm.CanEdit);
        Assert.Equal(error, vm.ErrorMessage);
        Assert.Equal(!conflict, vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task SameScopeCleanCompletedConflictStillRequiresExplicitReload()
    {
        var repository = new Repository
        {
            Journal = Details("saved", draft: false, review: Ready),
            Write = (_, _) => Task.FromResult(new DailyJournalWriteResult(DailyJournalWriteStatus.Conflict, Details("remote", revision: 2))),
        };
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        await vm.ReopenReviewCommand.ExecuteAsync(null);
        Assert.False(vm.IsDirty);
        Assert.False(vm.ReopenReviewCommand.CanExecute(null));
        vm.Deactivate();
        Assert.True(vm.TryOpenScope(Day, null, "All accounts"));
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);

        Assert.Single(repository.Reads);
        Assert.True(vm.IsCompleted);
        Assert.Equal("saved", vm.Text);
        Assert.Contains("changed elsewhere", vm.ErrorMessage);
        Assert.False(vm.ReopenReviewCommand.CanExecute(null));
    }

    [Fact]
    public async Task ScopeOpeningCannotReplaceInProgressSaveOrUseEmptyAccountId()
    {
        var started = Signal();
        var pending = new TaskCompletionSource<DailyJournalWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Repository { Write = (_, _) => { started.TrySetResult(); return pending.Task; } };
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        vm.Text = "Saving";
        Task saving = vm.SaveCommand.ExecuteAsync(null);
        await Wait(started.Task);
        Assert.False(vm.TryOpenScope(Day, null, "All accounts"));
        Assert.False(vm.TryOpenScope(Day.AddDays(1), Guid.NewGuid(), "Other"));
        pending.SetResult(new(DailyJournalWriteStatus.Created, Details("Saving")));
        await saving;
        Assert.False(vm.TryOpenScope(Day, Guid.Empty, "Invalid"));
        Assert.Equal(Day.ToDateTime(TimeOnly.MinValue), vm.SelectedDate);
        Assert.Null(vm.SelectedAccount.Id);
    }

    [Fact]
    public async Task SavingCompletingAndReopeningNotifyOnceAfterEachRealRevisionOnly()
    {
        var repository = new Repository();
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        int notifications = 0;
        vm.JournalDataCommitted += (_, _) => { notifications++; Assert.False(vm.IsSaving); Assert.False(vm.IsDirty); };
        vm.WentWell = Ready.WentWell;
        vm.NeedsImprovement = Ready.NeedsImprovement;
        vm.NextTradingDay = Ready.NextTradingDay;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(1, notifications);
        vm.OpenEditorCommand.Execute(null);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(1, notifications);
        vm.OpenEditorCommand.Execute(null);
        await vm.CompleteReviewCommand.ExecuteAsync(null);
        Assert.Equal(2, notifications);
        Assert.True(vm.IsCompleted);
        await vm.ReopenReviewCommand.ExecuteAsync(null);
        Assert.Equal(3, notifications);
        Assert.True(vm.IsDraft);
    }

    [Theory]
    [InlineData(DailyJournalWriteStatus.AlreadyExists)]
    [InlineData(DailyJournalWriteStatus.Conflict)]
    [InlineData(DailyJournalWriteStatus.NotFound)]
    [InlineData(DailyJournalWriteStatus.AccountUnavailable)]
    public async Task RejectedResultsNeverNotifyOrEraseLocalDraft(DailyJournalWriteStatus status)
    {
        var repository = new Repository { Write = (_, _) => Task.FromResult(new DailyJournalWriteResult(status, Details("remote"))) };
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        vm.Text = "Local";
        int notifications = 0;
        vm.JournalDataCommitted += (_, _) => notifications++;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0, notifications);
        Assert.Equal("Local", vm.Text);
        Assert.True(vm.IsDirty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RolledBackCancellationAndFailureNeverNotify(bool cancel)
    {
        var started = Signal();
        var pending = new TaskCompletionSource<DailyJournalWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Repository { Write = (_, _) => { started.TrySetResult(); return pending.Task; } };
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        vm.Text = "Retained";
        int notifications = 0;
        vm.JournalDataCommitted += (_, _) => notifications++;
        Task saving = vm.SaveCommand.ExecuteAsync(null);
        await Wait(started.Task);
        if (cancel) { vm.CancelOperationCommand.Execute(null); pending.SetCanceled(); }
        else pending.SetException(new IOException("transaction rolled back"));
        await saving;
        Assert.Equal(0, notifications);
        Assert.Equal("Retained", vm.Text);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task CommitAfterCancellationStillPublishesAndNotifiesExactlyOnce()
    {
        var started = Signal();
        var pending = new TaskCompletionSource<DailyJournalWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Repository { Write = (_, _) => { started.TrySetResult(); return pending.Task; } };
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.OpenEditorCommand.Execute(null);
        vm.Text = "Committed";
        int notifications = 0;
        vm.JournalDataCommitted += (_, _) => notifications++;
        Task saving = vm.SaveCommand.ExecuteAsync(null);
        await Wait(started.Task);
        vm.CancelOperationCommand.Execute(null);
        pending.SetResult(new(DailyJournalWriteStatus.Created, Details("Committed")));
        await saving;
        Assert.Equal(1, notifications);
        Assert.True(vm.IsExisting);
        Assert.False(vm.IsDirty);
        Assert.Null(vm.ErrorMessage);
    }

    private static JournalViewModel Create(Repository repository, Dialogs? dialogs = null, IReadOnlyList<AccountListItem>? accounts = null)
    {
        var reader = new Accounts(accounts ?? []);
        return new(repository, reader, dialogs ?? new Dialogs(), new(new FakeTradingCalendarDayReader(), reader), new Clock());
    }
    private static DailyJournalDetails Details(string text, bool draft = true, long revision = 1,
        DateOnly? date = null, Guid? accountId = null, Guid? id = null, DailyReviewAnswers? review = null) =>
        new(DailyJournalEntry.Rehydrate(id ?? Guid.NewGuid(), date ?? Day, accountId, text, draft, revision, Now, Now, review),
            accountId.HasValue ? DailyJournalAccountState.Active : DailyJournalAccountState.AllAccounts, accountId.HasValue ? "Account" : null);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task Wait(Task task) => task.WaitAsync(TimeSpan.FromSeconds(10));
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Dialogs : IDialogService
    {
        public bool Result { get; set; }
        public List<ConfirmationDialogRequest> Requests { get; } = [];
        public bool Confirm(ConfirmationDialogRequest request) { Requests.Add(request); return Result; }
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
        public Func<object, CancellationToken, Task<DailyJournalWriteResult>>? Write { get; set; }
        public ConcurrentQueue<(DateOnly Date, Guid? AccountId)> Reads { get; } = new();
        public ConcurrentQueue<CreateDailyJournalCommand> Creates { get; } = new();
        public Task<DailyJournalDetails?> GetAsync(DateOnly date, Guid? accountId = null, CancellationToken cancellationToken = default)
        {
            Reads.Enqueue((date, accountId));
            return Task.FromResult(Journal?.Entry.TradingDate == date && Journal.Entry.TradingAccountId == accountId ? Journal : null);
        }
        public Task<DailyJournalWriteResult> CreateAsync(CreateDailyJournalCommand command, CancellationToken cancellationToken = default)
        {
            Creates.Enqueue(command);
            if (Write is not null) return Write(command, cancellationToken);
            Journal = Details(command.Text, command.IsDraft, date: command.TradingDate, accountId: command.TradingAccountId, review: command.Review);
            return Task.FromResult(new DailyJournalWriteResult(DailyJournalWriteStatus.Created, Journal));
        }
        public Task<DailyJournalWriteResult> UpdateAsync(UpdateDailyJournalCommand command, CancellationToken cancellationToken = default)
        {
            if (Write is not null) return Write(command, cancellationToken);
            var previous = Journal!.Entry;
            bool changed = previous.Text != command.Text || previous.IsDraft != command.IsDraft || previous.Review != command.Review;
            Journal = Details(command.Text, command.IsDraft, command.ExpectedRevision + (changed ? 1 : 0), previous.TradingDate,
                previous.TradingAccountId, previous.Id, command.Review);
            return Task.FromResult(new DailyJournalWriteResult(changed ? DailyJournalWriteStatus.Updated : DailyJournalWriteStatus.Unchanged, Journal));
        }
        public Task<IReadOnlyList<DailyJournalRevision>> GetHistoryAsync(Guid journalId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Calendar integration must not load history.");
    }
}
