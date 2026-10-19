using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Desktop.Tests.Journals;
using PersonalTradingJournal.Desktop.Tests.Navigation;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

public sealed class TradingPlanWorkspaceTests
{
    [Fact]
    public async Task EmptyContextAndNewYorkDateNeverPersistUntilExplicitCreation()
    {
        await using var f = await Fixture.CreateAsync(); var vm = f.Vm;
        Assert.Equal(new DateOnly(2026, 10, 17), vm.SelectedDate); // Clock is 18 October UTC.
        Assert.NotNull(vm.SelectedAccount); Assert.Null(vm.SelectedAccount.Id); Assert.Null(vm.Editor); Assert.True(vm.CanCreateDraft);
        Assert.False(vm.Notice.HasText);
        Assert.Equal(PersonalTradingJournal.Domain.TradingPlans.TradingPlanScope.AllAccounts, new TradingPlanKey(vm.SelectedAccount.Id, vm.SelectedDate).Scope); Assert.True(vm.CanCreateDraft);
        Assert.Equal(0, f.Store.Writes); Assert.Null(await f.Store.Inner.LoadAsync(new(f.Source.Account.Id, vm.SelectedDate)));
        await vm.CreateDraftAsync(); Assert.Equal(1, f.Store.Writes); Assert.NotNull(vm.Editor);
        await vm.NextDayCommand.ExecuteAsync(null); Assert.Equal(new DateOnly(2026, 10, 18), vm.SelectedDate); Assert.Null(vm.Editor);
        Assert.False(vm.Notice.HasText); // Previous creation feedback must not survive a successful empty context load.
        await vm.TodayCommand.ExecuteAsync(null); Assert.NotNull(vm.Editor); Assert.Equal(1, f.Store.Writes);
        vm.DateText = "not a date"; await vm.OpenDateCommand.ExecuteAsync(null); await vm.PreviousDayCommand.ExecuteAsync(null);
        Assert.Equal("not a date", vm.DateText); Assert.True(vm.HasDateError); Assert.False(vm.CanSave); Assert.Equal(1, f.Store.Writes);
    }

    [Fact]
    public async Task EmptyContextLoadFailureRemainsVisibleThenSuccessfulReloadClearsItWithoutWriting()
    {
        await using var f = await Fixture.CreateAsync();
        f.Store.Read = (_, _) => throw new InvalidOperationException("Synthetic load failure");
        await f.Vm.ReloadCommand.ExecuteAsync(null);
        Assert.Null(f.Vm.Editor); Assert.True(f.Vm.Notice.HasText); Assert.Equal(PlanNoticeKind.Error, f.Vm.Notice.Kind);
        f.Store.Read = null;
        await f.Vm.ReloadCommand.ExecuteAsync(null);
        Assert.Null(f.Vm.Editor); Assert.False(f.Vm.Notice.HasText); Assert.True(f.Vm.CanCreateDraft); Assert.Equal(0, f.Store.Writes);
    }

