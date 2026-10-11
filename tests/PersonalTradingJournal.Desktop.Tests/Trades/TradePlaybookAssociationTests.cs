using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Desktop.ViewModels.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed partial class TradesViewModelTests
{
    internal static async Task<TradesViewModel> CreateAssociationRenderModel()
    {
        var setup = CreateTradingSetup("Synthetic NQ range retest"); var item = CreateTradeListItem();
        var definition = TradingSetupPlaybookRevision.Create(setup,
            new(string.Join(" ", Enumerable.Repeat("Synthetic context remains readable without changing the original plan.", 12)),
                "Retest the synthetic range", "Observe acceptance", "Return inside range", "Record planned execution",
                [new(Guid.NewGuid(), "Synthetic chart context, confirmation and lesson.", kind: PlaybookExampleKind.Valid)]), setup.UpdatedAtUtc);
        var store = new AssociationFake { Definition = definition, History = [new(1, setup.Id, setup.UpdatedAtUtc, definition)] };
        var reader = new FakeTradeDetailReader(); reader.EnqueueResult(CreateTradeDetail(item) with { TradingSetupId = setup.Id, TradingSetupName = setup.Name, IsTradingSetupActive = true });
        var setups = new FakeTradingSetupReader(); setups.EnqueueResult([CreateTradingSetupListItem(setup)]);
        var vm = CreateViewModel(tradeDetailReader: reader, tradingSetupReader: setups, playbookAssociations: store);
        await vm.EnsureLoadedAsync(); await vm.ShowTradeDetailCommand.ExecuteAsync(item); return vm;
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task PlaybookConfirmationShowsExactRevisionAndPreservesSelectionOnDeclineOrConflict(bool confirm, bool write)
    {
        var setup = CreateTradingSetup("Synthetic plan"); var item = CreateTradeListItem();
        var definition = TradingSetupPlaybookRevision.Create(setup, new("context", "entry", "confirmation", "invalidation", "execution"), setup.UpdatedAtUtc);
        var store = new AssociationFake { Definition = definition, Result = write };
        var setupReader = new FakeTradingSetupReader(); setupReader.EnqueueResult([CreateTradingSetupListItem(setup)]);
        var reader = new FakeTradeDetailReader(); var initial = CreateTradeDetail(item);
        reader.EnqueueResult(initial); reader.EnqueueResult(initial with { TradingSetupId = setup.Id, TradingSetupName = setup.Name, IsTradingSetupActive = true });
        var dialog = new FakeDialogService { ConfirmationResult = confirm };
        var vm = CreateViewModel(tradeDetailReader: reader, tradingSetupReader: setupReader, dialogService: dialog, playbookAssociations: store);
        await vm.EnsureLoadedAsync(); await vm.ShowTradeDetailCommand.ExecuteAsync(item);
        vm.SelectedTradeDetailTradingSetup = vm.TradeDetailTradingSetupOptions.Single();
        int notifications = 0; vm.TradeDataCommitted += (_, _) => notifications++;
        await vm.SaveTradingSetupCommand.ExecuteAsync(null);
        Assert.Contains("revision 1", dialog.ConfirmationRequest!.Message);
        Assert.Contains("does not verify", dialog.ConfirmationRequest.Message);
        Assert.Equal(confirm ? 1 : 0, store.Writes); Assert.Equal(confirm && write ? 1 : 0, notifications);
        Assert.Equal(setup.Id, vm.SelectedTradeDetailTradingSetup!.Id);
        if (confirm && !write) Assert.Contains("changed", vm.TradingSetupSaveErrorMessage);
        if (confirm && write) { Assert.Contains("captured revision 1", vm.PlaybookAssociationStatus); Assert.Single(vm.CapturedAssociations); }
        else Assert.Same(initial, vm.SelectedTradeDetail);
    }

    [Fact]
    public async Task PlaybookSaveUsesRevisionPreviewedAtSelectionNotNewerGuidanceAndFailureKeepsSelection()
    {
        var setup = CreateTradingSetup("Revision conflict plan"); var item = CreateTradeListItem();
        var first = TradingSetupPlaybookRevision.Create(setup, new("context", "entry", "confirm", "invalidate", "execute"), setup.UpdatedAtUtc);
        var store = new AssociationFake { Definition = first, Result = false };
        var setups = new FakeTradingSetupReader(); setups.EnqueueResult([CreateTradingSetupListItem(setup)]);
        var reader = new FakeTradeDetailReader(); reader.EnqueueResult(CreateTradeDetail(item));
        var dialog = new FakeDialogService { ConfirmationResult = true };
        var vm = CreateViewModel(tradeDetailReader: reader, tradingSetupReader: setups, dialogService: dialog, playbookAssociations: store);
        await vm.EnsureLoadedAsync(); await vm.ShowTradeDetailCommand.ExecuteAsync(item);
        vm.SelectedTradeDetailTradingSetup = vm.TradeDetailTradingSetupOptions.Single();
        Assert.Contains("revision 1",vm.SelectedPlaybookPreviewText);
        store.Definition = first.Revise(setup, 1, new("changed", "e", "c", "i", "x"), setup.UpdatedAtUtc.AddSeconds(1));
        await vm.SaveTradingSetupCommand.ExecuteAsync(null);
        Assert.Same(first, store.Submitted!.Definition); Assert.Contains("revision 1",dialog.ConfirmationRequest!.Message);
        Assert.Contains("changed",vm.TradingSetupSaveErrorMessage); Assert.Equal(setup.Id,vm.SelectedTradeDetailTradingSetup.Id);
        await vm.RefreshAssociationPreviewCommand.ExecuteAsync(null);
        Assert.Contains("revision 2",vm.SelectedPlaybookPreviewText);
        store.Failure = true;
        await vm.SaveTradingSetupCommand.ExecuteAsync(null);
        Assert.NotNull(vm.TradingSetupSaveErrorMessage); Assert.Equal(setup.Id,vm.SelectedTradeDetailTradingSetup.Id);
        int written = store.Writes;
        store.TradeToken = "changed-while-details-open";
        await vm.RefreshAssociationPreviewCommand.ExecuteAsync(null);
        await vm.SaveTradingSetupCommand.ExecuteAsync(null);
        Assert.Equal(written, store.Writes);
        Assert.Contains("Trade changed", vm.TradingSetupSaveErrorMessage);
    }

    [Fact]
    public async Task PlaybookReadDoesNotManufactureLegacyCaptureAndEditBlocksAssociation()
    {
        var setup = CreateTradingSetup("Legacy"); var item = CreateTradeListItem();
        var reader = new FakeTradeDetailReader(); reader.EnqueueResult(CreateTradeDetail(item) with { TradingSetupId = setup.Id, TradingSetupName = setup.Name, IsTradingSetupActive = true });
        var setups = new FakeTradingSetupReader(); setups.EnqueueResult([CreateTradingSetupListItem(setup)]);
        var store = new AssociationFake(); var vm = CreateViewModel(tradeDetailReader: reader, tradingSetupReader: setups, playbookAssociations: store);
        await vm.EnsureLoadedAsync(); await vm.ShowTradeDetailCommand.ExecuteAsync(item);
        await vm.LoadPlaybookHistoryCommand.ExecuteAsync(null);
        Assert.Contains("Legacy / no captured", vm.PlaybookAssociationStatus); Assert.Empty(vm.CapturedAssociations); Assert.Equal(0, store.Writes);
        vm.ShowManualEntryCommand.Execute(null);
        Assert.False(vm.SaveTradingSetupCommand.CanExecute(null)); Assert.False(vm.ClearTradingSetupCommand.CanExecute(null));
    }

    internal sealed class AssociationFake : ITradePlaybookAssociationStore
    {
        public TradingSetupPlaybookRevision? Definition; public bool Result = true; public int Writes; public bool Failure;
        public TradePlaybookAssociationPreview? Submitted;
        public string TradeToken = "trade";
        public List<TradePlaybookAssociation> History = [];
        public Task<TradePlaybookAssociationPreview> PreviewAsync(Guid tradeId, Guid? setupId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TradePlaybookAssociationPreview(tradeId, setupId, Definition?.SetupName, setupId.HasValue ? Definition : null, TradeToken, "setup"));
        public Task<bool> TryApplyAsync(TradePlaybookAssociationPreview preview, CancellationToken cancellationToken = default)
        { Writes++; Submitted = preview; if (Failure) throw new IOException("Synthetic failure"); if (Result) History.Insert(0, new(Writes, preview.SetupId, DateTimeOffset.UtcNow, preview.Definition)); return Task.FromResult(Result); }
        public Task<IReadOnlyList<TradePlaybookAssociation>> ReadHistoryAsync(Guid tradeId, int? beforeSequence = null, int limit = 20, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TradePlaybookAssociation>>(History.Where(h => !beforeSequence.HasValue || h.Sequence < beforeSequence).Take(limit).ToArray());
    }
}
