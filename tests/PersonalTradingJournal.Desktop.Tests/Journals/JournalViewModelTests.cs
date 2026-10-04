using System.Collections.Concurrent;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalViewModelTests
{
    private static readonly DateOnly Day = new(2026, 9, 9);
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("2026-10-01T01:00:00Z", "2026-09-30")]
    [InlineData("2026-03-08T04:59:00Z", "2026-03-07")]
    [InlineData("2026-03-08T05:00:00Z", "2026-03-08")]
    [InlineData("2026-03-08T07:00:00Z", "2026-03-08")]
    [InlineData("2026-11-01T03:59:00Z", "2026-10-31")]
    [InlineData("2026-11-01T04:00:00Z", "2026-11-01")]
    [InlineData("2026-11-01T06:00:00Z", "2026-11-01")]
    public async Task DefaultDateUsesNewYorkCalendarIncludingDst(string instant, string date)
    {
        var repository = new Repository();
        var vm = new JournalViewModel(repository, new AccountsReader(), new Dialogs(), new Clock(DateTimeOffset.Parse(instant)));

        Assert.Equal(DateTime.Parse(date), vm.SelectedDate);
        Assert.Null(vm.SelectedAccount.Id);
        Assert.Empty(repository.Reads);
        Assert.False(vm.CanEdit);
        await vm.ActivateAsync();

        Assert.Equal(DateOnly.Parse(date), Assert.Single(repository.Reads).Date);
        Assert.Null(Assert.Single(repository.Reads).AccountId);
        Assert.Contains("separate", vm.ScopeMessage);
    }

    [Theory]
    [InlineData(2026, 3, 8)]
    [InlineData(2026, 11, 1)]
    public async Task SelectedDstDateIsPassedAsAnExplicitDate(int year, int month, int day)
    {
        var repository = new Repository();
        var vm = Create(repository);
        vm.SelectedDate = new DateTime(year, month, day, 20, 31, 0, DateTimeKind.Local);
        await vm.ActivateAsync();
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(year, month, day), Assert.Single(repository.Reads).Date);
        Assert.Equal(new DateOnly(year, month, day), Assert.Single(repository.Creates).TradingDate);
        Assert.Equal(DateTimeKind.Unspecified, vm.SelectedDate!.Value.Kind);
    }

    [Fact]
    public async Task MissingEntryRemainsUnsavedUntilExplicitSaveIncludingEmptyText()
    {
        var repository = new Repository();
        var vm = Create(repository);
        await vm.ActivateAsync();

        Assert.False(vm.IsExisting);
        Assert.True(vm.IsDraft);
        Assert.False(vm.IsDirty);
        Assert.True(vm.CanEdit);
        Assert.Equal("New draft — not saved", vm.StatusText);
        Assert.Empty(repository.Creates);
        await vm.SaveCommand.ExecuteAsync(null);

        CreateDailyJournalCommand command = Assert.Single(repository.Creates);
        Assert.Equal("", command.Text);
        Assert.True(command.IsDraft);
        Assert.True(vm.IsExisting);
        Assert.Equal(1L, vm.Revision);
        Assert.False(vm.IsDirty);
        Assert.Contains("Saved draft", vm.StatusText);
    }

    [Fact]
    public async Task TextIsExactAndEditingDoesNotAutosave()
    {
        const string exact = "  αβ\r\n\tПреглед 🧭\n\r\n  ";
        var repository = new Repository();
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.Text = "first draft";
        vm.Text = exact;

        Assert.True(vm.IsDirty);
        Assert.Equal("Unsaved changes", vm.StatusText);
        Assert.Empty(repository.Creates);
        Assert.Empty(repository.Updates);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(exact, Assert.Single(repository.Creates).Text);
        Assert.Equal(exact, vm.Text);
        Assert.False(vm.IsDirty);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExistingSaveUsesLoadedIdentityRevisionAndPreservesDraftFlag(bool draft)
    {
        var repository = new Repository { Journal = Details("original", draft: draft, revision: 7) };
        Guid id = repository.Journal.Entry.Id;
        var vm = Create(repository);
        await vm.ActivateAsync();
        Assert.Equal(draft, vm.IsDraft);
        vm.Text = "revised";
        await vm.SaveCommand.ExecuteAsync(null);

        UpdateDailyJournalCommand command = Assert.Single(repository.Updates);
        Assert.Equal(id, command.JournalId);
        Assert.Equal(7L, command.ExpectedRevision);
        Assert.Equal(draft, command.IsDraft);
        Assert.Equal("revised", command.Text);
        Assert.Equal(8L, vm.Revision);
        Assert.False(vm.IsDirty);
        Assert.Equal(draft, vm.IsDraft);
        Assert.Contains(draft ? "Saved draft" : "Saved journal", vm.StatusText);
        Assert.Empty(repository.Creates);
    }

    [Fact]
    public async Task UnchangedSaveKeepsRevisionAndReportsNoChanges()
    {
        var repository = new Repository { Journal = Details("unchanged", revision: 3) };
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.Text = "temporary";
        vm.Text = "unchanged";
        Assert.False(vm.IsDirty);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(3L, vm.Revision);
        Assert.False(vm.IsDirty);
        Assert.Equal("No changes to save.", vm.StatusText);
    }

    [Fact]
    public async Task OversizedTextStaysInEditorAndCannotBeSavedUntilShortened()
    {
        var repository = new Repository();
        var vm = Create(repository);
        await vm.ActivateAsync();
        string tooLong = new('x', DailyJournalEntry.MaximumTextLength + 1);
        vm.Text = tooLong;

        Assert.Equal(tooLong, vm.Text);
        Assert.Contains("100,000", vm.ErrorMessage);
        Assert.False(vm.SaveCommand.CanExecute(null));
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Empty(repository.Creates);

        vm.Text = tooLong[..DailyJournalEntry.MaximumTextLength];
        Assert.Null(vm.ErrorMessage);
        Assert.True(vm.SaveCommand.CanExecute(null));
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(DailyJournalEntry.MaximumTextLength, Assert.Single(repository.Creates).Text.Length);
    }

    [Fact]
    public async Task InvalidDateInputBlocksSavingWithoutLosingAppliedDateOrText()
    {
        var repository = new Repository();
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.Text = "keep this";
        DateTime? date = vm.SelectedDate;
        vm.HasDateInputError = true;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(date, vm.SelectedDate);
        Assert.Equal("keep this", vm.Text);
        Assert.Contains("valid journal date", vm.ErrorMessage);
        Assert.True(vm.CanChangeScope);
        Assert.Empty(repository.Creates);
        vm.HasDateInputError = false;
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task SaveIsSingleFlightAndBlocksEditingScopeChangesAndLeaving()
    {
        var started = Signal();
        var completion = new TaskCompletionSource<DailyJournalWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Repository
        {
            CreateBehavior = (_, _) => { started.TrySetResult(); return completion.Task; },
        };
        var dialogs = new Dialogs { ConfirmResult = true };
        var vm = Create(repository, dialogs: dialogs);
        await vm.ActivateAsync();
        vm.Text = "pending save";
        DateTime? selected = vm.SelectedDate;
        Task saving = vm.SaveCommand.ExecuteAsync(null);
        await Wait(started.Task);

        Assert.True(vm.IsSaving);
        Assert.True(vm.IsBusy);
        Assert.False(vm.CanEdit);
        Assert.False(vm.CanChangeScope);
        Assert.False(vm.ReloadCommand.CanExecute(null));
        Assert.False(vm.TryLeave());
        vm.SelectedDate = selected!.Value.AddDays(1);
        vm.SelectedAccount = new(Guid.NewGuid(), "Another");
        vm.Text = "must not replace captured text";
        await vm.SaveCommand.ExecuteAsync(null);
        await vm.ReloadCommand.ExecuteAsync(null);
        Assert.Equal(selected, vm.SelectedDate);
        Assert.Null(vm.SelectedAccount.Id);
        Assert.Equal("pending save", vm.Text);
        Assert.Single(repository.Creates);
        Assert.Single(repository.Reads);
        Assert.Empty(dialogs.Requests);

        completion.SetResult(new(DailyJournalWriteStatus.Created, Details("pending save")));
        await saving;
        Assert.False(vm.IsBusy);
        Assert.True(vm.CanEdit);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task CancelledSavePreservesDraftAndAllowsRetry()
    {
        var started = Signal();
        var repository = new Repository
        {
            CreateBehavior = async (_, token) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("Unreachable");
            },
        };
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.Text = "cancelled draft";
        Task saving = vm.SaveCommand.ExecuteAsync(null);
        await Wait(started.Task);
        vm.CancelOperationCommand.Execute(null);
        await saving;

        Assert.Equal("cancelled draft", vm.Text);
        Assert.True(vm.IsDirty);
        Assert.False(vm.IsExisting);
        Assert.False(vm.IsSaving);
        Assert.Contains("cancelled", vm.ErrorMessage);
        Assert.True(vm.SaveCommand.CanExecute(null));
        repository.CreateBehavior = null;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.IsExisting);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task CommittedSaveResultIsAcceptedEvenWhenCancellationWasRequested()
    {
        var started = Signal();
        var completion = new TaskCompletionSource<DailyJournalWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        var repository = new Repository
        {
            CreateBehavior = (_, token) => { observed = token; started.TrySetResult(); return completion.Task; },
        };
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.Text = "committed";
        Task saving = vm.SaveCommand.ExecuteAsync(null);
        await Wait(started.Task);
        vm.CancelOperationCommand.Execute(null);
        Assert.True(observed.IsCancellationRequested);
        Assert.True(vm.IsSaving);
        Assert.False(vm.TryLeave());
        completion.SetResult(new(DailyJournalWriteStatus.Created, Details("committed")));
        await saving;

        Assert.Equal("committed", vm.Text);
        Assert.True(vm.IsExisting);
        Assert.False(vm.IsDirty);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal(1L, vm.Revision);
    }

    [Fact]
    public async Task UnexpectedSaveFailurePreservesTextAndDoesNotExposeExceptionContents()
    {
        var repository = new Repository
        {
            CreateBehavior = (_, _) => throw new IOException("private journal text in a database failure"),
        };
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.Text = "my draft";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("my draft", vm.Text);
        Assert.True(vm.IsDirty);
        Assert.False(vm.IsExisting);
        Assert.Contains("could not be saved", vm.ErrorMessage);
        Assert.DoesNotContain("private journal", vm.ErrorMessage);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(DailyJournalWriteStatus.Conflict)]
    [InlineData(DailyJournalWriteStatus.AlreadyExists)]
    [InlineData(DailyJournalWriteStatus.NotFound)]
    public async Task ConcurrencyOutcomePreservesDraftAndRequiresExplicitConfirmedReload(DailyJournalWriteStatus status)
    {
        DailyJournalDetails remote = Details("remote text", revision: 9);
        var repository = new Repository
        {
            Journal = status == DailyJournalWriteStatus.AlreadyExists ? null : Details("loaded", revision: 4),
            CreateBehavior = (_, _) => Task.FromResult(new DailyJournalWriteResult(status, remote)),
            UpdateBehavior = (_, _) => Task.FromResult(new DailyJournalWriteResult(status, remote)),
        };
        var dialogs = new Dialogs();
        var vm = Create(repository, dialogs: dialogs);
        await vm.ActivateAsync();
        vm.Text = "local work";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("local work", vm.Text);
        Assert.True(vm.IsDirty);
        Assert.Contains("Reload", vm.ErrorMessage);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.True(vm.CanEdit);
        vm.Text = "more local work";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(1, repository.Creates.Count + repository.Updates.Count);
        repository.Journal = remote;
        await vm.ReloadCommand.ExecuteAsync(null);
        Assert.Single(repository.Reads);
        Assert.Equal("more local work", vm.Text);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.Equal("Keep editing", Assert.Single(dialogs.Requests).CancelButtonText);

        dialogs.ConfirmResult = true;
        await vm.ReloadCommand.ExecuteAsync(null);
        Assert.Equal(2, repository.Reads.Count);
        Assert.Equal("remote text", vm.Text);
        Assert.Equal(9L, vm.Revision);
        Assert.False(vm.IsDirty);
        Assert.Null(vm.ErrorMessage);
        Assert.True(vm.SaveCommand.CanExecute(null));
        repository.UpdateBehavior = null;
        vm.Text = "reviewed edit";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(9L, repository.Updates.Last().ExpectedRevision);
    }

    [Fact]
    public async Task FailedReloadDoesNotDiscardDirtyConflictTextOrRemoveSaveBlock()
    {
        var repository = new Repository
        {
            Journal = Details("old"),
            UpdateBehavior = (_, _) => Task.FromResult(new DailyJournalWriteResult(DailyJournalWriteStatus.Conflict, Details("new"))),
        };
        var vm = Create(repository, dialogs: new Dialogs { ConfirmResult = true });
        await vm.ActivateAsync();
        vm.Text = "precious local work";
        await vm.SaveCommand.ExecuteAsync(null);
        repository.ReadBehavior = (_, _, _) => throw new IOException("unavailable");
        await vm.ReloadCommand.ExecuteAsync(null);

        Assert.Equal("precious local work", vm.Text);
        Assert.True(vm.IsDirty);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.Contains("could not be loaded", vm.ErrorMessage);
    }

    [Fact]
    public async Task DirtyDateAndAccountChangesAndPageLeaveCanBeVetoed()
    {
        var repository = new Repository();
        var dialogs = new Dialogs();
        var vm = Create(repository, dialogs: dialogs);
        await vm.ActivateAsync();
        vm.Text = "keep editing";
        var changed = new List<string?>();
        vm.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        DateTime? originalDate = vm.SelectedDate;
        vm.SelectedDate = originalDate!.Value.AddDays(1);
        vm.SelectedAccount = new(Guid.NewGuid(), "Other");

        Assert.False(vm.TryLeave());
        Assert.Equal(originalDate, vm.SelectedDate);
        Assert.Null(vm.SelectedAccount.Id);
        Assert.Equal("keep editing", vm.Text);
        Assert.True(vm.IsDirty);
        Assert.Single(repository.Reads);
        Assert.Equal(3, dialogs.Requests.Count);
        Assert.Contains(nameof(vm.SelectedDate), changed);
        Assert.Contains(nameof(vm.SelectedAccount), changed);
        Assert.All(dialogs.Requests, request =>
        {
            Assert.Equal("Discard changes", request.ConfirmButtonText);
            Assert.Equal("Keep editing", request.CancelButtonText);
        });
    }

    [Fact]
    public async Task ApprovedScopeChangeDiscardsOnlyLocalWorkAndLoadsExactNewScope()
    {
        var repository = new Repository { Journal = Details("saved") };
        var dialogs = new Dialogs { ConfirmResult = true };
        var vm = Create(repository, dialogs: dialogs);
        await vm.ActivateAsync();
        vm.Text = "discard me";
        vm.SelectedDate = Day.AddDays(1).ToDateTime(TimeOnly.MinValue);
        await vm.LoadTask;

        Assert.Equal(Day.AddDays(1), repository.Reads.Last().Date);
        Assert.Equal("", vm.Text);
        Assert.False(vm.IsDirty);
        Assert.False(vm.IsExisting);
        Assert.Empty(repository.Creates);
        Assert.Empty(repository.Updates);
        Assert.Equal("saved", repository.Journal!.Entry.Text);
    }

    [Fact]
    public async Task ApprovedLeaveThenReentryReadsFreshSavedText()
    {
        var repository = new Repository { Journal = Details("saved") };
        var dialogs = new Dialogs { ConfirmResult = true };
        var vm = Create(repository, dialogs: dialogs);
        await vm.ActivateAsync();
        vm.Text = "discard";
        Assert.True(vm.TryLeave());
        vm.Deactivate();
        Assert.False(vm.CanEdit);
        repository.Journal = Details("fresh from storage", revision: 2);
        await vm.ActivateAsync();

        Assert.Equal("fresh from storage", vm.Text);
        Assert.False(vm.IsDirty);
        Assert.Equal(2, repository.Reads.Count);
    }

    [Fact]
    public async Task RepeatedActivationCannotOverwriteDirtyVisibleEditor()
    {
        var repository = new Repository();
        var vm = Create(repository);
        await vm.ActivateAsync();
        vm.Text = "keep this";
        await vm.ActivateAsync();

        Assert.Equal("keep this", vm.Text);
        Assert.Single(repository.Reads);
    }

    [Fact]
    public async Task InactiveAccountsAreAvailableAndSaveUsesTheirExactScope()
    {
        AccountListItem inactive = Account("Historical", active: false);
        AccountListItem active = Account("Active");
        var accounts = new AccountsReader { Items = [inactive, active] };
        var repository = new Repository();
        var vm = Create(repository, accounts);
        await vm.ActivateAsync();
        JournalAccountOption option = Assert.Single(vm.Accounts, a => a.Id == inactive.Id);
        Assert.Contains("inactive", option.Name);
        Assert.True(option.IsAvailable);
        vm.SelectedAccount = option;
        await vm.LoadTask;
        vm.Text = "historical notes";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(inactive.Id, repository.Reads.Last().AccountId);
        Assert.Equal(inactive.Id, Assert.Single(repository.Creates).TradingAccountId);
        Assert.True(vm.CanEdit);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task MissingAccountRetainsScopeAndOrphanedTextWithoutAllAccountsFallback()
    {
        Guid accountId = Guid.NewGuid();
        var repository = new Repository { Journal = Details("orphaned notes", accountId: accountId) with
        {
            AccountName = "Historical", AccountState = DailyJournalAccountState.Unavailable,
        } };
        var vm = Create(repository);
        vm.SelectedAccount = new(accountId, "Historical");
        await vm.ActivateAsync();

        Assert.Equal(accountId, vm.SelectedAccount.Id);
        Assert.False(vm.SelectedAccount.IsAvailable);
        Assert.Contains("unavailable", vm.SelectedAccount.Name);
        Assert.Equal("orphaned notes", vm.Text);
        Assert.True(vm.IsExisting);
        Assert.False(vm.CanEdit);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.Contains("no longer available", vm.ErrorMessage);
        Assert.Equal(accountId, Assert.Single(repository.Reads).AccountId);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Empty(repository.Updates);
    }

    [Fact]
    public async Task AccountRefreshRetainsMissingSelectionAndCanRecoverSameAccount()
    {
        AccountListItem account = Account("First name");
        var accounts = new AccountsReader { Items = [account] };
        var repository = new Repository();
        var vm = Create(repository, accounts);
        await vm.ActivateAsync();
        vm.SelectedAccount = vm.Accounts.Single(a => a.Id == account.Id);
        await vm.LoadTask;
        accounts.Items = [];
        await vm.ReloadCommand.ExecuteAsync(null);

        Assert.Equal(account.Id, vm.SelectedAccount.Id);
        Assert.False(vm.SelectedAccount.IsAvailable);
        Assert.False(vm.CanEdit);
        accounts.Items = [account with { Name = "Renamed", IsActive = false }];
        await vm.ReloadCommand.ExecuteAsync(null);
        Assert.Equal(account.Id, vm.SelectedAccount.Id);
        Assert.Equal("Renamed (inactive)", vm.SelectedAccount.Name);
        Assert.True(vm.SelectedAccount.IsAvailable);
        Assert.True(vm.CanEdit);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task NullAccountSelectionCannotWidenAnAccountScope()
    {
        AccountListItem account = Account("Specific");
        var vm = Create(accounts: new AccountsReader { Items = [account] });
        await vm.ActivateAsync();
        vm.SelectedAccount = vm.Accounts.Single(a => a.Id == account.Id);
        await vm.LoadTask;
        vm.SelectedAccount = null!;
        Assert.Equal(account.Id, vm.SelectedAccount.Id);
    }

    [Fact]
    public async Task AccountUnavailableSavePreservesLocalDraftAndBlocksRetryUntilReload()
    {
        AccountListItem account = Account("Removed while editing");
        var repository = new Repository
        {
            CreateBehavior = (_, _) => Task.FromResult(new DailyJournalWriteResult(DailyJournalWriteStatus.AccountUnavailable, null)),
        };
        var vm = Create(repository, new AccountsReader { Items = [account] });
        vm.SelectedAccount = new(account.Id, account.Name);
        await vm.ActivateAsync();
        vm.Text = "retained";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(account.Id, vm.SelectedAccount.Id);
        Assert.Equal("retained", vm.Text);
        Assert.True(vm.IsDirty);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.Contains("account", vm.ErrorMessage);
    }

    [Fact]
    public async Task RapidSelectionDiscardsLateOldReadAndDoesNotAcceptTypingWhileLoading()
    {
        var firstStarted = Signal();
        var secondStarted = Signal();
        var first = new TaskCompletionSource<DailyJournalDetails?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<DailyJournalDetails?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Repository
        {
            ReadBehavior = (date, _, _) =>
            {
                if (date == Day) { firstStarted.TrySetResult(); return first.Task; }
                secondStarted.TrySetResult(); return second.Task;
            },
        };
        var vm = Create(repository);
        Task firstRead = vm.ActivateAsync();
        await Wait(firstStarted.Task);
        vm.SelectedDate = Day.AddDays(1).ToDateTime(TimeOnly.MinValue);
        Task secondRead = vm.LoadTask;
        await Wait(secondStarted.Task);
        Assert.True(vm.IsLoading);
        Assert.False(vm.CanEdit);
        vm.Text = "cannot edit a pending scope";
        Assert.Equal("", vm.Text);
        second.SetResult(Details("new date", date: Day.AddDays(1)));
        await secondRead;
        Assert.Equal("new date", vm.Text);
        first.SetResult(Details("late old date"));
        await firstRead;

        Assert.Equal("new date", vm.Text);
        Assert.False(vm.IsLoading);
        Assert.Equal(Day.AddDays(1).ToDateTime(TimeOnly.MinValue), vm.SelectedDate);
        Assert.False(vm.IsDirty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelOrDeactivatePreventsLateReadFromPublishing(bool deactivate)
    {
        var started = Signal();
        var result = new TaskCompletionSource<DailyJournalDetails?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Repository { ReadBehavior = (_, _, _) => { started.TrySetResult(); return result.Task; } };
        var vm = Create(repository);
        Task loading = vm.ActivateAsync();
        await Wait(started.Task);
        if (deactivate) vm.Deactivate();
        else vm.CancelOperationCommand.Execute(null);
        Assert.False(vm.IsLoading);
        Assert.False(vm.CanEdit);
        result.SetResult(Details("late data"));
        await loading;

        Assert.Equal("", vm.Text);
        Assert.False(vm.IsExisting);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task InitialReadFailureIsVisibleAndExplicitReloadCanRecover()
    {
        var repository = new Repository { ReadBehavior = (_, _, _) => throw new IOException("sensitive details") };
        var vm = Create(repository);
        await vm.ActivateAsync();

        Assert.Contains("could not be loaded", vm.ErrorMessage);
        Assert.DoesNotContain("sensitive", vm.ErrorMessage);
        Assert.False(vm.CanEdit);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.False(vm.IsLoading);
        repository.ReadBehavior = null;
        await vm.ReloadCommand.ExecuteAsync(null);
        Assert.Null(vm.ErrorMessage);
        Assert.True(vm.CanEdit);
    }

    [Fact]
    public async Task ClearedDateRequiresSelectionAndNeverReadsOrWritesAnImplicitDate()
    {
        var repository = new Repository();
        var vm = Create(repository);
        vm.SelectedDate = null;
        await vm.ActivateAsync();
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(vm.CanEdit);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.Equal("Choose a journal date.", vm.StatusText);
        Assert.Empty(repository.Reads);
        Assert.Empty(repository.Creates);
    }

    private static JournalViewModel Create(Repository? repository = null, AccountsReader? accounts = null, Dialogs? dialogs = null) =>
        new(repository ?? new Repository(), accounts ?? new AccountsReader(), dialogs ?? new Dialogs(), new Clock(Now));

    private static DailyJournalDetails Details(string text, bool draft = true, long revision = 1,
        Guid? accountId = null, DateOnly? date = null, Guid? id = null) =>
        new(DailyJournalEntry.Rehydrate(id ?? Guid.NewGuid(), date ?? Day, accountId, text, draft, revision, Now, Now),
            accountId.HasValue ? DailyJournalAccountState.Active : DailyJournalAccountState.AllAccounts,
            accountId.HasValue ? "Account" : null);

    private static AccountListItem Account(string name, bool active = true) =>
        new(Guid.NewGuid(), name, TradingAccountType.Personal, null, null, "USD", null, active);

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task Wait(Task task) => task.WaitAsync(TimeSpan.FromSeconds(10));

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Dialogs : IDialogService
    {
        public bool ConfirmResult { get; set; }
        public List<ConfirmationDialogRequest> Requests { get; } = [];
        public bool Confirm(ConfirmationDialogRequest request) { Requests.Add(request); return ConfirmResult; }
        public void ShowInformation(InformationDialogRequest request) => throw new NotSupportedException();
    }

    private sealed class AccountsReader : ITradingAccountReader
    {
        public IReadOnlyList<AccountListItem> Items { get; set; } = [];
        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Items);
        public Task<TradingAccountDetails?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class Repository : IDailyJournalRepository
    {
        public DailyJournalDetails? Journal { get; set; }
        public Func<DateOnly, Guid?, CancellationToken, Task<DailyJournalDetails?>>? ReadBehavior { get; set; }
        public Func<CreateDailyJournalCommand, CancellationToken, Task<DailyJournalWriteResult>>? CreateBehavior { get; set; }
        public Func<UpdateDailyJournalCommand, CancellationToken, Task<DailyJournalWriteResult>>? UpdateBehavior { get; set; }
        public ConcurrentQueue<(DateOnly Date, Guid? AccountId)> Reads { get; } = new();
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
            if (CreateBehavior is not null) return CreateBehavior(command, cancellationToken);
            Journal = Details(command.Text, command.IsDraft, accountId: command.TradingAccountId, date: command.TradingDate);
            return Task.FromResult(new DailyJournalWriteResult(DailyJournalWriteStatus.Created, Journal));
        }

        public Task<DailyJournalWriteResult> UpdateAsync(UpdateDailyJournalCommand command, CancellationToken cancellationToken = default)
        {
            Updates.Enqueue(command);
            if (UpdateBehavior is not null) return UpdateBehavior(command, cancellationToken);
            bool unchanged = Journal!.Entry.Text == command.Text && Journal.Entry.IsDraft == command.IsDraft;
            Journal = Details(command.Text, command.IsDraft, command.ExpectedRevision + (unchanged ? 0 : 1),
                Journal.Entry.TradingAccountId, Journal.Entry.TradingDate, command.JournalId);
            return Task.FromResult(new DailyJournalWriteResult(unchanged ? DailyJournalWriteStatus.Unchanged : DailyJournalWriteStatus.Updated, Journal));
        }

        public Task<IReadOnlyList<DailyJournalRevision>> GetHistoryAsync(Guid journalId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The editor must not load revision history.");
    }
}
