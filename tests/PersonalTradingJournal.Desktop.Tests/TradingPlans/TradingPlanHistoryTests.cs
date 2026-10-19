using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

public sealed class TradingPlanHistoryTests
{
    internal static async Task<TradingPlanReadyRevision> Ready(TradingPlanWorkspaceTests.Fixture f, string focus = "Frozen focus")
    {
        var vm = f.Vm;
        if (vm.Editor!.IsReadOnly) await vm.StartDraftAsync();
        vm.Editor!.Focus.Text = focus; foreach (var check in vm.Editor.Checklist) check.IsChecked = true;
        Assert.True(await vm.SaveAsync()); await vm.MarkReadyAsync();
        return Assert.IsType<TradingPlanReadyRevision>(vm.Editor!.CurrentPlan.LatestReadyRevision);
    }
    internal static void Filters(TradingPlanWorkspaceViewModel vm)
    { vm.SelectTab(true); vm.HistoryAccount = vm.Accounts.First(a => a.Id.HasValue); vm.HistoryFrom = "01 Oct 2026"; vm.HistoryTo = "31 Oct 2026"; }
    internal static async Task View(TradingPlanWorkspaceViewModel vm, int revision)
    {
        await vm.SearchHistoryAsync(); await vm.ShowRevisionsAsync(vm.HistoryRows.Single());
        await vm.ViewRevisionAsync(vm.RevisionRows.Single(r => r.Revision.Id.Revision == revision));
        Assert.True(vm.HasHistoricalViewer);
    }

