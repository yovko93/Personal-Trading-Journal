using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.Journals;

public sealed class CalendarDayJournalsTests
{
    private static readonly DateOnly Date = new(2026, 10, 1);

    [Fact]
    public async Task OctoberFirstAllAccountsMatchesMarkerAndKeepsSeparateEntriesAndExactEditingTargets()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        Guid p21 = await AddAccount(db, "P 21"), other = await AddAccount(db, "Other", inactive: true);
        var first = (await db.Repository.CreateAsync(new(Date, p21, "P21 saved text", false))).Journal!.Entry;
        var vm = Create(db, dialogs: new FakeDialogService { ConfirmationResult = true });
        try
        {
            await vm.ActivateAsync(); await vm.SelectDayCommand.ExecuteAsync(Cell(vm));
            Assert.Equal("✓", Cell(vm).JournalIndicatorText);
            Assert.Equal(first.Id, Assert.Single(vm.DayJournals).Id);
            Assert.DoesNotContain("No Journal", vm.DayJournalStatusMessage);
            Assert.True(vm.IsSelectedDayEmpty);
            await db.Repository.CreateAsync(new(Date, null, "Global saved text", false));
            await db.Repository.CreateAsync(new(Date, other, "Other saved text"));
            vm.OnJournalCommitted(); await vm.JournalLoadTask;
            Assert.Equal(3, vm.DayJournals.Count); Assert.Equal(3, Cell(vm).JournalCount);
            Assert.Equal(new[] { "All accounts", "Other (inactive)", "P 21" }, vm.DayJournals.Select(r => r.AccountLabel));
            Assert.Equal(new[] { "Global saved text", "Other saved text", "P21 saved text" }, vm.DayJournals.Select(r => r.Text));
            await vm.OpenDayJournalCommand.ExecuteAsync(vm.DayJournals.Single(r => r.Id == first.Id));
            var editor = vm.InlineJournal!;
            Assert.Equal(first.Id, editor.JournalId); Assert.Equal(p21, editor.EditorAccount.Id);
            Assert.False(editor.IsEditorOpen); Assert.Null(vm.SelectedAccount.Id);
            await editor.ReopenReviewCommand.ExecuteAsync(null);
            editor.Text = "Changed P21";
            await editor.SaveCommand.ExecuteAsync(null); await vm.JournalLoadTask;
            Assert.Equal("Changed P21", vm.DayJournals.Single(r => r.Id == first.Id).Text);
            Assert.Equal("Completed", vm.DayJournals.Single(r => r.Id == first.Id).StateLabel);
            await editor.ReopenReviewCommand.ExecuteAsync(null); editor.WentWell = "Draft answer";
            await editor.SaveDraftAndCloseCommand.ExecuteAsync(null); await vm.JournalLoadTask;
            Assert.Equal("Draft", vm.DayJournals.Single(r => r.Id == first.Id).StateLabel);
            Assert.Equal(2, Cell(vm).DraftJournalCount);
            await editor.DeleteCommand.ExecuteAsync(null); await vm.JournalLoadTask;
            Assert.DoesNotContain(vm.DayJournals, r => r.Id == first.Id);
            Assert.Equal(2, Cell(vm).JournalCount);
            vm.CloseInlineJournalCommand.Execute(null);
            vm.SelectedAccount = vm.Accounts.Single(a => a.Id == other); await vm.LoadTask;
            Assert.Equal(other, Assert.Single(vm.DayJournals).AccountId);
            Assert.Equal(1, Cell(vm).JournalCount);
        }
        finally { vm.Deactivate(); }
    }

    [Fact]
    public async Task AddIsAlwaysExplicitNewFormCollisionKeepsFieldsAndMoveRemovesOldFilteredEntry()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        Guid account = await AddAccount(db, "P 21");
        await db.Repository.CreateAsync(new(Date, null, "Already global", false));
        var dialogs = new FakeDialogService { ConfirmationResult = true };
        var vm = Create(db, dialogs: dialogs);
        try
        {
            await vm.ActivateAsync(); await vm.SelectDayCommand.ExecuteAsync(Cell(vm));
            await vm.AddDayJournalCommand.ExecuteAsync(null);
            var editor = vm.InlineJournal!;
            Assert.False(editor.IsExisting); Assert.True(editor.IsEditorOpen); Assert.Null(editor.EditorAccount.Id);
            editor.Text = "New Account notes"; editor.WentWell = "Well";
            await editor.SaveCommand.ExecuteAsync(null);
            Assert.True(editor.IsEditorOpen); Assert.Contains("created", editor.ErrorMessage);
            Assert.Equal("New Account notes", editor.Text); Assert.Equal("Well", editor.WentWell);
            editor.EditorAccount = editor.Accounts.Single(a => a.Id == account);
            await editor.SaveDraftAndCloseCommand.ExecuteAsync(null); await vm.JournalLoadTask;
            Assert.Equal(2, vm.DayJournals.Count); Assert.False(editor.IsEditorOpen);
            Assert.Equal("Already global", (await db.Repository.GetAsync(Date))!.Entry.Text);
            vm.CloseInlineJournalCommand.Execute(null);
            vm.SelectedAccount = vm.Accounts.Single(a => a.Id == account); await vm.LoadTask;
            await vm.OpenDayJournalCommand.ExecuteAsync(Assert.Single(vm.DayJournals));
            editor = vm.InlineJournal!;
            var global = (await db.Repository.GetAsync(Date))!.Entry;
            await db.Repository.DeleteAsync(new(global.Id, global.Revision));
            editor.EditorAccount = editor.Accounts.Single(a => a.Id is null);
            await editor.SaveCommand.ExecuteAsync(null); await vm.JournalLoadTask;
            Assert.Empty(vm.DayJournals); Assert.False(Cell(vm).HasJournal);
            Assert.Null(vm.InlineJournal); // No off-filter saved card remains after moving out.
            Assert.Contains("No Journal", vm.DayJournalStatusMessage);
            vm.SelectedAccount = vm.Accounts.Single(a => a.Id is null); await vm.LoadTask;
            Assert.Equal("New Account notes", Assert.Single(vm.DayJournals).Text);
        }
        finally { vm.Deactivate(); }
    }

    [Fact]
    public async Task TradeFailureDoesNotBlockJournalsAndJournalFailureHasIndependentRecoveryWithoutDiscardingEdits()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        var repo = (IDailyJournalDayReader)db.Repository;
        var reader = new DayReader { Read = repo.GetDayAsync };
        await db.Repository.CreateAsync(new(Date, null, "Saved"));
        var trades = new FakeTradingCalendarDayReader { Handler = (_, _) => throw new InvalidOperationException("Trade failure") };
        var vm = Create(db, reader, trades);
        try
        {
            await vm.ActivateAsync(); await vm.SelectDayCommand.ExecuteAsync(Cell(vm));
            Assert.NotNull(vm.DayErrorMessage); Assert.Equal("Saved", Assert.Single(vm.DayJournals).Text);
            await vm.OpenDayJournalCommand.ExecuteAsync(vm.DayJournals[0]);
            var editor = vm.InlineJournal!; editor.Text = "Unsaved";
            reader.Read = (_, _, _) => throw new InvalidOperationException("Journal failure");
            await vm.RefreshDayJournalsCommand.ExecuteAsync(null);
            Assert.Contains("could not be loaded", vm.DayJournalStatusMessage);
            Assert.DoesNotContain("No Journal", vm.DayJournalStatusMessage);
            Assert.Equal("Unsaved", editor.Text); Assert.True(editor.IsEditorOpen);
            reader.Read = repo.GetDayAsync;
            await vm.RefreshCommand.ExecuteAsync(null);
            Assert.Equal("Saved", Assert.Single(vm.DayJournals).Text);
            Assert.Same(editor, vm.InlineJournal); Assert.Equal("Unsaved", editor.Text);
            Assert.False(vm.TryCloseInlineJournal());
        }
        finally { vm.Deactivate(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateDayOrAccountResultsCannotReplaceCurrentJournalList(bool switchAccount)
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        Guid account = await AddAccount(db, "P 21");
        var old = (await db.Repository.CreateAsync(new(Date, null, "Old"))).Journal!;
        await db.Repository.CreateAsync(new(switchAccount ? Date : Date.AddDays(1), account, "Current"));
        var repo = (IDailyJournalDayReader)db.Repository;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<IReadOnlyList<DailyJournalDetails>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken oldToken = default;
        var reader = new DayReader { Read = (_, _, ct) => { oldToken = ct; started.SetResult(); return pending.Task; } };
        var vm = Create(db, reader);
        try
        {
            await vm.ActivateAsync(); var first = vm.SelectDayCommand.ExecuteAsync(Cell(vm));
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            reader.Read = repo.GetDayAsync;
            if (switchAccount) { vm.SelectedAccount = vm.Accounts.Single(a => a.Id == account); await vm.LoadTask; }
            else await vm.SelectDayCommand.ExecuteAsync(Cell(vm, Date.AddDays(1)));
            Assert.True(oldToken.IsCancellationRequested);
            pending.SetResult([old]); await first;
            Assert.Equal("Current", Assert.Single(vm.DayJournals).Text);
        }
        finally { pending.TrySetResult([]); vm.Deactivate(); }
    }

    [Fact]
    public async Task StaleRowCannotOpenReplacementJournalAndOpeningAnotherRowHonorsUnsavedGuard()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        Guid account = await AddAccount(db, "P 21");
        var original = (await db.Repository.CreateAsync(new(Date, null, "Original"))).Journal!.Entry;
        await db.Repository.CreateAsync(new(Date, account, "Other"));
        var vm = Create(db);
        try
        {
            await vm.ActivateAsync(); await vm.SelectDayCommand.ExecuteAsync(Cell(vm));
            var stale = vm.DayJournals.Single(r => r.Id == original.Id);
            await db.Repository.DeleteAsync(new(original.Id, original.Revision));
            await db.Repository.CreateAsync(new(Date, null, "Replacement"));
            await vm.OpenDayJournalCommand.ExecuteAsync(stale);
            Assert.Contains("moved or deleted", vm.InlineJournal!.ErrorMessage);
            Assert.False(vm.InlineJournal.SaveCommand.CanExecute(null));
            await vm.InlineJournal.ReloadCommand.ExecuteAsync(null);
            Assert.False(vm.InlineJournal.CanEdit);
            vm.CloseInlineJournalCommand.Execute(null);
            await vm.RefreshDayJournalsCommand.ExecuteAsync(null);
            await vm.OpenDayJournalCommand.ExecuteAsync(vm.DayJournals.Single(r => r.AccountId == account));
            var editor = vm.InlineJournal!; editor.Text = "Keep local";
            await vm.OpenDayJournalCommand.ExecuteAsync(vm.DayJournals.Single(r => r.AccountId is null));
            Assert.Same(editor, vm.InlineJournal); Assert.Equal("Keep local", editor.Text);
            await vm.AddDayJournalCommand.ExecuteAsync(null);
            Assert.Same(editor, vm.InlineJournal);
        }
        finally { vm.Deactivate(); }
    }

    private static CalendarDayCell Cell(CalendarViewModel vm, DateOnly? date = null) => vm.Weeks.SelectMany(w => w.Days).Single(d => d.Date == (date ?? Date));
    private static CalendarViewModel Create(JournalSqliteTests.JournalTestDatabase db, IDailyJournalDayReader? reader = null,
        ITradingCalendarDayReader? trades = null, FakeDialogService? dialogs = null) => new(
        db.Provider.GetRequiredService<ITradingCalendarReader>(), new Clock(), trades ?? db.Provider.GetRequiredService<ITradingCalendarDayReader>(),
        db.Provider.GetRequiredService<ITradingAccountReader>(), journalStatusReader: db.Provider.GetRequiredService<IDailyJournalStatusReader>(),
        journalRepository: db.Repository, journalDialogs: dialogs ?? new FakeDialogService(), journalDayReader: reader);
    private static async Task<Guid> AddAccount(JournalSqliteTests.JournalTestDatabase db, string name, bool inactive = false)
    {
        var account = new TradingAccount(name, TradingAccountType.Personal, null, null, "USD", 0m, DateTimeOffset.UtcNow);
        if (inactive) account.Deactivate(DateTimeOffset.UtcNow);
        await db.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(account); return account.Id;
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 1, 15, 0, 0, TimeSpan.Zero); }
    private sealed class DayReader : IDailyJournalDayReader
    {
        public required Func<DateOnly, Guid?, CancellationToken, Task<IReadOnlyList<DailyJournalDetails>>> Read { get; set; }
        public Task<IReadOnlyList<DailyJournalDetails>> GetDayAsync(DateOnly date, Guid? accountId = null, CancellationToken cancellationToken = default) => Read(date, accountId, cancellationToken);
    }
}
