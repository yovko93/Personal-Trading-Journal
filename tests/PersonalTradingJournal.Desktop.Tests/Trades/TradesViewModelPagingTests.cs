using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed partial class TradesViewModelTests
{
    [Fact]
    public async Task InitialBrowseStateRequestsFirstTwentyTradesOpenedDescending()
    {
        var reader = new FakeTradeListReader();
        reader.EnqueuePage([], totalCount: 0);
        TradesViewModel viewModel = CreateViewModel(tradeListReader: reader);

        await viewModel.EnsureLoadedAsync();

        TradeListQuery query = Assert.Single(reader.RequestedQueries);
        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal(20, viewModel.PageSize);
        Assert.Equal(0, viewModel.TotalPages);
        Assert.Equal(TradeListSortColumn.OpenedAtUtc, viewModel.CurrentSortColumn);
        Assert.Equal(TradeListSortDirection.Descending, viewModel.CurrentSortDirection);
        Assert.Equal(
            new TradeListQuery(
                1,
                20,
                TradeListSortColumn.OpenedAtUtc,
                TradeListSortDirection.Descending),
            query);
        Assert.False(viewModel.CanGoPrevious);
        Assert.False(viewModel.CanGoNext);
    }

    [Fact]
    public async Task NextAndPreviousLoadRequestedPagesAndRespectBoundaries()
    {
        var reader = new FakeTradeListReader();
        reader.EnqueuePage([CreateTradeListItem()], 45, pageNumber: 1);
        reader.EnqueuePage([CreateTradeListItem()], 45, pageNumber: 2);
        reader.EnqueuePage([CreateTradeListItem()], 45, pageNumber: 1);
        TradesViewModel viewModel = CreateViewModel(tradeListReader: reader);
        await viewModel.EnsureLoadedAsync();

        Assert.False(viewModel.CanGoPrevious);
        Assert.True(viewModel.CanGoNext);
        await viewModel.NextTradePageCommand.ExecuteAsync(null);
        Assert.Equal(2, viewModel.CurrentPage);
        Assert.True(viewModel.CanGoPrevious);
        Assert.True(viewModel.CanGoNext);

        await viewModel.PreviousTradePageCommand.ExecuteAsync(null);
        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal([1, 2, 1], reader.RequestedQueries.Select(x => x.PageNumber));
    }

    [Fact]
    public async Task PagingAndSortingCommandsAreDisabledWhilePageLoads()
    {
        var reader = new FakeTradeListReader();
        reader.EnqueuePage([CreateTradeListItem()], 45);
        TradesViewModel viewModel = CreateViewModel(tradeListReader: reader);
        await viewModel.EnsureLoadedAsync();
        var release = new TaskCompletionSource<TradeListPage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        reader.EnqueuePageBehavior((_, cancellationToken) =>
            release.Task.WaitAsync(cancellationToken));

        Task load = viewModel.NextTradePageCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsTradeListLoading);
        Assert.False(viewModel.NextTradePageCommand.CanExecute(null));
        Assert.False(viewModel.PreviousTradePageCommand.CanExecute(null));
        Assert.False(viewModel.SortTradesCommand.CanExecute(
            TradeListSortColumn.Account));

        release.SetResult(new TradeListPage(
            [CreateTradeListItem()], 2, 20, 45));
        await load;
    }

    [Fact]
    public async Task SortingUsesColumnDefaultsThenTogglesAndReturnsToPageOne()
    {
        var reader = new FakeTradeListReader();
        for (int index = 0; index < 5; index++)
        {
            reader.EnqueuePage([CreateTradeListItem()], 45);
        }

        TradesViewModel viewModel = CreateViewModel(tradeListReader: reader);
        await viewModel.EnsureLoadedAsync();
        await viewModel.NextTradePageCommand.ExecuteAsync(null);
        await viewModel.SortTradesCommand.ExecuteAsync(TradeListSortColumn.Account);
        await viewModel.SortTradesCommand.ExecuteAsync(TradeListSortColumn.Account);
        await viewModel.SortTradesCommand.ExecuteAsync(TradeListSortColumn.NetPnL);

        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal(TradeListSortColumn.NetPnL, viewModel.CurrentSortColumn);
        Assert.Equal(TradeListSortDirection.Descending, viewModel.CurrentSortDirection);
        Assert.Equal(
            [
                new TradeListQuery(1, 20, TradeListSortColumn.OpenedAtUtc, TradeListSortDirection.Descending),
                new TradeListQuery(2, 20, TradeListSortColumn.OpenedAtUtc, TradeListSortDirection.Descending),
                new TradeListQuery(1, 20, TradeListSortColumn.Account, TradeListSortDirection.Ascending),
                new TradeListQuery(1, 20, TradeListSortColumn.Account, TradeListSortDirection.Descending),
                new TradeListQuery(1, 20, TradeListSortColumn.NetPnL, TradeListSortDirection.Descending),
            ],
            reader.RequestedQueries);
        Assert.Equal("↓", viewModel.NetPnLSortIndicator);
        Assert.Equal(string.Empty, viewModel.AccountSortIndicator);
    }

    [Fact]
    public async Task RefreshPreservesCurrentPageAndSort()
    {
        var reader = new FakeTradeListReader();
        for (int index = 0; index < 5; index++)
        {
            reader.EnqueuePage([CreateTradeListItem()], 45);
        }

        TradesViewModel viewModel = CreateViewModel(tradeListReader: reader);
        await viewModel.EnsureLoadedAsync();
        await viewModel.SortTradesCommand.ExecuteAsync(TradeListSortColumn.NetPnL);
        await viewModel.SortTradesCommand.ExecuteAsync(TradeListSortColumn.NetPnL);
        await viewModel.NextTradePageCommand.ExecuteAsync(null);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        TradeListQuery refresh = reader.RequestedQueries[^1];
        Assert.Equal(2, refresh.PageNumber);
        Assert.Equal(TradeListSortColumn.NetPnL, refresh.SortColumn);
        Assert.Equal(TradeListSortDirection.Ascending, refresh.SortDirection);
    }

    [Fact]
    public async Task SuccessfulCreateResetsBrowseToFirstPageOpenedDescending()
    {
        ManualTradeSaveFixture fixture = CreateSaveFixture();
        fixture.TradeListReader.EnqueuePage([CreateTradeListItem()], 45);
        fixture.TradeListReader.EnqueuePage([CreateTradeListItem()], 45);
        fixture.TradeListReader.EnqueuePage([CreateTradeListItem()], 45, pageNumber: 2);
        fixture.TradeListReader.EnqueuePage([CreateTradeListItem()], 45, pageNumber: 3);
        fixture.TradeListReader.EnqueuePage([CreateTradeListItem()], 46, pageNumber: 1);
        await fixture.ViewModel.EnsureLoadedAsync();
        await fixture.ViewModel.SortTradesCommand.ExecuteAsync(
            TradeListSortColumn.Account);
        await fixture.ViewModel.NextTradePageCommand.ExecuteAsync(null);
        await fixture.ViewModel.NextTradePageCommand.ExecuteAsync(null);

        await fixture.ViewModel.SaveManualTradeCommand.ExecuteAsync(null);

        TradeListQuery reload = fixture.TradeListReader.RequestedQueries[^1];
        Assert.Equal(1, fixture.ViewModel.CurrentPage);
        Assert.Equal(TradeListSortColumn.OpenedAtUtc, reload.SortColumn);
        Assert.Equal(TradeListSortDirection.Descending, reload.SortDirection);
    }

    [Fact]
    public async Task SuccessfulCloseReloadsCurrentPageAndSort()
    {
        CloseTradeFixture fixture = await CreateCloseTradeFixtureAsync();
        fixture.ListReader.EnqueuePage([fixture.OpenListItem], 45);
        fixture.ListReader.EnqueuePage([fixture.OpenListItem], 45, pageNumber: 2);
        await fixture.ViewModel.SortTradesCommand.ExecuteAsync(
            TradeListSortColumn.NetPnL);
        await fixture.ViewModel.NextTradePageCommand.ExecuteAsync(null);
        ShowValidCloseForm(fixture.ViewModel);
        fixture.DetailReader.EnqueueResult(fixture.OpenDetail with
        {
            Status = PersonalTradingJournal.Domain.Trades.TradeStatus.Closed,
            ClosedAtUtc = CloseExecutedAtUtc,
            OpenQuantity = 0m,
        });
        fixture.ListReader.EnqueuePage([], 45, pageNumber: 2);

        await fixture.ViewModel.SaveCloseTradeCommand.ExecuteAsync(null);

        TradeListQuery reload = fixture.ListReader.RequestedQueries[^1];
        Assert.Equal(2, reload.PageNumber);
        Assert.Equal(TradeListSortColumn.NetPnL, reload.SortColumn);
        Assert.Equal(TradeListSortDirection.Descending, reload.SortDirection);
    }

    [Fact]
    public async Task FailedSortLoadRetainsPriorBrowseStateAndRows()
    {
        TradeListItem initial = CreateTradeListItem();
        var reader = new FakeTradeListReader();
        reader.EnqueuePage([initial], 45);
        reader.EnqueueException(new IOException("database unavailable"));
        TradesViewModel viewModel = CreateViewModel(tradeListReader: reader);
        await viewModel.EnsureLoadedAsync();
        IReadOnlyList<TradeListItem> rows = viewModel.RecentTrades;

        await viewModel.SortTradesCommand.ExecuteAsync(TradeListSortColumn.Account);

        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal(45, viewModel.TotalCount);
        Assert.Equal(TradeListSortColumn.OpenedAtUtc, viewModel.CurrentSortColumn);
        Assert.Equal(TradeListSortDirection.Descending, viewModel.CurrentSortDirection);
        Assert.Same(rows, viewModel.RecentTrades);
        Assert.True(viewModel.HasTradeListError);
    }

    [Fact]
    public async Task DeleteReloadCorrectsAnInvalidLastPage()
    {
        TradeListItem deleted = CreateTradeListItem();
        TradeListItem remaining = CreateTradeListItem();
        var reader = new FakeTradeListReader();
        reader.EnqueuePage([remaining], 41, pageNumber: 1);
        reader.EnqueuePage([remaining], 41, pageNumber: 2);
        reader.EnqueuePage([deleted], 41, pageNumber: 3);
        reader.EnqueuePage([], 40, pageNumber: 3);
        reader.EnqueuePage([remaining], 40, pageNumber: 2);
        var deletionStore = new FakeTradeDeletionStore
        {
            Result = new TradeDeletionInfo(deleted.Id, []),
        };
        var dialog = new FakeDialogService { ConfirmationResult = true };
        TradesViewModel viewModel = CreateViewModel(
            tradeListReader: reader,
            tradeDeletionStore: deletionStore,
            dialogService: dialog);
        await viewModel.EnsureLoadedAsync();
        await viewModel.NextTradePageCommand.ExecuteAsync(null);
        await viewModel.NextTradePageCommand.ExecuteAsync(null);

        await viewModel.DeleteTradeCommand.ExecuteAsync(deleted);

        Assert.Equal(2, viewModel.CurrentPage);
        Assert.Equal(40, viewModel.TotalCount);
        Assert.Equal([3, 2], reader.RequestedQueries
            .TakeLast(2)
            .Select(query => query.PageNumber));
    }

    [Fact]
    public async Task ResetTransientStatePreservesLoadedBrowseState()
    {
        TradeListItem thirdPageItem = CreateTradeListItem();
        var reader = new FakeTradeListReader();
        reader.EnqueuePage([thirdPageItem], 45);
        reader.EnqueuePage([thirdPageItem], 45);
        reader.EnqueuePage([thirdPageItem], 45);
        reader.EnqueuePage([thirdPageItem], 45);
        reader.EnqueuePage([thirdPageItem], 45);
        TradesViewModel viewModel = CreateViewModel(tradeListReader: reader);
        await viewModel.EnsureLoadedAsync();
        await viewModel.SortTradesCommand.ExecuteAsync(TradeListSortColumn.Account);
        await viewModel.SortTradesCommand.ExecuteAsync(TradeListSortColumn.Account);
        await viewModel.NextTradePageCommand.ExecuteAsync(null);
        await viewModel.NextTradePageCommand.ExecuteAsync(null);
        IReadOnlyList<TradeListItem> rows = viewModel.RecentTrades;
        viewModel.ShowManualEntryCommand.Execute(null);

        viewModel.ResetTransientState();

        Assert.Equal(3, viewModel.CurrentPage);
        Assert.Equal(TradeListSortColumn.Account, viewModel.CurrentSortColumn);
        Assert.Equal(TradeListSortDirection.Descending, viewModel.CurrentSortDirection);
        Assert.Same(rows, viewModel.RecentTrades);
        Assert.False(viewModel.IsManualEntryVisible);
    }
}
