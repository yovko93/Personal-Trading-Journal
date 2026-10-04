using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Application.Journals;

public sealed record CreateDailyJournalCommand(
    DateOnly TradingDate,
    Guid? TradingAccountId,
    string Text,
    bool IsDraft = true,
    DailyReviewAnswers? Review = null);