    [Fact]
    public async Task OneSessionSavesEverySectionRetainsSelectionThenReadyAndStartDraftSurviveReactivation()
    {
        await using var f = await Fixture.CreateAsync(seed: true); var vm = f.Vm; var editor = vm.Editor!;
        editor.Focus.Text = "Patient synthetic focus"; editor.News.Text = "Synthetic restricted time";
        editor.Contexts[0].Levels[0].Text = "123.125"; editor.Scenarios[0].Condition.Text = "Recorded confirmation";
        editor.RiskPerTrade.Text = "0"; editor.MaximumTrades.Text = "3";
        foreach (var item in editor.Checklist) item.IsChecked = true;
        await vm.MarkReadyAsync(); Assert.Same(editor, vm.Editor); Assert.Equal(0, f.Store.Writes);
        Assert.Contains("Save", editor.Message, StringComparison.OrdinalIgnoreCase);
        var instrument = editor.SelectedInstrument;
        Assert.True(await vm.SaveAsync()); Assert.Same(instrument, editor.SelectedInstrument);
        Assert.Equal(TradingPlanChecklist.None, editor.Confirmed); Assert.True(editor.IsPreparationComplete);
        var saved = await f.Store.Inner.LoadAsync(editor.Key);
        Assert.Equal("Recorded confirmation", saved!.WorkingDraft!.Content.Scenarios[0].Condition);
        Assert.Equal(123.125m, saved.WorkingDraft.Content.Instruments[0].Levels.PreviousDayHigh);
        Assert.Equal(0m, saved.WorkingDraft.Content.RiskLimits.RiskPerTrade);
        var selectedDateText = vm.DateText;
        vm.DateText = "20 Oct 2026";
        Assert.False(vm.MarkReadyCommand.CanExecute(null));
        int writes = f.Store.Writes; await vm.MarkReadyAsync(); Assert.Equal(writes, f.Store.Writes);
        vm.DateText = selectedDateText; Assert.True(vm.MarkReadyCommand.CanExecute(null));
        await vm.MarkReadyAsync(); Assert.True(vm.Editor!.IsReadOnly); Assert.Contains("saved and verified", vm.Message);
        vm.DateText = "20 Oct 2026"; Assert.False(vm.StartDraftCommand.CanExecute(null));
        writes = f.Store.Writes; await vm.StartDraftAsync(); Assert.Equal(writes, f.Store.Writes);
        vm.DateText = selectedDateText; Assert.True(vm.StartDraftCommand.CanExecute(null));
        var readyId = vm.Editor.CurrentPlan.LatestReadyRevision!.Id;
        using var restarted = f.NewWorkspace(); await restarted.ActivateAsync();
        await restarted.SelectContextAsync(vm.SelectedDate, restarted.Accounts.Single(a => a.Id == f.Source.Account.Id));
        Assert.True(restarted.Editor!.IsReadOnly); Assert.Equal(TradingPlanChecklist.None, restarted.Editor.Confirmed);
        Assert.Equal("Patient synthetic focus", restarted.Editor.Focus.Text);
        await restarted.StartDraftAsync(); Assert.False(restarted.Editor!.IsReadOnly); Assert.Equal(TradingPlanChecklist.None, restarted.Editor.Confirmed);
        Assert.Equal(readyId, restarted.Editor.CurrentPlan.LatestReadyRevision!.Id);
        Assert.Equal(TradingPlanChecklist.None, (await f.Store.Inner.LoadReadyAsync(readyId))!.Content.Confirmed);
        Assert.Contains("preserved", restarted.Editor.PreservedReadyLabel);
    }

    [Theory]
    [InlineData(TradingPlanWriteStatus.StorageFailure)] [InlineData(TradingPlanWriteStatus.Conflict)]
    [InlineData(TradingPlanWriteStatus.ReferenceChanged)] [InlineData(TradingPlanWriteStatus.ReferenceUnavailable)]
    [InlineData(TradingPlanWriteStatus.Cancelled)]
    public async Task FailedSavesKeepAllRawInputAndToken(TradingPlanWriteStatus status)
    {
        await using var f = await Fixture.CreateAsync(seed: true); var editor = f.Vm.Editor!; var token = editor.Token;
        editor.Focus.Text = "unsaved"; editor.Scenarios[0].Response.Text = "retained response"; editor.MaximumTrades.Text = "4";
        f.Store.Status = status; Assert.False(await f.Vm.SaveAsync());
        Assert.Equal(PlanNotice.ForWrite(status), f.Vm.Notice.Kind);
        Assert.Same(editor, f.Vm.Editor); Assert.Equal(token, editor.Token); Assert.True(editor.IsDirty);
        Assert.Equal("retained response", editor.Scenarios[0].Response.Text); Assert.Equal("4", editor.MaximumTrades.Text);
        Assert.NotEqual("Draft saved and verified.", f.Vm.Message);
        editor.MaximumTrades.Text = "3.5"; int writes = f.Store.Writes;
        Assert.False(await f.Vm.SaveAsync()); Assert.Equal(writes, f.Store.Writes); Assert.Equal("3.5", editor.MaximumTrades.Text);
        Assert.Equal(PlanNoticeKind.Warning, f.Vm.Notice.Kind);
    }

