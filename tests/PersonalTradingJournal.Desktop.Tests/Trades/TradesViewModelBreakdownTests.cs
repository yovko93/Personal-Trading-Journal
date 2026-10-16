using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Navigation;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed partial class TradesViewModelTests
{
    internal static BreakdownTradeContext CohortContext(params Guid[] ids) => new(Guid.NewGuid(),
        new(new("USD"), BreakdownGroupKey.ForInstrument(Guid.NewGuid())), "Synthetic Instrument",
        "Applied New York closure dates · USD", "Net preferred", new(ids));

    [Fact]
    public async Task ExistingDetailsReturnAndConfirmedDeleteKeepCohortAndRecoverPage()
    {
        var reader = new FakeTradeListReader(); var details = new FakeTradeDetailReader();
        var removed = CreateTradeListItem(); var remaining = CreateTradeListItem();
        var deletion = new FakeTradeDeletionStore { Result = new(removed.Id, []) };
        var vm = CreateViewModel(tradeListReader: reader, tradeDetailReader: details,
            tradeDeletionStore: deletion, dialogService: new() { ConfirmationResult = true });
        var scope = CohortContext(removed.Id, remaining.Id); vm.SetBreakdownScope(scope);
        reader.EnqueuePage([remaining], 21); await vm.EnsureLoadedAsync();
        reader.EnqueuePage([removed], 21); await vm.NextTradePageCommand.ExecuteAsync(null);
        details.EnqueueResult(CreateTradeDetail(removed)); await vm.ShowTradeDetailCommand.ExecuteAsync(removed);
        Assert.Equal(removed.Id, vm.SelectedTradeDetail!.Id);
        vm.CloseTradeDetailCommand.Execute(null);
        Assert.Equal(2, vm.CurrentPage); Assert.Same(scope, vm.BreakdownScope);
        reader.EnqueuePage([], 20); reader.EnqueuePage([remaining], 20);
        await vm.DeleteTradeCommand.ExecuteAsync(removed);
        Assert.Equal(removed.Id, deletion.RequestedTradeId); Assert.Equal(1, deletion.CallCount);
        Assert.Equal(1, vm.CurrentPage); Assert.Equal(remaining.Id, Assert.Single(vm.RecentTrades).Id);
        Assert.All(reader.RequestedQueries, q => Assert.Same(scope.Cohort, q.Cohort));
        // The original cohort remains frozen even after a missing source detail is opened.
        details.EnqueueResult(null); await vm.ShowTradeDetailCommand.ExecuteAsync(removed);
        Assert.True(vm.IsTradeDetailNotFound); Assert.Same(scope, vm.BreakdownScope);
        vm.DisposeBrowse();
    }

    [Fact]
    public async Task ScopedCancellationRetainsScopeAndNextRefreshRetriesSameIds()
    {
        var reader = new FakeTradeListReader(); var item = CreateTradeListItem();
        var vm = CreateViewModel(tradeListReader: reader); var scope = CohortContext(item.Id);
        vm.SetBreakdownScope(scope); reader.EnqueuePage([item], 1); await vm.EnsureLoadedAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.EnqueuePageBehavior(async (_, ct) =>
        {
            started.SetResult(); await Task.Delay(Timeout.Infinite, ct); throw new InvalidOperationException();
        });
        Task refresh = vm.RefreshCommand.ExecuteAsync(null); await started.Task;
        vm.RefreshCommand.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
        Assert.Same(scope, vm.BreakdownScope); Assert.Equal(item.Id, Assert.Single(vm.RecentTrades).Id);
        reader.EnqueuePage([item], 1); await vm.RefreshCommand.ExecuteAsync(null);
        Assert.All(reader.RequestedQueries, q => Assert.Same(scope.Cohort, q.Cohort));
        vm.DisposeBrowse();
    }

    [Fact]
    public async Task CohortPagingSortingAndRecoveryNeverBroadenAndExitRestoresOrdinaryPage()
    {
        var reader = new FakeTradeListReader();
        var item = CreateTradeListItem();
        reader.EnqueuePage([item], 45); reader.EnqueuePage([item], 45);
        var vm = CreateViewModel(tradeListReader: reader);
        await vm.EnsureLoadedAsync(); await vm.NextTradePageCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.CurrentPage);
        var scope = CohortContext(item.Id);
        Assert.True(vm.SetBreakdownScope(scope));
        reader.EnqueuePage([item], 21); await vm.EnsureLoadedAsync();
        reader.EnqueuePage([item], 21); await vm.SortTradesCommand.ExecuteAsync(TradeListSortColumn.Account);
        reader.EnqueuePage([item], 21); await vm.NextTradePageCommand.ExecuteAsync(null);
        reader.EnqueuePage([], 1); reader.EnqueuePage([item], 1);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(1, vm.CurrentPage);
        Assert.All(reader.RequestedQueries.Skip(2), q => { Assert.Same(scope.Cohort, q.Cohort); Assert.Null(q.TradingAccountId); });
        Assert.Equal(new[] { 2, 1 }, reader.RequestedQueries.TakeLast(2).Select(q => q.PageNumber));
        reader.EnqueuePage([item], 45);
        await vm.ExitBreakdownScopeAsync();
        Assert.False(vm.HasBreakdownScope); Assert.Null(reader.RequestedQueries.Last().Cohort);
        Assert.Equal(2, vm.CurrentPage); Assert.Equal(TradeListSortColumn.OpenedAtUtc, vm.CurrentSortColumn);
        vm.DisposeBrowse();
    }

    [Theory]
    [InlineData("replace")]
    [InlineData("exit")]
    [InlineData("deactivate")]
    [InlineData("dispose")]
    public async Task ObsoleteCohortReadCannotPublishAfterOwnershipChanges(string change)
    {
        var reader = new FakeTradeListReader();
        var vm = CreateViewModel(tradeListReader: reader);
        var old = CreateTradeListItem(); var current = CreateTradeListItem();
        vm.SetBreakdownScope(CohortContext(old.Id));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<TradeListPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        reader.EnqueuePageBehavior((_, ct) => { token = ct; started.SetResult(); return release.Task; });
        Task pending = vm.EnsureLoadedAsync(); await started.Task;
        Task newer = Task.CompletedTask;
        reader.EnqueuePage([current], 1);
        if (change == "replace") { vm.SetBreakdownScope(CohortContext(current.Id)); newer = vm.EnsureLoadedAsync(); }
        else if (change == "exit") newer = vm.ExitBreakdownScopeAsync();
        else if (change == "dispose") vm.DisposeBrowse();
        else vm.DeactivateBrowse();
        Assert.True(token.IsCancellationRequested);
        release.SetResult(new([old], 1, 20, 1)); await pending; await newer;
        Assert.DoesNotContain(vm.RecentTrades, t => t.Id == old.Id);
        if (change is "replace" or "exit") Assert.Equal(current.Id, Assert.Single(vm.RecentTrades).Id);
        else Assert.Empty(vm.RecentTrades);
        vm.DisposeBrowse();
    }

    [Fact]
    public async Task FailureAndEmptyCohortRemainScopedAndUnsavedFormVetoesLeaving()
    {
        var reader = new FakeTradeListReader();
        var vm = CreateViewModel(tradeListReader: reader);
        var scope = CohortContext(); vm.SetBreakdownScope(scope);
        reader.EnqueueException(new IOException("Synthetic")); await vm.EnsureLoadedAsync();
        Assert.NotNull(vm.TradeListErrorMessage); Assert.True(vm.HasBreakdownScope);
        reader.EnqueuePage([], 0); await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Null(vm.TradeListErrorMessage); Assert.Empty(vm.RecentTrades);
        Assert.All(reader.RequestedQueries, q => Assert.Same(scope.Cohort, q.Cohort));
        vm.ShowManualEntryCommand.Execute(null); vm.QuantityText = "7";
        Assert.False(vm.TryLeaveBreakdownScope());
        await vm.ExitBreakdownScopeAsync();
        Assert.True(vm.HasBreakdownScope); Assert.Equal("7", vm.QuantityText); Assert.True(vm.IsTradeFormVisible);
        vm.DisposeBrowse();
    }
}
