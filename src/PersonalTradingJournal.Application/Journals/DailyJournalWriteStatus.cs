namespace PersonalTradingJournal.Application.Journals;

public enum DailyJournalWriteStatus
{
    Created = 1,
    Updated = 2,
    Unchanged = 3,
    AlreadyExists = 4,
    NotFound = 5,
    Conflict = 6,
    AccountUnavailable = 7,
    Deleted = 8,
}