    [Theory]
    [InlineData(PlanLeaveChoice.Save, true)] [InlineData(PlanLeaveChoice.Discard, true)] [InlineData(PlanLeaveChoice.Cancel, false)]
    public async Task ContextGuardSavesDiscardsOrCancelsWithoutRetargetingIdentity(PlanLeaveChoice choice, bool changes)
    {
        await using var f = await Fixture.CreateAsync(seed: true); var vm = f.Vm; var old = vm.Editor!; var key = old.Key;
        old.Focus.Text = "guarded text"; f.Interaction.Choice = choice;
        var next = vm.SelectedDate.AddDays(1);
        Assert.Equal(changes, await vm.SelectContextAsync(next, vm.SelectedAccount));
        var stored = (await f.Store.Inner.LoadAsync(key))!;
        Assert.Equal(choice == PlanLeaveChoice.Save ? "guarded text" : f.Source.Content.Focus, stored.WorkingDraft!.Content.Focus);
        if (!changes) { Assert.Same(old, vm.Editor); Assert.Equal(key.NewYorkDate, vm.SelectedDate); Assert.Equal("guarded text", old.Focus.Text); }
        else { Assert.Null(vm.Editor); Assert.Equal(next, vm.SelectedDate); }
        Assert.Equal(1, f.Interaction.Prompts);
    }

    [Fact]
    public async Task InvalidSaveAndLaterGuardVetoDoNotDiscardPreparation()
    {
        await using var f = await Fixture.CreateAsync(seed: true); var vm = f.Vm; var editor = vm.Editor!;
        editor.MaximumTrades.Text = "bad"; f.Interaction.Choice = PlanLeaveChoice.Save;
        Assert.False(await vm.SelectContextAsync(vm.SelectedDate.AddDays(1), vm.SelectedAccount)); Assert.Same(editor, vm.Editor);
        f.Interaction.Choice = PlanLeaveChoice.Discard; Assert.True(await vm.PrepareLeaveAsync());
        Assert.Equal("bad", editor.MaximumTrades.Text); vm.ClearLeaveConsent();
        Assert.False(vm.HasLeaveConsent); Assert.Same(editor, vm.Editor); Assert.Equal("bad", editor.MaximumTrades.Text);
        Assert.Equal(0, f.Store.Writes);
    }

    [Fact]
    public async Task DuplicateSaveReadyAndCancellationCannotPublishTwoTransitions()
    {
        await using var f = await Fixture.CreateAsync(seed: true); var vm = f.Vm;
        vm.Editor!.Focus.Text = "Unsaved input during cancellation";
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Store.BeforeWrite = async ct => { entered.SetResult(); await release.Task.WaitAsync(ct); };
        var saving = vm.SaveAsync(); await entered.Task;
        Assert.False(await vm.SaveAsync()); Assert.False(await vm.PrepareLeaveAsync()); Assert.Equal(1, f.Store.Writes);
        await vm.CancelAsync(); Assert.False(await saving); Assert.True(vm.Editor.IsDirty);
        f.Store.BeforeWrite = null; Assert.True(await vm.SaveAsync());
        entered = new(TaskCreationOptions.RunContinuationsAsynchronously); release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Store.BeforeWrite = async ct => { entered.SetResult(); await release.Task.WaitAsync(ct); };
        var ready = vm.MarkReadyAsync(); await entered.Task; await vm.MarkReadyAsync();
        int count = f.Store.Writes; release.SetResult(); await ready;
        Assert.Equal(count, f.Store.Writes); Assert.True(vm.Editor!.IsReadOnly);
        Assert.Single(await f.Store.Inner.ReadyHistoryAsync(vm.Editor.Token.PlanId));
    }

