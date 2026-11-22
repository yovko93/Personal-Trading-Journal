using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed partial class TradesViewModelTests
{
    [Fact]
    public async Task ImportedDescriptionSavesClearsAndCancelsWithoutEnteringManualEdit()
    {
        var row = CreateTradeListItem();
        var detail = CreateTradeDetail(row) with { IsImported = true, Description = "Existing" };
        var reader = new FakeTradeDetailReader();
        reader.EnqueueResult(detail);
        reader.EnqueueResult(detail);
        var store = new RecordingDescriptions();
        var model = CreateViewModel(tradeDetailReader: reader, importedDescriptionStore: store);
        await model.ShowTradeDetailCommand.ExecuteAsync(row);

        Assert.False(model.ShowSelectedTradeEditCommand.CanExecute(null));
        await model.ShowTradeEditCommand.ExecuteAsync(row);
        Assert.False(model.IsTradeEditVisible);
        Assert.Equal(UpdateTradeUseCase.ImportedTradeMessage, model.TradeUpdateErrorMessage);
        Assert.True(model.OpenDescriptionEditorCommand.CanExecute(null));

        model.OpenDescriptionEditorCommand.Execute(null);
        Assert.Equal("Existing", model.TradeDescriptionDraft);
        model.TradeDescriptionDraft = "Discard me";
        Assert.False(model.CloseTradeDetailCommand.CanExecute(null));
        Assert.False(model.OpenSetupEditorCommand.CanExecute(null));
        Assert.False(model.TryLeaveBreakdownScope());
        model.CancelDescriptionEditorCommand.Execute(null);
        Assert.Empty(store.Writes);
        Assert.Same(detail, model.SelectedTradeDetail);
        Assert.True(model.TryLeaveBreakdownScope());

        model.OpenDescriptionEditorCommand.Execute(null);
        model.TradeDescriptionDraft = "  Annotated  ";
        await model.SaveTradeDescriptionCommand.ExecuteAsync(null);
        Assert.Equal((row.Id, "Annotated"), Assert.Single(store.Writes));
        Assert.Equal(detail with { Description = "Annotated" }, model.SelectedTradeDetail);
        Assert.False(model.IsDescriptionEditorOpen);

        model.OpenDescriptionEditorCommand.Execute(null);
        model.TradeDescriptionDraft = " ";
        await model.SaveTradeDescriptionCommand.ExecuteAsync(null);
        Assert.Equal((row.Id, (string?)null), store.Writes[1]);
        Assert.Equal(detail with { Description = null }, model.SelectedTradeDetail);
    }

    [Fact]
    public async Task ManualTradeUsesItsExistingEditAction()
    {
        var row = CreateTradeListItem();
        var reader = new FakeTradeDetailReader();
        reader.EnqueueResult(CreateTradeDetail(row));
        var model = CreateViewModel(tradeDetailReader: reader, importedDescriptionStore: new RecordingDescriptions());
        await model.ShowTradeDetailCommand.ExecuteAsync(row);
        Assert.False(model.OpenDescriptionEditorCommand.CanExecute(null));
        Assert.True(model.ShowSelectedTradeEditCommand.CanExecute(null));
    }

    private sealed class RecordingDescriptions : IImportedTradeDescriptionStore
    {
        public List<(Guid Id, string? Text)> Writes { get; } = [];

        public Task<string?> SaveDescriptionAsync(Guid tradeId, string? description, CancellationToken cancellationToken = default)
        {
            string? saved = Trade.NormalizeDescription(description);
            Writes.Add((tradeId, saved));
            return Task.FromResult(saved);
        }
    }
}
