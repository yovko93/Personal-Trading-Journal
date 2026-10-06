namespace PersonalTradingJournal.Application.Journals;

/// <summary>Permanently deletes one exact entry and its history only at the reviewed revision.</summary>
public sealed record DeleteDailyJournalCommand(Guid JournalId, long ExpectedRevision);
