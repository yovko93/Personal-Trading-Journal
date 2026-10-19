using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

public sealed class DailyTradingPrinciplesTests
{
    [Fact]
    public async Task GlobalListAndUnsubmittedInputSurviveContextsReadyAndRestartWithoutPlanWrites()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync(seed: true); var vm = f.Vm;
        var editor = vm.Editor!; var token = editor.Token; var dirty = editor.IsDirty;
        vm.Principles.Input = "  Не търгувай ако си уморен  "; await vm.Principles.AddCommand.ExecuteAsync(null);
        Assert.Empty(vm.Principles.Input); Assert.Single(vm.Principles.Rows); Assert.Equal(token, editor.Token); Assert.Equal(dirty, editor.IsDirty);
        vm.PreparationExpanded = vm.ScenariosExpanded = vm.RiskExpanded = vm.Principles.IsExpanded = false;
        vm.Principles.Input = "Unsubmitted input";
        await vm.NextDayCommand.ExecuteAsync(null); Assert.Null(vm.Editor);
        await vm.SelectContextAsync(vm.SelectedDate, vm.Accounts.Single(x => x.Id is null));
        Assert.Single(vm.Principles.Rows); Assert.Equal("Unsubmitted input", vm.Principles.Input);
        Assert.False(vm.PreparationExpanded); Assert.False(vm.ScenariosExpanded); Assert.False(vm.RiskExpanded); Assert.False(vm.Principles.IsExpanded);
        Assert.Equal(0, f.Store.Writes); Assert.Equal(0, f.Interaction.Prompts);
        using var other = f.NewWorkspace(); await other.ActivateAsync();
        Assert.Equal(vm.Principles.Rows, other.Principles.Rows); Assert.True(other.PreparationExpanded);
        await vm.SelectContextAsync(editor.Key.NewYorkDate, vm.Accounts.Single(x => x.Id == editor.Key.AccountId));
        await vm.MarkReadyAsync(); Assert.True(vm.Editor!.IsReadOnly); token = vm.Editor.Token;
        await vm.Principles.AddCommand.ExecuteAsync(null); Assert.Equal(2, vm.Principles.Rows.Count); Assert.Equal(token, vm.Editor.Token);
        await vm.Principles.RemoveCommand.ExecuteAsync(vm.Principles.Rows[0]); Assert.Equal(1, vm.Principles.Rows.Single().Number);
        Assert.Equal(token, vm.Editor.Token); Assert.False(vm.Editor.IsDirty);
    }

    [Fact]
    public async Task InvalidFailedCancelledAndConflictingOperationsKeepInputAndPreventDoubleSubmission()
    {
        var store = new ControlledStore(); using var vm = new DailyTradingPrinciplesViewModel(store); await vm.LoadAsync();
        vm.Input = " \t "; await vm.AddCommand.ExecuteAsync(null); Assert.True(vm.HasError); Assert.Equal(0, store.Writes);
        vm.Input = "Синтетичен текст"; Assert.False(vm.HasError);
        store.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var write = vm.AddCommand.ExecuteAsync(null); Assert.True(vm.IsBusy); Assert.False(vm.AddCommand.CanExecute(null));
        await vm.AddCommand.ExecuteAsync(null); Assert.Equal(1, store.Writes);
        store.Pending.SetResult(new(PrincipleWriteStatus.StorageFailure)); await write;
        Assert.Equal("Синтетичен текст", vm.Input); Assert.Equal(PlanNoticeKind.Error, vm.Notice.Kind); Assert.Empty(vm.Rows);
        store.Pending = null; store.Status = PrincipleWriteStatus.Conflict; await vm.AddCommand.ExecuteAsync(null);
        Assert.Equal(2, store.Reads); Assert.Equal("Синтетичен текст", vm.Input); Assert.Equal(PlanNoticeKind.Warning, vm.Notice.Kind);
        store.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously); write = vm.AddCommand.ExecuteAsync(null);
        await vm.CancelAsync(); await write; Assert.Equal("Синтетичен текст", vm.Input); Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task InitialReminderLoadBelongsToPublicContextTaskAndCancellationDrainsIt()
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync();
        var store = new ControlledStore { ReadPending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var vm = f.NewWorkspace(principles: store);
        int planReads = 0; f.Store.Read = (_, _) => { planReads++; return Task.FromResult<PersonalTradingJournal.Domain.TradingPlans.TradingPlan?>(null); };
        var activation = vm.ActivateAsync();
        try { Assert.False(vm.ContextTask.IsCompleted); }
        finally { await vm.CancelAsync(); await activation; }
        Assert.False(vm.IsBusy); Assert.False(vm.Principles.IsBusy);
        Assert.Equal(0, planReads); Assert.Equal(0, f.Store.Writes);
    }

    private sealed class ControlledStore : IDailyTradingPrincipleStore
    {
        public int Reads, Writes;
        public TaskCompletionSource<DailyTradingPrinciples>? ReadPending;
        public TaskCompletionSource<PrincipleWriteResult>? Pending;
        public PrincipleWriteStatus Status;
        public Task<DailyTradingPrinciples> ReadAsync(CancellationToken cancellationToken = default)
        { Reads++; return ReadPending?.Task.WaitAsync(cancellationToken) ?? Task.FromResult(new DailyTradingPrinciples(0, [])); }
        public Task<PrincipleWriteResult> AddAsync(string text, long expectedVersion, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Writes); return Pending?.Task.WaitAsync(cancellationToken) ?? Task.FromResult(new PrincipleWriteResult(Status)); }
        public Task<PrincipleWriteResult> RemoveAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) => AddAsync("", expectedVersion, cancellationToken);
    }
}
