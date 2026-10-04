using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Application.Journals;

/// <summary>A disconnected journal and the current availability of its Account reference.</summary>
public sealed record DailyJournalDetails(
    DailyJournalEntry Entry,
    DailyJournalAccountState AccountState,
    string? AccountName);
