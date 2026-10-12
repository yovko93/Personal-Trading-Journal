using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Application.Tests.Trades;

public sealed class ConfirmTradePlaybookAssociationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExactPreviewIsSubmittedOnceWithoutRefreshingEvenOnConflict(bool success)
    {
        var store = new Store { Result = success };
        var preview = new TradePlaybookAssociationPreview(Guid.NewGuid(), null, null, null, "trade", "selection");
        Assert.Equal(success, await new ConfirmTradePlaybookAssociation(store).ExecuteAsync(preview));
        Assert.Same(preview, store.Submitted); Assert.Equal(1, store.Calls);
    }

    [Fact]
    public async Task CancellationAndInvalidIdentityNeverSubmit()
    {
        var store = new Store(); var command = new ConfirmTradePlaybookAssociation(store);
        var preview = new TradePlaybookAssociationPreview(Guid.NewGuid(), null, null, null, "trade", "selection");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command.ExecuteAsync(preview, new(true)));
        await Assert.ThrowsAsync<ArgumentException>(() => command.ExecuteAsync(preview with { TradeId = Guid.Empty }));
        Assert.Equal(0, store.Calls);
    }

    private sealed class Store : ITradePlaybookAssociationStore
    {
        public bool Result; public int Calls; public TradePlaybookAssociationPreview? Submitted;
        public Task<bool> TryApplyAsync(TradePlaybookAssociationPreview preview, CancellationToken cancellationToken = default)
        { Calls++; Submitted = preview; return Task.FromResult(Result); }
        public Task<TradePlaybookAssociationPreview> PreviewAsync(Guid tradeId, Guid? setupId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Must not reload selection");
        public Task<IReadOnlyList<TradePlaybookAssociation>> ReadHistoryAsync(Guid tradeId, int? beforeSequence = null, int limit = 20, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