    [Fact]
    public async Task ObsoleteLoadCannotReplaceNewerContextEvenIfReaderIgnoresCancellation()
    {
        await using var f = await Fixture.CreateAsync(seed: true); var vm = f.Vm;
        var first = vm.SelectedDate.AddDays(1); var second = first.AddDays(1);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Store.Read = async (key, ct) => { if (key.NewYorkDate == first) { entered.SetResult(); await release.Task; } return null; };
        var loading = vm.SelectContextAsync(first, vm.SelectedAccount); await entered.Task;
        await vm.SelectContextAsync(second, vm.SelectedAccount); release.SetResult(); await loading;
        Assert.Equal(second, vm.SelectedDate); Assert.Null(vm.Editor); Assert.True(vm.CanCreateDraft); Assert.Equal(0, f.Store.Writes);
    }

    [Fact]
    public async Task ReferenceConflictRecoveryIsExplicitAndDoesNotReplaceInput()
    {
        await using var f = await Fixture.CreateAsync(seed: true); var vm = f.Vm; var editor = vm.Editor!;
        using var other = f.NewWorkspace(); await other.ActivateAsync(); await other.SelectContextAsync(vm.SelectedDate, other.Accounts.Single(a => a.Id == f.Source.Account.Id));
        other.Editor!.News.Text = "other saved change"; Assert.True(await other.SaveAsync());
        editor.Focus.Text = "my text"; Assert.False(await vm.SaveAsync()); Assert.Equal("my text", editor.Focus.Text);
        f.Interaction.Choice = PlanLeaveChoice.Cancel; await vm.ReloadAsync(); Assert.Same(editor, vm.Editor);
        f.Interaction.Choice = PlanLeaveChoice.Discard; await vm.ReloadAsync(); Assert.Equal("other saved change", vm.Editor!.News.Text);
        Assert.NotEqual(editor.Token, vm.Editor.Token);
    }

    [Fact]
    public async Task ShellRetainsInvalidSessionDuringRelatedVisitAndGuardsClosingFromOtherPage()
    {
        await using var f = await Fixture.CreateAsync(seed: true);
        using var shell = MainWindowViewModelTests.CreatePlanShell(f.Vm);
        shell.NavigateCommand.Execute(NavigationDestination.TradingPlan); var editor = f.Vm.Editor!;
        editor.MaximumTrades.Text = "invalid";
        f.Interaction.Choice = PlanLeaveChoice.Cancel; shell.NavigateCommand.Execute(NavigationDestination.Dashboard); await shell.PlanNavigationTask;
        Assert.Equal(NavigationDestination.TradingPlan, shell.CurrentDestination); Assert.Same(editor, f.Vm.Editor);
        f.Interaction.Choice = PlanLeaveChoice.Discard; Assert.True(await shell.PreparePlanCloseAsync());
        shell.CancelPlanClose(); Assert.Equal("invalid", editor.MaximumTrades.Text); Assert.False(shell.CanCloseWindow());
        f.Interaction.Choice = PlanLeaveChoice.Save; Assert.False(await shell.PreparePlanCloseAsync()); Assert.Equal(0, f.Store.Writes);
    }

    [Theory]
    [InlineData(TradingPlanWriteStatus.Conflict)] [InlineData(TradingPlanWriteStatus.StorageFailure)]
    [InlineData(TradingPlanWriteStatus.ReferenceChanged)] [InlineData(TradingPlanWriteStatus.Cancelled)]
    public async Task FailedReadyNeverChangesTheVisibleDraftOrItsFields(TradingPlanWriteStatus status)
    {
        await using var f = await Fixture.CreateAsync(seed: true); var editor = f.Vm.Editor!;
        foreach (var item in editor.Checklist) item.IsChecked = true;
        Assert.True(await f.Vm.SaveAsync()); var token = editor.Token; f.Store.Status = status;
        await f.Vm.MarkReadyAsync(); Assert.Same(editor, f.Vm.Editor); Assert.False(editor.IsReadOnly);
        Assert.Equal(token, editor.Token); Assert.Equal(TradingPlanChecklist.None, editor.Confirmed);
        Assert.Empty(await f.Store.Inner.ReadyHistoryAsync(token.PlanId));
    }

