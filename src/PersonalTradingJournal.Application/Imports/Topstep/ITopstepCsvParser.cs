namespace PersonalTradingJournal.Application.Imports.Topstep;

/// <summary>Read-only parsing of Topstep source rows, not reconstruction or import.</summary>
public interface ITopstepCsvParser
{
    /// <summary>The caller owns the readable source stream; cancellation and I/O failures propagate.</summary>
    Task<TopstepCsvParseResult> ParseAsync(
        Stream source,
        CancellationToken cancellationToken = default);
}
