namespace PersonalTradingJournal.Application.Screenshots;

/// <summary>
/// Reads authoritative screenshot metadata for one persisted Trade.
/// </summary>
/// <remarks>
/// Implementations must reject an empty Trade identifier before database work. A valid
/// Trade with no screenshots returns an empty collection. Historical screenshots remain
/// visible regardless of the current activation state of referenced Account or Instrument
/// data. Technical failures and cancellation propagate to the caller.
///
/// Results are ordered with captured screenshots first by <see cref="TradeScreenshotListItem.CapturedAtUtc"/>
/// ascending. Screenshots without a capture timestamp follow. Within the resulting chronology,
/// <see cref="TradeScreenshotListItem.CreatedAtUtc"/> ascending and then
/// <see cref="TradeScreenshotListItem.Id"/> ascending provide deterministic tie-breaking.
/// </remarks>
public interface ITradeScreenshotReader
{
    /// <summary>
    /// Gets the ordered screenshot metadata associated with one Trade.
    /// </summary>
    /// <param name="tradeId">The non-empty Trade identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the read.</param>
    /// <returns>An ordered collection, or an empty collection when none exist.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="tradeId"/> is <see cref="Guid.Empty"/>.
    /// </exception>
    Task<IReadOnlyList<TradeScreenshotListItem>> GetForTradeAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default);
}