    [Fact]
    public async Task RelatedRulesNavigationKeepsInvalidInputsAndCloseStillGuardsHiddenDraft()
    {
        await using var f = await Fixture.CreateAsync(seed: true); var p = f.Db.Provider;
        var rulesStore = p.GetRequiredService<ITradingRuleStore>(); var reader = p.GetRequiredService<ITradingRuleReader>();
        var writer = new WriteTradingRuleUseCase(rulesStore, TimeProvider.System);
        var rule = (await writer.ExecuteAsync(new(Guid.NewGuid(), 0, TradingRuleAuthoringAction.Create,
            new PersonalTradingJournal.Domain.Rules.TradingRuleDefinition("Synthetic principle")))).Value!;
        var rules = new PersonalTradingJournal.Desktop.ViewModels.Rules.RulesViewModel(reader, rulesStore, writer, new TestDoubles.FakeDialogService());
        using var shell = MainWindowViewModelTests.CreatePlanShell(f.Vm, rules: rules);
        shell.NavigateCommand.Execute(NavigationDestination.TradingPlan);
        var editor = f.Vm.Editor!; await editor.LoadRulesAsync(); editor.RuleCandidate = editor.RuleChoices.Single(r => r.Id == rule.RuleId);
        await editor.LinkRuleAsync(); editor.MaximumTrades.Text = "retained invalid count";
        f.Vm.SelectTab(true);
        await editor.LinkedRules[0].OpenCommand.ExecuteAsync(null); await rules.LoadTask;
        Assert.Equal(NavigationDestination.Rules, shell.CurrentDestination); Assert.Equal(rule.RuleId, rules.SelectedRule!.Id);
        Assert.Same(editor, f.Vm.Editor); Assert.Equal(0, f.Interaction.Prompts); Assert.Equal(0, f.Store.Writes);
        f.Interaction.Choice = PlanLeaveChoice.Cancel; Assert.False(await shell.PreparePlanCloseAsync());
        shell.NavigateCommand.Execute(NavigationDestination.TradingPlan);
        Assert.Same(editor, f.Vm.Editor); Assert.Equal("retained invalid count", editor.MaximumTrades.Text);
        Assert.True(f.Vm.IsHistorySelected);
    }

    [Fact]
    public async Task TypedDestinationDateSavesTheOriginalIdentityOnlyAfterGuardAccepts()
    {
        await using var f = await Fixture.CreateAsync(seed: true); var vm = f.Vm; var key = vm.Editor!.Key;
        vm.Editor.Focus.Text = "original key text"; vm.DateText = "20 Oct 2026"; Assert.False(vm.CanSave);
        f.Interaction.Choice = PlanLeaveChoice.Save; await vm.OpenDateCommand.ExecuteAsync(null);
        Assert.Equal(new DateOnly(2026, 10, 20), vm.SelectedDate); Assert.Null(vm.Editor);
        Assert.Equal("original key text", (await f.Store.Inner.LoadAsync(key))!.WorkingDraft!.Content.Focus);
        Assert.Null(await f.Store.Inner.LoadAsync(new(key.AccountId, vm.SelectedDate)));
    }

    [Fact]
    public async Task InactiveAccountCanReadReadyButCannotCreateAndFailedLoadDoesNotInventAnEmptyPlan()
    {
        await using var f = await Fixture.CreateAsync(seed: true); var vm = f.Vm;
        f.Source.Account.Deactivate(DateTimeOffset.UtcNow); await f.Db.Provider.GetRequiredService<ITradingAccountStore>().UpdateAsync(f.Source.Account);
        await vm.ReloadAsync(); Assert.NotNull(vm.Editor); Assert.False(vm.SelectedAccount!.IsActive);
        await vm.NextDayCommand.ExecuteAsync(null); Assert.False(vm.CanCreateDraft); Assert.Contains("Reopen", vm.EmptyMessage);
        f.Store.Read = (_, _) => throw new IOException("Synthetic private diagnostic not shown");
        await vm.ReloadAsync(); Assert.False(vm.CanCreateDraft); Assert.Contains("could not be loaded", vm.Message);
        Assert.DoesNotContain("Synthetic private", vm.Message); Assert.Equal(0, f.Store.Writes);
    }