    [Theory] [InlineData(20)] [InlineData(21)] [InlineData(40)]
    public async Task InclusiveExactScopePagesUseLookaheadWithoutEmptyFinalPage(int count)
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(); var store = f.Store.Inner;
        var first = new DateOnly(2026, 10, 1);
        for (int i = 0; i < count; i++) Assert.Equal(TradingPlanWriteStatus.Success,
            (await store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, new(f.Source.Account.Id, first.AddDays(i)), new()))).Status);
        var vm = f.Vm; Filters(vm); vm.HistoryTo = first.AddDays(count - 1).ToString("yyyy-MM-dd"); await vm.SearchHistoryAsync();
        Assert.Equal(20, vm.HistoryRows.Count); Assert.Equal(count > 20, vm.HasOlderPlans);
        Assert.Equal(first.AddDays(count - 1), vm.HistoryRows[0].State.Key.NewYorkDate);
        if (count > 20)
        {
            await vm.OlderPlansCommand.ExecuteAsync(null);
            Assert.Equal(first.AddDays(count - 21), vm.HistoryRows[0].State.Key.NewYorkDate);
            Assert.Equal(count - 20, vm.HistoryRows.Count); Assert.False(vm.HasOlderPlans);
            await vm.NewerPlansCommand.ExecuteAsync(null); Assert.Equal(20, vm.HistoryRows.Count);
        }
        vm.HistoryFrom = first.AddDays(1).ToString("yyyy-MM-dd"); vm.HistoryTo = vm.HistoryFrom;
        Assert.True(vm.HasUnappliedHistory); Assert.False(vm.OlderPlansCommand.CanExecute(null));
        await vm.SearchHistoryAsync(); Assert.Equal(first.AddDays(1), Assert.Single(vm.HistoryRows).State.Key.NewYorkDate);
        vm.HistoryAccount = new(Guid.NewGuid(), "Same label", "USD", true); await vm.SearchHistoryAsync(); Assert.Empty(vm.HistoryRows);
        Assert.Equal(0, f.Store.Writes); // Only explicit seeding via the inner store wrote.
    }

    [Fact]
    public async Task InvalidFiltersAndTabsRetainRawEditorFieldsTokenAndSelection()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true); var vm = f.Vm; var editor = vm.Editor!;
        editor.MaximumTrades.Text = "invalid count"; var selected = editor.SelectedInstrument; var token = editor.Token;
        Filters(vm); await vm.SearchHistoryAsync(); var rows = vm.HistoryRows;
        vm.HistoryFrom = "not a date"; await vm.SearchHistoryAsync();
        Assert.Equal("not a date", vm.HistoryFrom); Assert.NotEmpty(vm.HistoryError); Assert.Same(rows, vm.HistoryRows);
        vm.SelectTab(false); vm.SelectTab(true); await vm.ActivateAsync();
        Assert.Same(editor, vm.Editor); Assert.Same(selected, editor.SelectedInstrument); Assert.Equal(token, editor.Token);
        Assert.Equal("invalid count", editor.MaximumTrades.Text); Assert.True(vm.NeedsClosePreparation);
        f.Interaction.Choice = PlanLeaveChoice.Cancel; Assert.False(await vm.PrepareLeaveAsync());
        Assert.Same(editor, vm.Editor); Assert.Equal(0, f.Store.Writes);
    }

    [Fact]
    public async Task OpeningAnotherHistoryPlanHonorsVetoWithoutNormalizingRetainedRawDate()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true);
        var vm = f.Vm; var editor = vm.Editor!; var token = editor.Token;
        editor.Focus.Text = "unsaved preparation"; vm.DateText = "malformed retained date";
        vm.SelectTab(true); f.Interaction.Choice = PlanLeaveChoice.Cancel;
        await vm.OpenHistoryPlanAsync(new(editor.Key.AccountId, editor.Key.NewYorkDate.AddDays(1)));
        Assert.Same(editor, vm.Editor); Assert.Equal(token, editor.Token); Assert.True(vm.IsHistorySelected);
        Assert.Equal("malformed retained date", vm.DateText); Assert.Equal("unsaved preparation", editor.Focus.Text);
        Assert.Equal(0, f.Store.Writes);
    }

    [Fact]
    public async Task OpeningHistoryPlanReportsAReadFailureInTheStillVisibleHistoryWorkspace()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true); Filters(f.Vm); await f.Vm.SearchHistoryAsync();
        var rows = f.Vm.HistoryRows;
        f.Store.Read = (_, _) => throw new IOException("Private synthetic path detail must not surface");
        await f.Vm.OpenHistoryPlanAsync(new(f.Source.Account.Id, new(2026, 10, 18)));
        Assert.True(f.Vm.IsHistorySelected); Assert.Same(rows, f.Vm.HistoryRows);
        Assert.Contains("could not be loaded", f.Vm.HistoryMessage); Assert.DoesNotContain("Private", f.Vm.HistoryMessage);
        Assert.False(f.Vm.CanCreateDraft); Assert.Equal(0, f.Store.Writes);
    }

    [Fact]
    public async Task HistoricalViewerIsExactFrozenReadOnlyAndDoesNotTouchPendingDraft()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true);
        var first = await Ready(f, "First exact revision"); await Ready(f, "Second exact revision"); await f.Vm.StartDraftAsync();
        var editor = f.Vm.Editor!; editor.Focus.Text = "Pending third"; editor.RiskPerTrade.Text = "invalid";
        f.Source.Account.UpdateDetails("Renamed current Account", f.Source.Account.AccountType, null, null, "EUR", 1, DateTimeOffset.UtcNow);
        await f.Db.Provider.GetRequiredService<ITradingAccountStore>().UpdateAsync(f.Source.Account);
        Filters(f.Vm); await View(f.Vm, 1); var viewer = f.Vm.HistoricalViewer!;
        Assert.True(viewer.IsReadOnly); Assert.Equal(first.Content.Focus, viewer.Focus.Text);
        Assert.Contains(first.References.Account.Name, f.Vm.HistoricalLabel); Assert.Contains("USD", f.Vm.HistoricalLabel);
        Assert.DoesNotContain("Renamed current", f.Vm.HistoricalLabel);
        viewer.Focus.Text = "Cannot change"; Assert.Equal(first.Content.Focus, viewer.Focus.Text);
        Assert.Equal(TradingPlanChecklist.All, viewer.Confirmed); Assert.Same(editor, f.Vm.Editor);
        Assert.Equal("invalid", editor.RiskPerTrade.Text);
        var persisted = (await f.Store.Inner.LoadReadyAsync(first.Id))!;
        Assert.Equal(first.Id, persisted.Id); Assert.Equal(first.ReadyAtUtc, persisted.ReadyAtUtc);
        Assert.True(SessionPreparationViewModel.SameContent(first.Content, persisted.Content));
    }

    [Fact]
    public async Task StartHistoricalUsesExactRevisionThenConflictsInsteadOfReplacingDraft()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true);
        var first = await Ready(f, "First revision"); await Ready(f, "Second revision");
        Filters(f.Vm); await View(f.Vm, 1); await f.Vm.TransferAsync(false);
        Assert.False(f.Vm.IsHistorySelected); var draft = f.Vm.Editor!.CurrentPlan.WorkingDraft!;
        Assert.Equal(first.Id, draft.DerivedFromRevision); Assert.Equal(first.Content.Focus, draft.Content.Focus);
        Assert.Equal(TradingPlanChecklist.None, draft.Content.Confirmed); Assert.Equal(2, f.Vm.Editor.CurrentPlan.LatestReadyRevision!.Id.Revision);
        Filters(f.Vm); await View(f.Vm, 1); await f.Vm.TransferAsync(false);
        Assert.True(f.Vm.HasRecoveryTarget); Assert.Contains("already has", f.Vm.HistoryMessage);
        var token = f.Vm.Editor.Token; await f.Vm.OpenRecoveryCommand.ExecuteAsync(null);
        Assert.False(f.Vm.IsHistorySelected); Assert.Equal(token, f.Vm.Editor.Token);
        Assert.Equal(2, (await f.Store.Inner.ReadyHistoryAsync(first.Id.PlanId)).Count);
    }

    [Fact]
    public async Task ConflictRecoveryRereadsAnAlreadySelectedPlanWhenAnotherWriterStartedItsDraft()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true); var ready = await Ready(f);
        var vm = f.Vm; Filters(vm); await View(vm, 1); var oldEditor = vm.Editor!;
        var changed = await f.Store.Inner.WriteAsync(new(TradingPlanWriteOperation.StartDraft, ready.Key, ready.Content, oldEditor.Token, ready.Id));
        Assert.Equal(TradingPlanWriteStatus.Success, changed.Status);
        await vm.TransferAsync(false); Assert.True(vm.HasRecoveryTarget); Assert.Same(oldEditor, vm.Editor);
        await vm.OpenRecoveryCommand.ExecuteAsync(null);
        Assert.False(vm.IsHistorySelected); Assert.False(vm.Editor!.IsReadOnly);
        Assert.Equal(changed.Plan!.Version, vm.Editor.Token.Version); Assert.NotSame(oldEditor, vm.Editor);
    }

    [Fact]
    public async Task CopyResetsDailyContextKeepsSourceAndSurvivesNewServiceScope()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true); var source = await Ready(f);
        Filters(f.Vm); await View(f.Vm, 1); f.Vm.CopyDate = "18 Oct 2026"; await f.Vm.TransferAsync(true);
        Assert.False(f.Vm.IsHistorySelected); var target = f.Vm.Editor!.CurrentPlan;
        Assert.NotEqual(source.Id.PlanId, target.Id); Assert.Null(target.LatestReadyRevision);
        Assert.True(SessionPreparationViewModel.SameContent(source.Content.CopyForward(), target.WorkingDraft!.Content));
        Assert.Equal(source.Id, (await f.Store.Inner.LoadAsync(source.Key))!.LatestReadyRevision!.Id);
        await using var scope = f.Db.Provider.CreateAsyncScope(); var store = scope.ServiceProvider.GetRequiredService<ITradingPlanStore>();
        Assert.True(SessionPreparationViewModel.SameContent(target.WorkingDraft.Content, (await store.LoadAsync(target.Key))!.WorkingDraft!.Content));
        using var restarted = f.NewWorkspace(); await restarted.ActivateAsync(); Filters(restarted); await restarted.SearchHistoryAsync();
        Assert.Equal(2, restarted.HistoryRows.Count); Assert.Contains(restarted.HistoryRows, r => r.Lifecycle == "Draft"); Assert.Contains(restarted.HistoryRows, r => r.Lifecycle == "Ready");
        await restarted.ShowRevisionsAsync(restarted.HistoryRows.Single(r => r.State.Key == source.Key)); await restarted.ViewRevisionAsync(restarted.RevisionRows.Single());
        Assert.Equal(source.Content.Focus, restarted.HistoricalViewer!.Focus.Text);
    }

    [Theory] [InlineData(TradingPlanWriteStatus.Cancelled)] [InlineData(TradingPlanWriteStatus.StorageFailure)]
    [InlineData(TradingPlanWriteStatus.ReferenceChanged)] [InlineData(TradingPlanWriteStatus.ReferenceUnavailable)]
    public async Task FailedTransferKeepsAllPendingInputAndExactSource(TradingPlanWriteStatus status)
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true); await Ready(f);
        Filters(f.Vm); await View(f.Vm, 1); await f.Vm.StartDraftAsync();
        var editor = f.Vm.Editor!; editor.MaximumTrades.Text = "retained invalid";
        f.Interaction.Choice = PlanLeaveChoice.Discard; f.Store.Status = status;
        f.Vm.CopyDate = "20 Oct 2026"; await f.Vm.TransferAsync(true);
        Assert.Same(editor, f.Vm.Editor); Assert.Equal("retained invalid", editor.MaximumTrades.Text);
        Assert.True(f.Vm.HasHistoricalViewer); Assert.Equal("20 Oct 2026", f.Vm.CopyDate);
        Assert.Null(await f.Store.Inner.LoadAsync(new(f.Source.Account.Id, new(2026, 10, 20))));
        Assert.False(f.Vm.HasLeaveConsent); Assert.False(f.Vm.HistoryMessage.StartsWith("Copied Draft", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConcurrentTargetAndDuplicateSubmissionNeverOverwriteOrRetarget()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true); var source = await Ready(f);
        Filters(f.Vm); await View(f.Vm, 1); f.Vm.CopyDate = "19 Oct 2026";
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Store.BeforeWrite = async ct => { entered.SetResult(); await release.Task.WaitAsync(ct); };
        int writes = f.Store.Writes; var copy = f.Vm.TransferAsync(true); await entered.Task;
        f.Vm.CopyDate = "20 Oct 2026"; await f.Vm.TransferAsync(true); Assert.Equal(writes + 1, f.Store.Writes);
        Assert.Equal("19 Oct 2026", f.Vm.CopyDate); Assert.False(await f.Vm.SelectContextAsync(new(2026, 10, 21), f.Vm.SelectedAccount));
        var target = new TradingPlanKey(source.Key.AccountId, new(2026, 10, 19));
        var competing = await f.Store.Inner.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, target, new(focus: "competing Draft")));
        release.SetResult(); await copy;
        Assert.True(f.Vm.HasRecoveryTarget); Assert.Contains("already exists", f.Vm.HistoryMessage);
        Assert.Equal("competing Draft", (await f.Store.Inner.LoadAsync(target))!.WorkingDraft!.Content.Focus);
        await f.Vm.OpenRecoveryCommand.ExecuteAsync(null); Assert.Equal(competing.Plan!.Id, f.Vm.Editor!.Token.PlanId);
    }

    [Fact]
    public async Task CancellationDrainsTransferAndNoTargetIsCreated()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true); await Ready(f); Filters(f.Vm); await View(f.Vm, 1);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Store.BeforeWrite = async ct => { entered.SetResult(); await Task.Delay(Timeout.Infinite, ct); };
        f.Vm.CopyDate = "22 Oct 2026"; var copying = f.Vm.TransferAsync(true); await entered.Task;
        await f.Vm.CancelAsync(); await copying;
        Assert.False(f.Vm.IsBusy); Assert.True(f.Vm.HasHistoricalViewer); Assert.Contains("cancelled", f.Vm.HistoryMessage);
        Assert.Null(await f.Store.Inner.LoadAsync(new(f.Source.Account.Id, new(2026, 10, 22))));
    }

    [Fact]
    public async Task LateHistoryAndRevisionReadsCannotReplaceNewerSelection()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true); await Ready(f, "one"); await Ready(f, "two"); Filters(f.Vm);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Store.HistoryRead = async (q, c, _) => { entered.SetResult(); await release.Task; return await f.Store.Inner.HistoryAsync(q, c); };
        var first = f.Vm.SearchHistoryAsync(); await entered.Task; f.Store.HistoryRead = null;
        f.Vm.HistoryTo = "01 Oct 2026"; await f.Vm.SearchHistoryAsync(); Assert.Empty(f.Vm.HistoryRows);
        release.SetResult(); await first; Assert.Empty(f.Vm.HistoryRows);
        Filters(f.Vm); await f.Vm.SearchHistoryAsync(); await f.Vm.ShowRevisionsAsync(f.Vm.HistoryRows.Single());
        entered = new(TaskCreationOptions.RunContinuationsAsynchronously); release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Store.ReadyRead = async (id, _) => { entered.SetResult(); await release.Task; return await f.Store.Inner.LoadReadyAsync(id); };
        var old = f.Vm.ViewRevisionAsync(f.Vm.RevisionRows.Single(r => r.Revision.Id.Revision == 1)); await entered.Task;
        f.Store.ReadyRead = null; await f.Vm.ViewRevisionAsync(f.Vm.RevisionRows.Single(r => r.Revision.Id.Revision == 2));
        release.SetResult(); await old; Assert.Equal("two", f.Vm.HistoricalViewer!.Focus.Text);
    }

    [Fact]
    public async Task DeletedAccountDiscoveryAfterRestartKeepsFrozenIdentityAndInactivePlansRemainReadable()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true); var source = await Ready(f);
        var p = f.Db.Provider;
        Assert.Equal(DeleteTradingAccountResult.Deleted, await new DeleteTradingAccountUseCase(p.GetRequiredService<ITradingAccountStore>(), p.GetRequiredService<ITradingAccountDeletionStore>()).ExecuteAsync(f.Source.Account.Id));
        var replacement = new PersonalTradingJournal.Domain.Accounts.TradingAccount(f.Source.Account.Name, PersonalTradingJournal.Domain.Accounts.TradingAccountType.Personal, null, null, "EUR", 0, DateTimeOffset.UtcNow);
        await p.GetRequiredService<ITradingAccountStore>().AddAsync(replacement);
        using var restarted = f.NewWorkspace(); await restarted.ActivateAsync(); restarted.SelectTab(true);
        restarted.HistoryFrom = "17 Oct 2026"; restarted.HistoryTo = "17 Oct 2026"; await restarted.DiscoverAccountsAsync();
        var historical = restarted.HistoryAccounts.Single(a => a.Id == source.Key.AccountId);
        Assert.False(historical.IsAvailable); Assert.Contains("USD", historical.Label); Assert.Equal(3, restarted.HistoryAccounts.Count);
        restarted.HistoryAccount = historical; await restarted.SearchHistoryAsync(); var row = Assert.Single(restarted.HistoryRows);
        Assert.Contains("unavailable", row.Account); Assert.Equal(source.References.Account.Name, row.State.Summary!.AccountName);
        await restarted.ShowRevisionsAsync(row); await restarted.ViewRevisionAsync(restarted.RevisionRows.Single());
        Assert.Equal(source.Content.Focus, restarted.HistoricalViewer!.Focus.Text);
        restarted.CopyDate = "18 Oct 2026"; await restarted.TransferAsync(true); Assert.Contains("unavailable", restarted.HistoryMessage);
        Assert.True(restarted.HasHistoricalViewer);
    }

    [Fact]
    public async Task HistoryStatusAndRevisionPagingRetainOriginalBoundaries()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true);
        for (int i = 1; i <= 21; i++) await Ready(f, $"revision {i}");
        Filters(f.Vm); f.Vm.HistoryStatus = TradingPlanStatus.Draft; await f.Vm.SearchHistoryAsync(); Assert.Empty(f.Vm.HistoryRows);
        f.Vm.HistoryStatus = TradingPlanStatus.Ready; await f.Vm.SearchHistoryAsync(); await f.Vm.ShowRevisionsAsync(f.Vm.HistoryRows.Single());
        Assert.Equal(20, f.Vm.RevisionRows.Count); Assert.True(f.Vm.HasOlderRevisions);
        await f.Vm.OlderRevisionsCommand.ExecuteAsync(null); Assert.Equal(1, Assert.Single(f.Vm.RevisionRows).Revision.Id.Revision); Assert.False(f.Vm.HasOlderRevisions);
        await f.Vm.NewerRevisionsCommand.ExecuteAsync(null); Assert.Equal(21, f.Vm.RevisionRows[0].Revision.Id.Revision);
        await f.Vm.StartDraftAsync(); f.Vm.HistoryStatus = TradingPlanStatus.Draft; await f.Vm.SearchHistoryAsync();
        Assert.Equal("Draft after Ready", Assert.Single(f.Vm.HistoryRows).Lifecycle);
    }

    [Fact]
    public async Task BadCopyDateAndMissingVerifiedReadbackNeverReportSuccess()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(true); await Ready(f); Filters(f.Vm); await View(f.Vm, 1);
        int count = f.Store.Writes; f.Vm.CopyDate = "broken"; await f.Vm.TransferAsync(true);
        Assert.Equal("broken", f.Vm.CopyDate); Assert.NotEmpty(f.Vm.CopyError); Assert.Equal(count, f.Store.Writes);
        var editor = f.Vm.Editor; f.Vm.CopyDate = "23 Oct 2026"; f.Store.AfterWrite = _ => new(TradingPlanWriteStatus.Success);
        await f.Vm.TransferAsync(true); Assert.Same(editor, f.Vm.Editor); Assert.True(f.Vm.HasRecoveryTarget);
        Assert.Contains("could not be verified", f.Vm.HistoryMessage);
    }
}
