namespace PersonalTradingJournal.Application.Trades;

/// <summary>
/// Determines whether a persisted Trade exists without loading a presentation projection.
/// </summary>
public interface ITradeExistenceReader
{
    /// <summary>
    /// Determines whether the identified Trade exists.
    /// </summary>
    /// <param name="tradeId">The non-empty Trade identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the read.</param>
    /// <returns><see langword="true"/> when the Trade exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="tradeId"/> is <see cref="Guid.Empty"/>.
    /// </exception>
    Task<bool> ExistsAsync(
        Guid tradeId,
        CancellationToken cancellationToken = default);
}
