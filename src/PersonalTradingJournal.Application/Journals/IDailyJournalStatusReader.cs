namespace PersonalTradingJournal.Application.Journals;

/// <summary>Read-only, bounded status lookup for one visible Calendar grid.</summary>
public interface IDailyJournalStatusReader
{
    /// <summary>The largest Monday–Sunday month grid contains six weeks.</summary>
    const int MaximumVisibleDays = 42;

    /// <summary>
    /// Returns only entries in the inclusive range, sorted by trading date.
    /// Null Account ID selects only explicit All accounts journals, not Account-specific journals.
    /// A reversed range, a range exceeding 42 days, or an empty Account ID is invalid.
    /// </summary>
    Task<IReadOnlyList<DailyJournalStatus>> GetAsync(
        DateOnly from,
        DateOnly through,
        Guid? tradingAccountId = null,
        CancellationToken cancellationToken = default);
}
