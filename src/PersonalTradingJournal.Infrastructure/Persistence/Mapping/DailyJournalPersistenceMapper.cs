using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Persistence.Mapping;

public static class DailyJournalPersistenceMapper
{
    public static DailyJournalEntry ToDomain(DailyJournalRecord record) => DailyJournalEntry.Rehydrate(
        record.Id, record.TradingDate, record.TradingAccountId, record.Text, record.IsDraft,
        record.Revision, record.CreatedAtUtc, record.UpdatedAtUtc,
        new DailyReviewAnswers(record.WentWell, record.NeedsImprovement, record.NextTradingDay));

    public static DailyJournalRecord ToRecord(DailyJournalEntry entry) => new()
    {
        Id = entry.Id, TradingDate = entry.TradingDate, TradingAccountId = entry.TradingAccountId,
        Text = entry.Text, IsDraft = entry.IsDraft, Revision = entry.Revision,
        WentWell = entry.Review.WentWell, NeedsImprovement = entry.Review.NeedsImprovement,
        NextTradingDay = entry.Review.NextTradingDay,
        CreatedAtUtc = entry.CreatedAtUtc, UpdatedAtUtc = entry.UpdatedAtUtc
    };

    public static DailyJournalRevisionRecord ToRevision(DailyJournalEntry entry) => new()
    {
        JournalId = entry.Id, Revision = entry.Revision, Text = entry.Text,
        WentWell = entry.Review.WentWell, NeedsImprovement = entry.Review.NeedsImprovement,
        NextTradingDay = entry.Review.NextTradingDay,
        IsDraft = entry.IsDraft, SavedAtUtc = entry.UpdatedAtUtc
    };
}