    [Fact]
    public async Task LateCancellationPreservesACommittedSuccessAndMissingReadbackIsNeverSuccess()
    {
        await using var f = await Fixture.CreateAsync(seed: true); var vm = f.Vm;
        vm.Editor!.Focus.Text = "committed text";
        f.Store.AfterWrite = result => { vm.Editor.CancelOperation(); return result; };
        Assert.True(await vm.SaveAsync()); Assert.False(vm.Editor.IsDirty); Assert.Equal("Draft saved and verified.", vm.Message);
        vm.Editor.Focus.Text = "unverified text";
        f.Store.AfterWrite = _ => new(TradingPlanWriteStatus.Success); Assert.False(await vm.SaveAsync());
        Assert.True(vm.Editor.IsDirty); Assert.Contains("could not", vm.Message);
    }

    [Fact]
    public async Task DeletedSelectedAccountRetainsExactReadyContextRatherThanSameNameReplacement()
    {
        await using var f = await Fixture.CreateAsync(seed: true); var vm = f.Vm;
        foreach (var item in vm.Editor!.Checklist) item.IsChecked = true;
        Assert.True(await vm.SaveAsync()); await vm.MarkReadyAsync(); var readyId = vm.Editor!.CurrentPlan.LatestReadyRevision!.Id;
        var p = f.Db.Provider;
        Assert.Equal(DeleteTradingAccountResult.Deleted, await new DeleteTradingAccountUseCase(
            p.GetRequiredService<ITradingAccountStore>(), p.GetRequiredService<ITradingAccountDeletionStore>()).ExecuteAsync(f.Source.Account.Id));
        var replacement = new PersonalTradingJournal.Domain.Accounts.TradingAccount(f.Source.Account.Name,
            PersonalTradingJournal.Domain.Accounts.TradingAccountType.Personal, null, null, "EUR", 0, DateTimeOffset.UtcNow);
        await p.GetRequiredService<ITradingAccountStore>().AddAsync(replacement);
        await vm.ReloadAsync();
        Assert.Equal(f.Source.Account.Id, vm.SelectedAccount!.Id); Assert.False(vm.SelectedAccount.IsAvailable);
        Assert.Contains(vm.SelectedAccount, vm.Accounts); Assert.Contains("unavailable", vm.SelectedAccount.Label);
        Assert.Equal(readyId, vm.Editor!.CurrentPlan.LatestReadyRevision!.Id); Assert.Contains("USD", vm.ContextLabel);
        Assert.Contains("saved Account", vm.ContextLabel); Assert.NotEqual(replacement.Id, vm.Editor.Key.AccountId);
        await vm.StartDraftAsync(); Assert.True(vm.Editor.IsReadOnly); Assert.Contains("unavailable", vm.Message);
    }

