namespace PersonalTradingJournal.Application.Imports.Tradovate;

/// <summary>
/// Parses Tradovate matched-fill CSV data without reconstructing executions or Trades.
/// </summary>
public interface ITradovateCsvParser
{
    /// <summary>
    /// Reads and normalizes the supplied CSV stream. The caller retains ownership of
    /// <paramref name="source"/> and is responsible for disposing it.
    /// </summary>
    Task<TradovateCsvParseResult> ParseAsync(
        Stream source,
        CancellationToken cancellationToken = default);
}
