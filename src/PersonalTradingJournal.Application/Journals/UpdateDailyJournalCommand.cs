namespace PersonalTradingJournal.Application.Journals;

public sealed record UpdateDailyJournalCommand(
    Guid JournalId,
    long ExpectedRevision,
    string Text,
    bool IsDraft = true);
