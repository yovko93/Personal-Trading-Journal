namespace PersonalTradingJournal.Application.Screenshots;

/// <summary>
/// Opens screenshot binary content by its metadata identifier.
/// </summary>
public interface ITradeScreenshotContentReader
{
    /// <summary>
    /// Opens the identified screenshot content for reading.
    /// </summary>
    /// <param name="screenshotId">The non-empty screenshot metadata identifier.</param>
    /// <param name="cancellationToken">The token used to cancel retrieval.</param>
    /// <returns>
    /// The screenshot and its caller-owned stream, or <see langword="null"/> when metadata
    /// does not exist.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="screenshotId"/> is <see cref="Guid.Empty"/>.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when metadata exists but its physical content is missing.
    /// </exception>
    Task<TradeScreenshotContent?> OpenAsync(
        Guid screenshotId,
        CancellationToken cancellationToken = default);
}
