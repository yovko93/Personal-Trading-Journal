using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Application.Journals;

/// <summary>An immutable snapshot of one committed journal revision.</summary>
public sealed record DailyJournalRevision(
    Guid JournalId,
    long Revision,
    string Text,
    bool IsDraft,
    DateTimeOffset SavedAtUtc,
    DailyReviewAnswers? Review = null);
