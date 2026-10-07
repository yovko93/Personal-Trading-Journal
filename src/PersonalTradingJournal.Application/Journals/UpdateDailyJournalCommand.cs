using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Application.Journals;

public sealed record UpdateDailyJournalCommand(
    Guid JournalId,
    long ExpectedRevision,
    string Text,
    bool IsDraft = true,
    DailyReviewAnswers? Review = null,
    // Explicit editor reopening is local until the next save; apply that edit in one revision.
    bool ReopenCompleted = false,
    // Omitted means retain scope; an explicit null ID means move to All accounts.
    DailyJournalAccountScope? TargetScope = null);
