using PersonalTradingJournal.Application.Mistakes;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
using PersonalTradingJournal.Domain.Mistakes;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed partial class TradesViewModelTests
{
    private static readonly DateTimeOffset MistakeCreatedAtUtc =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SelectedTradeLoadsActiveAndInactiveAssignedMistakes()
    {
        TradeListItem listItem = CreateTradeListItem();
        TradeMistakeListItem active = CreateTradeMistakeListItem(
            listItem.Id,
            name: "FOMO");
        TradeMistakeListItem inactive = CreateTradeMistakeListItem(
            listItem.Id,
            name: "Moved Stop",
            active: false,
            note: "Historical context");
        var reader = new FakeTradeMistakeReader();
        reader.EnqueueResult([active, inactive]);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(CreateTradeDetail(listItem));
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader,
            tradeMistakeReader: reader);

        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);

        Assert.Equal(listItem.Id, reader.RequestedTradeId);
        Assert.Equal([active, inactive], viewModel.TradeMistakes);
        Assert.True(viewModel.HasTradeMistakes);
        Assert.False(viewModel.TradeMistakes[1].IsTradingMistakeActive);
        Assert.Equal("Historical context", viewModel.TradeMistakes[1].Note);
        Assert.Null(viewModel.TradeMistakesErrorMessage);
    }

    [Fact]
    public async Task NoAssignmentsAndReadFailureHaveIndependentSafeStates()
    {
        TradeListItem emptyItem = CreateTradeListItem(symbol: "NQ");
        TradeListItem failedItem = CreateTradeListItem(symbol: "ES");
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(CreateTradeDetail(emptyItem));
        detailReader.EnqueueResult(CreateTradeDetail(failedItem));
        var reader = new FakeTradeMistakeReader();
        reader.EnqueueResult([]);
        reader.EnqueueException(new InvalidOperationException("database path"));
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader,
            tradeMistakeReader: reader);

        await viewModel.ShowTradeDetailCommand.ExecuteAsync(emptyItem);
        Assert.Empty(viewModel.TradeMistakes);
        Assert.False(viewModel.HasTradeMistakes);
        Assert.Null(viewModel.TradeMistakesErrorMessage);

        await viewModel.ShowTradeDetailCommand.ExecuteAsync(failedItem);
        Assert.Empty(viewModel.TradeMistakes);
        Assert.Equal(
            "Trade mistakes could not be loaded.",
            viewModel.TradeMistakesErrorMessage);
        Assert.NotNull(viewModel.SelectedTradeDetail);
        Assert.Null(viewModel.TradeDetailErrorMessage);
    }

    [Fact]
    public async Task AvailableOptionsContainOnlyActiveUnassignedCatalogMistakes()
    {
        TradeListItem listItem = CreateTradeListItem();
        TradingMistake activeAssigned = CreateTradingMistake("FOMO");
        TradingMistake activeAvailable = CreateTradingMistake("Overtrading");
        TradingMistake inactive = CreateTradingMistake("Moved Stop", active: false);
        var catalogReader = new FakeTradingMistakeReader();
        catalogReader.EnqueueResult(
            [
                CreateTradingMistakeListItem(activeAssigned),
                CreateTradingMistakeListItem(activeAvailable),
                CreateTradingMistakeListItem(inactive),
            ]);
        var associationReader = new FakeTradeMistakeReader();
        associationReader.EnqueueResult(
            [CreateTradeMistakeListItem(
                listItem.Id,
                activeAssigned.Id,
                activeAssigned.Name)]);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(CreateTradeDetail(listItem));
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader,
            tradingMistakeReader: catalogReader,
            tradeMistakeReader: associationReader);

        await viewModel.EnsureLoadedAsync();
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);

        TradingMistakeListItem option = Assert.Single(
            viewModel.AvailableTradingMistakes);
        Assert.Equal(activeAvailable.Id, option.Id);
        Assert.DoesNotContain(
            viewModel.AvailableTradingMistakes,
            item => item.Id == activeAssigned.Id || item.Id == inactive.Id);
    }

    [Theory]
    [InlineData("  Entered late after chasing.  ", "Entered late after chasing.")]
    [InlineData("   ", null)]
    public async Task AssignPassesExactIdentifiersAndOptionalNoteThenReloadsAuthoritatively(
        string note,
        string? expectedNote)
    {
        TradeListItem listItem = CreateTradeListItem();
        TradingMistake catalogMistake = CreateTradingMistake("FOMO");
        var assignmentStore = new FakeTradeMistakeStore();
        var catalogStore = new FakeTradingMistakeStore { Mistake = catalogMistake };
        var catalogReader = new FakeTradingMistakeReader();
        catalogReader.EnqueueResult([CreateTradingMistakeListItem(catalogMistake)]);
        var associationReader = new FakeTradeMistakeReader();
        associationReader.EnqueueResult([]);
        TradeMistakeListItem persisted = CreateTradeMistakeListItem(
            listItem.Id,
            catalogMistake.Id,
            catalogMistake.Name,
            note: expectedNote);
        associationReader.EnqueueResult([persisted]);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(CreateTradeDetail(listItem));
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader,
            tradingMistakeReader: catalogReader,
            tradeMistakeReader: associationReader,
            tradingMistakeStore: catalogStore,
            tradeMistakeStore: assignmentStore);
        await viewModel.EnsureLoadedAsync();
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);
        viewModel.SelectedTradingMistake = Assert.Single(
            viewModel.AvailableTradingMistakes);
        viewModel.TradeMistakeNoteText = note;

        await viewModel.AssignMistakeCommand.ExecuteAsync(null);

        TradeMistake added = Assert.IsType<TradeMistake>(
            assignmentStore.AddedTradeMistake);
        Assert.Equal(listItem.Id, added.TradeId);
        Assert.Equal(catalogMistake.Id, added.TradingMistakeId);
        Assert.Equal(expectedNote, added.Note);
        Assert.Equal(2, associationReader.CallCount);
        Assert.Equal(CancellationToken.None, associationReader.CancellationToken);
        Assert.Equal([persisted], viewModel.TradeMistakes);
        Assert.Empty(viewModel.AvailableTradingMistakes);
        Assert.Null(viewModel.SelectedTradingMistake);
        Assert.Equal(string.Empty, viewModel.TradeMistakeNoteText);
        Assert.Equal(
            "Trading mistake assigned successfully.",
            viewModel.AssignMistakeSuccessMessage);
        Assert.Null(viewModel.AssignMistakeErrorMessage);
    }

    [Theory]
    [InlineData("duplicate", "This trading mistake is already assigned to the trade.")]
    [InlineData("inactive", "The selected trading mistake is inactive.")]
    [InlineData("missing", "The selected trading mistake is no longer available.")]
    [InlineData("technical", "Trading mistake could not be assigned.")]
    public async Task AssignMapsExpectedFailuresToSafeMessages(
        string failure,
        string expectedMessage)
    {
        TradeListItem listItem = CreateTradeListItem();
        TradingMistake catalogMistake = CreateTradingMistake(
            "FOMO",
            active: failure != "inactive");
        var associationStore = new FakeTradeMistakeStore
        {
            Exists = failure == "duplicate",
            AddException = failure == "technical"
                ? new InvalidOperationException("database details")
                : null,
        };
        var catalogStore = new FakeTradingMistakeStore
        {
            Mistake = failure == "missing" ? null : catalogMistake,
        };
        var catalogReader = new FakeTradingMistakeReader();
        catalogReader.EnqueueResult(
            [CreateTradingMistakeListItem(catalogMistake) with { IsActive = true }]);
        var associationReader = new FakeTradeMistakeReader();
        associationReader.EnqueueResult([]);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(CreateTradeDetail(listItem));
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader,
            tradingMistakeReader: catalogReader,
            tradeMistakeReader: associationReader,
            tradingMistakeStore: catalogStore,
            tradeMistakeStore: associationStore);
        await viewModel.EnsureLoadedAsync();
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);
        viewModel.SelectedTradingMistake = Assert.Single(
            viewModel.AvailableTradingMistakes);

        await viewModel.AssignMistakeCommand.ExecuteAsync(null);

        Assert.Equal(expectedMessage, viewModel.AssignMistakeErrorMessage);
        Assert.Null(viewModel.AssignMistakeSuccessMessage);
        Assert.Equal(1, associationReader.CallCount);
    }

    [Fact]
    public async Task SuccessfulRemovalUsesExactAssociationAndReloadsOptions()
    {
        TradeListItem listItem = CreateTradeListItem();
        TradingMistake catalogMistake = CreateTradingMistake(
            "Historical",
            active: false);
        TradeMistake domainAssociation = new(
            listItem.Id,
            catalogMistake.Id,
            "Review note",
            MistakeCreatedAtUtc);
        TradeMistakeListItem association = CreateTradeMistakeListItem(
            listItem.Id,
            catalogMistake.Id,
            catalogMistake.Name,
            active: false,
            note: domainAssociation.Note,
            id: domainAssociation.Id);
        var store = new FakeTradeMistakeStore
        {
            TradeMistakeToReturn = domainAssociation,
        };
        var associationReader = new FakeTradeMistakeReader();
        associationReader.EnqueueResult([association]);
        associationReader.EnqueueResult([]);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(CreateTradeDetail(listItem));
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader,
            tradeMistakeReader: associationReader,
            tradeMistakeStore: store);
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);

        Assert.True(viewModel.RemoveMistakeCommand.CanExecute(association));
        await viewModel.RemoveMistakeCommand.ExecuteAsync(association);

        Assert.Equal(association.Id, store.RequestedTradeMistakeId);
        Assert.Equal(1, store.RemoveCallCount);
        Assert.Equal(2, associationReader.CallCount);
        Assert.Equal(CancellationToken.None, associationReader.CancellationToken);
        Assert.Empty(viewModel.TradeMistakes);
        Assert.Equal(
            "Trading mistake removed successfully.",
            viewModel.RemoveMistakeSuccessMessage);
        Assert.Null(viewModel.RemoveMistakeErrorMessage);
    }

    [Fact]
    public async Task MissingRemovalUsesSafeMessageWithoutReload()
    {
        TradeListItem listItem = CreateTradeListItem();
        TradeMistakeListItem association = CreateTradeMistakeListItem(listItem.Id);
        var store = new FakeTradeMistakeStore();
        var associationReader = new FakeTradeMistakeReader();
        associationReader.EnqueueResult([association]);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(CreateTradeDetail(listItem));
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader,
            tradeMistakeReader: associationReader,
            tradeMistakeStore: store);
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);

        await viewModel.RemoveMistakeCommand.ExecuteAsync(association);

        Assert.Equal(
            "The selected trade mistake is no longer available.",
            viewModel.RemoveMistakeErrorMessage);
        Assert.Null(viewModel.RemoveMistakeSuccessMessage);
        Assert.Equal(1, associationReader.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PostMutationReloadFailurePreservesMutationSuccess(
        bool assign)
    {
        TradeListItem listItem = CreateTradeListItem();
        TradingMistake catalogMistake = CreateTradingMistake("FOMO");
        TradeMistake domainAssociation = new(
            listItem.Id,
            catalogMistake.Id,
            null,
            MistakeCreatedAtUtc);
        TradeMistakeListItem association = CreateTradeMistakeListItem(
            listItem.Id,
            catalogMistake.Id,
            catalogMistake.Name,
            id: domainAssociation.Id);
        var catalogReader = new FakeTradingMistakeReader();
        catalogReader.EnqueueResult([CreateTradingMistakeListItem(catalogMistake)]);
        var associationReader = new FakeTradeMistakeReader();
        associationReader.EnqueueResult(assign ? [] : [association]);
        associationReader.EnqueueException(new InvalidOperationException("reload failed"));
        var store = new FakeTradeMistakeStore
        {
            TradeMistakeToReturn = domainAssociation,
        };
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(CreateTradeDetail(listItem));
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader,
            tradingMistakeReader: catalogReader,
            tradeMistakeReader: associationReader,
            tradingMistakeStore: new FakeTradingMistakeStore
            {
                Mistake = catalogMistake,
            },
            tradeMistakeStore: store);
        await viewModel.EnsureLoadedAsync();
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);

        if (assign)
        {
            viewModel.SelectedTradingMistake = Assert.Single(
                viewModel.AvailableTradingMistakes);
            await viewModel.AssignMistakeCommand.ExecuteAsync(null);
            Assert.Equal(
                "Trading mistake assigned successfully.",
                viewModel.AssignMistakeSuccessMessage);
            Assert.Null(viewModel.AssignMistakeErrorMessage);
        }
        else
        {
            await viewModel.RemoveMistakeCommand.ExecuteAsync(association);
            Assert.Equal(
                "Trading mistake removed successfully.",
                viewModel.RemoveMistakeSuccessMessage);
            Assert.Null(viewModel.RemoveMistakeErrorMessage);
        }

        Assert.Equal("Trade mistakes could not be loaded.", viewModel.TradeMistakesErrorMessage);
        Assert.Equal(assign ? 0 : 1, viewModel.TradeMistakes.Count);
    }

    [Fact]
    public async Task SwitchingSelectedTradeClearsMistakeDraftAndMessages()
    {
        TradeListItem first = CreateTradeListItem(symbol: "NQ");
        TradeListItem second = CreateTradeListItem(symbol: "ES");
        TradingMistake catalogMistake = CreateTradingMistake("FOMO");
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(CreateTradeDetail(first));
        detailReader.EnqueueResult(CreateTradeDetail(second));
        var associationReader = new FakeTradeMistakeReader();
        associationReader.EnqueueResult([]);
        associationReader.EnqueueResult([]);
        var catalogReader = new FakeTradingMistakeReader();
        catalogReader.EnqueueResult([CreateTradingMistakeListItem(catalogMistake)]);
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader,
            tradingMistakeReader: catalogReader,
            tradeMistakeReader: associationReader);
        await viewModel.EnsureLoadedAsync();
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(first);
        viewModel.SelectedTradingMistake = Assert.Single(
            viewModel.AvailableTradingMistakes);
        viewModel.TradeMistakeNoteText = "stale draft";

        await viewModel.ShowTradeDetailCommand.ExecuteAsync(second);

        Assert.Null(viewModel.SelectedTradingMistake);
        Assert.Equal(string.Empty, viewModel.TradeMistakeNoteText);
        Assert.Null(viewModel.AssignMistakeErrorMessage);
        Assert.Null(viewModel.RemoveMistakeErrorMessage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MistakeMutationBusyStatePreventsOverlappingMutations(
        bool assigning)
    {
        TradeListItem listItem = CreateTradeListItem();
        TradingMistake catalogMistake = CreateTradingMistake("FOMO");
        TradeMistake domainAssociation = new(
            listItem.Id,
            catalogMistake.Id,
            null,
            MistakeCreatedAtUtc);
        TradeMistakeListItem association = CreateTradeMistakeListItem(
            listItem.Id,
            catalogMistake.Id,
            catalogMistake.Name,
            id: domainAssociation.Id);
        var store = new FakeTradeMistakeStore
        {
            TradeMistakeToReturn = domainAssociation,
            HoldAdd = assigning,
            HoldRemove = !assigning,
        };
        var catalogReader = new FakeTradingMistakeReader();
        catalogReader.EnqueueResult([CreateTradingMistakeListItem(catalogMistake)]);
        var associationReader = new FakeTradeMistakeReader();
        associationReader.EnqueueResult(assigning ? [] : [association]);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(CreateTradeDetail(listItem));
        TradesViewModel viewModel = CreateViewModel(
            tradeDetailReader: detailReader,
            tradingMistakeReader: catalogReader,
            tradeMistakeReader: associationReader,
            tradingMistakeStore: new FakeTradingMistakeStore
            {
                Mistake = catalogMistake,
            },
            tradeMistakeStore: store);
        await viewModel.EnsureLoadedAsync();
        await viewModel.ShowTradeDetailCommand.ExecuteAsync(listItem);

        Task operation;
        if (assigning)
        {
            viewModel.SelectedTradingMistake = Assert.Single(
                viewModel.AvailableTradingMistakes);
            operation = viewModel.AssignMistakeCommand.ExecuteAsync(null);
            await store.AddStarted;
        }
        else
        {
            operation = viewModel.RemoveMistakeCommand.ExecuteAsync(association);
            await store.RemoveStarted;
        }

        Assert.Equal(assigning, viewModel.IsAssigningMistake);
        Assert.Equal(!assigning, viewModel.IsRemovingMistake);
        Assert.False(viewModel.AssignMistakeCommand.CanExecute(null));
        Assert.False(viewModel.RemoveMistakeCommand.CanExecute(association));
        Assert.False(viewModel.RefreshCommand.CanExecute(null));

        if (assigning)
        {
            store.ReleaseAdd();
        }
        else
        {
            store.ReleaseRemove();
        }

        await operation;
        Assert.False(viewModel.IsAssigningMistake);
        Assert.False(viewModel.IsRemovingMistake);
    }

    private static TradingMistake CreateTradingMistake(
        string name,
        bool active = true)
    {
        var mistake = new TradingMistake(name, null, MistakeCreatedAtUtc);
        if (!active)
        {
            mistake.Deactivate(MistakeCreatedAtUtc.AddMinutes(1));
        }

        return mistake;
    }

    private static TradingMistakeListItem CreateTradingMistakeListItem(
        TradingMistake mistake) =>
        new(
            mistake.Id,
            mistake.Name,
            mistake.Description,
            mistake.IsActive,
            mistake.CreatedAtUtc,
            mistake.UpdatedAtUtc);

    private static TradeMistakeListItem CreateTradeMistakeListItem(
        Guid tradeId,
        Guid? catalogId = null,
        string name = "FOMO",
        bool active = true,
        string? note = null,
        Guid? id = null) =>
        new(
            id ?? Guid.NewGuid(),
            tradeId,
            catalogId ?? Guid.NewGuid(),
            name,
            active,
            note,
            MistakeCreatedAtUtc,
            MistakeCreatedAtUtc);
}
