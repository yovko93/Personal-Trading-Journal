using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;
using Xunit.Abstractions;

namespace PersonalTradingJournal.Desktop.Tests.TradingPlans;

public sealed class DailyTradingPrinciplesTests(ITestOutputHelper output)
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
        try
        {
            // Busy is set before Task.Run enters the store. Observe actual entry,
            // not thread-pool scheduling speed, before checking duplicate submission.
            await store.WriteEntered.Task;
            await vm.AddCommand.ExecuteAsync(null); Assert.Equal(1, store.Writes);
        }
        finally
        {
            store.Pending.TrySetResult(new(PrincipleWriteStatus.StorageFailure));
            await write;
        }
        Assert.Equal("Синтетичен текст", vm.Input); Assert.Equal(PlanNoticeKind.Error, vm.Notice.Kind); Assert.Empty(vm.Rows);
        store.Pending = null; store.Status = PrincipleWriteStatus.Conflict; await vm.AddCommand.ExecuteAsync(null);
        Assert.Equal(2, store.Reads); Assert.Equal("Синтетичен текст", vm.Input); Assert.Equal(PlanNoticeKind.Warning, vm.Notice.Kind);
        store.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously); write = vm.AddCommand.ExecuteAsync(null);
        await vm.CancelAsync(); await write; Assert.Equal("Синтетичен текст", vm.Input); Assert.False(vm.IsBusy);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("leave")]
    [InlineData("dispose")]
    public async Task InitialReminderLoadBelongsToPublicContextTaskAndCancellationDrainsIt(string operation)
    {
        await using var f = await TradingPlanWorkspaceTests.Fixture.CreateAsync();
        using var context = new ControlledContext();
        using var store = new CancellationInterleavingStore();
        var accounts = new CountingAccounts(f.Db.Provider.GetRequiredService<ITradingAccountReader>());
        using var vm = f.NewWorkspace(principles: store, accounts: accounts);
        int planReads = 0;
        f.Store.Read = (_, _) => { Interlocked.Increment(ref planReads); return Task.FromResult<PersonalTradingJournal.Domain.TradingPlans.TradingPlan?>(null); };
        var activation = context.Start(vm.ActivateAsync);
        var owner = vm.ContextTask;
        try
        {
            await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Same(activation, owner); Assert.False(owner.IsCompleted);
            Assert.True(vm.IsBusy); Assert.True(vm.Principles.IsBusy);
            // Hold child cancellation open while its completion releases every
            // captured context continuation. This exercises the child/owner gap,
            // rather than merely waiting for ReadAsync to enter.
            store.OnCancellation = () => context.DrainUntil(owner);
            if (operation == "cancel") await vm.CancelAsync().WaitAsync(TimeSpan.FromSeconds(30));
            else if (operation == "leave") Assert.True(await vm.PrepareLeaveAsync().WaitAsync(TimeSpan.FromSeconds(30)));
            else vm.Dispose();
            context.DrainUntil(owner);
            await activation.WaitAsync(TimeSpan.FromSeconds(30));
            output.WriteLine($"{operation}: child callbacks={store.Cancellations}, Plan reads={Volatile.Read(ref planReads)}, Account reads={accounts.Reads}, owner completed={owner.IsCompleted}");
            Assert.Equal(1, store.Cancellations);
            Assert.Equal(0, Volatile.Read(ref planReads)); Assert.Equal(0, f.Store.Writes);
            Assert.Equal(0, accounts.Reads); Assert.Equal(0, store.Writes);
            Assert.True(owner.IsCompletedSuccessfully); Assert.True(store.Pending.IsCompleted);
            Assert.Equal(0, context.Pending);
            Assert.False(vm.IsBusy); Assert.False(vm.Principles.IsBusy); Assert.Null(vm.Editor);

            await vm.CancelAsync().WaitAsync(TimeSpan.FromSeconds(30));
            await vm.CancelAsync().WaitAsync(TimeSpan.FromSeconds(30)); // Idle/repeated cancellation is safe.
            if (operation != "dispose")
            {
                await vm.ActivateAsync().WaitAsync(TimeSpan.FromSeconds(30));
                Assert.True(vm.ContextTask.IsCompletedSuccessfully);
                Assert.Equal(1, Volatile.Read(ref planReads)); Assert.Equal(1, accounts.Reads);
                Assert.Equal(2, store.Reads); Assert.Equal(0, f.Store.Writes);
                Assert.False(vm.IsBusy); Assert.False(vm.Principles.IsBusy);
                Assert.Contains(vm.Accounts, a => a.Id == f.Source.Account.Id);
            }
        }
        finally
        {
            store.OnCancellation = null;
            store.Release();
            context.DrainUntil(activation);
            await vm.CancelAsync().WaitAsync(TimeSpan.FromSeconds(30));
            await activation.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    private sealed class ControlledContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
        public int Pending => _queue.Count;
        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));
        public Task Start(Func<Task> action)
        {
            var previous = Current; SetSynchronizationContext(this);
            try { return action(); } finally { SetSynchronizationContext(previous); }
        }
        public void DrainUntil(Task completion)
        {
            var previous = Current; SetSynchronizationContext(this);
            var elapsed = Stopwatch.StartNew();
            try
            {
                while (!completion.IsCompleted)
                {
                    var remaining = TimeSpan.FromSeconds(30) - elapsed.Elapsed;
                    if (remaining <= TimeSpan.Zero || !_queue.TryTake(out var work, remaining))
                        throw new TimeoutException("Context continuation did not complete within the existing 30-second workflow bound.");
                    work.Callback(work.State);
                }
            }
            finally { SetSynchronizationContext(previous); }
        }
        public void Dispose() => _queue.Dispose();
    }

    private sealed class CancellationInterleavingStore : IDailyTradingPrincipleStore, IDisposable
    {
        private readonly TaskCompletionSource<DailyTradingPrinciples> _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration _registration;
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Action? OnCancellation;
        public int Reads, Writes, Cancellations;
        public Task Pending => _pending.Task;
        public Task<DailyTradingPrinciples> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref Reads) != 1) return Task.FromResult(new DailyTradingPrinciples(0, []));
            _registration = cancellationToken.Register(() =>
            {
                Interlocked.Increment(ref Cancellations);
                _pending.TrySetCanceled(cancellationToken);
                OnCancellation?.Invoke();
            });
            Entered.TrySetResult();
            return _pending.Task;
        }
        public Task<PrincipleWriteResult> AddAsync(string text, long expectedVersion, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Writes); throw new InvalidOperationException("Read-only activation must not write Principles."); }
        public Task<PrincipleWriteResult> RemoveAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) => AddAsync("", expectedVersion, cancellationToken);
        public void Release() => _pending.TrySetResult(new DailyTradingPrinciples(0, []));
        public void Dispose() { Release(); _registration.Dispose(); }
    }

    private sealed class CountingAccounts(ITradingAccountReader inner) : ITradingAccountReader
    {
        public int Reads;
        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Reads); return inner.GetAllAsync(cancellationToken); }
        public Task<TradingAccountDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Reads); return inner.GetByIdAsync(id, cancellationToken); }
    }

    private sealed class ControlledStore : IDailyTradingPrincipleStore
    {
        public int Reads, Writes;
        public TaskCompletionSource WriteEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<PrincipleWriteResult>? Pending;
        public PrincipleWriteStatus Status;
        public Task<DailyTradingPrinciples> ReadAsync(CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Reads); return Task.FromResult(new DailyTradingPrinciples(0, [])); }
        public Task<PrincipleWriteResult> AddAsync(string text, long expectedVersion, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Writes); WriteEntered.TrySetResult(); return Pending?.Task.WaitAsync(cancellationToken) ?? Task.FromResult(new PrincipleWriteResult(Status)); }
        public Task<PrincipleWriteResult> RemoveAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) => AddAsync("", expectedVersion, cancellationToken);
    }
}