    internal sealed class Interaction : ITradingPlanInteraction
    {
        public PlanLeaveChoice Choice = PlanLeaveChoice.Cancel; public int Prompts;
        public bool DeleteAccepted; public int DeletePrompts;
        public bool ConfirmDelete(string date, string scope) { DeletePrompts++; return DeleteAccepted; }
        public PlanLeaveChoice ConfirmLeave() { Prompts++; return Choice; }
        public bool ConfirmCurrency(string savedCurrency, string currentCurrency) => false;
    }
    internal sealed class Fixture : IAsyncDisposable
    {
        public JournalSqliteTests.JournalTestDatabase Db = null!;
        public readonly SessionPreparationTests.Fixture Source = new();
        public readonly Interaction Interaction = new();
        public CountingStore Store = null!;
        public TradingPlanWorkspaceViewModel Vm = null!;
        public static async Task<Fixture> CreateAsync(bool seed = false)
        {
            var f = new Fixture { Db = await JournalSqliteTests.JournalTestDatabase.CreateAsync() };
            await f.Db.Provider.GetRequiredService<ITradingAccountStore>().AddAsync(f.Source.Account);
            await f.Db.Provider.GetRequiredService<IInstrumentStore>().AddAsync(f.Source.Instrument);
            f.Store = new(f.Db.Provider.GetRequiredService<ITradingPlanStore>());
            if (seed) Assert.Equal(TradingPlanWriteStatus.Success, (await f.Store.Inner.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, f.Source.Store.Plan.Key, f.Source.Content))).Status);
            f.Vm = f.NewWorkspace(); await f.Vm.ActivateAsync();
            if (seed) await f.Vm.SelectContextAsync(f.Source.Store.Plan.Key.NewYorkDate, f.Vm.Accounts.Single(a => a.Id == f.Source.Account.Id));
            return f;
        }
        public TradingPlanWorkspaceViewModel NewWorkspace(PersonalTradingJournal.Desktop.Screenshots.ITradingPlanImages? images = null) => new(Store, Db.Provider.GetRequiredService<ITradingAccountReader>(),
            Db.Provider.GetRequiredService<IInstrumentReader>(), Db.Provider.GetRequiredService<ITradingPlanReferenceReader>(),
            Db.Provider.GetRequiredService<ITradingSetupReader>(), Db.Provider.GetRequiredService<ITradingSetupPlaybookStore>(),
            Db.Provider.GetRequiredService<ITradingSetupPlaybookHistoryReader>(), Db.Provider.GetRequiredService<ITradingRuleReader>(),
            Db.Provider.GetRequiredService<ITradingRuleStore>(), Interaction, new Clock(), images);
        public async ValueTask DisposeAsync() { await Vm.CancelAsync(); Vm.Dispose(); Source.Vm.Dispose(); await Db.DisposeAsync(); }
        private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 18, 1, 0, 0, TimeSpan.Zero); }
    }
    internal sealed class CountingStore(ITradingPlanStore inner) : ITradingPlanStore
    {
        public ITradingPlanStore Inner => inner;
        public int Writes, Reads;
        public TradingPlanWriteStatus? Status;
        public Func<CancellationToken, Task>? BeforeWrite;
        public Func<TradingPlanWriteResult, TradingPlanWriteResult>? AfterWrite;
        public Func<TradingPlanKey, CancellationToken, Task<TradingPlan?>>? Read;
        public Func<TradingPlanHistoryQuery, DateOnly?, CancellationToken, Task<IReadOnlyList<TradingPlanLoadedState>>>? HistoryRead;
        public Func<TradingPlanRevisionId, CancellationToken, Task<TradingPlanReadyRevision?>>? ReadyRead;
        public async Task<TradingPlanWriteResult> WriteAsync(TradingPlanWriteCommand command, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Writes); if (BeforeWrite is not null) await BeforeWrite(cancellationToken); var result = Status.HasValue ? new TradingPlanWriteResult(Status.Value) : await inner.WriteAsync(command, cancellationToken); return AfterWrite?.Invoke(result) ?? result; }
        public Task<TradingPlan?> LoadAsync(TradingPlanKey key, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Reads); return Read?.Invoke(key, cancellationToken) ?? inner.LoadAsync(key, cancellationToken); }
        public Task<TradingPlanReadyRevision?> LoadReadyAsync(TradingPlanRevisionId id, CancellationToken cancellationToken = default) => ReadyRead?.Invoke(id, cancellationToken) ?? inner.LoadReadyAsync(id, cancellationToken);
        public Task<IReadOnlyList<TradingPlanLoadedState>> HistoryAsync(TradingPlanHistoryQuery query, DateOnly? beforeDate = null, CancellationToken cancellationToken = default) => HistoryRead?.Invoke(query, beforeDate, cancellationToken) ?? inner.HistoryAsync(query, beforeDate, cancellationToken);
        public Task<IReadOnlyList<TradingPlanHistoryAccount>> HistoryAccountsAsync(DateOnly firstDate, DateOnly lastDate, int offset = 0, int limit = 21, CancellationToken cancellationToken = default) => inner.HistoryAccountsAsync(firstDate, lastDate, offset, limit, cancellationToken);
        public Task<IReadOnlyList<TradingPlanReadyRevision>> ReadyHistoryAsync(Guid planId, int? beforeRevision = null, int limit = 20, CancellationToken cancellationToken = default) => inner.ReadyHistoryAsync(planId, beforeRevision, limit, cancellationToken);
    }
}
