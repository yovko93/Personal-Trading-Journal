using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Accounts;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.Accounts;

public sealed class AccountPayoutViewModelTests
{
    [Fact]
    public async Task PositiveEntryRefreshesBalanceAndRemovalRequiresConfirmationForExactAccount()
    {
        var store = new Payouts(); var dialogs = new FakeDialogService();
        var vm = Create(store, dialogs);
        await vm.EnsureLoadedAsync(); await vm.ViewAccountCommand.ExecuteAsync(store.Id);
        vm.PayoutAmountText = "0"; await vm.AddPayoutCommand.ExecuteAsync(null);
        Assert.Empty(store.Rows); Assert.NotNull(vm.PayoutError);
        vm.PayoutAmountText = "10"; await vm.AddPayoutCommand.ExecuteAsync(null);
        Assert.Null(vm.PayoutError); Assert.Single(vm.Payouts); Assert.Equal(990, Assert.Single(vm.Accounts).CurrentBalance!.Value);
        vm.PayoutAmountText = "20"; await vm.AddPayoutCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Payouts.Count); Assert.Equal(30, Assert.Single(vm.Accounts).PayoutTotal);
        var first = vm.Payouts[0];
        await vm.RemovePayoutCommand.ExecuteAsync(first);
        Assert.Equal(2, store.Rows.Count); Assert.True(dialogs.ConfirmationRequest!.IsDestructive);
        dialogs.ConfirmationResult = true;
        await vm.RemovePayoutCommand.ExecuteAsync(first with { AccountId = Guid.NewGuid() });
        Assert.Equal(2, store.Rows.Count);
        await vm.RemovePayoutCommand.ExecuteAsync(first);
        Assert.Single(vm.Payouts); Assert.Equal(980, Assert.Single(vm.Accounts).CurrentBalance!.Value);
        Assert.All(store.Rows, x => Assert.Equal("EUR", x.Currency));
    }
    internal static AccountsViewModel Create(Payouts payouts, FakeDialogService dialogs)
    {
        var store = new FakeTradingAccountStore(); var clock = new FixedTimeProvider();
        return new(payouts, new(store, clock), new(store, clock), new(payouts), new(store, clock),
            new(store, new FakeTradingAccountDeletionStore()), dialogs, balanceReader: payouts, payoutStore: payouts);
    }
    internal sealed class Payouts : IAccountPayoutStore, ITradingAccountReader, ITradingAccountBalanceReader
    {
        internal Guid Id = Guid.NewGuid();
        internal List<AccountPayout> Rows = [];
        public Task<IReadOnlyList<AccountPayout>> GetAsync(Guid id, CancellationToken token = default) => Task.FromResult<IReadOnlyList<AccountPayout>>(Rows.Where(p => p.AccountId == id).ToArray());
        public Task AddAsync(Guid id, string currency, decimal amount, CancellationToken token = default)
        { Rows.Add(new(Guid.NewGuid(), id, amount, currency, DateTimeOffset.UtcNow)); return Task.CompletedTask; }
        public Task RemoveAsync(Guid id, Guid payout, CancellationToken token = default)
        { Rows.RemoveAll(x => x.AccountId == id && x.Id == payout); return Task.CompletedTask; }
        public Task<TradingAccountDetails?> GetByIdAsync(Guid id, CancellationToken token = default) =>
            Task.FromResult<TradingAccountDetails?>(new(Id, "Synthetic payout Account", TradingAccountType.Personal, null, null, "EUR", 1000, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        public Task<IReadOnlyList<AccountListItem>> GetAllAsync(CancellationToken token = default) => GetAllWithBalancesAsync(token);
        public Task<IReadOnlyList<AccountListItem>> GetAllWithBalancesAsync(CancellationToken token = default) =>
            Task.FromResult<IReadOnlyList<AccountListItem>>([new(Id, "Synthetic payout Account", TradingAccountType.Personal, null, null, "EUR", 1000, true)
                { PayoutTotal = Rows.Sum(x => x.Amount), CurrentBalance = AccountCurrentBalanceCalculator.Calculate(1000, "EUR", [], payouts: Rows.Sum(x => x.Amount)) }]);
    }
}
