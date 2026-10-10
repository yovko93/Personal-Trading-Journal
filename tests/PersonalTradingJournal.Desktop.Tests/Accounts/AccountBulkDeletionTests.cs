using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Accounts;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.Accounts;

public sealed class AccountBulkDeletionTests
{
    [Theory]
    [InlineData("cancel", 0, 0)]
    [InlineData("zero", 0, 0)]
    [InlineData("missing", 0, 0)]
    [InlineData("changed", 1, 0)]
    [InlineData("failure", 1, 0)]
    [InlineData("success", 1, 1)]
    [InlineData("cleanup", 1, 1)]
    public async Task ConfirmationAndOutcomesRespectExactAccountAndCommitBoundary(string scenario, int writes, int notifications)
    {
        var store = new Store();
        var files = new FakeTradeScreenshotFileStorage();
        var dialogs = new FakeDialogService { ConfirmationResult = scenario != "cancel" };
        if (scenario == "zero") store.Plan = store.Plan! with { TradeCount = 0 };
        if (scenario == "missing") store.Plan = null;
        if (scenario == "changed") store.Status = AccountTradeDeletionStatus.Changed;
        if (scenario == "failure") store.Fail = true;
        if (scenario == "cleanup") files.DeleteException = new IOException("Sensitive path should not appear.");
        var vm = Create(store, dialogs, files);
        await vm.EnsureLoadedAsync();
        int committed = 0; vm.TradeDataCommitted += (_, _) => committed++;
        await vm.DeleteAllTradesCommand.ExecuteAsync(store.Account);
        Assert.Equal(writes, store.Deletes);
        Assert.Equal(notifications, committed);
        Assert.Equal(store.Account, store.PreparedAccount);
        Assert.False(vm.IsDeleting);
        if (scenario is not ("zero" or "missing"))
        {
            Assert.Contains("Synthetic exact Account", dialogs.ConfirmationRequest!.Message);
            Assert.Contains("2 Trades", dialogs.ConfirmationRequest.Message);
            Assert.Contains("importing the same source rows again", dialogs.ConfirmationRequest.Message);
            Assert.Contains("Journals", dialogs.ConfirmationRequest.Message);
            Assert.True(dialogs.ConfirmationRequest.IsDestructive);
            Assert.Equal("Delete All Trades", dialogs.ConfirmationRequest.ConfirmButtonText);
        }
        else Assert.Null(dialogs.ConfirmationRequest);
        if (scenario == "changed") Assert.Contains("Refresh and confirm again", vm.ActionErrorMessage);
        if (scenario == "cleanup")
        {
            Assert.Contains("cleanup is incomplete", dialogs.InformationRequest!.Message);
            Assert.DoesNotContain("Sensitive", dialogs.InformationRequest.Message);
            Assert.Equal(CancellationToken.None, files.DeleteCancellationToken);
        }
        Assert.Equal(notifications, files.DeleteCallCount);
        if (writes > 0) Assert.Equal(store.Plan, store.Confirmed);
    }

    [Fact]
    public async Task PreparingAndDeletingDisableBothDestructiveActionsAndIgnoreOverlappingInvocation()
    {
        var store = new Store { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var vm = Create(store, new FakeDialogService { ConfirmationResult = true }, new());
        await vm.EnsureLoadedAsync();
        var pending = vm.DeleteAllTradesCommand.ExecuteAsync(store.Account);
        await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(vm.IsDeleting);
        Assert.False(vm.DeleteAccountCommand.CanExecute(store.Account));
        Assert.False(vm.DeleteAllTradesCommand.CanExecute(store.Account));
        await vm.DeleteAllTradesCommand.ExecuteAsync(Guid.NewGuid());
        Assert.Equal(1, store.Prepares);
        store.Pending.SetResult(store.Plan);
        await pending;
        Assert.Equal(1, store.Deletes);
    }

    internal static AccountsViewModel Create(Store store, IDialogService dialogs, FakeTradeScreenshotFileStorage files)
    {
        var reader = new FakeTradingAccountReader();
        var account = new AccountListItem(store.Account, "Synthetic exact Account", TradingAccountType.Personal, null, null, "USD", 0m, true);
        reader.EnqueueResult([account]); reader.EnqueueResult([account]);
        var accounts = new FakeTradingAccountStore();
        var clock = new FixedTimeProvider();
        return new(reader, new(accounts, clock), new(accounts, clock), new(reader), new(accounts, clock),
            new(accounts, new FakeTradingAccountDeletionStore()), dialogs, new(store, files));
    }

    internal sealed class Store : IAccountTradeDeletionStore
    {
        internal readonly Guid Account = Guid.NewGuid();
        internal AccountTradeDeletionPlan? Plan;
        internal Store() => Plan = new(Account, "Synthetic exact Account", 2, "synthetic-version");
        internal bool Fail;
        internal int Prepares, Deletes;
        internal Guid PreparedAccount;
        internal AccountTradeDeletionPlan? Confirmed;
        internal AccountTradeDeletionStatus Status = AccountTradeDeletionStatus.Deleted;
        internal TaskCompletionSource<AccountTradeDeletionPlan?>? Pending;
        internal TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<AccountTradeDeletionPlan?> PrepareAsync(Guid id, CancellationToken token)
        { Prepares++; PreparedAccount = id; Started.TrySetResult(); return Pending?.Task ?? Task.FromResult(Plan); }
        public Task<AccountTradeDeletionCommit> DeleteAsync(AccountTradeDeletionPlan plan, CancellationToken token)
        {
            Deletes++; Confirmed = plan;
            if (Fail) throw new IOException("Sensitive failure");
            return Task.FromResult(new AccountTradeDeletionCommit(Status, Status == AccountTradeDeletionStatus.Deleted ? plan.TradeCount : 0,
                Status == AccountTradeDeletionStatus.Deleted ? ["synthetic.png"] : []));
        }
    }
}
