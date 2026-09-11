namespace PersonalTradingJournal.Application.Trades;

/// <summary>
/// Reads one complete, authoritative projection of a persisted Trade lifecycle.
/// </summary>
/// <remarks>
/// An existing Trade is returned regardless of the current activation state of its
/// referenced account or instrument. A missing Trade is represented by <see langword="null"/>;
/// technical failures and cancellation propagate to the caller.
/// </remarks>
public interface ITradeDetailReader
{
    /// <summary>
    /// Gets the authoritative detail projection for one persisted Trade.
    /// </summary>
    /// <param name="tradeId">The non-empty Trade identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the read.</param>
    /// <returns>The Trade detail, or <see langword="null"/> when it does not exist.</returns>
    Task<TradeDetail?> GetByIdAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default);
}
