using System.Globalization;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Desktop.Converters;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Accounts;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.Accounts;

public sealed class AccountBalancePresentationTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void EstimateSurvivesEveryColorAndCoverageIsAccessible(int sign, bool estimated)
    {
        var item = Item(sign, estimated);
        var display = (AccountBalanceDisplay)new AccountBalanceDisplayConverter().Convert(item, typeof(object), null!, CultureInfo.InvariantCulture);
        Assert.Equal(sign, display.Comparison);
        Assert.Equal(estimated, display.ValueText.StartsWith("Estimated"));
        Assert.Contains("USD", display.ValueText);
        Assert.Contains("Not a broker balance", display.Description);
        if (estimated) { Assert.Contains("1 commission, 2 fee entries", display.SupportingText); Assert.Contains("unknown", display.Description); }
        else Assert.Empty(display.SupportingText);
    }

    [Fact]
    public async Task CommittedWriteDuringLoadRejectsOldBalanceAndInactiveInvalidationLoadsOnReturn()
    {
        var old = new TaskCompletionSource<IReadOnlyList<AccountListItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new Balances(); reader.Reads.Enqueue(_ => old.Task); reader.Reads.Enqueue(_ => Task.FromResult<IReadOnlyList<AccountListItem>>([Item(1, false)]));
        var vm = Create(reader);
        var loading = vm.EnsureLoadedAsync();
        vm.InvalidateBalances(refreshNow: true);
        old.SetResult([Item(-1, true)]);
        await loading;
        Assert.Equal(2, reader.Calls);
        Assert.Equal(1, Assert.Single(vm.Accounts).CurrentBalance!.Comparison);
        reader.Reads.Enqueue(_ => Task.FromResult<IReadOnlyList<AccountListItem>>([Item(0, false)]));
        vm.InvalidateBalances(refreshNow: false);
        Assert.Equal(2, reader.Calls);
        await vm.EnsureLoadedAsync();
        Assert.Equal(3, reader.Calls); Assert.Equal(0, Assert.Single(vm.Accounts).CurrentBalance!.Comparison);
    }

    [Fact]
    public async Task ManualRefreshUsesBalanceReaderAndReadErrorCanRecover()
    {
        var reader = new Balances(); reader.Reads.Enqueue(_ => throw new IOException("synthetic"));
        var vm = Create(reader);
        await vm.EnsureLoadedAsync(); Assert.NotNull(vm.ErrorMessage);
        reader.Reads.Enqueue(_ => Task.FromResult<IReadOnlyList<AccountListItem>>([Item(1, true)]));
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Null(vm.ErrorMessage); Assert.True(Assert.Single(vm.Accounts).CurrentBalance!.IsEstimated);
    }

    internal static AccountListItem Item(int sign, bool estimated) => new(Guid.NewGuid(), "Synthetic Account", TradingAccountType.Personal, null, null, "USD", 1000, true)
    { CurrentBalance = new(1000 + sign * 10, sign, 2, estimated ? 1 : 0, estimated ? 2 : 0, estimated ? 1 : 0, 0, 0, AccountBalanceUnavailable.None) };
    internal static AccountsViewModel Create(ITradingAccountBalanceReader balances)
    {
        var reader = new FakeTradingAccountReader(); var store = new FakeTradingAccountStore(); var clock = new FixedTimeProvider();
        return new(reader, new(store, clock), new(store, clock), new(reader), new(store, clock),
            new(store, new FakeTradingAccountDeletionStore()), new FakeDialogService(), balanceReader: balances);
    }
    internal sealed class Balances : ITradingAccountBalanceReader
    {
        internal int Calls;
        internal readonly Queue<Func<CancellationToken, Task<IReadOnlyList<AccountListItem>>>> Reads = new();
        public Task<IReadOnlyList<AccountListItem>> GetAllWithBalancesAsync(CancellationToken token = default)
        { Calls++; return Reads.Dequeue()(token); }
    }
}
