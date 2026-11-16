namespace PersonalTradingJournal.Application.Trades;

/// <summary>Confirms exactly the preview supplied by the user; never refreshes/retries a stale selection.</summary>
public sealed class ConfirmTradePlaybookAssociation(ITradePlaybookAssociationStore store)
{
    public Task<bool> ExecuteAsync(TradePlaybookAssociationPreview preview, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (preview.TradeId == Guid.Empty || preview.SetupId == Guid.Empty ||
            string.IsNullOrWhiteSpace(preview.TradeToken) || string.IsNullOrWhiteSpace(preview.SelectionToken) ||
            (preview.Definition is { } definition && definition.TradingSetupId != preview.SetupId))
            throw new ArgumentException("A matching Trade, Setup and exact preview are required.", nameof(preview));
        cancellationToken.ThrowIfCancellationRequested();
        return store.TryApplyAsync(preview, cancellationToken);
    }
}
