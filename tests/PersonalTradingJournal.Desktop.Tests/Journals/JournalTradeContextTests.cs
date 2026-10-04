using System.Collections.Concurrent;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Journals;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class JournalTradeContextTests
{
    private static readonly DateOnly Day = new(2026, 9, 9);

    [Theory]
    [InlineData(2026, 3, 8, 23)]
    [InlineData(2026, 11, 1, 25)]
    public async Task ScopeUsesExactAccountAndNewYorkDateIncludingDst(int year, int month, int day, int hours)
    {
        AccountListItem account = Account("Historical", active: false);
        var reader = new FakeTradingCalendarDayReader();
        var context = new JournalTradeContextViewModel(reader, new AccountsReader { Items = [account] });
        DateOnly date = new(year, month, day);
        await context.ActivateAsync(date, account.Id);

        var call = Assert.Single(reader.Calls);
        Assert.Equal(date, call.Query.Date);
        Assert.Equal(account.Id, call.Query.TradingAccountId);
        Assert.Equal(TimeSpan.FromHours(hours), call.Query.ClosedBeforeUtc - call.Query.ClosedFromUtc);
        Assert.Contains(date.ToString("yyyy-MM-dd"), context.ScopeLabel);
        Assert.Contains("New York", context.ScopeLabel);
        Assert.Contains("Historical (inactive)", context.ScopeLabel);
        Assert.True(context.HasResult);
        Assert.True(context.IsEmpty);
        Assert.Null(context.ErrorMessage);
    }

    [Fact]
    public async Task RowsAndCurrencySummariesPreserveReaderEconomicsAndClassification()
    {
        AccountListItem account = Account("First");
        TradeListItem usdVerified = Row(Day, account.Id, "USD", 20m, 18m);
        TradeListItem usdEstimated = Row(Day, account.Id, "USD", -3m, null);
        TradeListItem eurUnavailable = Row(Day, account.Id, "EUR", null, null);
        var classification = new CalendarTradeClassification(Guid.NewGuid(), "Breakout", false,
            [new(Guid.NewGuid(), "Chased", true), new(Guid.NewGuid(), null, null)]);
        TradingCalendarDayDetails result = TradingCalendarDayDetails.Create(Day, [usdVerified, usdEstimated, eurUnavailable]) with
        {
            Classifications = new Dictionary<Guid, CalendarTradeClassification> { [usdVerified.Id] = classification },
            References = new Dictionary<Guid, CalendarTradeReferenceState> { [usdVerified.Id] = new(false, null) },
        };
        var reader = new FakeTradingCalendarDayReader { Handler = (_, _) => Task.FromResult(result) };
        var context = new JournalTradeContextViewModel(reader, new AccountsReader { Items = [account] });
        await context.ActivateAsync(Day, null);

        Assert.Equal(result.Trades, context.Rows.Select(row => row.Trade));
        Assert.Equal(3, context.ClosedTradeCount);
        Assert.Equal("3 closed Trades", context.CountText);
        Assert.Contains("All accounts", context.ScopeLabel);
        Assert.False(context.IsEmpty);
        Assert.All(context.Rows, row => Assert.Null(row.Editor));
        var classified = context.Rows.Single(row => row.Trade.Id == usdVerified.Id);
        Assert.Equal("Breakout (inactive)", classified.SetupText);
        Assert.Equal("Chased, Unavailable Mistake", classified.MistakesText);
        Assert.Equal("Account (inactive)", classified.AccountText);
        Assert.Equal("Unavailable Instrument", classified.InstrumentText);
        var usd = Assert.Single(context.Summaries, summary => summary.Currency == "USD");
        var eur = Assert.Single(context.Summaries, summary => summary.Currency == "EUR");
        Assert.Same(result.Currencies.Single(currency => currency.Currency == "USD").Metrics, usd.Metrics);
        Assert.Equal(15m, usd.Amount);
        Assert.True(usd.Metrics.EffectiveNet.IsEstimated);
        Assert.Null(eur.Amount);
        Assert.Contains("unavailable", eur.Description);
    }

    [Fact]
    public async Task AllAccountsAndSpecificAccountScopesReadIndependently()
    {
        AccountListItem first = Account("First");
        AccountListItem second = Account("Second");
        TradeListItem[] rows = [Row(Day, first.Id, "USD", 5m, 4m), Row(Day, second.Id, "EUR", 7m, 6m)];
        var reader = new FakeTradingCalendarDayReader
        {
            Handler = (query, _) => Task.FromResult(TradingCalendarDayDetails.Create(query.Date,
                rows.Where(row => query.TradingAccountId is null || row.TradingAccountId == query.TradingAccountId))),
        };
        var context = new JournalTradeContextViewModel(reader, new AccountsReader { Items = [first, second] });
        await context.ActivateAsync(Day, null);
        Assert.Equal(2, context.Rows.Count);
        Assert.Equal(2, context.Summaries.Count);
        await context.SetScopeAsync(Day, first.Id);
        Assert.Equal(first.Id, Assert.Single(context.Rows).Trade.TradingAccountId);
        Assert.Equal("USD", Assert.Single(context.Summaries).Currency);
        Assert.Equal("1 closed Trade", context.CountText);
        await context.SetScopeAsync(Day, null);
        Assert.Equal(2, context.Rows.Count);
        Assert.Equal(new Guid?[] { null, first.Id, null }, reader.Calls.Select(call => call.Query.TradingAccountId));
    }

    [Fact]
    public async Task EmptyAndUnselectedDatesAreDifferentStates()
    {
        var reader = new FakeTradingCalendarDayReader();
        var context = new JournalTradeContextViewModel(reader, new AccountsReader());
        await context.ActivateAsync(Day, null);
        Assert.True(context.HasResult);
        Assert.True(context.IsEmpty);
        Assert.Equal("0 closed Trades", context.CountText);
        Assert.Contains("No closed Trades", context.StatusText);
        await context.SetScopeAsync(null, null);
        Assert.False(context.HasResult);
        Assert.False(context.IsEmpty);
        Assert.Contains("unavailable", context.CountText);
        Assert.False(context.RefreshCommand.CanExecute(null));
        Assert.Single(reader.Calls);
        Assert.Empty(context.Rows);
        Assert.Empty(context.Summaries);
    }

    [Fact]
    public async Task MissingAccountShowsUnavailableWithoutQueryingAllAccountsOrLosingScope()
    {
        AccountListItem account = Account("Removed");
        var accounts = new AccountsReader { Items = [account] };
        var reader = new FakeTradingCalendarDayReader();
        var context = new JournalTradeContextViewModel(reader, accounts);
        await context.ActivateAsync(Day, account.Id);
        accounts.Items = [];
        await context.RefreshCommand.ExecuteAsync(null);

        Assert.Single(reader.Calls);
        Assert.Contains("selected account", context.ErrorMessage);
        Assert.Contains("Unavailable account", context.ScopeLabel);
        Assert.False(context.HasResult);
        Assert.False(context.IsEmpty);
        Assert.Contains("unavailable", context.CountText);
        accounts.Items = [account with { IsActive = false }];
        await context.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(account.Id, reader.Calls.Last().Query.TradingAccountId);
        Assert.Contains("Removed (inactive)", context.ScopeLabel);
        Assert.True(context.HasResult);
        Assert.Null(context.ErrorMessage);
    }

    [Fact]
    public async Task RefreshClearsOldRowsImmediatelyAndErrorsRemainSeparateFromEmpty()
    {
        var reader = RowsReader();
        var context = new JournalTradeContextViewModel(reader, new AccountsReader());
        await context.ActivateAsync(Day, null);
        Assert.Single(context.Rows);
        var started = Signal();
        var pending = ResultSignal();
        reader.Handler = (_, _) => { started.TrySetResult(); return pending.Task; };
        Task refreshing = context.RefreshCommand.ExecuteAsync(null);
        await Wait(started.Task);
        Assert.True(context.IsLoading);
        Assert.Empty(context.Rows);
        Assert.Empty(context.Summaries);
        Assert.False(context.HasResult);
        Assert.False(context.IsEmpty);
        Assert.Contains("unavailable", context.CountText);
        pending.SetException(new IOException("private storage contents"));
        await refreshing;
        Assert.False(context.IsLoading);
        Assert.False(context.IsEmpty);
        Assert.Contains("could not be loaded", context.ErrorMessage);
        Assert.DoesNotContain("private storage", context.ErrorMessage);
        reader.Handler = (query, _) => Task.FromResult(TradingCalendarDayDetails.Create(query.Date, []));
        await context.RefreshCommand.ExecuteAsync(null);
        Assert.Null(context.ErrorMessage);
        Assert.True(context.HasResult);
        Assert.True(context.IsEmpty);
    }

    [Fact]
    public async Task AccountReadFailureCannotLeaveStaleRowsVisible()
    {
        var accounts = new AccountsReader();
        var reader = RowsReader();
        var context = new JournalTradeContextViewModel(reader, accounts);
        await context.ActivateAsync(Day, null);
        accounts.Failure = new IOException("failure");
        await context.RefreshCommand.ExecuteAsync(null);

        Assert.Single(reader.Calls);
        Assert.Empty(context.Rows);
        Assert.Empty(context.Summaries);
        Assert.NotNull(context.ErrorMessage);
        Assert.False(context.HasResult);
        Assert.False(context.IsEmpty);
    }

    [Fact]
    public async Task NewScopeRejectsLateOldRowsAndLateOldFailure()
    {
        var started = Signal();
        var pending = ResultSignal();
        var reader = new FakeTradingCalendarDayReader
        {
            Handler = (query, _) =>
            {
                if (query.Date == Day) { started.TrySetResult(); return pending.Task; }
                return Task.FromResult(TradingCalendarDayDetails.Create(query.Date, [Row(query.Date, Guid.NewGuid(), "EUR", 12m, 10m)]));
            },
        };
        var context = new JournalTradeContextViewModel(reader, new AccountsReader());
        Task old = context.ActivateAsync(Day, null);
        await Wait(started.Task);
        await context.SetScopeAsync(Day.AddDays(1), null);
        Assert.True(reader.Calls.First().Token.IsCancellationRequested);
        pending.SetException(new IOException("late old failure"));
        await old;

        Assert.Equal("EUR", Assert.Single(context.Rows).Trade.Currency);
        Assert.Contains(Day.AddDays(1).ToString("yyyy-MM-dd"), context.ScopeLabel);
        Assert.Null(context.ErrorMessage);
        Assert.False(context.IsLoading);
        Assert.True(context.HasResult);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelOrDeactivateRejectsLateSuccess(bool deactivate)
    {
        var started = Signal();
        var pending = ResultSignal();
        var reader = new FakeTradingCalendarDayReader { Handler = (_, _) => { started.TrySetResult(); return pending.Task; } };
        var context = new JournalTradeContextViewModel(reader, new AccountsReader());
        Task loading = context.ActivateAsync(Day, null);
        await Wait(started.Task);
        if (deactivate) context.Deactivate();
        else context.CancelCommand.Execute(null);
        Assert.False(context.IsLoading);
        Assert.True(Assert.Single(reader.Calls).Token.IsCancellationRequested);
        pending.SetResult(TradingCalendarDayDetails.Create(Day, [Row(Day, Guid.NewGuid(), "USD", 10m, 9m)]));
        await loading;
        Assert.Empty(context.Rows);
        Assert.Empty(context.Summaries);
        Assert.False(context.HasResult);
        Assert.False(context.IsEmpty);
        if (!deactivate) Assert.Contains("cancelled", context.StatusText);
        Assert.Equal(!deactivate, context.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public async Task DataCommitRefreshesOnlyActiveContext()
    {
        var reader = RowsReader();
        var context = new JournalTradeContextViewModel(reader, new AccountsReader());
        context.OnDataCommitted();
        Assert.Empty(reader.Calls);
        await context.ActivateAsync(Day, null);
        context.OnDataCommitted();
        await context.LoadTask;
        Assert.Equal(2, reader.Calls.Count);
        context.Deactivate();
        context.OnDataCommitted();
        Assert.Equal(2, reader.Calls.Count);
        Assert.Empty(context.Rows);
        await context.ActivateAsync(Day.AddDays(1), null);
        Assert.Equal(Day.AddDays(1), reader.Calls.Last().Query.Date);
    }

    [Fact]
    public async Task RepeatedCommitsRejectLatePreviousRefresh()
    {
        var reader = RowsReader();
        var context = new JournalTradeContextViewModel(reader, new AccountsReader());
        await context.ActivateAsync(Day, null);
        var started = Signal();
        var pending = ResultSignal();
        reader.Handler = (_, _) => { started.TrySetResult(); return pending.Task; };
        context.OnDataCommitted();
        Task old = context.LoadTask;
        await Wait(started.Task);
        reader.Handler = (query, _) => Task.FromResult(TradingCalendarDayDetails.Create(query.Date, []));
        context.OnDataCommitted();
        await context.LoadTask;
        pending.SetResult(TradingCalendarDayDetails.Create(Day, [Row(Day, Guid.NewGuid(), "USD", 10m, 9m)]));
        await old;

        Assert.True(context.IsEmpty);
        Assert.Empty(context.Rows);
        Assert.Equal("0 closed Trades", context.CountText);
    }

    [Fact]
    public async Task DirtyJournalIsUnaffectedByContextRefreshOrAccountChanges()
    {
        AccountListItem account = Account("Selected");
        var accounts = new AccountsReader { Items = [account] };
        var journalRepository = new JournalRepository();
        var contextReader = RowsReader();
        var context = new JournalTradeContextViewModel(contextReader, accounts);
        var journal = Journal(journalRepository, context, accounts);
        journal.SelectedAccount = new(account.Id, account.Name);
        await journal.ActivateAsync();
        journal.Text = "  keep my draft\r\n\t";
        var options = journal.Accounts;
        var selected = journal.SelectedAccount;
        DateTime? date = journal.SelectedDate;
        accounts.Items = [];
        context.OnDataCommitted();
        await context.LoadTask;
        Assert.NotNull(context.ErrorMessage);
        Assert.Equal("  keep my draft\r\n\t", journal.Text);
        Assert.True(journal.IsDirty);
        Assert.Same(options, journal.Accounts);
        Assert.Same(selected, journal.SelectedAccount);
        Assert.Equal(date, journal.SelectedDate);
        Assert.True(journal.CanEdit);
        Assert.True(journal.SaveCommand.CanExecute(null));
        Assert.Null(journal.ErrorMessage);
        Assert.Single(journalRepository.Reads);
        Assert.Empty(journalRepository.Creates);
        Assert.Empty(journalRepository.Updates);
        accounts.Items = [account];
        await context.RefreshCommand.ExecuteAsync(null);
        Assert.Null(context.ErrorMessage);
        Assert.Equal("  keep my draft\r\n\t", journal.Text);
        Assert.Single(journalRepository.Reads);
    }

    [Fact]
    public async Task ContextRefreshAndCancellationRemainIndependentOfPendingJournalSave()
    {
        var saveStarted = Signal();
        var saved = new TaskCompletionSource<DailyJournalWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken saveToken = default;
        var repository = new JournalRepository
        {
            SaveHandler = (_, token) => { saveToken = token; saveStarted.TrySetResult(); return saved.Task; },
        };
        var reader = RowsReader();
        var context = new JournalTradeContextViewModel(reader, new AccountsReader());
        var journal = Journal(repository, context);
        await journal.ActivateAsync();
        journal.Text = "pending save";
        Task save = journal.SaveCommand.ExecuteAsync(null);
        await Wait(saveStarted.Task);
        Assert.True(context.RefreshCommand.CanExecute(null));
        var readStarted = Signal();
        var pending = ResultSignal();
        reader.Handler = (_, _) => { readStarted.TrySetResult(); return pending.Task; };
        Task refresh = context.RefreshCommand.ExecuteAsync(null);
        await Wait(readStarted.Task);
        context.CancelCommand.Execute(null);
        pending.SetResult(TradingCalendarDayDetails.Create(Day, []));
        await refresh;
        Assert.True(journal.IsSaving);
        Assert.Equal("pending save", journal.Text);
        Assert.False(saveToken.IsCancellationRequested);
        Assert.Single(repository.Creates);
        Assert.Single(repository.Reads);
        saved.SetResult(new(DailyJournalWriteStatus.Created, Details("pending save")));
        await save;
        Assert.False(journal.IsDirty);
        Assert.True(journal.IsExisting);
        Assert.Null(journal.ErrorMessage);
    }

    [Fact]
    public async Task ContextFailureDoesNotBlockJournalAndJournalFailureDoesNotBlockContext()
    {
        var repository = new JournalRepository();
        var reader = new FakeTradingCalendarDayReader { Handler = (_, _) => throw new IOException("failed context") };
        var context = new JournalTradeContextViewModel(reader, new AccountsReader());
        var journal = Journal(repository, context);
        await journal.ActivateAsync();
        Assert.NotNull(context.ErrorMessage);
        Assert.Null(journal.ErrorMessage);
        Assert.True(journal.CanEdit);
        journal.Text = "can save with failed context";
        await journal.SaveCommand.ExecuteAsync(null);
        Assert.True(journal.IsExisting);
        Assert.Single(repository.Creates);
        journal.Deactivate();
        repository.ReadHandler = (_, _, _) => throw new IOException("failed journal");
        reader.Handler = (query, _) => Task.FromResult(TradingCalendarDayDetails.Create(query.Date, [Row(query.Date, Guid.NewGuid(), "USD", 3m, 2m)]));
        await journal.ActivateAsync();
        Assert.NotNull(journal.ErrorMessage);
        Assert.Null(context.ErrorMessage);
        Assert.Single(context.Rows);
    }

    [Fact]
    public async Task DirtyScopeVetoLeavesContextUntouchedAndApprovedChangeLoadsNewDate()
    {
        var reader = RowsReader();
        var context = new JournalTradeContextViewModel(reader, new AccountsReader());
        var dialogs = new FakeDialogService();
        var journal = Journal(new JournalRepository(), context, dialogs: dialogs);
        await journal.ActivateAsync();
        journal.Text = "dirty";
        journal.SelectedDate = Day.AddDays(1).ToDateTime(TimeOnly.MinValue);
        Assert.Single(reader.Calls);
        Assert.Contains(Day.ToString("yyyy-MM-dd"), context.ScopeLabel);
        dialogs.ConfirmationResult = true;
        journal.SelectedDate = Day.AddDays(1).ToDateTime(TimeOnly.MinValue);
        await journal.LoadTask;
        Assert.Equal(Day.AddDays(1), reader.Calls.Last().Query.Date);
        Assert.Contains(Day.AddDays(1).ToString("yyyy-MM-dd"), context.ScopeLabel);
        Assert.False(journal.IsDirty);
        Assert.Equal("", journal.Text);
    }

    [Fact]
    public async Task CancellingJournalReadDoesNotCancelItsContextRead()
    {
        var journalStarted = Signal();
        var journalResult = new TaskCompletionSource<DailyJournalDetails?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new JournalRepository { ReadHandler = (_, _, _) => { journalStarted.TrySetResult(); return journalResult.Task; } };
        var contextStarted = Signal();
        var contextResult = ResultSignal();
        var reader = new FakeTradingCalendarDayReader { Handler = (_, _) => { contextStarted.TrySetResult(); return contextResult.Task; } };
        var context = new JournalTradeContextViewModel(reader, new AccountsReader());
        var journal = Journal(repository, context);
        Task loading = journal.ActivateAsync();
        await Wait(Task.WhenAll(journalStarted.Task, contextStarted.Task));
        journal.CancelOperationCommand.Execute(null);
        Assert.False(journal.IsLoading);
        Assert.True(context.IsLoading);
        Assert.False(Assert.Single(reader.Calls).Token.IsCancellationRequested);
        contextResult.SetResult(TradingCalendarDayDetails.Create(Day, [Row(Day, Guid.NewGuid(), "USD", 3m, 2m)]));
        journalResult.SetResult(Details("late journal text"));
        await loading;
        Assert.Single(context.Rows);
        Assert.Equal("", journal.Text);
        Assert.False(journal.IsExisting);
    }

    private static JournalViewModel Journal(JournalRepository repository, JournalTradeContextViewModel context,
        AccountsReader? accounts = null, FakeDialogService? dialogs = null) =>
        new(repository, accounts ?? new AccountsReader(), dialogs ?? new(), context, new FixedTimeProvider());

    private static FakeTradingCalendarDayReader RowsReader() => new()
    {
        Handler = (query, _) => Task.FromResult(TradingCalendarDayDetails.Create(query.Date,
            [Row(query.Date, query.TradingAccountId ?? Guid.NewGuid(), "USD", 10m, 9m)])),
    };

    private static TradeListItem Row(DateOnly date, Guid account, string currency, decimal? gross, decimal? net)
    {
        DateTimeOffset closed = new(date.ToDateTime(new TimeOnly(16, 0)), TimeSpan.Zero);
        return new(Guid.NewGuid(), account, "Account", Guid.NewGuid(), "ES", TradeDirection.Long,
            TradeStatus.Closed, closed.AddHours(-1), closed, 0m, 100m, 101m,
            net.HasValue ? gross - net : null, gross, net, currency, 2m);
    }

    private static AccountListItem Account(string name, bool active = true) =>
        new(Guid.NewGuid(), name, TradingAccountType.Personal, null, null, "USD", null, active);

    private static DailyJournalDetails Details(string text, Guid? account = null) => new(
        new DailyJournalEntry(Day, account, text, FixedTimeProvider.FixedUtcNow),
        account.HasValue ? DailyJournalAccountState.Active : DailyJournalAccountState.AllAccounts, account.HasValue ? "Account" : null);

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static TaskCompletionSource<TradingCalendarDayDetails> ResultSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task Wait(Task task) => task.WaitAsync(TimeSpan.FromSeconds(10));

    private sealed class AccountsReader : ITradingAccountReader
    {
        public IReadOnlyList<AccountListItem> Items { get; set; } = [];
        public Exception? Failure { get; set; }
        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Failure is { } failure ? Task.FromException<IReadOnlyList<AccountListItem>>(failure) : Task.FromResult(Items);
        public Task<TradingAccountDetails?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class JournalRepository : IDailyJournalRepository
    {
        public ConcurrentQueue<(DateOnly Date, Guid? Account)> Reads { get; } = new();
        public ConcurrentQueue<CreateDailyJournalCommand> Creates { get; } = new();
        public ConcurrentQueue<UpdateDailyJournalCommand> Updates { get; } = new();
        public Func<DateOnly, Guid?, CancellationToken, Task<DailyJournalDetails?>>? ReadHandler { get; set; }
        public Func<CreateDailyJournalCommand, CancellationToken, Task<DailyJournalWriteResult>>? SaveHandler { get; set; }
        public Task<DailyJournalDetails?> GetAsync(DateOnly tradingDate, Guid? tradingAccountId = null, CancellationToken cancellationToken = default)
        {
            Reads.Enqueue((tradingDate, tradingAccountId));
            return ReadHandler?.Invoke(tradingDate, tradingAccountId, cancellationToken) ?? Task.FromResult<DailyJournalDetails?>(null);
        }
        public Task<DailyJournalWriteResult> CreateAsync(CreateDailyJournalCommand command, CancellationToken cancellationToken = default)
        {
            Creates.Enqueue(command);
            return SaveHandler?.Invoke(command, cancellationToken) ?? Task.FromResult(new DailyJournalWriteResult(
                DailyJournalWriteStatus.Created, Details(command.Text, command.TradingAccountId)));
        }
        public Task<DailyJournalWriteResult> UpdateAsync(UpdateDailyJournalCommand command, CancellationToken cancellationToken = default)
        {
            Updates.Enqueue(command);
            throw new NotSupportedException();
        }
        public Task<IReadOnlyList<DailyJournalRevision>> GetHistoryAsync(Guid journalId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
