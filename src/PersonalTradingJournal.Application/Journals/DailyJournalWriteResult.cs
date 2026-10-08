namespace PersonalTradingJournal.Application.Journals;

public sealed record DailyJournalWriteResult(
    DailyJournalWriteStatus Status,
    DailyJournalDetails? Journal);
