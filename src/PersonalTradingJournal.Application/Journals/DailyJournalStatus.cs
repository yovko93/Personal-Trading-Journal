namespace PersonalTradingJournal.Application.Journals;

/// <summary>
/// Compact committed state for one date in an exact journal Account scope.
/// Contains no journal text or review answers.
/// </summary>
public sealed record DailyJournalStatus(
    Guid JournalId,
    DateOnly TradingDate,
    bool IsDraft,
    long Revision,
    Guid? TradingAccountId = null);
