using System.Text;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Imports;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Import;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Infrastructure.Imports.Tradovate;

namespace PersonalTradingJournal.Desktop.Tests.Navigation;

public sealed partial class MainWindowViewModelTests
{
    [Theory]
    [InlineData("Imported", false)]
    [InlineData("Imported", true)]
    [InlineData("NoChanges", false)]
    [InlineData("Blocked", false)]
    [InlineData("Rollback", false)]
    [InlineData("Failure", false)]
    public async Task ActiveDirtyJournalRefreshesTradeContextOnlyAfterCommittedTopstepImport(
        string outcome, bool cancelledPresentationAfterCommit)
    {
        var (fixture, store) = await CreateDelayedTopstepFixture();
        using var main = fixture.Main;
        Task confirmation = fixture.Import.ConfirmImportCommand.ExecuteAsync(null);
        await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await Task.WhenAll(fixture.Journal.LoadTask, fixture.Journal.TradeContext.LoadTask);
        fixture.Journal.Text = "Local journal draft remains exact.\r\n  ";
        DateTime? date = fixture.Journal.SelectedDate;
        var row = CalendarPage.CalendarDayDetailsTests.Row(DateOnly.FromDateTime(date!.Value), -285m, null);
        fixture.JournalReader.Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [row], ct));
        Assert.Single(fixture.JournalReader.Calls);

        if (outcome == "Rollback")
        {
            fixture.Import.CancelOperationCommand.Execute(null);
            store.ReturnResult.SetCanceled(store.Token);
        }
        else if (outcome == "Failure") store.ReturnResult.SetException(new IOException("Synthetic rollback"));
        else
        {
            TopstepImportStatus status = Enum.Parse<TopstepImportStatus>(outcome);
            store.Commit(new(status, status == TopstepImportStatus.Imported ? 1 : 0, 0, 0, [], [], []));
            if (cancelledPresentationAfterCommit)
            {
                fixture.Import.ResetTransientState();
                Assert.True(store.Token.IsCancellationRequested);
            }
            store.ReturnResult.SetResult();
        }
        await confirmation.WaitAsync(TimeSpan.FromSeconds(10));
        await fixture.Journal.TradeContext.LoadTask.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(outcome == "Imported" ? 2 : 1, fixture.JournalReader.Calls.Count);
        if (outcome == "Imported")
        {
            Assert.Equal(row.Id, Assert.Single(fixture.Journal.TradeContext.Rows).Trade.Id);
            Assert.Equal(-285m, Assert.Single(fixture.Journal.TradeContext.Summaries).Amount);
        }
        else Assert.Empty(fixture.Journal.TradeContext.Rows);
        AssertJournalDraftUnchanged(fixture, date, "Local journal draft remains exact.\r\n  ");

        // The import generation was consumed by the notification. Clicking the current
        // destination must neither refresh context twice nor re-activate the editor.
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        Assert.Equal(outcome == "Imported" ? 2 : 1, fixture.JournalReader.Calls.Count);
        Assert.Single(fixture.JournalRepository.ReadCalls);
    }

    [Fact]
    public async Task CommittedImportRejectsOlderTradeContextWithoutReloadingDirtyJournal()
    {
        var (fixture, store) = await CreateDelayedTopstepFixture();
        using var main = fixture.Main;
        Task confirmation = fixture.Import.ConfirmImportCommand.ExecuteAsync(null);
        await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await Task.WhenAll(fixture.Journal.LoadTask, fixture.Journal.TradeContext.LoadTask);
        fixture.Journal.Text = "Keep my draft.";
        DateTime? date = fixture.Journal.SelectedDate;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = new TaskCompletionSource<TradingCalendarDayDetails>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.JournalReader.Handler = (_, _) => { started.TrySetResult(); return old.Task; };
        Task staleRead = fixture.Journal.TradeContext.RefreshCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var row = CalendarPage.CalendarDayDetailsTests.Row(DateOnly.FromDateTime(date!.Value), 30m, 27m);
        fixture.JournalReader.Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [row], ct));
        store.Commit(new(TopstepImportStatus.Imported, 1, 0, 0, [row.Id], [], []));
        store.ReturnResult.SetResult();
        await confirmation.WaitAsync(TimeSpan.FromSeconds(10));
        await fixture.Journal.TradeContext.LoadTask.WaitAsync(TimeSpan.FromSeconds(10));
        old.SetResult(TradingCalendarDayDetails.Create(DateOnly.FromDateTime(date.Value), []));
        await staleRead.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(3, fixture.JournalReader.Calls.Count);
        Assert.Equal(row.Id, Assert.Single(fixture.Journal.TradeContext.Rows).Trade.Id);
        AssertJournalDraftUnchanged(fixture, date, "Keep my draft.");
    }

    [Fact]
    public async Task ImportCommittedWhileJournalIsInactiveReadsOnlyWhenJournalIsEntered()
    {
        var (fixture, store) = await CreateDelayedTopstepFixture();
        using var main = fixture.Main;
        Task confirmation = fixture.Import.ConfirmImportCommand.ExecuteAsync(null);
        await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        main.NavigateCommand.Execute(NavigationDestination.Notebook);
        var row = CalendarPage.CalendarDayDetailsTests.Row(DateOnly.FromDateTime(fixture.Journal.SelectedDate!.Value), 30m, 27m);
        fixture.JournalReader.Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [row], ct));
        store.Commit(new(TopstepImportStatus.Imported, 1, 0, 0, [row.Id], [], []));
        store.ReturnResult.SetResult();
        await confirmation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Empty(fixture.JournalReader.Calls);
        Assert.Empty(fixture.JournalRepository.ReadCalls);

        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await Task.WhenAll(fixture.Journal.LoadTask, fixture.Journal.TradeContext.LoadTask);
        Assert.Single(fixture.JournalReader.Calls);
        Assert.Equal(row.Id, Assert.Single(fixture.Journal.TradeContext.Rows).Trade.Id);
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        Assert.Single(fixture.JournalReader.Calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PendingTradeDeletionFromTradesOrCalendarRefreshesDirtyJournalOnlyAfterCommit(
        bool calendarEditor, bool cancel)
    {
        var deletion = new FakeTradeDeletionStore { HoldDelete = true };
        var calendarRows = new FakeTradeListReader();
        var row = CalendarPage.CalendarDayDetailsTests.Row(new DateOnly(2026, 9, 10), 100m, 95m);
        calendarRows.EnqueueResult([row]);
        var inlineEditor = calendarEditor ? Trades.TradesViewModelTests.CreateViewModel(
            tradeListReader: calendarRows, tradeDeletionStore: deletion,
            dialogService: new FakeDialogService { ConfirmationResult = true }) : null;
        var fixture = CreateFixture(tradeDeletionStore: calendarEditor ? null : deletion, calendarEditor: inlineEditor);
        using var main = fixture.Main;
        var writer = inlineEditor ?? fixture.Trades;
        await writer.EnsureLoadedAsync();
        row = Assert.Single(writer.RecentTrades);
        deletion.Result = new(row.Id, []);
        fixture.Journal.SelectedDate = new DateTime(2026, 9, 10);
        fixture.JournalReader.Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [row], ct));
        Task write = writer.DeleteTradeCommand.ExecuteAsync(row);
        await deletion.DeleteStarted.WaitAsync(TimeSpan.FromSeconds(10));
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await Task.WhenAll(fixture.Journal.LoadTask, fixture.Journal.TradeContext.LoadTask);
        fixture.Journal.Text = "Written while deletion was pending.";
        Assert.Equal(row.Id, Assert.Single(fixture.Journal.TradeContext.Rows).Trade.Id);
        fixture.JournalReader.Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [], ct));

        if (cancel) writer.DeleteTradeCommand.Cancel();
        else deletion.ReleaseDelete();
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write);
        else await write.WaitAsync(TimeSpan.FromSeconds(10));
        await fixture.Journal.TradeContext.LoadTask.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(cancel ? 1 : 2, fixture.JournalReader.Calls.Count);
        if (cancel) Assert.Equal(row.Id, Assert.Single(fixture.Journal.TradeContext.Rows).Trade.Id);
        else Assert.Empty(fixture.Journal.TradeContext.Rows);
        AssertJournalDraftUnchanged(fixture, new DateTime(2026, 9, 10), "Written while deletion was pending.");
    }

    [Theory]
    [InlineData("Imported")]
    [InlineData("NoChanges")]
    [InlineData("Blocked")]
    [InlineData("Rollback")]
    [InlineData("Failure")]
    public async Task ActiveDirtyJournalRefreshesOnlyForCommittedTradovateImport(string outcome)
    {
        var (fixture, store) = await CreateDelayedJournalTradovateFixture();
        using var main = fixture.Main;
        Task confirmation = fixture.Import.ConfirmImportCommand.ExecuteAsync(null);
        await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        main.NavigateCommand.Execute(NavigationDestination.Journal);
        await Task.WhenAll(fixture.Journal.LoadTask, fixture.Journal.TradeContext.LoadTask);
        fixture.Journal.Text = "Tradovate import must keep this draft.";
        DateTime? date = fixture.Journal.SelectedDate;
        var row = CalendarPage.CalendarDayDetailsTests.Row(DateOnly.FromDateTime(date!.Value), 20m, null);
        fixture.JournalReader.Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [row], ct));
        if (outcome == "Rollback")
        {
            fixture.Import.CancelOperationCommand.Execute(null);
            store.Result.SetCanceled(store.Token);
        }
        else if (outcome == "Failure") store.Result.SetException(new IOException("Synthetic rollback"));
        else
        {
            TradovateImportStatus status = Enum.Parse<TradovateImportStatus>(outcome);
            store.Result.SetResult(new(status, status == TradovateImportStatus.Imported ? 1 : 0,
                0, 0, status == TradovateImportStatus.Imported ? [row.Id] : [], [], []));
        }
        await confirmation.WaitAsync(TimeSpan.FromSeconds(10));
        await fixture.Journal.TradeContext.LoadTask.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(outcome == "Imported" ? 2 : 1, fixture.JournalReader.Calls.Count);
        if (outcome == "Imported") Assert.Equal(row.Id, Assert.Single(fixture.Journal.TradeContext.Rows).Trade.Id);
        else Assert.Empty(fixture.Journal.TradeContext.Rows);
        AssertJournalDraftUnchanged(fixture, date, "Tradovate import must keep this draft.");
    }

    private static void AssertJournalDraftUnchanged(ViewModelFixture fixture, DateTime? date, string text)
    {
        Assert.Equal(NavigationDestination.Journal, fixture.Main.CurrentDestination);
        Assert.Equal(date, fixture.Journal.SelectedDate);
        Assert.Null(fixture.Journal.SelectedAccount.Id);
        Assert.Equal(text, fixture.Journal.Text);
        Assert.True(fixture.Journal.IsDirty);
        Assert.Null(fixture.Journal.ErrorMessage);
        Assert.Single(fixture.JournalRepository.ReadCalls);
        Assert.Empty(fixture.JournalRepository.CreateCalls);
    }

    private static async Task<(ViewModelFixture Fixture, JournalTradovateStore Store)> CreateDelayedJournalTradovateFixture()
    {
        Guid id = Guid.NewGuid();
        var accounts = new FakeTradingAccountReader();
        var instruments = new FakeInstrumentReader();
        for (int i = 0; i < 5; i++)
        {
            accounts.EnqueueResult([new AccountListItem(id, "Synthetic Tradovate", TradingAccountType.Personal,
                "Tradovate", null, "USD", null, true)]);
            accounts.EnqueueDetailResult(new(id, "Synthetic Tradovate", TradingAccountType.Personal,
                "Tradovate", null, "USD", null, true, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
            instruments.EnqueueResult([]);
        }
        var store = new JournalTradovateStore();
        var preparation = new TradovateImportPreparationService(accounts);
        var import = new ImportViewModel(accounts, new TradovateCsvParser(), new TradovateExecutionReconstructor(),
            new TradovateInstrumentResolver(instruments), preparation, new TradovateImportPreviewBuilder(),
            new JournalTradovatePicker(), new ImportTradovateTradesUseCase(preparation, store, new FixedTimeProvider()),
            new FakeDialogService { ConfirmationResult = true }, new TradovateOnlyFormatDetector(), null!, null!, null!, null!);
        var fixture = CreateFixture(importViewModel: import);
        fixture.Main.NavigateCommand.Execute(NavigationDestination.Import);
        await import.EnsureLoadedAsync();
        import.SelectedSource = import.Sources.Single(source => source.Name == "Tradovate");
        await import.SelectCsvCommand.ExecuteAsync(null);
        import.SelectedAccount = Assert.Single(import.Accounts);
        await import.BuildPreviewCommand.ExecuteAsync(null);
        Assert.True(import.ConfirmImportCommand.CanExecute(null),
            $"Synthetic Tradovate preview is {import.Phase}: {import.WorkflowErrorMessage}; " +
            string.Join("; ", import.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return (fixture, store);
    }

    private sealed class JournalTradovateStore : ITradovateImportStore
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<TradovateImportResult> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token { get; private set; }

        public Task<TradovateImportResult> ImportAsync(TradovateImportRequest request, CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            Started.TrySetResult();
            return Result.Task;
        }
    }

    private sealed class JournalTradovatePicker : ITradovateCsvFilePicker
    {
        public TradovateCsvFileSelection Pick() => new("synthetic-tradovate.csv", new MemoryStream(Encoding.UTF8.GetBytes(
            "symbol,_priceFormat,_priceFormatType,_tickSize,buyFillId,sellFillId,qty,buyPrice,sellPrice,pnl,boughtTimestamp,soldTimestamp,duration\n" +
            "MNQU6,0.00,Decimal,0.25,SYNTH-BUY,SYNTH-SELL,1,24000,24010,$20.00,09/10/2026 10:00:00,09/10/2026 10:10:00,00:10:00")));
    }
}
